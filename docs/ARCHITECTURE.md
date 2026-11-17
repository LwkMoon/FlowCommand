# Architecture

FlowCommand is two projects. The split matters: everything that decides *what is allowed* lives in a library with no Windows
dependency and is fully unit tested; everything that touches Windows lives in the app.

```text
text from the palette
   |
   v
IAiProvider  -- LocalRuleProvider (offline, default) | GeminiProvider | AnthropicProvider
   |            returns one JSON plan: action, parameters, requires_confirmation, explanation
   v
PlanValidator   strict: known action, enabled, only known parameter names, types, ranges, allowed values
   |
   v
RiskPolicy      low / moderate / high, confirmation rules, dry-run
   |
   v
IActionHandler  (Windows code: launch, window control, media keys, Bluetooth, ...)
   |
   v
result with an icon: verified, sent but unverified, failed, needs clarification, dry run
```

## Projects

| Project | Target | Contents |
|---|---|---|
| `src/FlowCommand.Core` | `net10.0`, no packages | Action catalogue, validator, risk policy, protected processes, prompt and tool schema, providers, offline interpreter, pipeline, configuration, default catalogue, name matching |
| `src/FlowCommand` | `net10.0-windows10.0.17763.0`, WPF plus WinForms for the tray icon, no packages | Windows, tray, key listener, hotkey, DPAPI store, log, action handlers, package-aware paths and startup |
| `tests/FlowCommand.Tests` | `net10.0` | References Core only. Also scans the repository files (manifests, docs) for drift |

## The trust boundary

An interpreter, whether the offline rules or an online AI, is **untrusted**. It can only name an action from the catalogue with
parameters that pass the validator. It never supplies a URL, a path, a command line or a script: those come from configuration. The
offline interpreter does not get a shortcut around this; its output is serialised to the same JSON and goes through the same validator
(a test asserts this, including a disabled action and an attempted injection through the spoken text).

## Offline interpreter (`LocalRuleProvider`)

Pure function from text to plan. Steps: lower-case, strip punctuation and filler ("please", "can you"), then try rules in a fixed order:
ports, screenshots, lock, Bluetooth, window controls, media keys and volume, search, close, switch, open, then settings pages. "Open X"
resolves X against folders, projects, websites, configured apps (exact name first, then "you said less than the full name", never
"you said more"), category defaults ("my browser"), Settings pages, and finally passes an unknown short name to the app resolver.
Compound requests ("open chrome and then youtube") are refused instead of half-executed. Unrecognised text returns the `unsupported`
meta action with examples. English only.

## Triggers

| Trigger | Mechanism | Notes |
|---|---|---|
| Double-tap one key | `SetWindowsHookEx(WH_KEYBOARD_LL)` in `DoubleTapKeyListener` | Only checks whether an event is the trigger key and uses other physical keys to cancel; never records, stores or blocks. Ignores injected input and any press with a modifier held. A text-box guard uses caret info and UI Automation (control type and read-only flag only) |
| Hotkey | `RegisterHotKey` on a hidden window | Optional; can bypass the text-box guard |
| Tray menu | WinForms `NotifyIcon` | Always available |

Pause removes both the hook and the hotkey. The hook is released on exit.

## Lifecycle

- Single instance through a named mutex. A second launch sets a named event that the first instance waits on, and the first shows Settings.
- Explicit shutdown: the app only ends from the tray **Exit** (or Windows shutdown). Windows, hook, hotkey, wait handle and mutex are released.
- First run (`firstRunCompleted` is false) shows the welcome window. Configs written before config version 3 count as completed.
- Exceptions: UI-thread, non-UI and unobserved-task exceptions are logged without key or text.

## Data and packaging

`AppPaths` and `StartupRegistration` are the only places that know whether the app is packaged (`PackageIdentity.IsPackaged`, which asks
Windows for the current package name).

| | Zip install | MSIX install |
|---|---|---|
| Settings and secrets | `%APPDATA%\FlowCommand` | package `LocalState\config` |
| Log | `%LOCALAPPDATA%\FlowCommand\logs` | package `LocalState\logs` |
| Start with Windows | HKCU Run key (opt-in) | Windows startup task `FlowCommandStartup` (opt-in) |
| Keys | DPAPI, current user, entropy string | same |

The real package path is used on purpose. Windows redirects `%APPDATA%` writes from packaged apps, which would make the path shown to the
user wrong. Details and the full MSIX reasoning are in [store/PACKAGING.md](store/PACKAGING.md).

## Network

Only `FlowCommand.Core/Ai` (the two online providers) and `ServiceHost` (the shared `HttpClient`) touch the network. A test fails if other
source files start using `HttpClient`, sockets or similar, and another fails if a provider address is missing from the privacy policy.

## Extension points

- New action: `ActionCatalog` entry, an `IActionHandler`, registration in `Handlers.cs`, tests. The prompt, tool schema, offline-mode checks and Settings list follow from the catalogue.
- New provider: implement `IAiProvider`, add it to `ProviderCatalog` and `AiProviderFactory`, add its address to the privacy policy.
- New offline phrasing: add a rule to `LocalRuleProvider` and a row in `LocalRuleProviderTests`.
