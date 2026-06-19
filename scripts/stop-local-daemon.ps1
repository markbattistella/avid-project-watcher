param(
    [string]$ServiceName = "AvidProjectWatcher",
    [string]$DaemonExePath,
    [string]$DaemonDirectory,
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = "SilentlyContinue"

function Normalize-Path {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    try {
        return [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
    }
    catch {
        return $Path.TrimEnd("\")
    }
}

$daemonExePath = Normalize-Path -Path $DaemonExePath
$daemonDirectory = Normalize-Path -Path $DaemonDirectory
$daemonProcessNames = @(
    "Avid Project Watcher Daemon.exe",
    "AvidProjectWatcher.Daemon.exe"
)
$daemonCommandMarkers = @(
    "Avid Project Watcher Daemon.exe",
    "AvidProjectWatcher.Daemon.exe",
    "Avid Project Watcher Daemon.dll",
    "AvidProjectWatcher.Daemon.dll"
)

function Test-IsDaemonProcess {
    param([object]$Process)

    if (-not $Process -or $Process.ProcessId -eq $PID) {
        return $false
    }

    $name = [string]$Process.Name
    if ($daemonProcessNames -contains $name) {
        return $true
    }

    $executablePath = Normalize-Path -Path $Process.ExecutablePath
    if ($daemonExePath -and $executablePath -and [string]::Equals($executablePath, $daemonExePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ($daemonDirectory -and $executablePath -and $executablePath.StartsWith($daemonDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $commandLine = [string]$Process.CommandLine
    if ($daemonDirectory -and $commandLine.IndexOf($daemonDirectory, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        foreach ($marker in $daemonCommandMarkers) {
            if ($commandLine.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                return $true
            }
        }
    }

    return $false
}

function Get-DaemonProcesses {
    @(Get-CimInstance Win32_Process | Where-Object { Test-IsDaemonProcess -Process $_ })
}

function Get-DaemonService {
    Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
}

function Stop-ServiceIfPresent {
    $service = Get-DaemonService
    if (-not $service) {
        return
    }

    & sc.exe stop $ServiceName | Out-Null

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Min($TimeoutSeconds, 20))
    do {
        Start-Sleep -Milliseconds 500
        $service = Get-DaemonService
    }
    while ($service -and $service.State -ne "Stopped" -and [DateTime]::UtcNow -lt $deadline)

    $service = Get-DaemonService
    if ($service -and $service.ProcessId -gt 0) {
        Stop-Process -Id $service.ProcessId -Force
    }

    & sc.exe delete $ServiceName | Out-Null
}

function Stop-DaemonProcesses {
    foreach ($process in Get-DaemonProcesses) {
        Stop-Process -Id $process.ProcessId -Force
    }
}

Stop-ServiceIfPresent
Stop-DaemonProcesses

$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $remaining = @(Get-DaemonProcesses)
    $service = Get-DaemonService

    if ($service -and $remaining.Count -eq 0) {
        & sc.exe delete $ServiceName | Out-Null
    }

    if ($remaining.Count -eq 0 -and -not $service) {
        exit 0
    }

    Stop-DaemonProcesses
    Start-Sleep -Milliseconds 500
}
while ([DateTime]::UtcNow -lt $deadline)

exit 1
