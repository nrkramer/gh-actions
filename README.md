# gh-actions

A GitHub Actions status indicator: a **waybar module on Linux** and a
**notification-area app on Windows**, sharing one core.

Both show the same aggregate state — green passing, amber running, red failing —
with a count of *live jobs* rather than runs, because one workflow fanning out
to eleven platforms is eleven things you're waiting on.

On Windows, clicking the tray icon opens a panel with per-repo, per-run,
per-platform detail, where clicking a job opens its log in the browser. On
Linux the module carries the same breakdown in its tooltip.

## Layout

```
src/GhActions.Core/     polling, ETags, distillation, aggregation
src/GhActions.Cli/      Linux — waybar module (gh-actions-core)
src/GhActions.Tray/     Windows — WPF tray app (gh-actions-tray.exe)
```

The front ends never call the core. It writes a cache JSON; they read it. That
keeps the indicator and the panel independent of when a poll happens, and lets
either platform's UI be replaced without touching the logic.

## Build

Needs the **.NET 10 SDK** (`winget install Microsoft.DotNet.SDK.10`, or your
distribution's `dotnet-sdk-10.0`). Either OS builds both targets — the tray
project sets `EnableWindowsTargeting`, so a Linux box produces the Windows
`.exe` and a Windows box produces the Linux binary.

On Windows:

```powershell
.\build.ps1                      # self-contained; runs with no .NET installed
.\build.ps1 -FrameworkDependent  # far smaller, needs the .NET Desktop Runtime
.\build.ps1 -LinuxToo            # also build the waybar binary
```

On Linux:

```bash
./build.sh                      # self-contained
SELF_CONTAINED=false ./build.sh # framework-dependent
```

| artifact | self-contained | framework-dependent |
|---|---|---|
| `dist/linux-x64/gh-actions-core` | 70 MB | 90 KB |
| `dist/win-x64/gh-actions-tray.exe` | 165 MB | 232 KB |

## Install

**Windows** — per-user, no admin rights. Run it from the repo after a build,
or copy `gh-actions-tray.exe` next to the script and run it anywhere:

```powershell
.\install\install-windows.ps1              # add -NoStartup to skip run-at-login
.\install\install-windows.ps1 -Uninstall   # leaves config.json alone
```

Then edit `%APPDATA%\gh-actions\config.json` and launch *GitHub Actions* from
the Start menu. **Windows 11 hides new tray icons** — drag it out of the `^`
overflow once to pin it.

**Linux** — installs the binary to `~/.local/bin` and writes a starter config.
It does not edit your waybar config:

```bash
./install/install-linux.sh
gh-actions-core tick        # should print waybar JSON
```

Then add the module to `~/.config/waybar/config.jsonc`:

```jsonc
"custom/actions": {
  "exec": "gh-actions-core tick",
  "return-type": "json",
  "interval": 15,
  "signal": 8,
  "tooltip": true
}
```

and list `"custom/actions"` in `modules-right` (or wherever you want it).

`interval` is only a heartbeat — the binary decides for itself whether a fetch
is due, and after one it signals waybar to re-read immediately, which is what
`"signal": 8` is for. Without that the bar can lag a full interval behind.

The module sets a CSS class from the aggregate state, so style it in
`style.css`:

```css
#custom-actions.success   { color: #9ece6a; }
#custom-actions.failure   { color: #f7768e; }
#custom-actions.running,
#custom-actions.queued    { color: #e0af68; }
#custom-actions.idle,
#custom-actions.cancelled,
#custom-actions.skipped,
#custom-actions.unknown   { color: #565f89; }
```

The text is the GitHub glyph `U+F09B`, with the live job count appended when
anything is running. It comes from a Font Awesome Brands font or a Nerd Font —
check yours resolves it:

```bash
fc-list ':charset=f09b' family
```

## Configuration

`%APPDATA%\gh-actions\config.json` on Windows,
`~/.config/gh-actions/config.json` on Linux:

```json
{
  "org": "your-org-or-username",
  "repos": ["repo-one", "repo-two"],
  "active_poll_seconds": 15,
  "idle_poll_seconds": 60,
  "runs_per_repo": 20
}
```

Picked up on the next poll — no restart. Polling is fast only while something
is live. Every request is conditional (`If-None-Match`), and a 304 doesn't
count against the 5000/hour limit, which is what makes a 15-second interval
across several repos affordable.

**Token**, in order: `token` in the config, then `GH_TOKEN` / `GITHUB_TOKEN`,
then `gh auth token`. The gh CLI is the nicest source where it's installed
because it refreshes itself, so the explicit sources come first and it is the
fallback. A fine-grained PAT needs **Actions: Read-only** plus Metadata, with
the resource owner set to the org for org repos — note that fine-grained tokens
return **404, not 403**, for a permission they lack.

## Implementation notes

- The tray icon is drawn at runtime: the notification area takes a bitmap, not
  text, so that's how the job count gets on screen.
- `GetHicon` returns a raw GDI handle that `Icon` doesn't own, so it's destroyed
  after cloning — otherwise it leaks a handle per poll.
- The panel is placed with `SetWindowPos` in physical pixels rather than WPF's
  `Left`/`Top`, which are DIPs whose meaning shifts under per-monitor DPI.
