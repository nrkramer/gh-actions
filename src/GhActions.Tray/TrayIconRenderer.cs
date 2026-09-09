using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace GhActions.Tray;

/// <summary>
/// The notification area takes a bitmap, not text, so the live job count has
/// to be drawn into the icon itself. This is the Windows equivalent of the
/// waybar pill: a state-coloured dot, with the count on top when work is live.
/// </summary>
internal static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Builds an icon the caller owns. GetHicon hands back a raw GDI handle
    /// that Icon does not own, so the handle is destroyed here after cloning
    /// into a managed Icon -- otherwise this leaks one handle per poll, which
    /// over a workday is thousands.
    /// </summary>
    public static Icon Render(string state, int live)
    {
        var size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            using var fill = new SolidBrush(ColorTranslator.FromHtml(Palette.StateHex(state)));

            if (live <= 0)
            {
                var pad = size * 0.18f;
                g.FillEllipse(fill, pad, pad, size - 2 * pad, size - 2 * pad);
            }
            else
            {
                // Filled disc plus the count, dark-on-colour so it stays legible
                // against both light and dark taskbars.
                g.FillEllipse(fill, 0, 0, size - 1, size - 1);

                var text = live > 99 ? "!" : live.ToString();
                var pt = size * (text.Length > 1 ? 0.42f : 0.56f);
                using var font = new Font("Segoe UI", pt, FontStyle.Bold, GraphicsUnit.Pixel);
                using var ink = new SolidBrush(ColorTranslator.FromHtml(Palette.Bg));
                using var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(text, font, ink, new RectangleF(0, 0, size, size), fmt);
            }
        }

        var handle = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(handle);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}

// Alias so the file reads without a WinForms using that would collide with WPF.
internal static class SystemInformation
{
    public static Size SmallIconSize => System.Windows.Forms.SystemInformation.SmallIconSize;
}
