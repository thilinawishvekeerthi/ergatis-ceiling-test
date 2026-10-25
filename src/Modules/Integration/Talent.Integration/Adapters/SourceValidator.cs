using System.Net.Mail;
using Talent.Integration.Contracts;

namespace Talent.Integration.Adapters;

/// <summary>Checks a source record before it touches canonical tables. Returns an error text, or null when valid.</summary>
internal static class SourceValidator
{
    public static readonly IReadOnlyCollection<string> EmployeeStatuses = ["Active", "OnLeave", "Terminated"];

    public static string? Validate(EmployeeSource e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return Required(e.EmployeeNumber, "EmployeeNumber", 50)
               ?? Required(e.FirstName, "FirstName", 200)
               ?? Required(e.LastName, "LastName", 200)
               ?? OptionalEmail(e.Email)
               ?? (EmployeeStatuses.Contains(e.Status) ? null : $"Unknown status '{Trim(e.Status)}'")
               ?? (string.Equals(e.ManagerEmployeeNumber, e.EmployeeNumber, StringComparison.Ordinal) ? "Employee cannot report to themselves" : null)
               ?? OptionalKey(e.OrgUnitCode, "OrgUnitCode")
               ?? OptionalKey(e.PositionCode, "PositionCode")
               ?? OptionalKey(e.ManagerEmployeeNumber, "ManagerEmployeeNumber");
    }

    public static string? Validate(OrgUnitSource o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return Required(o.Code, "Code", 50)
               ?? Required(o.Name, "Name", 200)
               ?? OptionalKey(o.ParentCode, "ParentCode")
               ?? (string.Equals(o.ParentCode, o.Code, StringComparison.Ordinal) ? "Org unit cannot be its own parent" : null);
    }

    public static string? Validate(PositionSource p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return Required(p.Code, "Code", 50)
               ?? Required(p.Title, "Title", 200)
               ?? OptionalKey(p.OrgUnitCode, "OrgUnitCode");
    }

    private static string? Required(string? value, string field, int max) =>
        string.IsNullOrWhiteSpace(value) ? $"{field} is required"
        : value.Length > max ? $"{field} is longer than {max} characters"
        : null;

    private static string? OptionalKey(string? value, string field) =>
        value is null ? null : Required(value, field, 50);

    private static string? OptionalEmail(string? email)
    {
        if (email is null)
        {
            return null;
        }

        return email.Length <= 320 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email
            ? null
            : "Email is not valid";
    }

    private static string Trim(string? s) => s is { Length: > 30 } ? s[..30] : s ?? string.Empty;
}
