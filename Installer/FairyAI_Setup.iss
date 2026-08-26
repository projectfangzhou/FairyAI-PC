; Fairy AI Installer Script for Inno Setup

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
DisableDirPage=no
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Dependencies\Everything\*"; DestDir: "{app}\Everything"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\Dependencies\OpenClaw\*"; DestDir: "{app}\OpenClaw"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Run]
; Auto-download and install .NET 8 Runtime if not present
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""irm https://dot.net/dotnet-install.ps1 | iex -Channel 8.0 -Runtime dotnet"""; \
    StatusMsg: "正在安装 .NET 8 运行库..."; Flags: runhidden waituntilterminated; \
    Check: not IsDotNet8Installed

; Show setup complete page
Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup-complete"; \
    Description: "启动 Fairy AI"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr "8.0"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

var
  LLMKeyPage: TInputQueryWizardPage;
  ASRKeyPage: TInputQueryWizardPage;
  TTSKeyPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  LLMKeyPage := CreateInputQueryPage(wpSelectDir,
    'Configure Text Model API',
    'Enter your LLM API Key',
    'Supported: Kimi, DeepSeek, MiMo, OpenAI, Ollama (local)');
  LLMKeyPage.Add('Provider:', False);
  LLMKeyPage.Add('API Key:', True);
  LLMKeyPage.Values[0] := 'Kimi';
  LLMKeyPage.Values[1] := '';

  ASRKeyPage := CreateInputQueryPage(LLMKeyPage.ID,
    'Configure Speech Recognition API',
    'Enter your ASR API Key (optional)',
    'Supported: MiMo-V2.5-ASR. Leave blank to use system speech.');
  ASRKeyPage.Add('Provider:', False);
  ASRKeyPage.Add('API Key:', True);
  ASRKeyPage.Values[0] := 'MiMo-V2.5-ASR';
  ASRKeyPage.Values[1] := '';

  TTSKeyPage := CreateInputQueryPage(ASRKeyPage.ID,
    'Configure Text-to-Speech API',
    'Enter your TTS API Key (optional)',
    'Supported: MiMo-V2.5-TTS. Leave blank to use system TTS.');
  TTSKeyPage.Add('Provider:', False);
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
  SL := TStringList.Create;
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
    SaveConfig;
end;
