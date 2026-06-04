# SleepDrives

A small Windows tray utility that **disables selected hard drives on a schedule** and re-enables them automatically — part of the **bdh-utils** collection.

Point it at the data drives you only use during the day (or only at night), set the hours, and SleepDrives takes them offline and brings them back on time, every day, without you thinking about it.

## What it does

- **Pick the drives to manage.** SleepDrives lists every physical disk on the machine. Tick the ones you want on a schedule. The Windows **boot and system disks are always protected** and can never be selected, so a schedule can't lock you out of your own PC.
- **Set when they're disabled.** Add one or more time windows, each with its own days of the week and a start/end time. Overnight windows (e.g. `22:00`–`07:00`) are supported and correctly span midnight.
- **Automatic enforcement.** While *"Enforce this schedule automatically"* is on, SleepDrives checks the clock every 30 seconds (and again whenever the PC wakes from sleep) and reconciles each managed drive: offline inside a window, online outside it. If you bring a drive back manually during a window, it's re-disabled on the next check.
- **Graceful first, but always eventually.** Before disabling a drive, SleepDrives flushes its volumes' buffers and tries a clean dismount (the *safely-remove* sequence). Because background services like the search indexer or antivirus usually keep a handle open on any mounted volume, a fully clean dismount often isn't possible — so after a short grace period (two minutes) of retrying, SleepDrives uses the Windows **Restart Manager** to ask the programs holding the drive to close, gives the clean dismount one more try, and only then **forces** the drive offline as a last resort. The buffer flush still happens first, so cached writes always reach the disk before it goes down.
- **Lives in the tray.** Closing the window minimises to the system tray so enforcement keeps running. The tray icon turns orange while drives are disabled and grey otherwise.
- **Starts with Windows (optional).** Because taking a disk offline needs administrator rights, the optional sign-in launch is registered as an elevated **scheduled task**, so it starts silently at logon instead of prompting every time.

"Disabling" a drive takes it **offline** via the Windows storage layer (`MSFT_Disk`) — the same action as *Offline* in Disk Management. Each volume is flushed, locked and dismounted first (the same steps as *Safely Remove Hardware*), so the filesystem is quiesced before the disk goes offline. It's fully reversible; enabling brings the disk back online (and clears the read-only flag if Windows set one).

> ⚠️ **Heads-up:** SleepDrives tries to dismount cleanly first, but after the grace period it *will* force a busy drive offline so the schedule is always honoured — buffers are flushed first, but a program with unsaved work open on that drive could still lose it. Don't schedule a drive you may be actively writing to during the window. SleepDrives requires administrator rights and will prompt via UAC on launch.

## Settings

Preferences are stored as JSON at:

```
%AppData%\bdh-utils\SleepDrives\settings.json
```

Drive selections are keyed by each disk's stable serial number, so they survive reboots and reconnecting the drive to a different port.

## Building from source

```powershell
dotnet build SleepDrives.sln -c Release
dotnet test  SleepDrives.sln -c Release
```

Requires the .NET 8 SDK on Windows. The app targets `net8.0-windows` (WPF + Windows Forms tray icon).

To regenerate the brand icon after changing the glyph:

```powershell
pwsh tools\make-icon.ps1
```

## Releases

Tagging a commit `vX.Y.Z` runs the test suite, publishes a **single-file, self-contained `SleepDrives.exe`** (no loose DLLs), builds an installer with Inno Setup, and attaches both to a GitHub Release — but only when every test passes.

---

SleepDrives was **developed entirely with AI**. It's **free and no-nonsense** — no ads, no telemetry, no accounts — and released as **free, open-source software** under the [Apache License 2.0](LICENSE).

Part of the [bdh-utils](https://github.com/bdh-utils) collection.
