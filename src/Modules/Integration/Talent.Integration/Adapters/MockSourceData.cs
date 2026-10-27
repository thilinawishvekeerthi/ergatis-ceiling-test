using Bogus;
using Talent.Integration.Contracts;

namespace Talent.Integration.Adapters;

/// <summary>
/// In-memory "Core HR" built with Bogus, shaped like the real source: source keys, parents by code, managers by
/// employee number. Same seed gives the same data. Screens and tests run against this before a real adapter exists.
/// </summary>
internal sealed class MockSourceData
{
    private readonly Lock _gate = new();
    private readonly List<SourceRecord<OrgUnitSource>> _orgUnits = [];
    private readonly List<SourceRecord<PositionSource>> _positions = [];
    private readonly List<SourceRecord<EmployeeSource>> _employees = [];

    public MockSourceData(int seed, int employees, DateTimeOffset baseTime)
    {
        Randomizer.Seed = new Random(seed);
        var faker = new Faker("en") { Random = new Randomizer(seed) };

        _orgUnits.Add(new("ORG-000", new OrgUnitSource("ORG-000", faker.Company.CompanyName(), null, true), baseTime));
        for (var i = 1; i <= 6; i++)
        {
            var code = $"ORG-{i:000}";
            var parent = i <= 3 ? "ORG-000" : $"ORG-{faker.Random.Int(1, 3):000}";
            _orgUnits.Add(new(code, new OrgUnitSource(code, $"{faker.Commerce.Department()} {i}", parent, true), baseTime));
        }

        for (var i = 1; i <= 12; i++)
        {
            var code = $"POS-{i:000}";
            var unit = _orgUnits[faker.Random.Int(1, _orgUnits.Count - 1)].ExternalId;
            _positions.Add(new(code, new PositionSource(code, faker.Name.JobTitle(), unit, true), baseTime));
        }

        for (var i = 1; i <= employees; i++)
        {
            var number = $"E{i:00000}";
            // The first employee is the top manager; everyone else reports to someone with a lower number.
            var manager = i == 1 ? null : $"E{faker.Random.Int(1, i - 1):00000}";
            var first = faker.Name.FirstName();
            var last = faker.Name.LastName();
            var employee = new EmployeeSource(
                number,
                first,
                last,
                $"{first}.{last}.{i}@example.com".ToLowerInvariant().Replace("'", string.Empty, StringComparison.Ordinal).Replace(' ', '-'),
                _orgUnits[faker.Random.Int(1, _orgUnits.Count - 1)].ExternalId,
                _positions[faker.Random.Int(0, _positions.Count - 1)].ExternalId,
                manager,
                DateOnly.FromDateTime(faker.Date.Past(15, baseTime.UtcDateTime)),
                faker.PickRandom("Active", "Active", "Active", "OnLeave"));
            _employees.Add(new(number, employee, baseTime));
        }
    }

    public IReadOnlyList<SourceRecord<OrgUnitSource>> OrgUnits => Snapshot(_orgUnits);

    public IReadOnlyList<SourceRecord<PositionSource>> Positions => Snapshot(_positions);

    public IReadOnlyList<SourceRecord<EmployeeSource>> Employees => Snapshot(_employees);

    /// <summary>Simulates a change in the source system: adds or replaces the employee and stamps the change time.</summary>
    public void Upsert(EmployeeSource employee, DateTimeOffset modifiedAt)
    {
        ArgumentNullException.ThrowIfNull(employee);
        lock (_gate)
        {
            var record = new SourceRecord<EmployeeSource>(employee.EmployeeNumber, employee, modifiedAt);
            var index = _employees.FindIndex(r => r.ExternalId == record.ExternalId);
            if (index >= 0)
            {
                _employees[index] = record;
            }
            else
            {
                _employees.Add(record);
            }
        }
    }

    private List<SourceRecord<T>> Snapshot<T>(List<SourceRecord<T>> source)
        where T : class
    {
        lock (_gate)
        {
            return [.. source];
        }
    }
}
