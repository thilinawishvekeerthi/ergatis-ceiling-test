using Talent.Assessment.Contracts;
using Talent.Assessment.Cycles;
using Talent.Assessment.Reviews;
using Talent.Config.Contracts;

namespace Talent.Assessment.Tests;

public sealed class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static Review NewReview() => Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static Review RatedReview(GridCell cell)
    {
        var review = NewReview();
        review.Rate(4, 3, "Strong year", cell, Now);
        return review;
    }

    [Fact]
    public void Create_StartsPending()
    {
        var review = NewReview();

        review.Status.ShouldBe(ReviewStatus.Pending);
        review.FinalCell.ShouldBeNull();
    }

    [Fact]
    public void Create_WhenReviewerIsTheEmployee_Throws()
    {
        var person = Guid.NewGuid();

        Should.Throw<InvalidReviewException>(() => Review.Create(Guid.NewGuid(), person, person));
    }

    [Fact]
    public void Create_WhenIdsEmpty_Throws() =>
        Should.Throw<InvalidReviewException>(() => Review.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

    [Fact]
    public void Rate_StoresRatingsAndSetsProposedAndFinalCell()
    {
        var review = NewReview();

        review.Rate(4, 3, "  Strong year ", new GridCell(2, 2), Now);

        review.Status.ShouldBe(ReviewStatus.Rated);
        review.PerformanceLevel.ShouldBe(4);
        review.PotentialLevel.ShouldBe(3);
        review.Comment.ShouldBe("Strong year");
        review.ProposedColumn.ShouldBe(2);
        review.FinalCell.ShouldBe(new GridCell(2, 2));
        review.IsCalibrated.ShouldBeFalse();
        review.RatedAt.ShouldBe(Now);
        review.DomainEvents.OfType<ReviewRated>().Single().ReviewId.ShouldBe(review.Id);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    public void Rate_WhenLevelBelowOne_Throws(int performance, int potential) =>
        Should.Throw<InvalidReviewException>(() => NewReview().Rate(performance, potential, null, new GridCell(0, 0), Now));

    [Fact]
    public void Rate_WhenCommentTooLong_Throws() =>
        Should.Throw<InvalidReviewException>(() => NewReview().Rate(3, 2, new string('x', 2001), new GridCell(1, 1), Now));

    [Fact]
    public void Rate_Again_ReplacesCalibration()
    {
        var review = RatedReview(new GridCell(1, 1));
        review.Calibrate(new GridCell(2, 2), PlacementMode.CalculatedWithOverride, "committee", "Delivered a major launch", Now);

        review.Rate(3, 2, null, new GridCell(1, 1), Now.AddDays(1));

        review.IsCalibrated.ShouldBeFalse();
        review.FinalCell.ShouldBe(new GridCell(1, 1));
        review.CalibrationReason.ShouldBeNull();
    }

    [Fact]
    public void Calibrate_MovesFinalCellButKeepsProposed_AndRecordsAuthorAndReason()
    {
        var review = RatedReview(new GridCell(1, 1));

        review.Calibrate(new GridCell(2, 2), PlacementMode.CalculatedWithOverride, "committee", " Delivered a major launch ", Now.AddDays(10));

        review.FinalCell.ShouldBe(new GridCell(2, 2));
        review.ProposedColumn.ShouldBe(1);
        review.ProposedRow.ShouldBe(1);
        review.IsCalibrated.ShouldBeTrue();
        review.CalibratedBy.ShouldBe("committee");
        review.CalibrationReason.ShouldBe("Delivered a major launch");
        review.CalibratedAt.ShouldBe(Now.AddDays(10));
        var moved = review.DomainEvents.OfType<PlacementCalibrated>().Single();
        (moved.FromColumn, moved.FromRow, moved.ToColumn, moved.ToRow).ShouldBe((1, 1, 2, 2));
    }

    [Fact]
    public void Calibrate_WhenReasonMissing_Throws()
    {
        var review = RatedReview(new GridCell(1, 1));

        Should.Throw<InvalidReviewException>(() =>
            review.Calibrate(new GridCell(2, 2), PlacementMode.CalculatedWithOverride, "committee", "  ", Now));
    }

    [Theory]
    [InlineData(PlacementMode.Manual)]
    [InlineData(PlacementMode.Calculated)]
    public void Calibrate_WhenGridDoesNotAllowOverride_Throws(PlacementMode mode)
    {
        var review = RatedReview(new GridCell(1, 1));

        Should.Throw<AssessmentConflictException>(() => review.Calibrate(new GridCell(2, 2), mode, "committee", "Reason", Now));
    }

    [Fact]
    public void Calibrate_WhenNotRatedYet_Throws() =>
        Should.Throw<AssessmentConflictException>(() =>
            NewReview().Calibrate(new GridCell(2, 2), PlacementMode.CalculatedWithOverride, "committee", "Reason", Now));

    [Fact]
    public void Calibrate_WhenAlreadyInThatCell_Throws()
    {
        var review = RatedReview(new GridCell(1, 1));

        Should.Throw<InvalidReviewException>(() =>
            review.Calibrate(new GridCell(1, 1), PlacementMode.CalculatedWithOverride, "committee", "Reason", Now));
    }
}
