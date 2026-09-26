using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MyAiAssistant.Services;

/// <summary>
/// Lobotomy Corporation style error display — red alert with digital glitch artifacts.
/// Shows error codes (404/500/etc) in the distinctive "core meltdown" visual style.
/// </summary>
public class LobotomyErrorDisplay
{
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Show a LobotoCorp-style error alert window.</summary>
    public static void Show(string errorCode, string message)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var window = CreateErrorWindow(errorCode, message);
                window.Show();
            });
            Log($"LobotomyError: {errorCode} — {message}");
        }
        catch (Exception ex) { Log($"LobotomyError display failed: {ex.Message}"); }
    }

    private static Window CreateErrorWindow(string errorCode, string message)
    {
        var window = new Window
        {
            Title = "CORE MELTDOWN",
            Width = 520,
            Height = 320,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(200, 20, 0, 0)),
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
        };

        var grid = new System.Windows.Controls.Grid();
        grid.Children.Add(CreateGlitchOverlay());

        var panel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(32),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Red alert banner
        var banner = new System.Windows.Controls.TextBlock
        {
            Text = "⚠ CORE MELTDOWN ⚠",
            Foreground = new SolidColorBrush(Color.FromRgb(255, 40, 40)),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(banner);

        // Error code
        var codeText = new System.Windows.Controls.TextBlock
        {
            Text = $"ERROR {errorCode}",
            Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
            FontSize = 42,
            FontWeight = FontWeights.ExtraBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 8),
        };
        panel.Children.Add(codeText);

        // Description
        var descText = new System.Windows.Controls.TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 150, 150)),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 24),
        };
        panel.Children.Add(descText);

        // Close button
        var closeBtn = new System.Windows.Controls.Button
        {
            Content = "STABILIZE SYSTEM",
            Width = 200,
            Height = 40,
            Background = new SolidColorBrush(Color.FromRgb(180, 20, 20)),
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        closeBtn.Click += (_, _) => window.Close();
        panel.Children.Add(closeBtn);

        grid.Children.Add(panel);
        window.Content = grid;

        // Start glitch animation
        StartGlitchAnimation(grid);

        // Auto-close after 8 seconds
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        timer.Tick += (_, _) => { timer.Stop(); try { window.Close(); } catch { } };
        timer.Start();

        return window;
    }

    private static System.Windows.Controls.Canvas CreateGlitchOverlay()
    {
        var canvas = new System.Windows.Controls.Canvas
        {
            Width = 520,
            Height = 320,
            IsHitTestVisible = false,
            Opacity = 0.4,
        };

        // Red digital glitch lines
        var rng = new Random();
        for (int i = 0; i < 15; i++)
        {
            var line = new Rectangle
            {
                Width = rng.Next(20, 200),
                Height = rng.Next(1, 4),
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)rng.Next(60, 160), 255, (byte)rng.Next(0, 40), (byte)rng.Next(0, 40))),
                RadiusX = 0,
                RadiusY = 0,
            };
            System.Windows.Controls.Canvas.SetLeft(line, rng.Next(0, 400));
            System.Windows.Controls.Canvas.SetTop(line, rng.Next(0, 300));
            canvas.Children.Add(line);
        }

        return canvas;
    }

    private static void StartGlitchAnimation(UIElement element)
    {
        // Rapid opacity flicker
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.85,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    /// <summary>Map HTTP status code to Lobotomy-style description.</summary>
    public static (string Code, string Description) MapHttpError(int statusCode) => statusCode switch
    {
        400 => ("400", "请求格式错误 — Sephirah 拒绝处理异常指令"),
        401 => ("401", "认证失败 — 核心权限验证未通过，请检查 API Key"),
        403 => ("403", "访问被禁止 — 该操作不在允许范围内"),
        404 => ("404", "目标未找到 — 所请求的资源已从管理区域消失"),
        408 => ("408", "请求超时 — 连接异常中断，异想体可能正在干扰"),
        429 => ("429", "请求过于频繁 — 触发速率限制，请稍后再试"),
        500 => ("500", "服务器内部错误 — 核心融毁风险升高"),
        502 => ("502", "网关错误 — 上游服务无响应"),
        503 => ("503", "服务不可用 — 系统正在经历紧急维护"),
        504 => ("504", "网关超时 — 远程服务响应超时"),
        _ => (statusCode.ToString(), $"未知错误 — 状态码 {statusCode}"),
    };

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [LOBOTOMY] {msg}\n"); } catch { }
    }
}
