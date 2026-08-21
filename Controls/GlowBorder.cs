using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MyAiAssistant.Controls;

public class GlowBorder : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private double _phase;

    // Apple-style gradient: pink → orange → yellow → cyan → purple → pink
    private static readonly Color[] CycleColors =
    [
        Color.FromRgb(255, 50, 130),
        Color.FromRgb(255, 130, 0),
        Color.FromRgb(255, 220, 0),
        Color.FromRgb(0, 210, 190),
        Color.FromRgb(110, 80, 255),
        Color.FromRgb(220, 50, 200),
    ];

    private double _speed = 1.0;
    public double Speed { get => _speed; set { _speed = value; RefreshTimer(); } }

    public bool IsAccelerated
    {
        get => (bool)GetValue(IsAcceleratedProperty);
        set => SetValue(IsAcceleratedProperty, value);
    }
    public static readonly DependencyProperty IsAcceleratedProperty =
        DependencyProperty.Register(nameof(IsAccelerated), typeof(bool), typeof(GlowBorder),
            new PropertyMetadata(false, (d, e) => { if (d is GlowBorder g) g.Speed = (bool)e.NewValue ? 3.0 : 1.0; }));

    public GlowBorder()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => { _phase += 0.8 * _speed; if (_phase > 360) _phase -= 360; InvalidateVisual(); };
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void RefreshTimer()
    {
        if (_timer.IsEnabled) { _timer.Stop(); _timer.Interval = TimeSpan.FromMilliseconds(16); _timer.Start(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double thickness = 3;
        double spread = 10;

        // Top edge → flows left to right
        DrawFlowingEdge(dc, 0, 0, w, thickness, spread, _phase, horizontal: true, reverse: false);

        // Right edge → flows top to bottom
        DrawFlowingEdge(dc, w - thickness, 0, thickness, h, spread, _phase + 90, horizontal: false, reverse: false);

        // Bottom edge → flows right to left
        DrawFlowingEdge(dc, 0, h - thickness, w, thickness, spread, _phase + 180, horizontal: true, reverse: true);

        // Left edge → flows bottom to top
        DrawFlowingEdge(dc, 0, 0, thickness, h, spread, _phase + 270, horizontal: false, reverse: true);
    }

    private void DrawFlowingEdge(DrawingContext dc, double x, double y, double len, double thick, double spread,
        double phaseOffset, bool horizontal, bool reverse)
    {
        int segments = 60;
        for (int i = 0; i < segments; i++)
        {
            double t = (double)i / segments;
            double pos = reverse ? 1.0 - t : t;
            double angle = (phaseOffset + pos * 360) % 360;
            Color color = SampleGradient(angle);

            double segLen = len / segments;
            double segX = horizontal ? x + pos * len : x;
            double segY = horizontal ? y : y + pos * len;
            double segW = horizontal ? segLen + 1 : thick;
            double segH = horizontal ? thick : segLen + 1;

            // Core line
            var core = new SolidColorBrush(color);
            core.Freeze();
            dc.DrawRectangle(core, null, new Rect(segX, segY, segW, segH));

            // Outer glow
            var glow = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B));
            glow.Freeze();
            if (horizontal)
                dc.DrawRectangle(glow, null, new Rect(segX, segY - spread, segW, spread));
            else
                dc.DrawRectangle(glow, null, new Rect(segX - spread, segY, spread, segH));

            // Inner glow
            var inner = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
            inner.Freeze();
            if (horizontal)
                dc.DrawRectangle(inner, null, new Rect(segX, segY + thick, segW, spread * 0.6));
            else
                dc.DrawRectangle(inner, null, new Rect(segX + thick, segY, spread * 0.6, segH));
        }
    }

    private static Color SampleGradient(double angle)
    {
        double normalized = ((angle % 360) + 360) % 360;
        double scaled = normalized / 360.0 * CycleColors.Length;
        int idx = (int)scaled % CycleColors.Length;
        int next = (idx + 1) % CycleColors.Length;
        double frac = scaled - Math.Floor(scaled);

        var a = CycleColors[idx];
        var b = CycleColors[next];
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * frac),
            (byte)(a.G + (b.G - a.G) * frac),
            (byte)(a.B + (b.B - a.B) * frac));
    }
}
