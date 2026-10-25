namespace Talent.Integration.Contracts;

/// <summary>Read side of the synced employee copy. Business modules never call a source system.</summary>
public interface IEmployeeReader
{
    Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<EmployeeDto?> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmployeeDto>> GetManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken);

    /// <summary>Paged, stable order by employee number. <paramref name="pageSize"/> is capped at 500.</summary>
    Task<PagedResult<EmployeeDto>> ListAsync(Guid? orgUnitId, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IOrgUnitReader
{
    Task<OrgUnitDto?> GetAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrgUnitDto>> ListAsync(CancellationToken cancellationToken);
}

public interface IPositionReader
{
    Task<PositionDto?> GetAsync(Guid positionId, CancellationToken cancellationToken);

    Task<PagedResult<PositionDto>> ListAsync(Guid? orgUnitId, int page, int pageSize, CancellationToken cancellationToken);
}
