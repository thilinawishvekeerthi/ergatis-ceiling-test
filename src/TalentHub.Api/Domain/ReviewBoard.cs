namespace TalentHub.Api.Domain;

public class ReviewBoard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HeadEmployeeId { get; set; } = string.Empty;
    public Employee HeadEmployee { get; set; } = null!;
    public ICollection<ReviewBoardMember> Members { get; set; } = new List<ReviewBoardMember>();
}

public class ReviewBoardMember
{
    public int Id { get; set; }
    public int ReviewBoardId { get; set; }
    public ReviewBoard ReviewBoard { get; set; } = null!;
    public string MemberEmployeeId { get; set; } = string.Empty;
    public Employee Member { get; set; } = null!;
}

public class ReviewMeeting
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public TalentReviewCycle Cycle { get; set; } = null!;
    public DateTime ScheduledAt { get; set; }
    public string HeadEmployeeId { get; set; } = string.Empty;
    public Employee HeadEmployee { get; set; } = null!;
    public ICollection<ReviewMeetingMember> Members { get; set; } = new List<ReviewMeetingMember>();
}

public class ReviewMeetingMember
{
    public int Id { get; set; }
    public int ReviewMeetingId { get; set; }
    public ReviewMeeting ReviewMeeting { get; set; } = null!;
    public string MemberEmployeeId { get; set; } = string.Empty;
    public Employee Member { get; set; } = null!;
}

public class LeadershipCandidate
{
    public int Id { get; set; }
    public string CandidateId { get; set; } = string.Empty;
    public Employee Candidate { get; set; } = null!;
    public int Score { get; set; }
    public string? Notes { get; set; }
    public string SelectedById { get; set; } = string.Empty;
    public Employee SelectedBy { get; set; } = null!;
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
}
