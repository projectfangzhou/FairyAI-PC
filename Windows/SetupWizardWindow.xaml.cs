using System.Windows;
using MyAiAssistant.Services;

namespace MyAiAssistant.Windows;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 1;
    private const int TotalSteps = 3;

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
            "5. AI 回复会自动语音播报\n\n" +
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
        ASRPanel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        TTSPanel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

        BackBtn.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
        FinishBtn.Visibility = _currentStep == TotalSteps ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetStepName(int step) => step switch
    {
        1 => "文本模型",
        2 => "语音识别",
        3 => "语音合成",
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
            }
        };

        // Set base URL and model based on provider
        var llmInfo = ConfigManager.GetProviderInfo(config.LLM.Provider);
        if (llmInfo.HasValue)
        {
            config.LLM.BaseUrl = llmInfo.Value.BaseUrl;
            config.LLM.Model = llmInfo.Value.Model;
        }

        ConfigManager.Save(config);
    }
}
