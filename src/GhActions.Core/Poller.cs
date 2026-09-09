using System.Text.Json;

namespace GhActions.Core;

public static class Poller
{
    /// <summary>Fast while anything is live, slow once everything has settled.</summary>
    public static int IntervalFor(Cache? cache, Config cfg) =>
        cache?.Repos.Any(r => r.Active.Count > 0) == true
            ? cfg.ActivePollSeconds
            : cfg.IdlePollSeconds;

    private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    /// <summary>
    /// Refresh the cache if it is stale (or <paramref name="force"/>), and
    /// return it either way. Callers treat the returned cache as the truth,
    /// so a skipped poll still yields the last good data.
    /// </summary>
    public static async Task<Cache> PollAsync(Config cfg, bool force, CancellationToken ct = default)
    {
        Paths.EnsureRunDir();
        var cache = JsonFile.Read<Cache?>(Paths.CachePath, null);

        if (!force && cache is not null && cache.Updated > 0
            && Now - cache.Updated < IntervalFor(cache, cfg))
            return cache;

        var outp = new Cache { Updated = Now, Org = cfg.Org };

        string token;
        try
        {
            token = TokenProvider.Get(cfg);
        }
        catch (Exception ex)
        {
            // Still write a cache: the bar must render something, and "no
            // token" is more useful on screen than a stale green tick.
            outp.Error = ex.Message;
            outp.Repos = cfg.Repos
                .Select(r => new RepoResult { Name = r, Error = "no token" })
                .ToList();
            JsonFile.WriteAtomic(Paths.CachePath, outp);
            return outp;
        }

        var store = JsonFile.Read(Paths.StorePath, new Dictionary<string, StoreEntry>());
        var gh = new GitHubClient(token, store);

        using var gate = new SemaphoreSlim(Math.Min(8, Math.Max(1, cfg.Repos.Count)));
        var tasks = cfg.Repos.Select(async repo =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { return await PollRepoAsync(gh, cfg, repo, ct).ConfigureAwait(false); }
            finally { gate.Release(); }
        });

        outp.Repos = (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();

        JsonFile.WriteAtomic(Paths.StorePath, store);
        JsonFile.WriteAtomic(Paths.CachePath, outp);
        return outp;
    }

    /// <summary>Active runs plus the most recent completed run, each with jobs.</summary>
    private static async Task<RepoResult> PollRepoAsync(
        GitHubClient gh, Config cfg, string repo, CancellationToken ct)
    {
        var result = new RepoResult { Name = repo };
        List<Run> runs;
        try
        {
            runs = await gh.FetchAsync(
                $"{GitHubClient.Api}/repos/{cfg.Org}/{repo}/actions/runs?per_page={cfg.RunsPerRepo}",
                Distill.Runs, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Surfaced in the UI rather than swallowed: one dead repo should
            // not blank the whole panel.
            result.Error = Describe(ex);
            return result;
        }

        var active = runs.Where(r => r.Status != "completed").ToList();
        var last = runs.FirstOrDefault(r => r.Status == "completed");

        var withJobs = last is null ? active : active.Append(last);
        await Task.WhenAll(withJobs.Select(async run =>
        {
            try
            {
                run.Jobs = await gh.FetchAsync(
                    $"{GitHubClient.Api}/repos/{cfg.Org}/{repo}/actions/runs/{run.Id}/jobs?per_page=100",
                    Distill.Jobs, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                run.Jobs = new List<Job>();
                run.Error = Describe(ex);
            }
        })).ConfigureAwait(false);

        result.Active = active;
        result.Last = last;
        return result;
    }

    private static string Describe(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: not null } h => $"HTTP {(int)h.StatusCode!}",
        HttpRequestException => "network unreachable",
        TaskCanceledException => "timed out",
        _ => ex.Message,
    };
}
