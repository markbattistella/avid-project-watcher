[CmdletBinding()]
param(
    [string]$DemoRoot = (Join-Path $env:TEMP "AvidWatcherDemo"),
    [int]$StepDelaySeconds = 3,
    [switch]$SkipBuild,
    [switch]$NoAdminUi,
    [switch]$NoExplorer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$isWindowsPlatform = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
if (-not $isWindowsPlatform) {
    throw "This demo script is intended for Windows because it opens PowerShell windows and Explorer."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectRoot = Join-Path $DemoRoot "Projects"
$archiveRoot = Join-Path $projectRoot "Archive"
$configPath = Join-Path $DemoRoot "config.json"
$statePath = Join-Path $DemoRoot "state.json"
$auditPath = Join-Path $DemoRoot "audit.sqlite"
$apiUrl = "http://localhost:47821"
$templateFolders = @("FOOTAGE", "SEQUENCES", "SFX", "GFX")

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function ConvertTo-QuotedPowerShellString {
    param([string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Get-PowerShellExecutable {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) {
        return $pwsh.Source
    }

    $powershell = Get-Command powershell -ErrorAction SilentlyContinue
    if ($powershell) {
        return $powershell.Source
    }

    throw "Could not find pwsh or powershell on PATH."
}

function Test-DaemonIsRunning {
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/status" -TimeoutSec 1 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Wait-ForDaemon {
    Write-Step "Waiting for daemon API at $apiUrl"

    for ($attempt = 1; $attempt -le 60; $attempt++) {
        if (Test-DaemonIsRunning) {
            Write-Host "Daemon is ready."
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Daemon did not become ready within 60 seconds."
}

function Start-DemoTerminal {
    param(
        [string]$Title,
        [string]$Command
    )

    $powerShellExe = Get-PowerShellExecutable
    $escapedTitle = $Title.Replace("'", "''")
    $wrappedCommand = "`$Host.UI.RawUI.WindowTitle = '$escapedTitle'; $Command"
    Start-Process -FilePath $powerShellExe -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        $wrappedCommand
    )
}

function Write-DemoConfig {
    $config = [ordered]@{
        version = 1
        watchedLocations = @(
            [ordered]@{
                id = ([guid]::NewGuid()).ToString()
                name = "Demo Projects"
                rootPath = $projectRoot
                enabled = $true
                folderTemplate = @(
                    foreach ($folder in $templateFolders) {
                        [ordered]@{ relativePath = $folder }
                    }
                )
                excludedPaths = @(
                    [ordered]@{ path = $archiveRoot }
                )
            }
        )
    }

    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath -Encoding UTF8
}

function New-AvpProject {
    param(
        [string]$RelativeDirectory,
        [bool]$ExpectTemplateFolders
    )

    $projectDirectory = Join-Path $projectRoot $RelativeDirectory
    $projectName = Split-Path $projectDirectory -Leaf
    $avpPath = Join-Path $projectDirectory "$projectName.avp"

    Write-Step "Creating $RelativeDirectory"
    New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
    Start-Sleep -Seconds $StepDelaySeconds

    Write-Host "Creating .avp: $avpPath"
    New-Item -ItemType File -Force -Path $avpPath | Out-Null

    if ($ExpectTemplateFolders) {
        Wait-ForTemplateFolders -ProjectDirectory $projectDirectory
    }
    else {
        Start-Sleep -Seconds ($StepDelaySeconds + 2)
        $createdTemplateFolders = @($templateFolders | Where-Object {
            Test-Path (Join-Path $projectDirectory $_)
        })

        if ($createdTemplateFolders.Count -eq 0) {
            Write-Host "Excluded project was left untouched, as expected." -ForegroundColor Green
        }
        else {
            Write-Warning "Excluded project unexpectedly contains template folders: $($createdTemplateFolders -join ', ')"
        }
    }

    Show-ProjectDirectory -ProjectDirectory $projectDirectory
}

function Wait-ForTemplateFolders {
    param([string]$ProjectDirectory)

    $deadline = (Get-Date).AddSeconds(30)

    do {
        $missing = @($templateFolders | Where-Object {
            -not (Test-Path (Join-Path $ProjectDirectory $_))
        })

        if ($missing.Count -eq 0) {
            Write-Host "Template folders created." -ForegroundColor Green
            return
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    Write-Warning "Timed out waiting for folders: $($missing -join ', ')"
}

function Show-ProjectDirectory {
    param([string]$ProjectDirectory)

    Write-Host "Current contents:"
    Get-ChildItem -Path $ProjectDirectory -Force |
        Sort-Object Name |
        ForEach-Object { Write-Host "  $($_.Name)" }
}

if (Test-DaemonIsRunning) {
    throw "A daemon is already running on $apiUrl. Stop the existing daemon/service before running this isolated demo."
}

if ($DemoRoot -notmatch "AvidWatcherDemo") {
    throw "Refusing to reset '$DemoRoot' because the path does not look like a demo folder. Use a path containing 'AvidWatcherDemo'."
}

Write-Step "Preparing demo folder at $DemoRoot"
if (Test-Path $DemoRoot) {
    Remove-Item -Path $DemoRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $projectRoot | Out-Null
New-Item -ItemType Directory -Force -Path $archiveRoot | Out-Null

Write-Step "Creating one legacy project before the watcher starts"
$legacyDirectory = Join-Path $projectRoot "2024\Legacy Project"
New-Item -ItemType Directory -Force -Path $legacyDirectory | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $legacyDirectory "Legacy Project.avp") | Out-Null
Write-Host "Legacy project is ready for manual Backfill testing later: $legacyDirectory"

Write-Step "Writing isolated demo config"
Write-DemoConfig
Write-Host "Config: $configPath"

if (-not $SkipBuild) {
    Write-Step "Restoring and building"
    Push-Location $repoRoot
    try {
        dotnet restore
        dotnet build .\AvidProjectWatcher.sln --no-restore --disable-build-servers /nr:false /m:1
    }
    finally {
        Pop-Location
    }
}

Write-Step "Opening daemon terminal"
$daemonCommand = @"
`$env:AVID_PROJECT_WATCHER_CONFIG = $(ConvertTo-QuotedPowerShellString $configPath)
`$env:AVID_PROJECT_WATCHER_STATE = $(ConvertTo-QuotedPowerShellString $statePath)
`$env:AVID_PROJECT_WATCHER_AUDIT_DB = $(ConvertTo-QuotedPowerShellString $auditPath)
Set-Location $(ConvertTo-QuotedPowerShellString $repoRoot)
dotnet run --project .\src\AvidProjectWatcher.Daemon
"@
Start-DemoTerminal -Title "Avid Project Watcher Daemon" -Command $daemonCommand

Wait-ForDaemon

if (-not $NoAdminUi) {
    Write-Step "Opening admin UI terminal"
    $adminCommand = @"
Set-Location $(ConvertTo-QuotedPowerShellString $repoRoot)
dotnet run --project .\src\AvidProjectWatcher.Admin
"@
    Start-DemoTerminal -Title "Avid Project Watcher Admin UI" -Command $adminCommand
}

if (-not $NoExplorer) {
    Write-Step "Opening Explorer at demo Projects folder"
    Start-Process explorer.exe -ArgumentList $projectRoot
}

Write-Step "Starting live simulation"
Write-Host "Watch Explorer and the daemon/admin windows. Each step waits $StepDelaySeconds second(s)."
Start-Sleep -Seconds $StepDelaySeconds

New-AvpProject -RelativeDirectory "2026\Episode 01" -ExpectTemplateFolders $true
Start-Sleep -Seconds $StepDelaySeconds

New-AvpProject -RelativeDirectory "2026\Episode 02" -ExpectTemplateFolders $true
Start-Sleep -Seconds $StepDelaySeconds

New-AvpProject -RelativeDirectory "Archive\Skipped Project" -ExpectTemplateFolders $false

Write-Step "Demo complete"
Write-Host "Demo root: $DemoRoot"
Write-Host "The daemon/admin windows are still open so you can inspect logs and run manual Backfill."
Write-Host "Manual Backfill check: select 'Demo Projects' in the admin UI, run dry run, then commit. It should update the 2024\Legacy Project folder."
