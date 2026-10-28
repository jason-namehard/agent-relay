#ifndef SourcePath
  #error SourcePath is required
#endif
#ifndef ReleaseVersion
  #error ReleaseVersion is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif
#ifndef IconPath
  #error IconPath is required
#endif

[Setup]
#ifdef TestAppId
AppId={#TestAppId}
#else
AppId={{6476C39F-39A5-4BF2-AFC7-1E9371D4B556}
#endif
AppName=Agent Relay
AppVersion={#ReleaseVersion}
AppPublisher=Agent Relay contributors
AppPublisherURL=https://github.com/jason-namehard/agent-relay
DefaultDirName={localappdata}\Programs\Agent Relay
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\gui\AgentRelay.Desktop.exe
LicenseFile={#SourcePath}\LICENSE
OutputDir={#OutputPath}
OutputBaseFilename=AgentRelay-v{#ReleaseVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
#ifdef TestAppId
Name: "{autoprograms}\{#TestAppId}"; Filename: "{app}\gui\AgentRelay.Desktop.exe"; WorkingDir: "{app}\gui"
Name: "{autodesktop}\{#TestAppId}"; Filename: "{app}\gui\AgentRelay.Desktop.exe"; WorkingDir: "{app}\gui"; Tasks: desktopicon
#else
Name: "{autoprograms}\Agent Relay"; Filename: "{app}\gui\AgentRelay.Desktop.exe"; WorkingDir: "{app}\gui"
Name: "{autodesktop}\Agent Relay"; Filename: "{app}\gui\AgentRelay.Desktop.exe"; WorkingDir: "{app}\gui"; Tasks: desktopicon
#endif

[Run]
Filename: "{app}\gui\AgentRelay.Desktop.exe"; Description: "{cm:LaunchProgram,Agent Relay}"; Flags: nowait postinstall skipifsilent
