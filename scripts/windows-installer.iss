; Avid Project Watcher
; Copyright (C) 2026  MB+MAB
;
; This program is free software: you can redistribute it and/or modify
; it under the terms of the GNU Affero General Public License as published by
; the Free Software Foundation, either version 3 of the License, or
; (at your option) any later version.
;
; This program is distributed in the hope that it will be useful,
; but WITHOUT ANY WARRANTY; without even the implied warranty of
; MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
; GNU Affero General Public License for more details.
;
; You should have received a copy of the GNU Affero General Public License
; along with this program.  If not, see <https://www.gnu.org/licenses/>.

#define AppName      "Avid Project Watcher"
#define AppPublisher "MB+MAB"
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
; Allow running on top of an existing install without uninstalling first
CloseApplications=force

[Types]
Name: full;   Description: "Both (recommended for new setups)"
Name: daemon; Description: "Daemon only - server or background machine"
Name: admin;  Description: "Admin UI only - connect to an existing daemon"

[Components]
Name: daemon; Description: "Watcher Daemon (background service)";   Types: full daemon; Flags: disablenouninstallwarning
Name: admin;  Description: "Admin UI (configuration tool)";         Types: full admin

[Tasks]
Name: cleaninstall; Description: "Clean install - remove local settings, state, and audit logs before installing"; Flags: unchecked

[Files]
Source: "{#AdminDir}\*";  DestDir: "{app}\Admin";  Components: admin;  Flags: recursesubdirs ignoreversion
Source: "{#DaemonDir}\*"; DestDir: "{app}\Daemon"; Components: daemon; Flags: recursesubdirs ignoreversion
Source: "detect-network-daemons.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\Avid Project Watcher Admin"; Filename: "{app}\Admin\{#AdminExe}"; Components: admin
Name: "{group}\Uninstall Avid Project Watcher"; Filename: "{uninstallexe}"

[InstallDelete]
Type: filesandordirs; Name: "{localappdata}\AvidProjectWatcher"; Check: IsCleanAdminInstallSelected
Type: filesandordirs; Name: "{commonappdata}\AvidProjectWatcher"; Check: IsCleanDaemonInstallSelected
Type: filesandordirs; Name: "{win}\System32\config\systemprofile\AppData\Local\AvidProjectWatcher"; Check: IsCleanDaemonInstallSelected

[Run]
; Register and start the daemon as a Windows Service
; Create the service with basic auto start first, then switch to delayed-auto.
; Delayed auto start fires after the network stack is fully ready, which matters
; for a service that needs to reach a network share.
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath=""{app}\Daemon\{#DaemonExe}"" DisplayName=""{#AppName}"" start=auto"; \
  Components: daemon; Flags: runhidden; StatusMsg: "Installing watcher service..."
Filename: "{sys}\sc.exe"; Parameters: "config {#ServiceName} start=delayed-auto"; \
  Components: daemon; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Watches Avid project folders and creates standard folder structures."""; \
  Components: daemon; Flags: runhidden
; If the service crashes, restart it after 10 seconds (up to 3 times).
; Failure count resets after 1 day of clean uptime.
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} reset=86400 actions=restart/10000/restart/10000/restart/10000"; \
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
var
  NetworkScanDone: Boolean;
  NetworkDaemonDetected: Boolean;
  NetworkDaemonSummary: String;
  DaemonInstallOverride: Boolean;

function IsCleanInstallSelected(): Boolean;
begin
  Result := IsTaskSelected('cleaninstall');
end;

function IsCleanAdminInstallSelected(): Boolean;
begin
  Result := IsCleanInstallSelected() and IsComponentSelected('admin');
end;

function IsCleanDaemonInstallSelected(): Boolean;
begin
  Result := IsCleanInstallSelected() and IsComponentSelected('daemon');
end;

function ScExePath(): String;
begin
  Result := ExpandConstant('{sys}\sc.exe');
end;

function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ScExePath(), 'query {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode = 0;
end;

function RunNetworkDaemonScan(): Boolean;
var
  OutputPath: String;
  ScriptPath: String;
  Params: String;
  ScanOutput: AnsiString;
  ResultCode: Integer;
begin
  Result := False;
  NetworkDaemonSummary := '';

  ExtractTemporaryFile('detect-network-daemons.ps1');

  ScriptPath := ExpandConstant('{tmp}\detect-network-daemons.ps1');
  OutputPath := ExpandConstant('{tmp}\avid-project-watcher-daemon-scan.txt');
  DeleteFile(OutputPath);

  Params :=
    '-NoProfile -ExecutionPolicy Bypass -File "' + ScriptPath + '"' +
    ' -OutputPath "' + OutputPath + '"';

  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Exit;

  if not FileExists(OutputPath) then
    Exit;

  if not LoadStringFromFile(OutputPath, ScanOutput) then
    Exit;

  NetworkDaemonSummary := Trim(String(ScanOutput));
  Result := NetworkDaemonSummary <> '';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Attempt: Integer;
begin
  Result := '';

  if not IsComponentSelected('daemon') then
    Exit;

  if not DaemonInstallOverride then
  begin
    if not NetworkScanDone then
    begin
      NetworkScanDone := True;
      NetworkDaemonDetected := RunNetworkDaemonScan();
    end;

    if NetworkDaemonDetected then
    begin
      Result :=
        'Daemon installation is blocked because an Avid Project Watcher daemon is already reachable on this network:' + #13#10 + #13#10 +
        NetworkDaemonSummary + #13#10 + #13#10 +
        'Install the Admin UI only on this machine.';
      Exit;
    end;
  end;

  if not ServiceExists() then
    Exit;

  Exec(ScExePath(), 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Exec(ScExePath(), 'delete {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  for Attempt := 1 to 15 do
  begin
    if not ServiceExists() then
      Exit;

    Sleep(1000);
  end;

  Result := 'The existing Avid Project Watcher service is still stopping. Wait a few seconds, then run the installer again.';
end;

procedure WizardFormKeyDown(Sender: TObject; var Key: Word; Shift: TShiftState);
begin
  if ((Key = 49) or (Key = 97)) and (ssCtrl in Shift) then
  begin
    DaemonInstallOverride := True;
    Key := 0;
    MsgBox(
      'Advanced daemon install override unlocked.' + #13#10 + #13#10 +
      'Use this only when this machine is intentionally watching a separate project share.',
      mbInformation, MB_OK);
  end;
end;

procedure InitializeWizard;
begin
  WizardForm.KeyPreview := True;
  WizardForm.OnKeyDown := @WizardFormKeyDown;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID <> wpSelectComponents then
    Exit;

  if not IsComponentSelected('daemon') then
    Exit;

  if DaemonInstallOverride then
    Exit;

  if not NetworkScanDone then
  begin
    NetworkScanDone := True;
    NetworkDaemonDetected := RunNetworkDaemonScan();
  end;

  if not NetworkDaemonDetected then
    Exit;

  WizardSelectComponents('admin');
  MsgBox(
    'An Avid Project Watcher daemon is already reachable on this network:' + #13#10 + #13#10 +
    NetworkDaemonSummary + #13#10 + #13#10 +
    'Daemon installation is blocked to prevent accidental multiple watchers on the same project network.' + #13#10 + #13#10 +
    'This installer has switched to Admin UI only.',
    mbCriticalError, MB_OK);

  Result := False;
end;

// Warn if the user picks Admin-only but no daemon URL has been configured.
// This is just a notice - the Admin UI can still be configured after install.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not IsComponentSelected('daemon')) then
    MsgBox(
      'Admin UI installed without the daemon.' + #13#10 + #13#10 +
      'Open the Admin UI, go to Settings, and enter the URL of a running daemon to connect to it.',
      mbInformation, MB_OK);
end;
