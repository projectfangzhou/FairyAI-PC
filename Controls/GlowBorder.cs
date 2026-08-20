using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MyAiAssistant.Controls;

public class GlowBorder : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private double _offset;

    private static readonly Color[] GradientColors =
    [
        Color.FromRgb(255, 0, 128),
        Color.FromRgb(255, 140, 0),
        Color.FromRgb(255, 255, 0),
        Color.FromRgb(64, 224, 208),
        Color.FromRgb(123, 104, 238),
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
        _offset += 2.0 * _speed;
        if (_offset > 1000) _offset = 0;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double thickness = 3;
        double w = ActualWidth;
        double h = ActualHeight;

        var stops = new GradientStopCollection();
        for (int i = 0; i < GradientColors.Length; i++)
        {
            double t = ((i * 200.0 + _offset) % 1000) / 1000.0;
            stops.Add(new GradientStop(GradientColors[i], t));
        }
        var sorted = stops.OrderBy(s => s.Offset).ToList();
        stops = new GradientStopCollection(sorted);

        dc.DrawRectangle(
            new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 0)),
            null, new Rect(0, 0, w, thickness));

        dc.DrawRectangle(
            new LinearGradientBrush(stops, new Point(1, 0), new Point(0, 0)),
            null, new Rect(0, h - thickness, w, thickness));

        dc.DrawRectangle(
            new LinearGradientBrush(stops, new Point(0, 0), new Point(0, 1)),
            null, new Rect(0, 0, thickness, h));

        dc.DrawRectangle(
            new LinearGradientBrush(stops, new Point(0, 1), new Point(0, 0)),
            null, new Rect(w - thickness, 0, thickness, h));

        // Corner glow blobs
        DrawCornerGlow(dc, 0, 0, stops, 0.3);
        DrawCornerGlow(dc, w, 0, stops, 0.2);
        DrawCornerGlow(dc, 0, h, stops, 0.15);
        DrawCornerGlow(dc, w, h, stops, 0.25);
    }

    private void DrawCornerGlow(DrawingContext dc, double cx, double cy,
        GradientStopCollection stops, double opacity)
    {
        double radius = 60 + Math.Sin(_offset * 0.02) * 20;
        var brush = new RadialGradientBrush
        {
            Center = new Point(cx / ActualWidth, cy / ActualHeight),
            RadiusX = radius / ActualWidth,
            RadiusY = radius / ActualHeight,
            Opacity = opacity,
            GradientStops = stops
        };
        dc.DrawRectangle(brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
