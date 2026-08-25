; Fairy AI Installer Script for Inno Setup
; Download Inno Setup from https://jrsoftware.org/isdl.php

#define MyAppName "Fairy AI"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Fairy AI"
#define MyAppExeName "MyAiAssistant.exe"

[Setup]
AppId={{F8A3B2C1-D4E5-6F78-9A0B-C1D2E3F4A5B6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=FairyAI_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
LicenseFile=
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "Compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "Compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: checkedonce
Name: "startupicon"; Description: "开机自启动"; GroupDescription: "附加选项:"; Flags: unchecked

[Files]
; Main application files
Source: "..\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Everything portable
Source: "..\Dependencies\Everything\*"; DestDir: "{app}\Everything"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; OpenClaw
Source: "..\Dependencies\OpenClaw\*"; DestDir: "{app}\OpenClaw"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start (if selected)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Show setup complete page
Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup-complete"; \
    Description: "启动 Fairy AI"; Flags: nowait postinstall skipifsilent

[Code]
// Check if .NET 8 Runtime is installed
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr "8.0"', '', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Check if Everything is installed
function IsEverythingInstalled(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Everything\Everything.exe')) or
            FileExists('C:\Program Files\Everything\Everything.exe');
end;

// Custom wizard page for API keys
var
  LLMKeyPage: TInputQueryWizardPage;
  ASRKeyPage: TInputQueryWizardPage;
  TTSKeyPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  // LLM API Key page
  LLMKeyPage := CreateInputQueryPage(wpSelectDir,
    '配置文本模型 API',
    '请填写你的 LLM (大语言模型) API Key',
    '支持: Kimi, DeepSeek, MiMo, OpenAI, Ollama (本地)');
  LLMKeyPage.Add('LLM 提供商:', False);
  LLMKeyPage.Add('API Key:', True);
  LLMKeyPage.Values[0] := 'Kimi (月之暗面)';
  LLMKeyPage.Values[1] := '';

  // ASR API Key page
  ASRKeyPage := CreateInputQueryPage(LLMKeyPage.ID,
    '配置语音识别 API',
    '请填写你的 ASR (语音识别) API Key',
    '支持: MiMo-V2.5-ASR (推荐)');
  ASRKeyPage.Add('ASR 提供商:', False);
  ASRKeyPage.Add('API Key:', True);
  ASRKeyPage.Values[0] := 'MiMo-V2.5-ASR';
  ASRKeyPage.Values[1] := '';

  // TTS API Key page
  TTSKeyPage := CreateInputQueryPage(ASRKeyPage.ID,
    '配置语音合成 API',
    '请填写你的 TTS (语音合成) API Key',
    '支持: MiMo-V2.5-TTS (推荐)');
  TTSKeyPage.Add('TTS 提供商:', False);
  TTSKeyPage.Add('API Key:', True);
  TTSKeyPage.Values[0] := 'MiMo-V2.5-TTS';
  TTSKeyPage.Values[1] := '';
end;

procedure SaveConfig;
var
  ConfigFile: string;
  SL: TStringList;
begin
  ConfigFile := ExpandConstant('{app}\config.json');
  SL := TStringList;
  try
    SL.Add('{');
    SL.Add('  "llm": {');
    SL.Add('    "provider": "' + LLMKeyPage.Values[0] + '",');
    SL.Add('    "apiKey": "' + LLMKeyPage.Values[1] + '"');
    SL.Add('  },');
    SL.Add('  "asr": {');
    SL.Add('    "provider": "' + ASRKeyPage.Values[0] + '",');
    SL.Add('    "apiKey": "' + ASRKeyPage.Values[1] + '"');
    SL.Add('  },');
    SL.Add('  "tts": {');
    SL.Add('    "provider": "' + TTSKeyPage.Values[0] + '",');
    SL.Add('    "apiKey": "' + TTSKeyPage.Values[1] + '"');
    SL.Add('  }');
    SL.Add('}');
    SL.SaveToFile(ConfigFile);
  finally
    SL.Free;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SaveConfig;
  end;
end;
