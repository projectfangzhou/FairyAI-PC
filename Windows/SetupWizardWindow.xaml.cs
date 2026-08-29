using System.IO;
using System.Windows;
using MyAiAssistant.Services;

namespace MyAiAssistant.Windows;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 1;
    private const int TotalSteps = 7;

    public SetupWizardWindow()
    {
        InitializeComponent();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateUI();
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateUI();
        }
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        SaveConfig();
        MessageBox.Show(
            "配置完成！\n\n" +
            "使用方式：\n" +
            "1. 屏幕顶部中央有一个紫色圆球\n" +
            "2. 点击圆球 → 展开灵动岛，开始语音对话\n" +
            "3. 再次点击圆球 → 收回灵动岛\n" +
            "4. 灵动岛内支持文字输入和语音交互\n" +
            "5. AI 回复会自动语音播报\n" +
            "6. 说\"看一下屏幕\"可让AI分析屏幕内容\n\n" +
            "按 Alt+Q 可随时退出应用。",
            "Fairy AI — 使用指南",
            MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }

    private void UpdateUI()
    {
        StepIndicator.Text = $"步骤 {_currentStep}/{TotalSteps}: {GetStepName(_currentStep)}";

        LLMPanel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        VisionPanel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        ASRPanel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        TTSPanel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        FallbackLLMPanel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        LocalVisionPanel.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;
        Live2DPanel.Visibility = _currentStep == 7 ? Visibility.Visible : Visibility.Collapsed;

        BackBtn.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
        FinishBtn.Visibility = _currentStep == TotalSteps ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetStepName(int step) => step switch
    {
        1 => "文本模型",
        2 => "视觉模型",
        3 => "语音识别",
        4 => "语音合成",
        5 => "备用模型",
        6 => "离线视觉",
        7 => "Live2D",
        _ => ""
    };

    private void SaveConfig()
    {
        var config = new AppConfig
        {
            LLM = new LLMConfig
            {
                Provider = (LLMProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Kimi",
                ApiKey = LLMApiKeyBox.Text.Trim()
            },
            Vision = new VisionConfig
            {
                Provider = (VisionProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "OpenAI",
                ApiKey = VisionApiKeyBox.Text.Trim()
            },
            ASR = new ASRConfig
            {
                Provider = (ASRProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "MiMo",
                ApiKey = ASRApiKeyBox.Text.Trim()
            },
            TTS = new TTSConfig
            {
                Provider = (TTSProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "MiMo",
                ApiKey = TTSApiKeyBox.Text.Trim(),
                Voice = (TTSVoiceCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "茉莉"
            },
            FallbackLLM = new LLMConfig
            {
                Provider = (FallbackLLMProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Kimi",
                ApiKey = FallbackLLMApiKeyBox.Text.Trim()
            },
            LocalVision = new LocalVisionConfig
            {
                Enabled = EnableLocalVisionCheckBox.IsChecked ?? false,
                ModelName = (LocalVisionModelCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()?.Split(' ')[0] ?? "llava-phi3"
            }
        };

        // Set base URL and model based on provider
        var llmInfo = ConfigManager.GetProviderInfo(config.LLM.Provider);
        if (llmInfo.HasValue)
        {
            config.LLM.BaseUrl = llmInfo.Value.BaseUrl;
            config.LLM.Model = llmInfo.Value.Model;
        }

        // Set vision base URL and model based on provider
        var visionPreset = ConfigManager.GetVisionProviderInfo(config.Vision.Provider);
        if (visionPreset.HasValue)
        {
            config.Vision.BaseUrl = visionPreset.Value.BaseUrl;
            config.Vision.Model = visionPreset.Value.Model;
        }

        // Set ASR base URL
        var asrPreset = ConfigManager.GetAsrProviderInfo(config.ASR.Provider);
        if (asrPreset.HasValue)
        {
            config.ASR.BaseUrl = asrPreset.Value.BaseUrl;
        }

        // Set TTS base URL
        var ttsPreset = ConfigManager.GetTtsProviderInfo(config.TTS.Provider);
        if (ttsPreset.HasValue)
        {
            config.TTS.BaseUrl = ttsPreset.Value.BaseUrl;
        }

        // Set fallback LLM base URL and model
        var fallbackLLMInfo = ConfigManager.GetProviderInfo(config.FallbackLLM.Provider);
        if (fallbackLLMInfo.HasValue)
        {
            config.FallbackLLM.BaseUrl = fallbackLLMInfo.Value.BaseUrl;
            config.FallbackLLM.Model = fallbackLLMInfo.Value.Model;
        }

        // Set local vision model path
        var localVisionInfo = ConfigManager.GetLocalVisionModelInfo(config.LocalVision.ModelName);
        if (localVisionInfo.HasValue)
        {
            config.LocalVision.ModelPath = localVisionInfo.Value.ModelPath;
        }

        // Set Live2D config
        config.Live2D = new Live2DConfig
        {
            Enabled = EnableLive2DCheckBox.IsChecked ?? false,
            ModelFolder = Live2DModelPathBox.Text.Trim()
        };

        // Auto-detect model3.json in the folder
        if (config.Live2D.Enabled && !string.IsNullOrWhiteSpace(config.Live2D.ModelFolder))
        {
            var modelDir = config.Live2D.ModelFolder;
            if (Directory.Exists(modelDir))
            {
                var modelJson = Directory.GetFiles(modelDir, "*.model3.json").FirstOrDefault()
                              ?? Directory.GetFiles(modelDir, "*.model.json").FirstOrDefault();
                if (modelJson != null)
                    config.Live2D.ModelJsonPath = modelJson;
            }
        }

        ConfigManager.Save(config);
    }

    private void OnBrowseLive2D(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 Live2D 模型文件 (model3.json)",
            Filter = "Live2D Model (*.model3.json;*.model.json)|*.model3.json;*.model.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            // Use the parent directory as model folder
            var modelPath = dialog.FileName;
            var modelDir = Path.GetDirectoryName(modelPath);
            if (modelDir != null)
            {
                Live2DModelPathBox.Text = modelDir;
                EnableLive2DCheckBox.IsChecked = true;
            }
        }
    }
}
