using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Shared.Security;

namespace Talent.Config.Defaults;

/// <summary>
/// Publishes the default scales and the classic nine-box so a new install works before the client sends its values.
/// These are defaults, not the client's answers to blockers B1 to B5. The client replaces them with new versions.
/// </summary>
internal static class ConfigDefaultsSeeder
{
    public const string PerformanceScaleCode = "PERFORMANCE_5";
    public const string PotentialScaleCode = "POTENTIAL_3";
    public const string NineBoxCode = "NINE_BOX";

    private const string PerformanceScale = """
        {"levels":[
          {"level":1,"label":"Unsatisfactory","minScore":1,"band":"LOW"},
          {"level":2,"label":"Needs improvement","minScore":2,"band":"LOW"},
          {"level":3,"label":"Meets expectations","minScore":3,"band":"MEDIUM"},
          {"level":4,"label":"Exceeds expectations","minScore":4,"band":"HIGH"},
          {"level":5,"label":"Outstanding","minScore":5,"band":"HIGH"}]}
        """;

    private const string PotentialScale = """
        {"levels":[
          {"level":1,"label":"Limited","minScore":1,"band":"LOW"},
          {"level":2,"label":"Moderate","minScore":2,"band":"MEDIUM"},
          {"level":3,"label":"High","minScore":3,"band":"HIGH"}]}
        """;

    // Rows are potential (low to high), columns are performance (low to high).
    private static readonly string[][] Labels =
    [
        ["Under Performer", "Effective", "Trusted Professional"],
        ["Dilemma", "Core Employee", "High Performer"],
        ["Enigma", "Growth Employee", "Star"],
    ];

    private static readonly string[][] Colours =
    [
        ["#d9534f", "#f0ad4e", "#9acd32"],
        ["#f0ad4e", "#ffd966", "#5cb85c"],
        ["#ffd966", "#5cb85c", "#2e8b57"],
    ];

    private static readonly decimal[] ColumnBounds = [1m, 3m, 4m];
    private static readonly decimal[] RowBounds = [1m, 2m, 3m];

    public static async Task EnsureAsync(ConfigDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);

        var admin = new ConfigAdminService(db, new SystemCurrentUser(), clock);

        var performanceId = await EnsureAsync(db, admin, ConfigType.Scale, PerformanceScaleCode, "Performance (5 levels)", PerformanceScale, cancellationToken);
        var potentialId = await EnsureAsync(db, admin, ConfigType.Scale, PotentialScaleCode, "Potential (3 levels)", PotentialScale, cancellationToken);
        await EnsureAsync(db, admin, ConfigType.Grid, NineBoxCode, "Nine-box (3 x 3)", NineBox(performanceId, potentialId), cancellationToken);
    }

    internal static async Task<Guid> EnsureAsync(
        ConfigDbContext db, ConfigAdminService admin, ConfigType type, string code, string name, string payload, CancellationToken cancellationToken)
    {
        var existing = await (
            from v in db.Versions
            join d in db.Definitions on v.DefinitionId equals d.Id
            where d.Type == type && d.Code == code
            orderby v.VersionNo
            select v.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing != Guid.Empty)
        {
            return existing;
        }

        var definitionId = await admin.CreateDefinitionAsync(type, code, name, cancellationToken);
        var versionId = await admin.CreateDraftAsync(definitionId, payload, cancellationToken);
        await admin.PublishAsync(versionId, cancellationToken);
        return versionId;
    }

    private static string NineBox(Guid columnScaleVersionId, Guid rowScaleVersionId)
    {
        var cells = new List<object>();
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var sum = row + column;
                cells.Add(new
                {
                    column,
                    row,
                    label = Labels[row][column],
                    colour = Colours[row][column],
                    band = sum <= 1 ? "LOW" : sum == 2 ? "MEDIUM" : "HIGH",
                });
            }
        }

        return PayloadJson.Write(new
        {
            mode = "CalculatedWithOverride",
            columnScaleVersionId,
            rowScaleVersionId,
            columnBounds = ColumnBounds,
            rowBounds = RowBounds,
            cells,
        });
    }
}
