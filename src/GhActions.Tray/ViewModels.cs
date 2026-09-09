using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using GhActions.Core;

namespace GhActions.Tray;

public sealed class JobVm
{
    public string Name { get; init; } = "";
    public string Short { get; init; } = "";
    public string Url { get; init; } = "";
    public string Age { get; init; } = "";
    public Brush Dot { get; init; } = Brushes.Gray;
    public string Tooltip => string.IsNullOrEmpty(Url) ? Name : Name + "\n" + Url;
}

public sealed class GroupVm
{
    public string Platform { get; init; } = "";
    public bool HasPlatform => !string.IsNullOrEmpty(Platform);
    public List<JobVm> Jobs { get; init; } = new();
}

public sealed class RunVm
{
    public string Title { get; init; } = "";
    public string Meta { get; init; } = "";
    public string Url { get; init; } = "";
    public Brush Dot { get; init; } = Brushes.Gray;
    public bool IsExpanded { get; set; }
    public List<GroupVm> Groups { get; init; } = new();
    public bool HasJobs => Groups.Count > 0;
}

public sealed class RepoVm
{
    public string Name { get; init; } = "";
    public string Error { get; init; } = "";
    public bool HasError => !string.IsNullOrEmpty(Error);
    public Brush Dot { get; init; } = Brushes.Gray;
    public List<RunVm> Runs { get; init; } = new();
    public bool IsEmpty => Runs.Count == 0 && !HasError;
}

public static class VmBuilder
{
    /// <summary>
    /// Flatten a cache into rows. <paramref name="expanded"/> carries the
    /// user's open/closed choices across rebuilds -- without it every poll
    /// would slam every section shut under the cursor.
    /// </summary>
    public static List<RepoVm> Build(Cache? cache, Dictionary<string, bool> expanded)
    {
        var outp = new List<RepoVm>();
        if (cache is null) return outp;

        foreach (var repo in cache.Repos)
        {
            var states = repo.Active.Select(Aggregator.RunState).ToList();
            if (repo.Last is not null) states.Add(Aggregator.RunState(repo.Last));

            var rstate = !string.IsNullOrEmpty(repo.Error) ? "unknown"
                : states.Count > 0 ? Vocab.Worst(states.ToHashSet()) : "idle";

            var runs = repo.Active.ToList();
            if (repo.Last is not null) runs.Add(repo.Last);

            outp.Add(new RepoVm
            {
                Name = repo.Name,
                Error = Trim(repo.Error, 40),
                Dot = Palette.StateBrush(rstate),
                Runs = runs.Select(r => BuildRun(r, expanded)).ToList(),
            });
        }
        return outp;
    }

    private static RunVm BuildRun(Run run, Dictionary<string, bool> expanded)
    {
        var rs = Aggregator.RunState(run);
        var key = run.Id.ToString();

        // Default open for anything you are actually waiting on or that broke;
        // settled green runs stay collapsed so the panel is not a wall of text.
        if (!expanded.TryGetValue(key, out var open))
            open = expanded[key] = run.Status != "completed" || rs == "failure";

        var groups = new List<GroupVm>();
        foreach (var job in run.Jobs)
        {
            var g = groups.FirstOrDefault(x => x.Platform == job.Platform);
            if (g is null)
            {
                g = new GroupVm { Platform = job.Platform };
                groups.Add(g);
            }
            g.Jobs.Add(new JobVm
            {
                Name = job.Name,
                Short = job.Short,
                Url = job.Url,
                Age = Format.RelTime(job.Started),
                Dot = Palette.StateBrush(job.State),
            });
        }

        return new RunVm
        {
            Title = $"{run.Name} #{run.Number}",
            Meta = $"{run.Branch} · {Format.RelTime(run.Started)} ago",
            Url = run.Url,
            Dot = Palette.StateBrush(rs),
            IsExpanded = open,
            Groups = groups,
        };
    }

    private static string Trim(string? s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n];
}
