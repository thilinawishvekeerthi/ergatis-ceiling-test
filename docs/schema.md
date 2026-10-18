# TalentHub — Data Schema

EF Core entities in `src/TalentHub.Api/Domain/`; constraints in `Data/TalentHubDbContext.cs`. Identity is ASP.NET Core Identity with `Employee` as the user type.

## Conventions

- People FKs are **string** ids (Identity GUID keys) and **Restrict** on delete (assessment/succession/plan history must survive employee changes).
- Definition tables (job families, jobs, scales) **cascade** to children.
- Status enums live in `Domain/Enums.cs`.

## Entity map

| Area | Entity | Key fields / notes |
|------|--------|--------------------|
| Identity | `Employee : IdentityUser` | FullName, Resume, JobId→Jobs, SupervisorRefId→self (Restrict; `SupervisorId` int kept Ignored for legacy), IsActive |
| Career Centre | `CareerPreference` | 1:1 with Employee (unique EmployeeId, cascade), mobility, workstation, long-term goals |
| Career Centre | `CareerPreferenceJob` | preference ↔ job, unique pair |
| Definitions | `JobFamily` → `Job` | Name unique; job title unique per family |
| Definitions | `AlternativeCareerPath` | From→To job family, unique pair; To-side Restrict |
| Definitions | `Scale` → `ScaleLevel` | Label + Value, unique per scale |
| Assessment | `TalentReviewCycle` | Name, StartDate, EndDate, IsOpen, optional Scale |
| Assessment | `CycleCompetency` | name unique per cycle, optional Weight |
| Assessment | `TalentAssessment` | Cycle+Subject+Reviewer unique; SubjectId/ReviewerId are string FKs (Restrict); Status Draft→Submitted→BoardPlaced; PerformanceRating, PotentialRating, Remarks |
| Assessment | `AssessmentCompetencyScore` | per-cycle-competency score, unique per assessment |
| Assessment | `NineBoxPlacement` | 1:1 with assessment; PerformanceAxis 1–3 × PotentialAxis 1–3; PlacedByRole, PlacedAt |
| Review boards | `ReviewBoard` / `ReviewBoardMember` | Name unique; Head Restrict; member unique per board |
| Review meetings | `ReviewMeeting` / `ReviewMeetingMember` | per cycle, ScheduledAt, Head + members |
| Leadership | `LeadershipCandidate` | Candidate unique; Score; SelectedBy |
| Succession | `CriticalPosition` | Title, Department, IncumbentEmployee, Notes |
| Succession | `SuccessionPlan` | Position+successor unique; Readiness enum: NotReady/Now/ReadyIn1Year/ReadyIn2Years; Suitability |
| Dev plans | `DevelopmentPlanConfig` | singleton (Id=1 seeded): ReviewFrequency, ReviewPeriodMonths (1–36), OverallRatingMethod |
| Dev plans | `DevelopmentPlan` | EmployeeId (string FK), Title, Status Draft→Submitted→Approved/Rejected, OverallRating |
| Dev plans | `DevelopmentPlanItem` | CompetencyName, Action, SupportRequired, MeasureOfSuccess, TargetDate |
| Alerts | `Alert` | EmployeeId (string FK), Message, CreatedAt, IsRead; indexed (EmployeeId, IsRead) |
| LMS | *(none)* | Deliberate: client APIs unknown; adapter `ILmsClient` + `MockLmsClient` in `Services/` |

## Enum glossary

- `AppRole`: Employee, Reviewer, Supervisor, ReviewBoard, HrAdmin — mirrors blueprint work categories; each is an authorization policy.
- `AssessmentStatus`: Draft → Submitted → BoardPlaced.
- `ReadinessLevel`: NotReady, Now, ReadyIn1Year, ReadyIn2Years.
- `DevelopmentPlanStatus`: Draft, Submitted, Approved, Rejected.
- `ReviewFrequency`: Monthly, Quarterly, HalfYearly, Yearly.
- `OverallRatingMethod`: AverageOfCompetencies, WeightedAverage, Manual.

## Bootstrap

`Db.Migrate` runs migrations when present, else `EnsureCreated` (no design-time migrations shipped). `Db.SeedAsync` seeds roles, 8 demo users (with normalized user names and hashed passwords), 4 job families with jobs and alternative paths, a 3-point scale, an open review cycle with 3 competencies, dev-plan config and one critical position. Seeding is idempotent (no-op when users exist).
