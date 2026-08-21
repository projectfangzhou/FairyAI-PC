using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MyAiAssistant.Controls;

public class GlowBorder : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private double _offset;

    private static readonly Color[] Palette =
    [
        Color.FromRgb(255, 0, 128),
        Color.FromRgb(255, 100, 0),
        Color.FromRgb(255, 200, 0),
        Color.FromRgb(0, 200, 180),
        Color.FromRgb(100, 80, 255),
        Color.FromRgb(200, 0, 200),
    ];

    private double _speed = 1.0;
    public double Speed
    {
        get => _speed;
        set
        {
            _speed = value;
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _timer.Interval = TimeSpan.FromMilliseconds(16);
                _timer.Start();
            }
        }
    }

    public bool IsAccelerated
    {
        get => (bool)GetValue(IsAcceleratedProperty);
        set => SetValue(IsAcceleratedProperty, value);
    }

    public static readonly DependencyProperty IsAcceleratedProperty =
        DependencyProperty.Register(nameof(IsAccelerated), typeof(bool), typeof(GlowBorder),
            new PropertyMetadata(false, (d, e) =>
            {
                if (d is GlowBorder g) g.Speed = (bool)e.NewValue ? 2.5 : 1.0;
            }));

    public GlowBorder()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _offset += 1.5 * _speed;
        if (_offset > 1000) _offset = 0;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double w = ActualWidth;
        double h = ActualHeight;
        double edgeThickness = 4;
        double glowRadius = 12;

        // Build flowing gradient stops
        var stops = BuildGradientStops();

        // === TOP EDGE ===
        DrawEdge(dc, new Rect(0, 0, w, edgeThickness + glowRadius),
            stops, new Point(0, 0), new Point(1, 0), glowRadius, true);

        // === BOTTOM EDGE ===
        DrawEdge(dc, new Rect(0, h - edgeThickness - glowRadius, w, edgeThickness + glowRadius),
            stops, new Point(1, 0), new Point(0, 0), glowRadius, true);

        // === LEFT EDGE ===
        DrawEdge(dc, new Rect(0, 0, edgeThickness + glowRadius, h),
            stops, new Point(0, 0), new Point(0, 1), glowRadius, false);

        // === RIGHT EDGE ===
        DrawEdge(dc, new Rect(w - edgeThickness - glowRadius, 0, edgeThickness + glowRadius, h),
            stops, new Point(0, 1), new Point(0, 0), glowRadius, false);
    }

    private GradientStopCollection BuildGradientStops()
    {
        var stops = new GradientStopCollection();
        for (int i = 0; i < Palette.Length; i++)
        {
            double t = ((i * 180.0 + _offset) % 1000) / 1000.0;
            stops.Add(new GradientStop(Palette[i], t));
        }
        return new GradientStopCollection(stops.OrderBy(s => s.Offset));
    }

    private void DrawEdge(DrawingContext dc, Rect bounds,
        GradientStopCollection stops, Point start, Point end,
        double glowRadius, bool isHorizontal)
    {
        double thickness = 4;

        // Core bright line
        var coreBrush = new LinearGradientBrush(stops, start, end);
        dc.DrawRectangle(coreBrush, null, isHorizontal
            ? new Rect(bounds.X, bounds.Y + glowRadius, bounds.Width, thickness)
            : new Rect(bounds.X + glowRadius, bounds.Y, thickness, bounds.Height));

        // Outer glow (soft, wider)
        var glowBrush = new LinearGradientBrush(stops, start, end)
        {
            Opacity = 0.4,
            Transform = isHorizontal
                ? new ScaleTransform(1, glowRadius / thickness)
                : new ScaleTransform(glowRadius / thickness, 1)
        };
        dc.DrawRectangle(glowBrush, null, isHorizontal
            ? new Rect(bounds.X, bounds.Y, bounds.Width, thickness * 2)
            : new Rect(bounds.X, bounds.Y, thickness * 2, bounds.Height));

        // Inner glow (subtle)
        var innerBrush = new LinearGradientBrush(stops, start, end)
        {
            Opacity = 0.2,
            Transform = isHorizontal
                ? new ScaleTransform(1, glowRadius / thickness / 2)
                : new ScaleTransform(glowRadius / thickness / 2, 1)
        };
        dc.DrawRectangle(innerBrush, null, isHorizontal
            ? new Rect(bounds.X, bounds.Y + glowRadius * 2, bounds.Width, thickness)
            : new Rect(bounds.X + glowRadius * 2, bounds.Y, thickness, bounds.Height));
    }
}
