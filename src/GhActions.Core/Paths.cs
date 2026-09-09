namespace GhActions.Core;

/// <summary>
/// Where config, cache and ETag store live on each platform.
///
/// Linux keeps the cache in XDG_RUNTIME_DIR, so it lives on tmpfs and is
/// cleared on reboot along with the ETags it pairs with.
/// Windows has no such location, so the cache goes to LocalApplicationData --
/// it is small and rewritten every poll, so persisting it across reboots is
/// harmless and saves one cold fetch at login.
/// </summary>
public static class Paths
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    public static string ConfigDir => IsWindows
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gh-actions")
        : Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            "gh-actions");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static string RunDir => IsWindows
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gh-actions")
        : Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/tmp";

    public static string CachePath => Path.Combine(RunDir, "gh-actions.json");
    public static string StorePath => Path.Combine(RunDir, "gh-actions-store.json");

    public static void EnsureRunDir() => Directory.CreateDirectory(RunDir);
}
