using Talent.Career.Contracts;
using Talent.Career.Profiles;

namespace Talent.Career.Tests;

public sealed class EmployeeProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Replace_SetsJobAndCompetencies_AndRaisesEvent()
    {
        var job = Guid.NewGuid();
        var profile = new EmployeeProfile(Guid.NewGuid());

        profile.Replace(job, [("SQL", 3), ("LEADERSHIP", 2)], Now);

        profile.CurrentJobId.ShouldBe(job);
        profile.Levels.ShouldBe(new Dictionary<string, int> { ["SQL"] = 3, ["LEADERSHIP"] = 2 });
        profile.UpdatedAt.ShouldBe(Now);
        var raised = profile.DomainEvents.OfType<EmployeeProfileUpdated>().Single();
        (raised.EmployeeId, raised.CompetencyCount).ShouldBe((profile.EmployeeId, 2));
    }

    [Fact]
    public void Replace_UpdatesInPlace_AddsAndRemoves()
    {
        var profile = new EmployeeProfile(Guid.NewGuid());
        profile.Replace(null, [("SQL", 3), ("LEADERSHIP", 2)], Now);
        var sql = profile.Competencies.Single(c => c.CompetencyCode == "SQL");

        profile.Replace(null, [("SQL", 4), ("ARCHITECTURE", 1)], Now.AddDays(1));

        profile.Competencies.Single(c => c.CompetencyCode == "SQL").ShouldBeSameAs(sql);
        profile.Levels.ShouldBe(new Dictionary<string, int> { ["SQL"] = 4, ["ARCHITECTURE"] = 1 });
    }

    [Fact]
    public void Create_WhenEmployeeEmpty_Throws() =>
        Should.Throw<InvalidCareerException>(() => new EmployeeProfile(Guid.Empty));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Replace_WhenLevelOutOfRange_Throws(int level) =>
        Should.Throw<InvalidCareerException>(() => new EmployeeProfile(Guid.NewGuid()).Replace(null, [("SQL", level)], Now));

    [Fact]
    public void Replace_WhenCompetencyRepeated_Throws() =>
        Should.Throw<InvalidCareerException>(() => new EmployeeProfile(Guid.NewGuid()).Replace(null, [("SQL", 1), ("SQL", 2)], Now));

    [Fact]
    public void Replace_WhenCodeInvalid_Throws() =>
        Should.Throw<InvalidCareerException>(() => new EmployeeProfile(Guid.NewGuid()).Replace(null, [("sql", 1)], Now));

    [Fact]
    public void Replace_WhenTooMany_Throws()
    {
        var many = Enumerable.Range(1, EmployeeProfile.MaxCompetencies + 1).Select(i => ($"SKILL_{i}", 1));

        Should.Throw<InvalidCareerException>(() => new EmployeeProfile(Guid.NewGuid()).Replace(null, many, Now));
    }

    [Fact]
    public void Replace_WithNothing_ClearsCompetencies()
    {
        var profile = new EmployeeProfile(Guid.NewGuid());
        profile.Replace(null, [("SQL", 3)], Now);

        profile.Replace(null, [], Now);

        profile.Competencies.ShouldBeEmpty();
    }
}
