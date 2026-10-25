using Npgsql;
using Respawn;

namespace Talent.TestSupport;

/// <summary>
/// One real PostgreSQL database for a test assembly. Real database, not EF Core InMemory (tdd-and-testing skill).
/// Start the server with scripts/pg-start.sh, or point TALENT_TEST_PG at any PostgreSQL 16+ server.
/// </summary>
public sealed class PostgresFixture : IAsyncDisposable
{
    private const string DefaultServer = "Host=127.0.0.1;Port=5433;Username=postgres";
    private readonly string _server = Environment.GetEnvironmentVariable("TALENT_TEST_PG") ?? DefaultServer;
    private readonly string _databaseName = $"talent_test_{Guid.NewGuid():N}";
    private Respawner? _respawner;

    public string ConnectionString => $"{_server};Database={_databaseName};Include Error Detail=true";

    /// <summary>Creates the database and lets <paramref name="migrate"/> build the schema.</summary>
    public async Task InitializeAsync(Func<string, Task> migrate)
    {
        ArgumentNullException.ThrowIfNull(migrate);

        await using (var admin = new NpgsqlConnection($"{_server};Database=postgres"))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        await migrate(ConnectionString);
    }

    /// <summary>Deletes all rows between tests. Schema and migration history stay.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["shared", "config"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("shared", "__ef_migrations"),
                new Respawn.Graph.Table("config", "__ef_migrations"),
                new Respawn.Graph.Table("shared", "audit_log"),
            ],
        });

        await _respawner.ResetAsync(connection);

        // The audit log is append-only. Tests reset it as the database owner, with its trigger off for one statement.
        await using var audit = new NpgsqlCommand(
            """
            DO $$
            BEGIN
                IF to_regclass('shared.audit_log') IS NOT NULL THEN
                    ALTER TABLE shared.audit_log DISABLE TRIGGER USER;
                    DELETE FROM shared.audit_log;
                    ALTER TABLE shared.audit_log ENABLE TRIGGER USER;
                END IF;
            END $$;
            """, connection);
        await audit.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection($"{_server};Database=postgres");
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
