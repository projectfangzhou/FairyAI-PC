using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyAiAssistant.Windows;

public partial class FloatingOrbWindow : Window
{
    public event Action? OrbClicked;
    private bool _isHovering;

    public FloatingOrbWindow()
    {
        InitializeComponent();
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 8;
    }

    private void OnOrbClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // Prevent event bubbling
        OrbClicked?.Invoke();
    }

    public void SetActive(bool active)
    {
        if (active)
        {
            ActiveDot.Visibility = Visibility.Visible;
            InactiveDot.Visibility = Visibility.Collapsed;
            PulseGlow();
        }
        else
        {
            ActiveDot.Visibility = Visibility.Collapsed;
            InactiveDot.Visibility = Visibility.Visible;
            StopPulse();
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_isHovering) return;
        _isHovering = true;

        var scaleUp = new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        GlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
        GlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _isHovering = false;

        var scaleDown = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        GlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDown);
        GlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDown);
    }

    public void PulseGlow()
    {
        var pulse = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(600))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        GlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        GlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    public void StopPulse()
    {
        GlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        GlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        GlowScale.ScaleX = 1;
        GlowScale.ScaleY = 1;
    }
}
