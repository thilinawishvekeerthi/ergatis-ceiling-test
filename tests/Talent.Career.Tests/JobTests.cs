using Talent.Career.Jobs;

namespace Talent.Career.Tests;

public sealed class JobTests
{
    private static Job NewJob(params JobRequirement[] requirements) =>
        Job.Create("SENIOR_DEV", " Senior Developer ", 5, "Builds things", requirements);

    [Fact]
    public void Create_TrimsTitleAndStartsActive()
    {
        var job = NewJob(new JobRequirement("SQL", 3));

        job.Title.ShouldBe("Senior Developer");
        job.IsActive.ShouldBeTrue();
        job.Requirements.Single().CompetencyCode.ShouldBe("SQL");
    }

    [Theory]
    [InlineData("")]
    [InlineData("lower")]
    [InlineData("A")]
    [InlineData("1ABC")]
    public void Create_WhenCodeInvalid_Throws(string code) =>
        Should.Throw<InvalidCareerException>(() => Job.Create(code, "Title", 1, null, []));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenTitleBlank_Throws(string title) =>
        Should.Throw<InvalidCareerException>(() => Job.Create("DEV", title, 1, null, []));

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Create_WhenLevelOutOfRange_Throws(int level) =>
        Should.Throw<InvalidCareerException>(() => Job.Create("DEV", "Developer", level, null, []));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_WhenRequiredLevelOutOfRange_Throws(int level) =>
        Should.Throw<InvalidCareerException>(() => NewJob(new JobRequirement("SQL", level)));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_WhenWeightOutOfRange_Throws(int weight) =>
        Should.Throw<InvalidCareerException>(() => NewJob(new JobRequirement("SQL", 3, weight)));

    [Fact]
    public void Create_WhenCompetencyRepeated_Throws() =>
        Should.Throw<InvalidCareerException>(() => NewJob(new JobRequirement("SQL", 3), new JobRequirement("SQL", 4)));

    [Fact]
    public void Create_WhenCompetencyCodeInvalid_Throws() =>
        Should.Throw<InvalidCareerException>(() => NewJob(new JobRequirement("not valid", 3)));

    [Fact]
    public void Create_WhenTooManyRequirements_Throws()
    {
        var many = Enumerable.Range(1, Job.MaxRequirements + 1).Select(i => new JobRequirement($"SKILL_{i}", 1)).ToArray();

        Should.Throw<InvalidCareerException>(() => NewJob(many));
    }

    [Fact]
    public void Update_ChangesRequirementsInPlace_AddsAndRemoves()
    {
        var job = NewJob(new JobRequirement("SQL", 3), new JobRequirement("LEADERSHIP", 2));
        var sql = job.Requirements.Single(r => r.CompetencyCode == "SQL");

        job.Update("Principal Developer", 6, null, true, [new JobRequirement("SQL", 5, 2), new JobRequirement("ARCHITECTURE", 4)]);

        job.Requirements.Select(r => r.CompetencyCode).Order().ShouldBe(["ARCHITECTURE", "SQL"]);
        job.Requirements.Single(r => r.CompetencyCode == "SQL").ShouldBeSameAs(sql);
        (sql.RequiredLevel, sql.Weight).ShouldBe((5, 2));
        job.Title.ShouldBe("Principal Developer");
        job.Description.ShouldBeNull();
    }

    [Fact]
    public void Update_CanDeactivate()
    {
        var job = NewJob();

        job.Update("Senior Developer", 5, null, false, []);

        job.IsActive.ShouldBeFalse();
    }
}
