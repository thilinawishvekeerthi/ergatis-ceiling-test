using Talent.Career.Contracts;
using Talent.Career.Jobs;
using Talent.Shared.Events;

namespace Talent.Career.Profiles;

/// <summary>An employee's level in one competency, from 1 to <see cref="Job.MaxCompetencyLevel"/>.</summary>
internal sealed class EmployeeCompetency
{
    // For EF Core.
    private EmployeeCompetency() => CompetencyCode = "";

    public EmployeeCompetency(string competencyCode, int level)
    {
        CompetencyCode = competencyCode;
        Level = level;
    }

    public Guid EmployeeId { get; private set; }
    public string CompetencyCode { get; private set; }
    public int Level { get; private set; }

    internal void Set(int level) => Level = level;
}

/// <summary>What the Career module knows about one employee: their current job and competency levels.</summary>
internal sealed class EmployeeProfile : IHasDomainEvents
{
    public const int MaxCompetencies = 100;

    private readonly List<IDomainEvent> _events = [];
    private readonly List<EmployeeCompetency> _competencies = [];

    // For EF Core.
    private EmployeeProfile()
    {
    }

    public EmployeeProfile(Guid employeeId)
    {
        if (employeeId == Guid.Empty)
        {
            throw new InvalidCareerException("A profile needs an employee.");
        }

        EmployeeId = employeeId;
    }

    public Guid EmployeeId { get; private set; }
    public Guid? CurrentJobId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<EmployeeCompetency> Competencies => _competencies;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    public IReadOnlyDictionary<string, int> Levels => _competencies.ToDictionary(c => c.CompetencyCode, c => c.Level, StringComparer.Ordinal);

    /// <summary>Replaces the current job and the whole competency list. Rows are updated in place, added or removed.</summary>
    public void Replace(Guid? currentJobId, IEnumerable<(string Code, int Level)> competencies, DateTimeOffset now)
    {
        var wanted = competencies.ToList();
        if (wanted.Count > MaxCompetencies)
        {
            throw new InvalidCareerException($"A profile can hold at most {MaxCompetencies} competencies.");
        }

        foreach (var (code, level) in wanted)
        {
            if (!CompetencyCodes.IsValid(code))
            {
                throw new InvalidCareerException($"'{code}' is not a valid competency code.");
            }

            if (level is < Job.MinCompetencyLevel or > Job.MaxCompetencyLevel)
            {
                throw new InvalidCareerException($"{code}: the level must be {Job.MinCompetencyLevel} to {Job.MaxCompetencyLevel}.");
            }
        }

        if (wanted.Select(w => w.Code).Distinct(StringComparer.Ordinal).Count() != wanted.Count)
        {
            throw new InvalidCareerException("A competency can appear only once in a profile.");
        }

        _competencies.RemoveAll(existing => wanted.All(w => w.Code != existing.CompetencyCode));
        foreach (var (code, level) in wanted)
        {
            var existing = _competencies.Find(c => c.CompetencyCode == code);
            if (existing is null)
            {
                _competencies.Add(new EmployeeCompetency(code, level));
            }
            else
            {
                existing.Set(level);
            }
        }

        CurrentJobId = currentJobId;
        UpdatedAt = now;
        _events.Add(new EmployeeProfileUpdated(now, EmployeeId, CurrentJobId, _competencies.Count));
    }
}
