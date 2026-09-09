namespace GhActions.Core;

/// <summary>
/// GitHub reports a run or job as a (status, conclusion) pair, where only
/// completed things carry a conclusion. Everything downstream wants one word,
/// so the pair is collapsed here and nowhere else.
/// </summary>
public static class Vocab
{
    public static readonly HashSet<string> Queued =
        new(StringComparer.Ordinal) { "queued", "waiting", "requested", "pending" };

    public static readonly HashSet<string> Running =
        new(StringComparer.Ordinal) { "in_progress" };

    public static readonly Dictionary<string, string> ConclusionMap =
        new(StringComparer.Ordinal)
        {
            ["success"] = "success",
            ["failure"] = "failure",
            ["timed_out"] = "failure",
            ["startup_failure"] = "failure",
            ["action_required"] = "failure",
            ["cancelled"] = "cancelled",
            ["skipped"] = "skipped",
            ["neutral"] = "skipped",
            ["stale"] = "skipped",
        };

    /// Worst first. Both per-run and whole-bar aggregation take the first hit.
    public static readonly string[] Severity =
    {
        "failure", "running", "queued", "success", "cancelled", "skipped", "unknown",
    };

    public static string StateOf(string? status, string? conclusion)
    {
        status ??= "";
        if (Running.Contains(status)) return "running";
        if (Queued.Contains(status)) return "queued";
        if (status == "completed")
            return conclusion is not null && ConclusionMap.TryGetValue(conclusion, out var s)
                ? s
                : "unknown";
        return "unknown";
    }

    public static string Worst(IEnumerable<string> states)
    {
        var set = states as ISet<string> ?? new HashSet<string>(states, StringComparer.Ordinal);
        foreach (var s in Severity)
            if (set.Contains(s))
                return s;
        return "unknown";
    }
}
