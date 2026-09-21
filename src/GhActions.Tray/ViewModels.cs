using System;
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
    public bool IsRunning { get; init; }
    public string Tooltip => string.IsNullOrEmpty(Url) ? Name : Name + "\n" + Url;
}

public sealed class GroupVm
{
    public string Platform { get; init; } = "";
    public bool HasPlatform => !string.IsNullOrEmpty(Platform);
    public List<JobVm> Jobs { get; init; } = new();
}

/// <summary>
/// A user's open/closed choice for one run, and the state the run was in when
/// they made it. The choice lapses when the state moves on, so collapsing a
/// running build does not hide it once it fails.
/// </summary>
public readonly record struct ExpandChoice(string State, bool Open);

public sealed class RunVm
{
    private readonly Action<bool> _remember;
    private bool _isExpanded;

    public RunVm(bool isExpanded, Action<bool> remember)
    {
        _isExpanded = isExpanded;
        _remember = remember;
    }

    public string Title { get; init; } = "";
    public string Meta { get; init; } = "";
    public string Url { get; init; } = "";
    public Brush Dot { get; init; } = Brushes.Gray;
    public bool IsRunning { get; init; }

    /// <summary>Written by the toggle's two-way binding, i.e. only by a click.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            _remember(value);
        }
    }

    public List<GroupVm> Groups { get; init; } = new();
    public bool HasJobs => Groups.Count > 0;
}

public sealed class RepoVm
{
    public string Name { get; init; } = "";
    public string Error { get; init; } = "";
    public bool HasError => !string.IsNullOrEmpty(Error);
    public Brush Dot { get; init; } = Brushes.Gray;
    public bool IsRunning { get; init; }
    public List<RunVm> Runs { get; init; } = new();
    public bool IsEmpty => Runs.Count == 0 && !HasError;
}

public static class VmBuilder
{
    /// <summary>
    /// Flatten a cache into rows. <paramref name="expanded"/> carries the
    /// user's open/closed clicks across rebuilds -- without it every poll
    /// would reset every section under the cursor. It holds clicks only: a
    /// run nobody has touched follows its current state, poll by poll.
    /// </summary>
    public static List<RepoVm> Build(Cache? cache, Dictionary<long, ExpandChoice> expanded)
    {
        var outp = new List<RepoVm>();
        if (cache is null) return outp;

        // Forget runs that have left the cache, or the map grows for as long
        // as the tray runs.
        var present = cache.Repos
            .SelectMany(r => r.Last is null ? r.Active : r.Active.Append(r.Last))
            .Select(r => r.Id)
            .ToHashSet();
        foreach (var id in expanded.Keys.Where(id => !present.Contains(id)).ToList())
            expanded.Remove(id);

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
                IsRunning = rstate == "running",
                Runs = runs.Select(r => BuildRun(r, expanded)).ToList(),
            });
        }
        return outp;
    }

    private static RunVm BuildRun(Run run, Dictionary<long, ExpandChoice> expanded)
    {
        var rs = Aggregator.RunState(run);

        // Default open for anything you are actually waiting on or that broke;
        // settled green runs stay collapsed so the panel is not a wall of text.
        // Recomputed every build, so a run that finishes green folds itself up.
        var open = expanded.TryGetValue(run.Id, out var choice) && choice.State == rs
            ? choice.Open
            : run.Status != "completed" || rs == "failure";

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
                IsRunning = job.State == "running",
            });
        }

        return new RunVm(open, v => expanded[run.Id] = new ExpandChoice(rs, v))
        {
            Title = $"{run.Name} #{run.Number}",
            Meta = $"{run.Branch} · {Format.RelTime(run.Started)} ago",
            Url = run.Url,
            Dot = Palette.StateBrush(rs),
            IsRunning = rs == "running",
            Groups = groups,
        };
    }

    private static string Trim(string? s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n];
}
