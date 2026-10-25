using Microsoft.EntityFrameworkCore;
using Talent.Integration.Canonical;
using Talent.Integration.Contracts;
using Talent.Integration.Persistence;

namespace Talent.Integration.Readers;

internal static class Paging
{
    public const int MaxPageSize = 500;

    public static (int Page, int PageSize) Normalise(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}

internal sealed class EmployeeReader(IntegrationDbContext db) : IEmployeeReader
{
    private static IQueryable<EmployeeDto> Project(IQueryable<Employee> q) =>
        q.Select(e => new EmployeeDto(
            e.Id, e.EmployeeNumber, e.FirstName, e.LastName, e.Email, e.OrgUnitId, e.PositionId, e.ManagerId, e.HireDate, e.Status));

    public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Project(db.Employees.AsNoTracking().Where(e => e.Id == employeeId)).SingleOrDefaultAsync(cancellationToken);

    public Task<EmployeeDto?> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken) =>
        Project(db.Employees.AsNoTracking().Where(e => e.EmployeeNumber == employeeNumber)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EmployeeDto>> GetManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);
        return await Project(db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).OrderBy(e => e.EmployeeNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken) =>
        await Project(db.Employees.AsNoTracking().Where(e => e.ManagerId == managerId).OrderBy(e => e.EmployeeNumber))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<EmployeeDto>> ListAsync(Guid? orgUnitId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalise(page, pageSize);
        var query = db.Employees.AsNoTracking().Where(e => orgUnitId == null || e.OrgUnitId == orgUnitId);
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderBy(e => e.EmployeeNumber).Skip((p - 1) * size).Take(size)).ToListAsync(cancellationToken);
        return new PagedResult<EmployeeDto>(items, p, size, total);
    }
}

internal sealed class OrgUnitReader(IntegrationDbContext db) : IOrgUnitReader
{
    public Task<OrgUnitDto?> GetAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        db.OrgUnits.AsNoTracking().Where(o => o.Id == orgUnitId)
            .Select(o => new OrgUnitDto(o.Id, o.Code, o.Name, o.ParentId, o.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<OrgUnitDto>> ListAsync(CancellationToken cancellationToken) =>
        await db.OrgUnits.AsNoTracking().OrderBy(o => o.Code)
            .Select(o => new OrgUnitDto(o.Id, o.Code, o.Name, o.ParentId, o.IsActive))
            .ToListAsync(cancellationToken);
}

internal sealed class PositionReader(IntegrationDbContext db) : IPositionReader
{
    public Task<PositionDto?> GetAsync(Guid positionId, CancellationToken cancellationToken) =>
        db.Positions.AsNoTracking().Where(p => p.Id == positionId)
            .Select(p => new PositionDto(p.Id, p.Code, p.Title, p.OrgUnitId, p.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<PositionDto>> ListAsync(Guid? orgUnitId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalise(page, pageSize);
        var query = db.Positions.AsNoTracking().Where(x => orgUnitId == null || x.OrgUnitId == orgUnitId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Code).Skip((p - 1) * size).Take(size)
            .Select(x => new PositionDto(x.Id, x.Code, x.Title, x.OrgUnitId, x.IsActive))
            .ToListAsync(cancellationToken);
        return new PagedResult<PositionDto>(items, p, size, total);
    }
}

internal sealed class DeadLetterReader(IntegrationDbContext db) : IDeadLetterReader
{
    public async Task<IReadOnlyList<DeadLetterDto>> ListOpenAsync(CancellationToken cancellationToken) =>
        await db.DeadLetters.AsNoTracking().Where(d => d.ResolvedAt == null)
            .OrderByDescending(d => d.LastFailedAt).ThenBy(d => d.Id).Take(1000)
            .Select(d => new DeadLetterDto(d.Id, d.System, d.Entity, d.ExternalId, d.Error, d.Attempts, d.FirstFailedAt, d.LastFailedAt))
            .ToListAsync(cancellationToken);
}
