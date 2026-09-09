using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GhActions.Core;

namespace GhActions.Cli;

/// <summary>
/// The Linux front end: waybar execs this to render the module. `tick` is what
/// the bar actually runs -- it decides for itself whether a poll is due, so
/// waybar's interval is only a heartbeat.
/// </summary>
internal static class Program
{
    // Font Awesome 7 Brands, verified present via fc-list :charset=f09b
    private const string Glyph = "";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var cmd = args.FirstOrDefault() ?? "tick";
        var force = args.Contains("--force");

        try
        {
            switch (cmd)
            {
                case "poll":
                    await CmdPoll(force);
                    return 0;
                case "bar":
                    CmdBar();
                    return 0;
                case "tick":
                    await CmdTick();
                    return 0;
                case "--help" or "-h" or "help":
                    Console.WriteLine("usage: gh-actions-core [poll [--force] | bar | tick]");
                    return 0;
                default:
                    Console.Error.WriteLine($"unknown command: {cmd}");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task CmdPoll(bool force)
    {
        await Poller.PollAsync(Config.Load(), force);
        NudgeWaybar();
    }

    /// <summary>Poll if due, then render. Never throws: the bar must always
    /// print something parseable or waybar shows an empty module.</summary>
    private static async Task CmdTick()
    {
        try { await CmdPoll(force: false); }
        catch { /* fall through and render whatever the cache still holds */ }
        CmdBar();
    }

    private static void CmdBar()
    {
        var cache = JsonFile.Read<Cache?>(Paths.CachePath, null);
        if (cache is null)
        {
            Emit(Glyph, "idle", "no data yet");
            return;
        }

        var (state, live) = Aggregator.Aggregate(cache);
        var text = live == 0 ? Glyph : $"{Glyph}  {live}";

        var lines = new List<string>();
        foreach (var repo in cache.Repos)
        {
            if (!string.IsNullOrEmpty(repo.Error))
            {
                lines.Add($"{repo.Name}  —  {repo.Error}");
                continue;
            }

            var bits = repo.Active
                .Select(r => $"{r.Name} #{r.Number} running")
                .ToList();

            if (repo.Last is { } last)
                bits.Add($"last: {last.Name} #{last.Number} {Aggregator.RunState(last)} " +
                         $"({Format.RelTime(last.Started)} ago)");

            lines.Add($"{repo.Name}\n    {(bits.Count > 0 ? string.Join("\n    ", bits) : "no runs")}");
        }

        var age = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - cache.Updated);
        lines.Add($"\nupdated {age}s ago · click for detail");

        Emit(text, state, string.Join("\n", lines).Trim());
    }

    private static void Emit(string text, string cls, string tooltip) =>
        Console.WriteLine(JsonSerializer.Serialize(
            new { text, @class = cls, tooltip }, JsonFile.Opts));

    /// <summary>
    /// Tell waybar to re-run the module now (custom/actions carries signal 8).
    /// Without it the bar can lag the panel by a whole interval, which reads
    /// as a wrong count rather than a late one.
    /// </summary>
    private static void NudgeWaybar()
    {
        if (Paths.IsWindows) return;
        try
        {
            var psi = new ProcessStartInfo("pkill")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            // -x matches the process name exactly; -f would pattern-match
            // whole command lines and can signal the wrong process.
            psi.ArgumentList.Add("-RTMIN+8");
            psi.ArgumentList.Add("-x");
            psi.ArgumentList.Add("waybar");

            using var p = Process.Start(psi);
            p?.WaitForExit(5_000);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // no pkill, or no waybar: the interval refresh still covers it
        }
    }
}
