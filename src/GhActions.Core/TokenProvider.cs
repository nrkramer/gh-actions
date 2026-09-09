using System.Diagnostics;

namespace GhActions.Core;

public static class TokenProvider
{
    /// <summary>
    /// config.token, then GH_TOKEN/GITHUB_TOKEN, then `gh auth token`.
    ///
    /// The gh CLI is the nicest source on Linux because it refreshes itself,
    /// but it is often absent on a Windows desktop, so the explicit sources
    /// come first and the subprocess is the fallback rather than the rule.
    /// </summary>
    public static string Get(Config cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.Token))
            return cfg.Token.Trim();

        foreach (var name in new[] { "GH_TOKEN", "GITHUB_TOKEN" })
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }

        return FromGhCli();
    }

    private static string FromGhCli()
    {
        var psi = new ProcessStartInfo
        {
            FileName = Paths.IsWindows ? "gh.exe" : "gh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("auth");
        psi.ArgumentList.Add("token");

        try
        {
            using var p = Process.Start(psi)
                          ?? throw new InvalidOperationException("could not start gh");
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15_000))
            {
                try { p.Kill(true); } catch { /* already gone */ }
                throw new TimeoutException("gh auth token timed out");
            }
            if (p.ExitCode != 0)
                throw new InvalidOperationException("gh auth token failed: " + stderr.Trim());
            return stdout.Trim();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                "no token: set \"token\" in config.json, or GH_TOKEN, or install the gh CLI");
        }
    }
}
