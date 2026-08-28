using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyAiAssistant.Services;

public class VisionService : IVisionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    // Win32 constants for animation control
    private const int SPI_GETANIMATION = 0x0048;
    private const int SPI_SETANIMATION = 0x0049;
    private const int ANIMINFO_SIZE = 44; // sizeof(ANIMATIONINFO)

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, IntPtr pvParam, int fWinIni);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const uint SRCCOPY = 0x00CC0020;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public async Task<string> CaptureScreenAsync(int quality = 75)
    {
        Log("Screen capture starting (no-animation mode)");

        byte[] pngBytes;
        IntPtr animInfoBuffer = IntPtr.Zero;
        bool animWasEnabled = false;

        try
        {
            // Step 1: Disable system animations to prevent motion artifacts during capture
            animWasEnabled = DisableAnimations(ref animInfoBuffer);

            // Step 2: Capture screen via raw GDI BitBlt (no WPF rendering, no compositing animations)
            pngBytes = CaptureScreenRaw();

            Log($"Screen captured: {pngBytes.Length} bytes, animations {(animWasEnabled ? "restored" : "were already off")}");
        }
        finally
        {
            // Step 3: Restore animations
            if (animInfoBuffer != IntPtr.Zero)
            {
                if (animWasEnabled)
                    SystemParametersInfo(SPI_SETANIMATION, ANIMINFO_SIZE, animInfoBuffer, 0x02); // SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
                Marshal.FreeHGlobal(animInfoBuffer);
            }
            else if (animWasEnabled)
            {
                // Fallback: re-enable by setting animations on
                var buf = Marshal.AllocHGlobal(ANIMINFO_SIZE);
                Marshal.StructureToPtr(new AnimationInfo { cbSize = ANIMINFO_SIZE, iMinAnimate = 1 }, buf, false);
                SystemParametersInfo(SPI_SETANIMATION, ANIMINFO_SIZE, buf, 0x02);
                Marshal.FreeHGlobal(buf);
            }
        }

        // Step 4: Encode as base64
        var base64 = Convert.ToBase64String(pngBytes);
        return await Task.FromResult(base64);
    }

    public async Task<string> AnalyzeScreenAsync(string prompt)
    {
        Log($"AnalyzeScreen: '{prompt}'");

        var base64 = await CaptureScreenAsync();

        // Try online vision first
        var config = ConfigManager.Load();
        if (!string.IsNullOrWhiteSpace(config.Vision.ApiKey))
        {
            return await AnalyzeImageAsync(prompt, base64);
        }

        // If online not configured, try local model
        if (config.LocalVision.Enabled && !string.IsNullOrWhiteSpace(config.LocalVision.ModelPath))
        {
            return await AnalyzeImageLocalAsync(prompt, base64, config.LocalVision);
        }

        return "视觉识别未配置。请在设置中填写视觉模型的 API Key，或启用本地视觉模型。";
    }

    /// <summary>Analyze image using local vision model (llava-phi3 or similar).</summary>
    public async Task<string> AnalyzeImageLocalAsync(string prompt, string base64Image, LocalVisionConfig localConfig)
    {
        Log($"Local vision request: model={localConfig.ModelName}");

        try
        {
            // Save base64 image to temp file
            var tempImage = Path.Combine(Path.GetTempPath(), $"fairy_vision_{Guid.NewGuid():N}.png");
            var imageBytes = Convert.FromBase64String(base64Image);
            await File.WriteAllBytesAsync(tempImage, imageBytes);

            // Use llama.cpp server or Ollama API for inference
            var endpoint = "http://localhost:11434/api/generate"; // Ollama default

            var payload = JsonSerializer.Serialize(new
            {
                model = localConfig.ModelName,
                prompt = $"[img]{tempImage}[/img]\n{prompt}",
                stream = false,
                options = new
                {
                    num_predict = localConfig.MaxTokens,
                    num_thread = localConfig.Threads
                }
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"Local vision error {resp.StatusCode}: {error}");
                return $"本地视觉模型调用失败: {resp.StatusCode}";
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var response = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() ?? "" : "";

            // Cleanup temp file
            try { File.Delete(tempImage); } catch { }

            Log($"Local vision response length: {response.Length}");
            return response;
        }
        catch (Exception ex)
        {
            Log($"Local vision exception: {ex.Message}");
            return $"本地视觉模型出错: {ex.Message}";
        }
    }

    public async Task<string> AnalyzeImageAsync(string prompt, string base64Image, string? customEndpoint = null, string? customApiKey = null)
    {
        var config = ConfigManager.Load();
        var endpoint = customEndpoint ?? config.Vision.BaseUrl;
        var apiKey = customApiKey ?? config.Vision.ApiKey;
        var model = config.Vision.Model;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log("Vision: no API key configured");
            return "视觉识别未配置。请在设置中填写视觉模型的 API Key。";
        }

        Log($"Vision request: endpoint={endpoint}, model={model}");

        try
        {
            var messages = new object[]
            {
                new { role = "system", content = "你是一个视觉分析助手。请详细描述用户屏幕截图中的内容，包括文字、界面元素、应用程序等。如果能看到文字，逐字抄录。回复用中文。" },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/png;base64,{base64Image}",
                                detail = "high"
                            }
                        }
                    }
                }
            };

            var payload = JsonSerializer.Serialize(new
            {
                model,
                messages,
                max_tokens = 4096,
                stream = false
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                Log($"Vision ERROR {resp.StatusCode}: {errorBody}");
                return $"视觉识别失败: {resp.StatusCode}";
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? "";

            Log($"Vision response length: {content.Length}");
            return content;
        }
        catch (Exception ex)
        {
            Log($"Vision exception: {ex.Message}");
            return $"视觉识别出错: {ex.Message}";
        }
    }

    /// <summary>Capture screen using raw GDI BitBlt — bypasses WPF/DWM compositing and animations.</summary>
    private static byte[] CaptureScreenRaw()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);

        IntPtr screenDC = GetDC(IntPtr.Zero);
        IntPtr memDC = CreateCompatibleDC(screenDC);
        IntPtr hBitmap = CreateCompatibleBitmap(screenDC, width, height);
        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        try
        {
            // BitBlt copies screen pixels directly — no animation, no WPF rendering
            BitBlt(memDC, 0, 0, width, height, screenDC, 0, 0, SRCCOPY);

            // Convert to WPF BitmapSource then encode as PNG
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Force freeze to detach from UI thread
            if (source.CanFreeze) source.Freeze();

            // Encode as PNG with quality setting
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        finally
        {
            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }
    }

    /// <summary>Disable system animations via SystemParametersInfo. Returns true if they were enabled.</summary>
    private static bool DisableAnimations(ref IntPtr buffer)
    {
        buffer = Marshal.AllocHGlobal(ANIMINFO_SIZE);
        Marshal.StructureToPtr(new AnimationInfo { cbSize = ANIMINFO_SIZE, iMinAnimate = 0 }, buffer, false);

        bool wasEnabled = SystemParametersInfo(SPI_GETANIMATION, ANIMINFO_SIZE, buffer, 0);

        var animInfo = Marshal.PtrToStructure<AnimationInfo>(buffer);
        bool previouslyOn = wasEnabled && animInfo.iMinAnimate != 0;

        if (previouslyOn)
        {
            // Disable animations
            Marshal.StructureToPtr(new AnimationInfo { cbSize = ANIMINFO_SIZE, iMinAnimate = 0 }, buffer, false);
            SystemParametersInfo(SPI_SETANIMATION, ANIMINFO_SIZE, buffer, 0);
            Log("System animations disabled for capture");
        }

        return previouslyOn;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public int cbSize;
        public int iMinAnimate;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
