using System.IO;
using System.Windows;
using MyAiAssistant.Services;

namespace MyAiAssistant.Windows;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 1;
    private const int TotalSteps = 14;

    public SetupWizardWindow()
    {
        InitializeComponent();
        // Events already connected in XAML — no double hookup needed
        EnableCustomPlatformCheckBox.Checked += (_, _) => UpdateCustomPlatformVisibility(true);
        EnableCustomPlatformCheckBox.Unchecked += (_, _) => UpdateCustomPlatformVisibility(false);
    }

    private void OnBiliGetQR(object sender, RoutedEventArgs e)
    {
        if (BiliQRCodePlaceholder != null) BiliQRCodePlaceholder.Text = "二维码\n生成中";
        if (BiliLoginStatus != null) { BiliLoginStatus.Text = "请使用B站APP扫码"; BiliLoginStatus.Foreground = System.Windows.Media.Brushes.Yellow; }
    }

    private void OnQQGetQR(object sender, RoutedEventArgs e)
    {
        if (QQQRCodePlaceholder != null) QQQRCodePlaceholder.Text = "二维码\n生成中";
        if (QQLoginStatus != null) { QQLoginStatus.Text = "请使用QQ扫码授权"; QQLoginStatus.Foreground = System.Windows.Media.Brushes.Yellow; }
    }

    private void OnWeChatGetQR(object sender, RoutedEventArgs e)
    {
        if (WeChatQRCodePlaceholder != null) WeChatQRCodePlaceholder.Text = "二维码\n生成中";
        if (WeChatLoginStatus != null) { WeChatLoginStatus.Text = "请使用微信扫码授权"; WeChatLoginStatus.Foreground = System.Windows.Media.Brushes.Yellow; }
    }

    private void OnLLMProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LLMProviderCombo == null || LLMEndpointCombo == null) return;
        var provider = (LLMProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        LLMEndpointCombo.Items.Clear();
        var endpoints = provider switch
        {
            "Kimi (月之暗面)" => new[] { "https://api.moonshot.cn/v1/chat/completions" },
            "DeepSeek" => new[] { "https://api.deepseek.com/v1/chat/completions" },
            "MiMo (小米)" => new[] { "https://api.xiaomimimo.com/v1/chat/completions" },
            "MiMo TokenPlan (小米)" => new[] { "https://token-plan-cn.xiaomimimo.com/v1/chat/completions" },
            "OpenAI" => new[] { "https://api.openai.com/v1/chat/completions" },
            "OpenRouter" => new[] { "https://openrouter.ai/api/v1/chat/completions" },
            "Ollama (本地)" => new[] { "http://localhost:11434/v1/chat/completions" },
            _ => new[] { "" }
        };
        foreach (var ep in endpoints) LLMEndpointCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ep });
        if (LLMEndpointCombo.Items.Count > 0) LLMEndpointCombo.SelectedIndex = 0;

        LLMModelNameBox.Text = provider switch
        {
            "Kimi (月之暗面)" => "kimi-k2.6",
            "DeepSeek" => "deepseek-chat",
            "MiMo (小米)" or "MiMo TokenPlan (小米)" => "mimo-v2.5",
            "OpenAI" => "gpt-4o-mini",
            "OpenRouter" => "openai/gpt-4o-mini",
            "Ollama (本地)" => "llama3",
            _ => ""
        };
    }

    private void OnVisionProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (VisionProviderCombo == null || VisionEndpointCombo == null) return;
        var provider = (VisionProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        VisionEndpointCombo.Items.Clear();
        var endpoints = provider switch
        {
            "OpenAI (GPT-4o)" => new[] { "https://api.openai.com/v1/chat/completions" },
            "Google Gemini" => new[] { "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" },
            "DeepSeek" => new[] { "https://api.deepseek.com/v1/chat/completions" },
            "MiMo (小米)" => new[] { "https://api.xiaomimimo.com/v1/chat/completions" },
            "MiMo TokenPlan (小米)" => new[] { "https://token-plan-cn.xiaomimimo.com/v1/chat/completions" },
            "OpenRouter" => new[] { "https://openrouter.ai/api/v1/chat/completions" },
            "Azure OpenAI" => new[] { "https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions" },
            _ => new[] { "" }
        };
        foreach (var ep in endpoints) VisionEndpointCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ep });
        if (VisionEndpointCombo.Items.Count > 0) VisionEndpointCombo.SelectedIndex = 0;

        VisionModelNameBox.Text = provider switch
        {
            "OpenAI (GPT-4o)" => "gpt-4o-mini",
            "Google Gemini" => "gemini-2.0-flash",
            "DeepSeek" => "deepseek-chat",
            "MiMo (小米)" or "MiMo TokenPlan (小米)" => "mimo-v2.5",
            "OpenRouter" => "openai/gpt-4o-mini",
            _ => ""
        };
    }

    private void OnTTSProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TTSProviderCombo == null || TTSEndpointCombo == null) return;
        var selected = (TTSProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";

        TTSEndpointCombo.Items.Clear();
        var endpoints = selected switch
        {
            "MiMo-V2.5-TTS" => new[] { "https://api.xiaomimimo.com/v1/audio/speech" },
            "MiMo TokenPlan (小米)" => new[] { "https://token-plan-cn.xiaomimimo.com/v1/audio/speech" },
            "OpenAI TTS" => new[] { "https://api.openai.com/v1/audio/speech" },
            "Microsoft Azure TTS" => new[] { "https://{region}.tts.speech.microsoft.com/cognitiveservice/v1" },
            "Google TTS" => new[] { "https://texttospeech.googleapis.com/v1/text:synthesize" },
            "GPT-SoVITS (语音克隆)" => new[] { "http://localhost:9880/tts" },
            "OpenRouter" => new[] { "https://openrouter.ai/api/v1/audio/speech" },
            _ => new[] { "" }
        };
        foreach (var ep in endpoints) TTSEndpointCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ep });
        if (TTSEndpointCombo.Items.Count > 0) TTSEndpointCombo.SelectedIndex = 0;

        TTSModelNameBox.Text = selected switch
        {
            "MiMo-V2.5-TTS" or "MiMo TokenPlan (小米)" => "mi-tts-v2.5",
            "OpenAI TTS" => "tts-1",
            "OpenRouter" => "openai/tts-1",
            _ => ""
        };

        var isGptSoVits = selected.Contains("GPT-SoVITS");
        var visibility = isGptSoVits ? Visibility.Visible : Visibility.Collapsed;
        VoiceCloneHint.Visibility = visibility;
        VoiceCloneFileHint.Visibility = visibility;
        VoiceCloneFileGrid.Visibility = visibility;
        VoiceClonePromptHint.Visibility = visibility;
        VoiceClonePromptTextBox.Visibility = visibility;
        VoiceCloneLangHint.Visibility = visibility;
        VoiceCloneLangCombo.Visibility = visibility;
    }

    private void UpdateCustomPlatformVisibility(bool visible)
    {
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        CustomTextUrlHint.Visibility = v;
        CustomTextBaseUrlBox.Visibility = v;
        CustomTextModelHint.Visibility = v;
        CustomTextModelBox.Visibility = v;
        CustomMultimodalHint.Visibility = v;
        CustomMultimodalBaseUrlBox.Visibility = v;
        CustomMultimodalModelBox.Visibility = v;
    }

    private void OnBrowseVoiceClone(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择参考音频文件 (用于GPT-SoVITS语音克隆)",
            Filter = "音频文件 (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            VoiceCloneAudioPathBox.Text = dialog.FileName;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateUI();
        }
    }

    private bool _llmTestPassed;
    private bool _visionTestPassed;

    private async void OnTestLLM(object sender, RoutedEventArgs e)
    {
        var baseUrl = (LLMEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        var model = LLMModelNameBox.Text.Trim();
        var apiKey = LLMApiKeyBox.Text.Trim();

        ModelTestSummary.Text = "测试中...";
        ModelTestSummary.Foreground = System.Windows.Media.Brushes.Yellow;

        var tester = new ModelTestService();
        var result = await tester.TestLlmAsync(baseUrl, model, apiKey);

        _llmTestPassed = result.Success;
        ModelTestSummary.Text = result.Success
            ? $"✓ {result.Message} ({result.LatencyMs}ms)"
            : $"✗ {result.Message}";
        ModelTestSummary.Foreground = result.Success
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;
    }

    private async void OnTestAllModels(object sender, RoutedEventArgs e)
    {
        ModelTestSummary.Text = "测试中...";
        ModelTestSummary.Foreground = System.Windows.Media.Brushes.Yellow;

        var tester = new ModelTestService();
        var results = new List<string>();

        // Test LLM
        var llmUrl = (LLMEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        var llmResult = await tester.TestLlmAsync(llmUrl, LLMModelNameBox.Text.Trim(), LLMApiKeyBox.Text.Trim());
        results.Add($"LLM: {(llmResult.Success ? "✓" : "✗")} {llmResult.Message}");

        // Test Vision
        var visUrl = (VisionEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        var visResult = await tester.TestVisionAsync(visUrl, VisionModelNameBox.Text.Trim(), VisionApiKeyBox.Text.Trim());
        results.Add($"视觉: {(visResult.Success ? "✓" : "✗")} {visResult.Message}");

        // Test TTS (optional)
        var ttsUrl = (TTSEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(TTSModelNameBox.Text))
        {
            var ttsResult = await tester.TestTtsAsync(ttsUrl, TTSModelNameBox.Text.Trim(), TTSApiKeyBox.Text.Trim());
            results.Add($"TTS: {(ttsResult.Success ? "✓" : "✗")} {ttsResult.Message}");
        }

        var allPassed = llmResult.Success && visResult.Success;
        ModelTestSummary.Text = string.Join("\n", results);
        ModelTestSummary.Foreground = allPassed
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;

        _llmTestPassed = llmResult.Success;
        _visionTestPassed = visResult.Success;
    }

    private async void OnTestVision(object sender, RoutedEventArgs e)
    {
        var baseUrl = (VisionEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        var model = VisionModelNameBox.Text.Trim();
        var apiKey = VisionApiKeyBox.Text.Trim();

        ModelTestSummary.Text = "测试中...";
        ModelTestSummary.Foreground = System.Windows.Media.Brushes.Yellow;

        var tester = new ModelTestService();
        var result = await tester.TestVisionAsync(baseUrl, model, apiKey);

        _visionTestPassed = result.Success;
        ModelTestSummary.Text = result.Success
            ? $"✓ {result.Message} ({result.LatencyMs}ms)"
            : $"✗ {result.Message}";
        ModelTestSummary.Foreground = result.Success
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        // Validate model names on step 2 (dedicated model name step)
        if (_currentStep == 2)
        {
            if (LLMModelNameBox == null || string.IsNullOrWhiteSpace(LLMModelNameBox.Text))
            {
                MessageBox.Show("请填写文本模型名称！", "模型名称必填", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (VisionModelNameBox == null || string.IsNullOrWhiteSpace(VisionModelNameBox.Text))
            {
                MessageBox.Show("请填写视觉模型名称！", "模型名称必填", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

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
        if (StepIndicator != null)
            StepIndicator.Text = $"步骤 {_currentStep}/{TotalSteps}: {GetStepName(_currentStep)}";

        if (LLMPanel != null) LLMPanel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        if (ModelNamePanel != null) ModelNamePanel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        if (VisionPanel != null) VisionPanel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        if (ASRPanel != null) ASRPanel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        if (TTSPanel != null) TTSPanel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        if (FallbackLLMPanel != null) FallbackLLMPanel.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;
        if (LocalVisionPanel != null) LocalVisionPanel.Visibility = _currentStep == 7 ? Visibility.Visible : Visibility.Collapsed;
        if (Live2DPanel != null) Live2DPanel.Visibility = _currentStep == 8 ? Visibility.Visible : Visibility.Collapsed;
        if (PersonalityPanel != null) PersonalityPanel.Visibility = _currentStep == 9 ? Visibility.Visible : Visibility.Collapsed;
        if (WakeWordPanel != null) WakeWordPanel.Visibility = _currentStep == 10 ? Visibility.Visible : Visibility.Collapsed;
        if (CustomPlatformPanel != null) CustomPlatformPanel.Visibility = _currentStep == 11 ? Visibility.Visible : Visibility.Collapsed;
        if (SkillsPanel != null) SkillsPanel.Visibility = _currentStep == 12 ? Visibility.Visible : Visibility.Collapsed;
        if (BiliLoginPanel != null) BiliLoginPanel.Visibility = _currentStep == 13 ? Visibility.Visible : Visibility.Collapsed;
        if (RelayPanel != null) RelayPanel.Visibility = _currentStep == 14 ? Visibility.Visible : Visibility.Collapsed;

        if (BackBtn != null) BackBtn.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (NextBtn != null) NextBtn.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
        if (FinishBtn != null) FinishBtn.Visibility = _currentStep == TotalSteps ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetStepName(int step) => step switch
    {
        1 => "文本模型提供商",
        2 => "★ 模型名称",
        3 => "视觉模型",
        4 => "语音识别",
        5 => "语音合成",
        6 => "备用模型",
        7 => "离线视觉",
        8 => "Live2D",
        9 => "AI人格",
        10 => "唤醒词与界面",
        11 => "自定义平台",
        12 => "AI技能",
        13 => "账号登录",
        14 => "中转网站",
        _ => ""
    };

    private void SaveConfig()
    {
        var config = new AppConfig
        {
            LLM = new LLMConfig
            {
                Provider = (LLMProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Kimi",
                ApiKey = LLMApiKeyBox.Text.Trim(),
                Model = LLMModelNameBox.Text.Trim()
            },
            Vision = new VisionConfig
            {
                Provider = (VisionProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "OpenAI",
                ApiKey = VisionApiKeyBox.Text.Trim(),
                Model = VisionModelNameBox.Text.Trim()
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
                Model = TTSModelNameBox.Text.Trim(),
                Voice = (TTSVoiceCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "茉莉",
                VoiceCloneAudioPath = VoiceCloneAudioPathBox.Text.Trim(),
                VoiceClonePromptText = VoiceClonePromptTextBox.Text.Trim(),
                VoiceCloneLang = (VoiceCloneLangCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "zh"
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

        // Use user-selected endpoint from dropdown (not preset lookup)
        config.LLM.BaseUrl = (LLMEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        config.Vision.BaseUrl = (VisionEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        config.TTS.BaseUrl = (TTSEndpointCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";

        // Vision base URL already set from dropdown above

        // Set ASR base URL
        var asrPreset = ConfigManager.GetAsrProviderInfo(config.ASR.Provider);
        if (asrPreset.HasValue)
        {
            config.ASR.BaseUrl = asrPreset.Value.BaseUrl;
        }

        // TTS base URL already set from dropdown above

        // Set fallback LLM base URL (keep user-entered model name)
        var fallbackLLMInfo = ConfigManager.GetProviderInfo(config.FallbackLLM.Provider);
        if (fallbackLLMInfo.HasValue)
        {
            config.FallbackLLM.BaseUrl = fallbackLLMInfo.Value.BaseUrl;
            if (string.IsNullOrWhiteSpace(config.FallbackLLM.Model))
                config.FallbackLLM.Model = fallbackLLMInfo.Value.Model;
        }

        // Set local vision model path
        var localVisionInfo = ConfigManager.GetLocalVisionModelInfo(config.LocalVision.ModelName);
        if (localVisionInfo.HasValue)
        {
            config.LocalVision.ModelPath = localVisionInfo.Value.ModelPath;
        }

        // Personality
        config.Personality.SystemPrompt = PersonalityPromptBox.Text.Trim();
        config.Personality.IsGameCharacter = IsGameCharacterCheckBox.IsChecked ?? false;
        config.Personality.GameCharacterName = GameCharacterNameBox.Text.Trim();

        // Wake word & UI
        config.WakeWord.WakeWord = WakeWordBox.Text.Trim();
        config.IslandUI.ShowInputBox = ShowInputBoxCheckBox.IsChecked ?? true;
        config.IslandUI.InputBoxPosition = (InputBoxPositionCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() switch
        {
            "左下" => "left-bottom",
            "中下" => "center-bottom",
            _ => "right-bottom"
        };
        config.Context.Enabled = EnableContextCompressionCheckBox.IsChecked ?? true;
        config.Performance.Mode = (PerformanceModeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant() ?? "balanced";

        // Custom platform
        config.CustomPlatform.Enabled = EnableCustomPlatformCheckBox.IsChecked ?? false;
        if (config.CustomPlatform.Enabled)
        {
            config.CustomPlatform.TextBaseUrl = CustomTextBaseUrlBox.Text.Trim();
            config.CustomPlatform.TextModel = CustomTextModelBox.Text.Trim();
            config.CustomPlatform.MultimodalBaseUrl = CustomMultimodalBaseUrlBox.Text.Trim();
            config.CustomPlatform.MultimodalModel = CustomMultimodalModelBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(config.CustomPlatform.MultimodalModel))
                config.CustomPlatform.MultimodalEnabled = true;
        }

        // Skills selection
        var selectedSkills = new List<string>();
        if (SkillSummarize.IsChecked == true) selectedSkills.Add("summarize");
        if (SkillTranslate.IsChecked == true) selectedSkills.Add("translate");
        if (SkillCode.IsChecked == true) selectedSkills.Add("code");
        if (SkillEmail.IsChecked == true) selectedSkills.Add("email");
        if (SkillBrainstorm.IsChecked == true) selectedSkills.Add("brainstorm");
        if (SkillAnalyze.IsChecked == true) selectedSkills.Add("analyze");

        // Deploy selected skills
        var skillsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills");
        var targetSkillsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FairyAI", "skills");
        System.IO.Directory.CreateDirectory(targetSkillsDir);

        foreach (var skill in selectedSkills)
        {
            var src = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "BlenderAddon", "skills", skill + ".md");
            // Also check installer skills folder
            var installerSkills = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "skills", skill + ".md");
            if (System.IO.File.Exists(installerSkills))
            {
                System.IO.File.Copy(installerSkills,
                    System.IO.Path.Combine(targetSkillsDir, skill + ".md"), overwrite: true);
            }
        }

        // Relay config
        config.Sync.Enabled = !string.IsNullOrWhiteSpace(RelayUrlBox.Text.Trim());
        config.Sync.SignalRUrl = RelayUrlBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(RelaySecretBox.Text.Trim()))
        {
            // Store relay secret encrypted
            var relaySecretPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "relay_secret.dat");
            System.IO.File.WriteAllText(relaySecretPath, Services.ApiKeyProtector.Protect(RelaySecretBox.Text.Trim()));
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
