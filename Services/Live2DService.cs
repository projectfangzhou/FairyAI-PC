using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MyAiAssistant.Windows;

namespace MyAiAssistant.Services;

public class Live2DService : ILive2DService, IDisposable
{
    private Live2DWindow? _window;
    private WebView2? _webView;
    private readonly string _logPath;
    private bool _isSpeaking;
    private CancellationTokenSource? _lipSyncCts;

    public bool IsEnabled => ConfigManager.HasLive2D();
    public bool IsWindowVisible => _window?.IsVisible ?? false;

    public Live2DService()
    {
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    }

    public void Initialize()
    {
        if (!IsEnabled) return;

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _window = new Live2DWindow();
                _webView = _window.WebView;
                _window.Closed += (_, _) => { _window = null; _webView = null; };

                _ = InitializeWebViewAsync();
            });
        }
        catch (Exception ex)
        {
            Log($"Live2D init error: {ex.Message}");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webView == null) return;

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "webview2_cache"));
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Build HTML page that loads Live2D model
            var html = BuildLive2DHtml();
            _webView.NavigateToString(html);

            Log("Live2D WebView initialized");
        }
        catch (Exception ex)
        {
            Log($"Live2D WebView init error: {ex.Message}");
        }
    }

    private string BuildLive2DHtml()
    {
        var config = ConfigManager.Load().Live2D;
        var modelDir = config.ModelFolder.Replace("\\", "/");
        var modelJson = config.ModelJsonPath.Replace("\\", "/");

        // If model folder is relative, make it absolute
        if (!Path.IsPathRooted(modelDir))
            modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelDir).Replace("\\", "/");
        if (!Path.IsPathRooted(modelJson))
            modelJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelJson).Replace("\\", "/");

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<style>
  body {{ margin: 0; padding: 0; overflow: hidden; background: transparent; }}
  #canvas {{ display: block; width: 100vw; height: 100vh; }}
  #loading {{ position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);
              color: #888; font-family: sans-serif; font-size: 14px; }}
</style>
</head>
<body>
<div id=""loading"">Loading Live2D...</div>
<canvas id=""canvas""></canvas>
<script>
// Live2D Cubism Web SDK stub - will be loaded from user's model folder
// Users should place Cubism SDK in their model folder or we load from CDN

const modelJsonPath = '{modelJson}';
const modelDir = '{modelDir}';

// Bridge to C#
function sendToCSharp(data) {{
    if (window.chrome && window.chrome.webview) {{
        window.chrome.webview.postMessage(JSON.stringify(data));
    }}
}}

// Lip sync state
let lipSyncValue = 0;
let isSpeaking = false;

// Live2D model instance (populated by SDK)
let live2DModel = null;

function initLive2D() {{
    // This is a simplified stub. In production, load actual Cubism SDK here.
    // The SDK files should be in the model folder or referenced via CDN.
    document.getElementById('loading').style.display = 'none';
    sendToCSharp({{ type: 'initialized' }});
}}

function setLipSync(value) {{
    lipSyncValue = Math.max(0, Math.min(1, value));
    if (live2DModel && live2DModel.setLipSyncValue) {{
        live2DModel.setLipSyncValue(lipSyncValue);
    }}
}}

function setExpression(name) {{
    if (live2DModel && live2DModel.setExpression) {{
        live2DModel.setExpression(name);
    }}
    sendToCSharp({{ type: 'expression', name: name }});
}}

function startMotion(group, index) {{
    if (live2DModel && live2DModel.startMotion) {{
        live2DModel.startMotion(group, index);
    }}
    sendToCSharp({{ type: 'motion', group: group, index: index }});
}}

function speak(text) {{
    isSpeaking = true;
    sendToCSharp({{ type: 'speak', text: text }});
}}

// Listen for C# messages
window.addEventListener('message', function(e) {{
    try {{
        const msg = JSON.parse(e.data);
        switch(msg.type) {{
            case 'lipSync': setLipSync(msg.value); break;
            case 'expression': setExpression(msg.name); break;
            case 'motion': startMotion(msg.group, msg.index); break;
            case 'speak': speak(msg.text); break;
        }}
    }} catch(err) {{}}
}});

// Auto-init when page loads
window.onload = initLive2D;
</script>
</body>
</html>";
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            Log($"Live2D msg: {json}");
        }
        catch { }
    }

    public void ShowWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_window == null) Initialize();
            _window?.Show();
            _window?.Activate();
        });
    }

    public void HideWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _window?.Hide();
        });
    }

    public void CloseWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _window?.Close();
            _window = null;
            _webView = null;
        });
    }

    public void SetLipSync(float value)
    {
        PostMessage(new { type = "lipSync", value });
    }

    public void StartLipSync()
    {
        _lipSyncCts?.Cancel();
        _lipSyncCts = new CancellationTokenSource();
        _ = AnimateLipSyncAsync(_lipSyncCts.Token);
    }

    public void StopLipSync()
    {
        _lipSyncCts?.Cancel();
        _isSpeaking = false;
        SetLipSync(0);
    }

    private async Task AnimateLipSyncAsync(CancellationToken ct)
    {
        var random = new Random();
        while (!ct.IsCancellationRequested)
        {
            var value = (float)(random.NextDouble() * 0.5 + 0.2);
            SetLipSync(value);
            try { await Task.Delay(80, ct); } catch { break; }
        }
    }

    public void SetExpression(string expression)
    {
        PostMessage(new { type = "expression", name = expression });
    }

    public void StartMotion(string motionGroup, int index)
    {
        PostMessage(new { type = "motion", group = motionGroup, index });
    }

    public void StartRandomMotion(string motionGroup)
    {
        PostMessage(new { type = "motion", group = motionGroup, index = -1 });
    }

    public void Speak(string text)
    {
        PostMessage(new { type = "speak", text });
        StartLipSync();
    }

    public void SetOpacity(double opacity)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_window != null) _window.Opacity = opacity;
        });
    }

    public void SetTopmost(bool topmost)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_window != null) _window.Topmost = topmost;
        });
    }

    public void SetClickThrough(bool enable)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_window == null) return;
            // Use WS_EX_TRANSPARENT to make window click-through
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
            var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (enable)
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_TRANSPARENT);
            else
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style & ~NativeMethods.WS_EX_TRANSPARENT);
        });
    }

    private void PostMessage(object msg)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(msg);
            Application.Current.Dispatcher.Invoke(() =>
            {
                _webView?.CoreWebView2?.PostWebMessageAsString(json);
            });
        }
        catch (Exception ex)
        {
            Log($"Live2D post msg error: {ex.Message}");
        }
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Live2D] {msg}\n"); } catch { }
    }

    public void Dispose()
    {
        _lipSyncCts?.Cancel();
        CloseWindow();
    }
}
