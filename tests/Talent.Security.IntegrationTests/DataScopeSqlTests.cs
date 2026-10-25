using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talent.Security.Contracts;
using Talent.Security.Persistence;
using Talent.Security.Scopes;

namespace Talent.Security.IntegrationTests;

/// <summary>The scope filter must run in SQL, not after loading rows (talent-security skill).</summary>
public sealed class DataScopeSqlTests(SecurityHostFixture host) : IClassFixture<SecurityHostFixture>
{
    private sealed class Reports(params Guid[] ids) : IScopeMemberSource
    {
        public Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid caller, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(scope == DataScope.DirectReports ? ids : []);
    }

    private static AccessGrant Grant(params DataScope[] scopes) => new(scopes.ToHashSet(), FieldClass.Internal, false);

    [Fact]
    public async Task Filter_RunsInSqlAndReturnsOnlyEmployeesInScope()
    {
        using var scope = host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        var me = host.Employees[SecurityHostFixture.Manager];
        var report = host.Employees[SecurityHostFixture.Employee];
        var user = SecurityContext.ForTest(SecurityHostFixture.Manager, me, ["MANAGER"]);
        var service = new DataScopeService(user, new Reports(report));

        var filter = await service.FilterAsync<UserAccount>(Grant(DataScope.Self, DataScope.DirectReports), a => a.EmployeeId, SecurityHostFixture.Ct);
        var query = db.Accounts.AsNoTracking().Where(filter);
        var subjects = await query.Select(a => a.Subject).ToListAsync(SecurityHostFixture.Ct);

        query.ToQueryString().ShouldContain("WHERE", Case.Insensitive);
        subjects.Order().ShouldBe([SecurityHostFixture.Manager, SecurityHostFixture.Employee]);
    }

    [Fact]
    public async Task Filter_ForSelfOnly_ReturnsOneRow()
    {
        using var scope = host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        var user = SecurityContext.ForTest(SecurityHostFixture.Employee, host.Employees[SecurityHostFixture.Employee], ["EMPLOYEE"]);

        var filter = await new DataScopeService(user, new Reports()).FilterAsync<UserAccount>(Grant(DataScope.Self), a => a.EmployeeId, SecurityHostFixture.Ct);

        (await db.Accounts.Where(filter).Select(a => a.Subject).ToListAsync(SecurityHostFixture.Ct)).ShouldBe([SecurityHostFixture.Employee]);
    }
}
