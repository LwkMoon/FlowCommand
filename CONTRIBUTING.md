# Contributing to FlowCommand

Thanks for taking an interest. Bug reports, documentation fixes, new built-in apps and sites, and new actions are all welcome.

## Before you start

- **Bugs:** open an issue using the bug template. Include your Windows version, FlowCommand version, provider/model and
  the steps to reproduce. Redact logs first; never post API keys, personal paths or private transcripts.
- **Features and new actions:** open an issue first so the scope and risk level can be agreed. FlowCommand deliberately
  has a small, explicit set of actions; anything that can change or delete data, or reach the network, needs discussion.
- **Security problems:** don't open a public issue. See [SECURITY.md](SECURITY.md).

## Development setup

Requirements: Windows 10 1809 or newer, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and optionally
Wispr Flow for end-to-end testing.

```powershell
git clone https://github.com/LwkMoon/FlowCommand.git
cd FlowCommand
dotnet restore FlowCommand.slnx
dotnet build FlowCommand.slnx -c Release
dotnet test tests/FlowCommand.Tests/FlowCommand.Tests.csproj -c Release
```

`FlowCommand.Core` and the tests target plain `net10.0`, so the unit tests run on any OS. The WPF app targets Windows only.
If PowerShell blocks the helper scripts, run them with `powershell -ExecutionPolicy Bypass -File .\scripts\<name>.ps1`.

## Project layout

| Path | Contents |
|---|---|
| `src/FlowCommand.Core` | Action catalogue, validation, risk policy, AI providers, prompt, pipeline, configuration. No Windows APIs. |
| `src/FlowCommand` | WPF palette and settings, tray icon, key listener, focus detection, DPAPI, and every Windows action handler. |
| `tests/FlowCommand.Tests` | xUnit tests. |
| `config.example.json` | Documented example configuration. A test keeps it in sync with the built-in defaults. |

## Adding an action

1. Add an `ActionSpec` to `ActionCatalog.cs` with typed parameters and an honest risk level (`Low`, `Moderate`, `High`).
2. Implement an `IActionHandler` in `src/FlowCommand/Platform/` and register it in `Handlers.cs`.
3. Handlers must read the real system state after acting where Windows allows it, and report `Verified = false` when it
   does not. Never claim success that was not observed.
4. Add the action name to `enabledActions` in `config.example.json`. If existing users should receive it automatically,
   also extend the version migration in `ConfigStore.Normalize` (and bump `AppConfig.CurrentVersion`).
5. Add tests (validation, risk, handler-independent logic) and update the README and `docs/commands.md`.

Rules that keep the safety model intact:

- The AI picks an action and its arguments; it never supplies commands, scripts, executable paths or URLs.
- Paths and URLs come from Windows or from the user's `config.json`.
- High-risk actions must require confirmation and show exactly what will happen.
- No shell, script or arbitrary-file operations.

## Adding apps and websites

Edit `DefaultCatalog.cs` and keep `config.example.json` identical (a test fails if they differ). A name or alias must not
be shared by two apps. Add `processName` only when the app's windows belong to that process (not for Store apps or console
windows), and `pathContains` when two apps share a process name.

## Pull requests

- Keep changes focused; one concern per pull request.
- Make sure `dotnet build` and `dotnet test` pass.
- Describe what you tested on a real Windows machine; the UI-facing code cannot be covered by unit tests.
- By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).
