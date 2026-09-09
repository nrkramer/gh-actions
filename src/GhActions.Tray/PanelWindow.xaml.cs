using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using GhActions.Core;

namespace GhActions.Tray;

public partial class PanelWindow : Window
{
    private readonly Dictionary<string, bool> _expanded = new();
    private DateTime _hiddenAt = DateTime.MinValue;

    /// <summary>Raised when the user asks for a refresh from inside the panel.</summary>
    public event Action? RefreshRequested;

    public PanelWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => HidePanel();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) HidePanel(); };
    }

    public void Render(Cache? cache)
    {
        var repos = VmBuilder.Build(cache, _expanded);
        RepoList.ItemsSource = repos;
        EmptyLabel.Visibility = repos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        Stamp.Text = cache is null || cache.Updated <= 0
            ? "never updated"
            : $"updated {(int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - cache.Updated)}s ago";
    }

    public void TogglePanel(Cache? cache)
    {
        // A click on the tray icon while the panel is open deactivates it
        // first, so without this guard the panel would hide and immediately
        // reappear -- indistinguishable from "clicking does nothing".
        if (IsVisible || (DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 250)
        {
            HidePanel();
            return;
        }
        ShowPanel(cache);
    }

    public void ShowPanel(Cache? cache)
    {
        Render(cache);

        Show();
        UpdateLayout();   // SizeToContent needs a pass before the height is real
        Native.PlaceInTrayCorner(new WindowInteropHelper(this).Handle);
        Activate();       // required, or Deactivated never fires and it will not dismiss
    }

    public void HidePanel()
    {
        if (!IsVisible) return;
        _hiddenAt = DateTime.UtcNow;
        Hide();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();

    private void OnJobClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } || string.IsNullOrEmpty(url)) return;
        try
        {
            // UseShellExecute hands the URL to the default browser. Without it
            // .NET tries to exec the URL as a program and throws.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the link:\n{url}\n\n{ex.Message}",
                "GitHub Actions", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        HidePanel();
    }
}
