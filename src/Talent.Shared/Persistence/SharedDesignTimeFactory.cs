using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Talent.Shared.Persistence;

/// <summary>Used only by <c>dotnet ef</c>. Reads TALENT_DB or falls back to the local dev database.</summary>
internal sealed class SharedDesignTimeFactory : IDesignTimeDbContextFactory<TalentSharedDbContext>
{
    public TalentSharedDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TALENT_DB")
                         ?? "Host=127.0.0.1;Port=5433;Username=postgres;Database=talent";
        var options = new DbContextOptionsBuilder<TalentSharedDbContext>()
            .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema))
            .Options;
        return new TalentSharedDbContext(options);
    }
}
