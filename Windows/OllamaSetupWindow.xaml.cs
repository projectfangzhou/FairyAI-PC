using System.Windows;
using MyAiAssistant.Services;

namespace MyAiAssistant.Windows;

public partial class OllamaSetupWindow : Window
{
    private OllamaModel? _selectedModel;

    public OllamaSetupWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ModelList.ItemsSource = OllamaService.AvailableModels;

        if (OllamaService.IsOllamaInstalled())
        {
            StatusText.Text = "✓ Ollama 已安装";
            InstallBtn.Visibility = Visibility.Collapsed;

            if (await OllamaService.IsOllamaRunningAsync())
            {
                StatusText.Text = "✓ Ollama 已安装且服务运行中";

                // Show already-installed models
                var local = await OllamaService.ListLocalModelsAsync();
                if (local.Count > 0)
                    StatusText.Text += $"\n本地已有模型: {string.Join(", ", local)}";
            }
            else
            {
                StatusText.Text = "✓ Ollama 已安装，但服务未运行。请启动 Ollama 应用后重试。";
            }
        }
        else
        {
            StatusText.Text = "✗ 未检测到 Ollama。需要先下载安装。";
            InstallBtn.Visibility = Visibility.Visible;
        }
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        OllamaService.OpenOllamaDownload();
        StatusText.Text = "已打开下载页面。安装完成后点击\"关闭\"再重新打开本窗口。";
    }

    private void OnModelSelected(object sender, RoutedEventArgs e)
    {
        _selectedModel = ModelList.SelectedItem as OllamaModel;
        DeployBtn.IsEnabled = _selectedModel != null;
    }

    private async void OnDeployClick(object sender, RoutedEventArgs e)
    {
        if (_selectedModel == null) return;

        DeployBtn.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        ProgressText.Text = $"正在下载 {_selectedModel.Name} ({_selectedModel.Size})...\n首次下载可能需要几分钟，请耐心等待。";

        try
        {
            var progress = new Progress<string>(line =>
            {
                Dispatcher.Invoke(() =>
                {
                    var compact = line.Length > 90 ? line[..90] + "..." : line;
                    ProgressText.Text = compact;
                });
            });

            await OllamaService.PullModelAsync(_selectedModel.Name, progress);
            OllamaService.ConfigureLocalModel(_selectedModel.Name);

            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Text = $"✓ {_selectedModel.Name} 下载完成，已设置为默认模型！\n现在可以开始对话了。";
            StatusText.Text = $"✓ 本地模型 {_selectedModel.Name} 已启用";
        }
        catch (Exception ex)
        {
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Text = $"✗ 部署失败: {ex.Message}";
        }
        finally
        {
            DeployBtn.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
