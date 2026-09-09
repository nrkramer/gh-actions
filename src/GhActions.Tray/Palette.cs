using System.Collections.Generic;
using System.Windows.Media;

namespace GhActions.Tray;

/// <summary>Tokyo Night, shared by the tray icon and the panel.</summary>
public static class Palette
{
    public static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s)!;

    public const string Bg = "#1a1b26";
    public const string BgRaised = "#292e42";
    public const string Fg = "#c0caf5";
    public const string Dim = "#565f89";
    public const string Blue = "#7aa2f7";
    public const string Purple = "#bb9af7";
    public const string Green = "#9ece6a";
    public const string Yellow = "#e0af68";
    public const string Red = "#f7768e";

    private static readonly Dictionary<string, string> States = new()
    {
        ["success"] = Green,
        ["failure"] = Red,
        ["running"] = Yellow,
        ["queued"] = Yellow,
        ["cancelled"] = Dim,
        ["skipped"] = Dim,
        ["unknown"] = Dim,
        ["idle"] = Blue,
    };

    public static string StateHex(string state) =>
        States.TryGetValue(state, out var c) ? c : Dim;

    public static Brush StateBrush(string state)
    {
        var b = new SolidColorBrush(Hex(StateHex(state)));
        b.Freeze();
        return b;
    }
}
