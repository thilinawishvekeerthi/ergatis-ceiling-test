using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Talent.SqlServerSmoke.Tests;

public sealed class SqlServerSmokeTests
{
    [Fact]
    public async Task Select_one_returns_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync(ct);

        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync(ct);

        result.ShouldBe(1);
    }
}
