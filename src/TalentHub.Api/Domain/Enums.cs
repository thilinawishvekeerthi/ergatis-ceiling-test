namespace TalentHub.Api.Domain;

public enum AppRole { Employee = 0, Reviewer = 1, Supervisor = 2, ReviewBoard = 3, HrAdmin = 4 }

public enum AssessmentStatus { Draft = 0, Submitted = 1, BoardPlaced = 2 }

public enum ReadinessLevel { NotReady = 0, Now = 1, ReadyIn1Year = 2, ReadyIn2Years = 3 }

public enum DevelopmentPlanStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3 }

public enum ReviewFrequency { Monthly = 0, Quarterly = 1, HalfYearly = 2, Yearly = 3 }

public enum OverallRatingMethod { AverageOfCompetencies = 0, WeightedAverage = 1, Manual = 2 }
