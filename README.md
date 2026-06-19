# Avid Project Watcher

Every time an editor creates a new Avid project, Avid Project Watcher automatically builds the standard folder structure inside it - FOOTAGE, SEQUENCES, GFX, SFX, or whatever your facility uses. No one has to remember. No one has to do it manually. It just happens.

---

## How it works

Two pieces work together:

**The Daemon** is the background service that does the actual work. It watches your Avid project share and reacts the moment a new `.avp` file appears - creating whatever folders you have configured. Editors never see it or interact with it.

**The Admin UI** is the desktop app you use to configure the daemon. You open it when you want to change what folders get created, add a new share to watch, or run a backfill on existing projects. Once you have saved your settings you can close it - the daemon keeps running.

Both need to be on the same machine, or the Admin UI needs to be pointed at the machine running the daemon. Editors themselves need neither.

---

## Downloads

Get the latest release from the [Releases page](https://github.com/markbattistella/avid-project-watcher/releases/latest).

| Platform | File | Contains |
| --- | --- | --- |
| Windows | `AvidProjectWatcher-Setup-win-x64.exe` | Single installer - choose what to install |
| macOS (Apple Silicon) | `AvidProjectWatcher-Admin-osx-arm64.dmg` | Admin UI app |
| macOS (Apple Silicon) | `AvidProjectWatcher-Daemon-osx-arm64.zip` | Daemon binary |

---

## Windows setup

Run `AvidProjectWatcher-Setup-win-x64.exe` as an administrator. The installer will ask what to put on the machine:

- **Both (recommended)** - installs the daemon as a Windows Service and the Admin UI. Use this for a new setup on a machine that has access to your Avid project share.
- **Daemon only** - installs just the background service, no desktop app. Use this on a dedicated server.
- **Admin UI only** - installs just the config tool. Use this on a workstation that will connect to a daemon running on another machine.

If you select the daemon component on a machine that does not already have the daemon installed, the installer scans the local subnet for an existing Avid Project Watcher daemon. If it finds one, daemon installation is blocked and the installer switches to **Admin UI only** so the machine does not accidentally create a second watcher for the same facility. Existing local daemon installs can still be updated by running the new installer on the daemon machine.

The installer also has an optional **Clean install** checkbox. It is off by default. When enabled, it removes local Avid Project Watcher settings before installing the selected components:

- Admin UI selected: removes this Windows user's Admin UI preferences.
- Daemon selected: removes the local daemon config, state, and audit database.

The daemon starts automatically after install and will start again on every reboot. You do not need to do anything else to keep it running.

To update, just run the new installer on top of the existing one. It stops the service, replaces the files, and restarts it.

To remove, go to **Settings → Apps** and uninstall from there, or use Add/Remove Programs.

---

## macOS setup

macOS requires two separate steps - one for the Admin UI, one for the daemon.

### Admin UI

Open `AvidProjectWatcher-Admin-osx-arm64.dmg` and drag the app to your Applications folder. On first launch, macOS may say the app is from an unidentified developer. Right-click the app and choose **Open**, then confirm. This only happens once.

### Daemon

Extract `AvidProjectWatcher-Daemon-osx-arm64.zip`. Inside you will find a single file: `AvidProjectWatcher.Daemon`. Open Terminal, navigate to the folder, and run:

```bash
chmod +x AvidProjectWatcher.Daemon
./AvidProjectWatcher.Daemon
```

Keep this Terminal window open. The daemon runs until you close it or quit Terminal.

For a permanent background setup (so it survives restarts), ask your IT person to register it as a launchd service. A setup guide for this is coming in a later release.

---

## First-time configuration

1. Make sure the daemon is running (Windows: it started automatically; macOS: you ran it from Terminal).
2. Open the Admin UI. The green dot in the top-left corner means it is connected to the daemon.
3. Click **New** in the sidebar to add a watch folder.
4. Set the **root path** to your Avid project share - for example `\\nas\Projects` or `/Volumes/Projects`. You can type it, paste it, or drag the folder from Finder or Explorer directly onto the field.
5. In the **Folder Template** section, add the folder names you want created inside each new project. These are flat names: `FOOTAGE`, `SEQUENCES`, `GFX`, `SFX`, and so on. Nested paths like `FOOTAGE/CAMERA` are not supported.
6. If there are subdirectories you want the watcher to ignore (for example an `Archive` folder where old projects live), add those under **Excluded Paths**.
7. Click **Apply Changes** in the top bar. The daemon picks up the new config straight away.

From this point, any new `.avp` file created anywhere under that root path will trigger automatic folder creation.

---

## Catching up existing projects (Backfill)

If you have projects that existed before you set this up, use the Backfill tool to create the missing folders.

1. Select the watch folder in the sidebar.
2. Scroll down to **Maintenance** and click **Backfill**.
3. Click **Dry Run** to preview what would be created without actually doing anything.
4. When you are happy with the results, click **Commit**. The daemon creates only what is missing - it never touches folders that already exist.

---

## Connecting to a daemon on another machine

By default, the daemon listens only on `localhost`. To manage it from another computer, first opt the server daemon into LAN access. On Windows, run this once from an elevated Command Prompt on the daemon machine, then restart the **AvidProjectWatcher** service:

```cmd
setx AVID_PROJECT_WATCHER_API_HOST 0.0.0.0 /M
```

Then open the Admin UI on your workstation, go to **Settings**, and change the daemon URL to point at the server - for example `http://192.168.1.50:47821`.

Make sure port `47821` is accessible between the two machines. Treat this as an internal admin port: expose it only on trusted networks or restrict it with a firewall rule.

---

## Audit log

Every folder creation, backfill, and config change is logged. Open the **Logs** panel in the Maintenance section to see recent activity, or export a CSV for your records.

---

## License

Copyright (C) 2026 Mark Battistella and Melissa Anderton-Battistella

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

See [LICENSE](LICENSE) for the full text.

---

## For developers

See [CONTRIBUTING.md](CONTRIBUTING.md) or browse the source. The project targets .NET 10 LTS.

```bash
dotnet restore
dotnet build
dotnet test
```

The daemon listens on `http://localhost:47821` by default. See `config.example.json` for the config structure.
