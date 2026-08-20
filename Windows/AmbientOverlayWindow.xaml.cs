using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyAiAssistant.Windows;

public partial class AmbientOverlayWindow : Window
{
    private bool _isActive;

    public AmbientOverlayWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public void ActivateOverlay()
    {
        if (_isActive) return;
        _isActive = true;

        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();

        var slideIn = new DoubleAnimation(300, 0, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PanelSlideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);

        GlowBorder.Start();
    }

    public void DeactivateOverlay()
    {
        if (!_isActive) return;
        _isActive = false;

        GlowBorder.Stop();

        var slideOut = new DoubleAnimation(0, 300, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        slideOut.Completed += (_, _) => Hide();
        PanelSlideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }
}
