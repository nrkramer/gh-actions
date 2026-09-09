using System.Globalization;

namespace GhActions.Core;

public static class Format
{
    /// <summary>"3m", "2h", "4d" since an ISO8601 timestamp; "" if unparseable.</summary>
    public static string RelTime(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        if (!DateTimeOffset.TryParse(
                iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var t))
            return "";

        var d = (long)Math.Max(0, (DateTimeOffset.UtcNow - t).TotalSeconds);
        if (d < 60) return $"{d}s";
        if (d < 3600) return $"{d / 60}m";
        if (d < 86400) return $"{d / 3600}h";
        return $"{d / 86400}d";
    }

    /// <summary>
    /// Reusable workflows name jobs "linux / Foo (bar)". Group on that prefix.
    /// Repos without them have no " / ", so fall back to a single unnamed group
    /// rather than inventing a grouping that is not there.
    /// </summary>
    public static string PlatformOf(string jobName)
    {
        var i = jobName.IndexOf(" / ", StringComparison.Ordinal);
        return i < 0 ? "" : jobName[..i].Trim();
    }

    public static string ShortJobName(string jobName)
    {
        var i = jobName.IndexOf(" / ", StringComparison.Ordinal);
        return i < 0 ? jobName : jobName[(i + 3)..].Trim();
    }
}
