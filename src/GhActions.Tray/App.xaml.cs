using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GhActions.Core;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;
using Icon = System.Drawing.Icon;

namespace GhActions.Tray;

/// <summary>
/// The whole Windows front end. The icon and the panel live in one process,
/// so there is no daemon, pidfile or IPC to keep them in step.
/// </summary>
public partial class App : Application
{
    private Forms.NotifyIcon _tray = null!;
    private PanelWindow _panel = null!;
    private DispatcherTimer _timer = null!;
    private Config _config = new();
    private Cache? _cache;
    private Icon? _currentIcon;
    private string _lastState = "";
    private int _polling;   // 0/1 guard: never two polls in flight

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Without this a fault anywhere in the UI shows the raw .NET crash
        // dialog and takes the tray icon with it. Log it and stay alive: a
        // broken panel should not cost you the indicator.
        DispatcherUnhandledException += (_, ev) =>
        {
            ev.Handled = ReportCrash(ev.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            if (ev.ExceptionObject is Exception ex) ReportCrash(ex);
        };

        _config = Config.LoadOrDefault();
        _panel = new PanelWindow();
        _panel.RefreshRequested += () => _ = Poll(force: true);

        _tray = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "GitHub Actions",
            Icon = TrayIconRenderer.Render("idle", 0),
            ContextMenuStrip = BuildMenu(),
        };
        _tray.MouseClick += (_, ev) =>
        {
            if (ev.Button == Forms.MouseButtons.Left) _panel.TogglePanel(_cache);
        };

        if (!System.IO.File.Exists(Paths.ConfigPath))
            _tray.ShowBalloonTip(
                10_000, "GitHub Actions",
                $"No config yet. Create {Paths.ConfigPath} with an \"org\" and a \"repos\" list.",
                Forms.ToolTipIcon.Warning);

        // A short fixed tick; Poller itself decides whether a fetch is due, so
        // this stays cheap (a file stat) when nothing is running.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _timer.Tick += (_, _) => _ = Poll(force: false);
        _timer.Start();

        _ = Poll(force: false);
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Refresh now", null, (_, _) => _ = Poll(force: true));
        menu.Items.Add("Open panel", null, (_, _) => _panel.ShowPanel(_cache));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Edit config…", null, (_, _) => OpenConfig());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        return menu;
    }

    private void OpenConfig()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Paths.ConfigDir);
            if (!System.IO.File.Exists(Paths.ConfigPath))
                System.IO.File.WriteAllText(Paths.ConfigPath, Config.Template);

            Process.Start(new ProcessStartInfo(Paths.ConfigPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "GitHub Actions",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task Poll(bool force)
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try
        {
            _config = Config.LoadOrDefault();
            var cache = await Task.Run(() => Poller.PollAsync(_config, force)).ConfigureAwait(true);
            _cache = cache;
            UpdateIndicator(cache);
            if (_panel.IsVisible) _panel.Render(cache);
        }
        catch (Exception ex)
        {
            _tray.Text = Clip("GitHub Actions — " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private void UpdateIndicator(Cache cache)
    {
        var (state, live) = Aggregator.Aggregate(cache);

        var next = TrayIconRenderer.Render(state, live);
        _tray.Icon = next;
        _currentIcon?.Dispose();   // only after the tray has taken the new one
        _currentIcon = next;

        _tray.Text = Clip(live > 0
            ? $"GitHub Actions — {state}, {live} job{(live == 1 ? "" : "s")} running"
            : $"GitHub Actions — {state}");

        // Windows gives us something waybar cannot: tell the user the moment a
        // build breaks, instead of waiting to be looked at.
        if (state == "failure" && _lastState is not ("failure" or ""))
            _tray.ShowBalloonTip(8_000, "Build failed",
                FirstFailing(cache) ?? "A workflow run failed.", Forms.ToolTipIcon.Error);

        _lastState = state;
    }

    private static string? FirstFailing(Cache cache)
    {
        foreach (var repo in cache.Repos)
        foreach (var run in repo.Active.Concat(repo.Last is null ? Array.Empty<Run>() : new[] { repo.Last }))
            if (Aggregator.RunState(run) == "failure")
                return $"{repo.Name}: {run.Name} #{run.Number}";
        return null;
    }

    /// <summary>
    /// Append a fault to the log beside the cache and tell the user where it
    /// went. Returns true when the app can keep running.
    /// </summary>
    private bool ReportCrash(Exception ex)
    {
        var log = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Paths.CachePath) ?? ".", "crash.log");
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
            System.IO.File.AppendAllText(log,
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Nowhere to write. Nothing further to be done about it.
        }

        try
        {
            _tray?.ShowBalloonTip(10_000, "GitHub Actions hit an error",
                $"{ex.GetType().Name}: {ex.Message}\n\nDetails in {log}",
                Forms.ToolTipIcon.Error);
        }
        catch
        {
            // The tray may not exist yet if this fired during startup.
        }

        return true;
    }

    /// <summary>NotifyIcon.Text throws above 63 characters.</summary>
    private static string Clip(string s) => s.Length <= 63 ? s : s[..60] + "…";

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;   // or the dead icon lingers until hovered
            _tray.Dispose();
        }
        _currentIcon?.Dispose();
        base.OnExit(e);
    }
}
