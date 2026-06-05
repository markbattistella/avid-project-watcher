#define AppName      "Avid Project Watcher"
#define AppPublisher "Mark Battistella"
#define AppURL       "https://github.com/markbattistella/avid-project-watcher"
#define AdminExe     "AvidProjectWatcher.Admin.exe"
#define DaemonExe    "AvidProjectWatcher.Daemon.exe"
#define ServiceName  "AvidProjectWatcher"

; Version and source paths are passed in from the CI:
;   iscc /DVersion=2026.06.05 /DAdminDir=.\out\admin\win-x64 /DDaemonDir=.\out\daemon\win-x64 scripts\windows-installer.iss

[Setup]
AppId={{9B2E4C3A-F1D7-4E8B-A3C6-2D5F8E9B0A1C}
AppName={#AppName}
AppVersion={#Version}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={autopf}\AvidProjectWatcher
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=dist
OutputBaseFilename=AvidProjectWatcher-Setup-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Types]
Name: full;   Description: "Both (recommended for new setups)"
Name: daemon; Description: "Daemon only — server or background machine"
Name: admin;  Description: "Admin UI only — connect to an existing daemon"

[Components]
Name: daemon; Description: "Watcher Daemon (background service)";   Types: full daemon; Flags: disablenouninstallwarning
Name: admin;  Description: "Admin UI (configuration tool)";         Types: full admin

[Files]
Source: "{#AdminDir}\*";  DestDir: "{app}\Admin";  Components: admin;  Flags: recursesubdirs ignoreversion
Source: "{#DaemonDir}\*"; DestDir: "{app}\Daemon"; Components: daemon; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Avid Project Watcher Admin"; Filename: "{app}\Admin\{#AdminExe}"; Components: admin
Name: "{group}\Uninstall Avid Project Watcher"; Filename: "{uninstallexe}"

[Run]
; Register and start the daemon as a Windows Service
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath=""{app}\Daemon\{#DaemonExe}"" DisplayName=""{#AppName}"" start=auto"; \
  Components: daemon; Flags: runhidden; StatusMsg: "Installing watcher service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Watches Avid project folders and creates standard folder structures."""; \
  Components: daemon; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; \
  Components: daemon; Flags: runhidden; StatusMsg: "Starting watcher service..."

; Launch Admin UI after install (if selected)
Filename: "{app}\Admin\{#AdminExe}"; Description: "Open Admin UI"; \
  Components: admin; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}";   Flags: runhidden; RunOnceId: StopService
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden; RunOnceId: DeleteService

[Code]
// Warn if the user picks Admin-only but no daemon URL has been configured.
// This is just a notice — the Admin UI can still be configured after install.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not IsComponentSelected('daemon')) then
    MsgBox(
      'Admin UI installed without the daemon.' + #13#10 + #13#10 +
      'Open the Admin UI, go to Settings, and enter the URL of a running daemon to connect to it.',
      mbInformation, MB_OK);
end;
