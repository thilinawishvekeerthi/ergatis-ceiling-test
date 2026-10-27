using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Talent.Security.Contracts;

namespace Talent.Security.Matrix;

/// <summary>
/// The permission matrix: role x action x object, with a data scope and a field class per grant (ADR-07).
/// It is a versioned config payload, so a change is a new version. Nothing here is hard-coded per client.
/// </summary>
internal sealed partial class PermissionMatrix
{
    public const string ConfigCode = "DEFAULT";
    private const int MaxGrants = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private readonly IReadOnlyList<Grant> _grants;

    private PermissionMatrix(IReadOnlyList<Grant> grants) => _grants = grants;

    public static PermissionMatrix Empty { get; } = new([]);

    public static bool TryParse(string json, out PermissionMatrix? matrix, out IReadOnlyList<string> errors)
    {
        matrix = null;
        var problems = new List<string>();
        errors = problems;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(json, Json);
        }
        catch (JsonException ex)
        {
            problems.Add($"The permission matrix is not valid JSON for this shape: {ex.Message}");
            return false;
        }

        if (payload?.Grants is not { Count: > 0 } grants)
        {
            problems.Add("The matrix needs at least one grant.");
            return false;
        }

        if (grants.Count > MaxGrants)
        {
            problems.Add($"The matrix has more than {MaxGrants} grants.");
            return false;
        }

        var parsed = new List<Grant>();
        for (var i = 0; i < grants.Count; i++)
        {
            var g = grants[i];
            if (g is null)
            {
                problems.Add($"Grant {i + 1} is empty.");
                continue;
            }

            var before = problems.Count;
            if (g.Role is null || !RolePattern().IsMatch(g.Role))
            {
                problems.Add($"Grant {i + 1}: the role must be capital letters, digits and underscores (2 to 50 characters).");
            }

            if (g.Resource is null || !ResourcePattern().IsMatch(g.Resource))
            {
                problems.Add($"Grant {i + 1}: the resource must be '*', 'name:*' or 'name:Object'.");
            }

            if (g.Actions is not { Count: > 0 } || g.Actions.Any(a => !Enum.IsDefined(a)))
            {
                problems.Add($"Grant {i + 1}: list at least one valid action.");
            }

            if (!Enum.IsDefined(g.Scope))
            {
                problems.Add($"Grant {i + 1}: unknown data scope.");
            }

            if (g.FieldClass is not (FieldClass.Public or FieldClass.Internal))
            {
                problems.Add($"Grant {i + 1}: fieldClass is Public or Internal. Use talentConfidential for confidential fields.");
            }

            if (problems.Count == before)
            {
                parsed.Add(new Grant(g.Role!, g.Resource!, [.. g.Actions!], g.Scope, g.FieldClass, g.TalentConfidential));
            }
        }

        if (problems.Count > 0)
        {
            return false;
        }

        var candidate = new PermissionMatrix(parsed);
        if (candidate.Evaluate(RolesOf(parsed), Resources.PermissionMatrix, PermissionAction.Approve) is not { CoversAllEmployees: true })
        {
            problems.Add("No role could approve changes to the permission matrix. Publishing this would lock everyone out.");
            return false;
        }

        matrix = candidate;
        return true;
    }

    /// <summary>Combines every grant that matches one of the roles. Returns null when none does: deny by default.</summary>
    public AccessGrant? Evaluate(IReadOnlySet<string> roles, string resource, PermissionAction action)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var scopes = new HashSet<DataScope>();
        var fieldClass = FieldClass.Public;
        var confidential = false;
        var found = false;

        foreach (var g in _grants)
        {
            if (!roles.Contains(g.Role) || !g.Actions.Contains(action) || !Matches(g.Resource, resource))
            {
                continue;
            }

            found = true;
            scopes.Add(g.Scope);
            fieldClass = (FieldClass)Math.Max((int)fieldClass, (int)g.FieldClass);
            confidential |= g.TalentConfidential;
        }

        return found ? new AccessGrant(scopes, fieldClass, confidential) : null;
    }

    /// <summary>True when a role may do the action on at least one object whose name starts with the prefix.</summary>
    public bool HasAny(IReadOnlySet<string> roles, string resourcePrefix, PermissionAction action) =>
        _grants.Any(g => roles.Contains(g.Role)
                         && g.Actions.Contains(action)
                         && (g.Resource == "*" || g.Resource.StartsWith(resourcePrefix, StringComparison.Ordinal)));

    private static bool Matches(string pattern, string resource) =>
        pattern == "*"
        || pattern == resource
        || pattern.EndsWith(":*", StringComparison.Ordinal) && resource.StartsWith(pattern[..^1], StringComparison.Ordinal);

    private static HashSet<string> RolesOf(List<Grant> grants) => [.. grants.Select(g => g.Role)];

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex RolePattern();

    [GeneratedRegex(@"^(\*|[a-z][a-z0-9_]*:(\*|[A-Za-z][A-Za-z0-9_.]*))$")]
    private static partial Regex ResourcePattern();

    private sealed record Grant(
        string Role, string Resource, HashSet<PermissionAction> Actions, DataScope Scope, FieldClass FieldClass, bool TalentConfidential);

    private sealed record Payload(List<GrantPayload>? Grants);

    private sealed record GrantPayload(
        string? Role, string? Resource, List<PermissionAction>? Actions, DataScope Scope, FieldClass FieldClass, bool TalentConfidential);
}
