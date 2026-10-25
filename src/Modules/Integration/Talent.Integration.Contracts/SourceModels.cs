namespace Talent.Integration.Contracts;

/// <summary>Organisation unit as the source system sends it. References use source keys.</summary>
public sealed record OrgUnitSource(string Code, string Name, string? ParentCode, bool IsActive);

public sealed record PositionSource(string Code, string Title, string? OrgUnitCode, bool IsActive);

public sealed record EmployeeSource(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Email,
    string? OrgUnitCode,
    string? PositionCode,
    string? ManagerEmployeeNumber,
    DateOnly? HireDate,
    string Status);
