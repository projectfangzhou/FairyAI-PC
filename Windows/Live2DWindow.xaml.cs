using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;

namespace MyAiAssistant.Windows;

public partial class Live2DWindow : Window
{
    public WebView2 WebView => WebViewControl;

    public Live2DWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position window at bottom-right of screen by default
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = screenWidth - Width - 20;
        Top = screenHeight - Height - 60;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle click-through
            return;
        }
        DragMove();
    }
}
