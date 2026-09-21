# CLAUDE.md

A GitHub Actions status indicator: a waybar module on Linux and a
notification-area (tray) app on Windows. Both show one aggregate state (green
passing, amber running, red failing) plus a count of live **jobs**, not runs.
README.md covers install, config and waybar setup for users.

## Layout

```
src/GhActions.Core/   net10.0 library: polling, ETags, distillation, aggregation
src/GhActions.Cli/    Linux front end, binary `gh-actions-core` (waybar JSON)
src/GhActions.Tray/   Windows front end, WPF + WinForms NotifyIcon, `gh-actions-tray.exe`
install/              per-user installers (no admin / no waybar config edits)
build.ps1, build.sh   publish into dist/<rid>/
```

There is no solution file; build projects directly.

## Build

- Needs the .NET 10 SDK. Either OS builds both targets (`EnableWindowsTargeting`
  on the tray project), but only Windows can *run* the tray app.
- Quick check: `dotnet build src/GhActions.Tray` / `dotnet build src/GhActions.Cli`.
- Release artifacts: `.\build.ps1` (add `-FrameworkDependent`, `-LinuxToo`) or
  `./build.sh` (`SELF_CONTAINED=false` for small builds).
- There are no tests. After a UI change, run the tray app to check it.

## How it fits together

- `Poller.PollAsync` decides whether a fetch is due (`active_poll_seconds` while
  anything is live, `idle_poll_seconds` otherwise). It fetches each repo's runs
  and then the jobs for the active runs plus the latest completed one. It then
  writes a cache JSON. Front ends call it on a short heartbeat and render
  whatever cache comes back.
- The cache (`Paths.CachePath`) is the contract between core and UI. It sits
  in `XDG_RUNTIME_DIR` on Linux and `%LOCALAPPDATA%\gh-actions` on Windows.
  **`[JsonPropertyName]`s in `Model.cs` are the on-disk format**; do not rename
  them or add a naming policy.
- `GitHubClient` makes conditional GETs with ETags. The ETag store
  (`gh-actions-store.json`) holds *distilled* payloads, so a 304 replays them.
  304s are free against the rate limit, and that is what makes 15s polling
  affordable. It uses one shared `HttpClient`, with the token set per request.
- `Distill` shrinks API responses to the few fields the UI uses. `Vocab.StateOf`
  is the only place that turns GitHub's (status, conclusion) pair into one state
  word: `failure running queued success cancelled skipped unknown`, listed worst
  first in `Vocab.Severity`. `idle` means no data and is added only by the UI.
- `Aggregator`: a run's state is the worst of its jobs' states. The live count is
  jobs in `running`/`queued`, and a run whose jobs are not fetched yet counts as 1.
- Jobs named `platform / name` (reusable workflows) are grouped by the prefix
  (`Format.PlatformOf`).
- Token order: `config.token`, then `GH_TOKEN`/`GITHUB_TOKEN`, then `gh auth token`.
- Config is re-read on every poll, so it applies without a restart. It lives at
  `%APPDATA%\gh-actions\config.json` or `~/.config/gh-actions/config.json`.

## Windows tray specifics

- `App.xaml.cs` holds the tray icon, the 5s `DispatcherTimer`, and a
  single-flight poll guard. Unhandled exceptions go to `crash.log` next to the
  cache and show a balloon tip. The app must not die and take the icon with it.
- `TrayIconRenderer` draws the icon (a state-coloured disc with the count) with
  GDI+. The `GetHicon` handle must be destroyed after cloning, and the previous
  `Icon` disposed only after the tray has taken the new one.
- `PanelWindow` is a chromeless, topmost flyout. It is placed with `SetWindowPos`
  in physical pixels (`Native.cs`), not WPF `Left`/`Top`. It hides on
  `Deactivated`. The 250ms `_hiddenAt` guard stops a tray click from re-opening
  it right after the click itself deactivated it.
- `VmBuilder` rebuilds all view models on each render. Run open/closed state is
  kept in `_expanded` as `ExpandChoice(state, open)`, recorded **only when the
  user clicks** and dropped once the run's state changes. Runs nobody has clicked
  follow their default: open while running or failed, closed when settled green.
- `StateMark` is the dot beside each row. It is drawn in code and turns into a
  spinning gear while the row is `running`.
- Colours are Tokyo Night. They appear in `Palette.cs` (for code and the icon)
  **and** in `App.xaml` resources (for XAML); keep the two in sync. Buttons and
  scrollbars are fully retemplated, since stock WPF chrome clashes with the palette.
- Project gotchas, already explained in the csproj: implicit usings are off
  (`Application` is ambiguous between WPF and WinForms), and
  `InvariantGlobalization` is false (WPF bindings throw without cultures).
  PerMonitorV2 DPI is set in `app.manifest`.

## Linux CLI specifics

- `gh-actions-core tick` (what waybar runs) polls if due, then prints
  `{text, class, tooltip}`. It must always print valid JSON. After a poll it
  sends `SIGRTMIN+8` to waybar (`"signal": 8`) so the bar refreshes immediately.
- The glyph is U+F09B (Font Awesome Brands GitHub logo).

## Conventions

- Comments explain *why*: the constraint or the failure being avoided. Match
  that tone and density.
- Commit messages are short imperative subject lines ("Fix …", "Add …").
