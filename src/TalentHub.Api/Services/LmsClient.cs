using TalentHub.Api.Domain;

namespace TalentHub.Api.Services;

public record LmsCourse(int Id, string Title, string Url, string RoleOrCompetency);
public record LmsEmployeeCourse(int CourseId, string Status, string? AssignedOn);

public interface ILmsClient
{
    Task<IReadOnlyList<LmsCourse>> GetCoursesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LmsEmployeeCourse>> GetEmployeeCoursesAsync(string employeeUserId, CancellationToken ct = default);
}

/// <summary>Stand-in for the client-provided LMS APIs (courses per role/competency; per-employee course status).</summary>
public class MockLmsClient : ILmsClient
{
    private static readonly List<LmsCourse> Courses =
    [
        new(1, "Leadership Foundations", "https://lms.local/course/1", "Leadership"),
        new(2, "Advanced Facilitation", "https://lms.local/course/2", "Collaboration"),
        new(3, "Delivery Excellence", "https://lms.local/course/3", "Delivery"),
        new(4, "People Management 101", "https://lms.local/course/4", "HR Manager"),
    ];

    private static readonly List<LmsEmployeeCourse> EmployeeCourses =
    [
        new(1, "Ongoing", "2026-09-01"),
        new(3, "Assigned", "2026-10-01"),
    ];

    public Task<IReadOnlyList<LmsCourse>> GetCoursesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LmsCourse>>(Courses);

    public Task<IReadOnlyList<LmsEmployeeCourse>> GetEmployeeCoursesAsync(string employeeUserId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LmsEmployeeCourse>>(EmployeeCourses);
}
