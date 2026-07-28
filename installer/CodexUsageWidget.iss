#define MyAppName "Codex Usage Widget"
#define MyAppVersion "1.2.1"
#define MyAppPublisher "Codex Usage Widget"
#define MyAppExeName "CodexUsageWidget.exe"

[Setup]
AppId={{593D5857-A6C5-4786-B979-EF81135A4EE0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\CodexUsageWidget
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\installer-output
OutputBaseFilename=CodexUsageWidget-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
AppMutex=Local\CodexUsageWidget.SingleInstance
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "launchatstartup"; Description: "Lancer automatiquement avec Windows"; GroupDescription: "Options de demarrage :"; Flags: checkedonce

[Files]
Source: "..\dist\CodexUsageWidget.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CodexUsageWidget"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: launchatstartup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /IM {#MyAppExeName} /F >nul 2>&1"; Flags: runhidden; RunOnceId: "StopWidget"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\CodexUsageWidget"

[Code]
var
  CodexPage: TOutputMsgWizardPage;

function IsCodexInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result :=
    Exec(
      ExpandConstant('{cmd}'),
      '/d /c where codex >nul 2>&1',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) and
    (ResultCode = 0);
end;

procedure InitializeWizard;
begin
  CodexPage :=
    CreateOutputMsgPage(
      wpWelcome,
      'Verification de Codex CLI',
      'Le widget utilise votre installation locale de Codex.',
      '');

  if IsCodexInstalled then
  begin
    CodexPage.MsgLabel.Caption :=
      'Codex CLI a ete detecte sur cet ordinateur.' + #13#10 + #13#10 +
      'Le widget utilisera la session Codex deja authentifiee pour afficher ' +
      'les quotas, les credits et les tokens de la conversation recente.';
  end
  else
  begin
    CodexPage.MsgLabel.Caption :=
      'Codex CLI n''a pas ete detecte.' + #13#10 + #13#10 +
      'Vous pouvez tout de meme installer le widget. Il affichera les dernieres ' +
      'donnees en cache ou restera en attente jusqu''a ce que Codex CLI soit ' +
      'installe et authentifie.' + #13#10 + #13#10 +
      'Documentation : https://developers.openai.com/codex/cli';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SettingsDirectory: String;
  SettingsFile: String;
  StartWithWindowsValue: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  SettingsDirectory := ExpandConstant('{localappdata}\CodexUsageWidget');
  SettingsFile := SettingsDirectory + '\settings.json';
  if FileExists(SettingsFile) then
    Exit;

  ForceDirectories(SettingsDirectory);
  if WizardIsTaskSelected('launchatstartup') then
    StartWithWindowsValue := 'true'
  else
    StartWithWindowsValue := 'false';

  SaveStringToFile(
    SettingsFile,
    '{' + #13#10 +
    '  "StartWithWindows": ' + StartWithWindowsValue + #13#10 +
    '}' + #13#10,
    False);
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
end;
