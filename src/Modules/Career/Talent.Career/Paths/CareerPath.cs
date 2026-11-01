namespace Talent.Career.Paths;

/// <summary>One allowed move from a job to a more senior or lateral job. Paths together form a graph with no loops.</summary>
internal sealed class CareerPath
{
    // For EF Core.
    private CareerPath()
    {
    }

    private CareerPath(Guid fromJobId, Guid toJobId)
    {
        Id = Guid.NewGuid();
        FromJobId = fromJobId;
        ToJobId = toJobId;
    }

    public Guid Id { get; private set; }
    public Guid FromJobId { get; private set; }
    public Guid ToJobId { get; private set; }

    public static CareerPath Create(Guid fromJobId, Guid toJobId, IEnumerable<(Guid From, Guid To)> existing)
    {
        if (fromJobId == toJobId)
        {
            throw new InvalidCareerException("A job cannot lead to itself.");
        }

        var edges = existing.ToList();
        if (edges.Contains((fromJobId, toJobId)))
        {
            throw new CareerConflictException("That career path already exists.");
        }

        if (CareerGraph.Reachable(edges, toJobId, int.MaxValue).ContainsKey(fromJobId))
        {
            throw new CareerConflictException("That path would make a loop: the target job already leads back to the source job.");
        }

        return new CareerPath(fromJobId, toJobId);
    }
}

/// <summary>Walks the career path graph.</summary>
internal static class CareerGraph
{
    /// <summary>
    /// Every job reachable from <paramref name="start"/> in at most <paramref name="maxSteps"/> moves, with the fewest moves to reach it.
    /// The start job itself is not included.
    /// </summary>
    public static Dictionary<Guid, int> Reachable(IEnumerable<(Guid From, Guid To)> edges, Guid start, int maxSteps)
    {
        var next = edges.ToLookup(e => e.From, e => e.To);
        var steps = new Dictionary<Guid, int>();
        var queue = new Queue<(Guid Job, int Steps)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var (job, taken) = queue.Dequeue();
            if (taken >= maxSteps)
            {
                continue;
            }

            foreach (var target in next[job])
            {
                if (target == start || steps.ContainsKey(target))
                {
                    continue;
                }

                steps[target] = taken + 1;
                queue.Enqueue((target, taken + 1));
            }
        }

        return steps;
    }
}
