namespace Talent.Assessment.Contracts;

/// <summary>Draft is set up, Open collects ratings, Calibration lets the committee move placements, Closed is final.</summary>
public enum CycleStatus
{
    Draft = 0,
    Open = 1,
    Calibration = 2,
    Closed = 3,
}

public enum ReviewStatus
{
    Pending = 0,
    Rated = 1,
}
