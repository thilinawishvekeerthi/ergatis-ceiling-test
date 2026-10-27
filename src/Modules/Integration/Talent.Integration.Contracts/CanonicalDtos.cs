namespace Talent.Integration.Contracts;

/// <summary>Canonical employee. <see cref="Id"/> is the Talent id; business modules store only this id.</summary>
public sealed record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Email,
    Guid? OrgUnitId,
    Guid? PositionId,
    Guid? ManagerId,
    DateOnly? HireDate,
    string Status);

public sealed record OrgUnitDto(Guid Id, string Code, string Name, Guid? ParentId, bool IsActive);

public sealed record PositionDto(Guid Id, string Code, string Title, Guid? OrgUnitId, bool IsActive);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
