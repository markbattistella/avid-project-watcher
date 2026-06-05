# Avid Project Watcher

Background watcher and local admin UI for creating standard folder structures inside Avid project folders when `.avp` files are created.

## Projects

- `src/AvidProjectWatcher.Core`: filesystem, config, exclusions, folder planning, backfill, audit, and watcher logic.
- `src/AvidProjectWatcher.Daemon`: local background service/API host.
- `src/AvidProjectWatcher.Admin`: Avalonia desktop admin UI.
- `tests/AvidProjectWatcher.Core.Tests`: focused core tests.

## Build

This project targets .NET 10 LTS.

```bash
dotnet restore
dotnet build
dotnet test
```

## Run

Start the daemon:

```bash
dotnet run --project src/AvidProjectWatcher.Daemon
```

Start the admin UI:

```bash
dotnet run --project src/AvidProjectWatcher.Admin
```

The daemon listens on `http://localhost:47821` by default.

See `config.example.json` for the local config shape.

## Windows Demo

From PowerShell or Command Prompt on Windows:

```powershell
.\test-demo.cmd
```

This builds the solution, writes an isolated demo config under `%TEMP%\AvidWatcherDemo`, opens daemon/admin windows, opens Explorer, then creates demo `.avp` projects with delays so you can watch template folders appear.

Useful options:

```powershell
.\test-demo.cmd -SkipBuild
.\test-demo.cmd -StepDelaySeconds 5
.\test-demo.cmd -NoAdminUi
.\test-demo.cmd -NoExplorer
```

The demo refuses to run if another daemon is already listening on `http://localhost:47821`, because that would mean the isolated demo config would not be used.

## UI Preview

For quick design review without building the desktop app, open:

```text
design/admin-ui-preview/index.html
```

The preview is static HTML/CSS that mirrors the admin UI layout and visual tokens used by the Avalonia app.

## Windows Service

On the Windows machine that should own the watcher, run PowerShell as Administrator:

```powershell
.\scripts\install-windows-service.ps1
```

To remove it:

```powershell
.\scripts\uninstall-windows-service.ps1
```

Editors do not need to run the admin UI. The admin UI only configures the local daemon.
