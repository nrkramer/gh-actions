using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GhActions.Tray;

/// <summary>
/// The state marker beside a repo, run or job: a coloured dot, or a turning
/// gear while the thing is running. Drawn rather than taken from an icon font
/// so it rotates about its true centre -- a font glyph's box is the line
/// height, not the glyph, and wobbles when spun.
/// </summary>
public sealed class StateMark : FrameworkElement
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(StateMark),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpinningProperty = DependencyProperty.Register(
        nameof(Spinning), typeof(bool), typeof(StateMark),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((StateMark)d).UpdateAnimation()));

    private static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
        "Angle", typeof(double), typeof(StateMark),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    // Unit gear centred on (0.5, 0.5); scaled to the element at draw time.
    private static readonly Geometry Gear = BuildGear(teeth: 8, outer: 0.5, root: 0.37, hole: 0.16);

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool Spinning
    {
        get => (bool)GetValue(SpinningProperty);
        set => SetValue(SpinningProperty, value);
    }

    public StateMark()
    {
        // The panel rebuilds its rows every poll. Stopping on unload keeps
        // discarded rows from ticking an animation clock nobody can see.
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => BeginAnimation(AngleProperty, null);
    }

    private void UpdateAnimation()
    {
        if (Spinning && IsLoaded)
            BeginAnimation(AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.4))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });
        else
            BeginAnimation(AngleProperty, null);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        if (!Spinning)
        {
            dc.DrawEllipse(Fill, null, new Point(w / 2, h / 2), w / 2, h / 2);
            return;
        }

        // A gear reads smaller than a disc of the same size, so it is drawn a
        // little past the box rather than shifting the row's layout.
        var s = Math.Max(w, h) * 1.25;
        dc.PushTransform(new RotateTransform((double)GetValue(AngleProperty), w / 2, h / 2));
        dc.PushTransform(new TranslateTransform((w - s) / 2, (h - s) / 2));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.DrawGeometry(Fill, null, Gear);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static Geometry BuildGear(int teeth, double outer, double root, double hole)
    {
        const double c = 0.5;
        var step = 2 * Math.PI / teeth;
        Point At(double r, double a) => new(c + r * Math.Cos(a), c + r * Math.Sin(a));

        var g = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = g.Open())
        {
            // Each tooth: up a flank, across a flat top, down the other flank.
            ctx.BeginFigure(At(root, -step * 0.28), true, true);
            for (var i = 0; i < teeth; i++)
            {
                var a = i * step;
                if (i > 0) ctx.LineTo(At(root, a - step * 0.28), true, true);
                ctx.LineTo(At(outer, a - step * 0.17), true, true);
                ctx.LineTo(At(outer, a + step * 0.17), true, true);
                ctx.LineTo(At(root, a + step * 0.28), true, true);
            }

            // The axle hole; EvenOdd cuts it out of the body.
            ctx.BeginFigure(new Point(c - hole, c), true, true);
            ctx.ArcTo(new Point(c + hole, c), new Size(hole, hole), 0, false,
                SweepDirection.Clockwise, true, true);
            ctx.ArcTo(new Point(c - hole, c), new Size(hole, hole), 0, false,
                SweepDirection.Clockwise, true, true);
        }
        g.Freeze();
        return g;
    }
}
