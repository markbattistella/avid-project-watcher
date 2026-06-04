param(
    [string]$ServiceName = "AvidProjectWatcher",
    [string]$DisplayName = "Avid Project Watcher",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = "$PSScriptRoot\..\publish\daemon"
)

$ErrorActionPreference = "Stop"

$project = "$PSScriptRoot\..\src\AvidProjectWatcher.Daemon\AvidProjectWatcher.Daemon.csproj"
dotnet publish $project -c Release -r $Runtime --self-contained false -o $PublishDir

$exe = Join-Path $PublishDir "AvidProjectWatcher.Daemon.exe"
if (-not (Test-Path $exe)) {
    throw "Published daemon executable was not found at $exe"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= "`"$exe`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
sc.exe description $ServiceName "Watches configured Avid project scopes and creates standard project folders." | Out-Null
Start-Service -Name $ServiceName

Write-Host "$DisplayName installed and started."
