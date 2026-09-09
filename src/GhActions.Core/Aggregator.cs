namespace GhActions.Core;

public static class Aggregator
{
    /// <summary>
    /// A run's state is the worst of its jobs, falling back to the run's own
    /// status when jobs have not been fetched. A run reported "in_progress"
    /// whose jobs have all failed is a failure you want to see now.
    /// </summary>
    public static string RunState(Run? run)
    {
        if (run is null) return "unknown";
        if (run.Jobs.Count > 0)
            return Vocab.Worst(run.Jobs.Select(j => j.State).ToHashSet(StringComparer.Ordinal));
        return Vocab.StateOf(run.Status, run.Conclusion);
    }

    /// <summary>
    /// One word for the whole indicator, plus a count of live *jobs*.
    ///
    /// Counting jobs rather than runs is what makes the number mean something:
    /// one workflow fanning out to 11 platforms is 11 things you are waiting
    /// on, not 1.
    /// </summary>
    public static (string State, int Live) Aggregate(Cache? cache)
    {
        var states = new HashSet<string>(StringComparer.Ordinal);
        var live = 0;

        foreach (var repo in cache?.Repos ?? new List<RepoResult>())
        {
            if (!string.IsNullOrEmpty(repo.Error)) states.Add("unknown");

            foreach (var run in repo.Active)
            {
                if (run.Jobs.Count > 0)
                    live += run.Jobs.Count(j => j.State is "running" or "queued");
                else
                    live += 1; // jobs not fetched yet: the run is one live thing

                states.Add(RunState(run));
            }

            if (repo.Last is not null) states.Add(RunState(repo.Last));
        }

        return states.Count == 0 ? ("idle", 0) : (Vocab.Worst(states), live);
    }
}
