namespace Talent.Integration.Canonical;

/// <summary>Canonical rows are owned by Integration and written only by the sync. Source keys live in <see cref="ExternalIdMapEntry"/>.</summary>
internal sealed class OrgUnit
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset SourceModifiedAt { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}

internal sealed class Position
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public Guid? OrgUnitId { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset SourceModifiedAt { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}

internal sealed class Employee
{
    public Guid Id { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public Guid? OrgUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public Guid? ManagerId { get; set; }

    public DateOnly? HireDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset SourceModifiedAt { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}

/// <summary>(system, entity, external id) to Talent id. Every cross-system key goes through here.</summary>
internal sealed class ExternalIdMapEntry
{
    public string System { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public Guid InternalId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Last successful sync per system and entity. The next delta starts here.</summary>
internal sealed class SyncState
{
    public string System { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public DateTimeOffset LastSuccessAt { get; set; }
}

internal sealed class DeadLetter
{
    public Guid Id { get; set; }

    public string System { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string Error { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTimeOffset FirstFailedAt { get; set; }

    public DateTimeOffset LastFailedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}

internal static class EntityNames
{
    public const string OrgUnit = "OrgUnit";
    public const string Position = "Position";
    public const string Employee = "Employee";
}
