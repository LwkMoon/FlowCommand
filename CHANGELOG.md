# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

Preparation for a Microsoft Store release.

### Added
- **Offline mode** (`provider: "local"`): a rule-based interpreter for common commands. It needs no account or key and sends nothing
  anywhere. New installs start in this mode; online Gemini or Claude remains available with your own key.
- Welcome screen on first run: what FlowCommand does, offline or online, trigger choice, optional start with Windows.
- Tray: **Pause listening for the trigger** (removes the keyboard hook and hotkey), **Privacy policy**, **Report a problem or get support**.
- Settings: About, privacy and data section with **Delete all my FlowCommand data**, log folder, data location.
- MSIX packaging (`packaging/`, `scripts/New-MsixPackage.ps1`, `scripts/Install-DevMsix.ps1`) and CI jobs that build and check it.
- Application icon, tile assets and Store logo from one generated design.
- Documentation: privacy policy and data inventory, Store readiness audit, packaging guide, listing draft, test plan, architecture,
  release checklist, third-party notices.
- Tests for the offline interpreter, first-run migration, package manifest, privacy policy and listing limits.

### Changed
- Wispr Flow is described as an optional companion, not a requirement. It was never integrated; any tool that types text works.
- Default provider for new installs is offline. Existing `config.json` files keep their provider (config version 3 only adds `firstRunCompleted`).
- A second launch now brings the running copy's Settings window forward instead of showing an error.
- Microsoft Store installs keep settings, keys and logs in the package's own folder and use a Windows startup task instead of the Run key.
- Per-monitor DPI awareness; accessibility names on the main Settings controls.
- Dependency audit (`dotnet list package --vulnerable`) added to CI.
- Documentation no longer uses em-dashes.

## [1.0.0] 

First public release.

### Added
- Command palette that receives the text Wispr Flow inserts, interprets it with Google Gemini or Anthropic Claude,
  validates the structured answer locally, and runs one approved Windows action.
- Open the palette by double-tapping the backtick key (configurable: Caps Lock, Scroll Lock or Pause), or with an
  optional global hotkey. The double-tap is ignored while a text box has focus, so normal dictation is untouched.
- Applications: open, close (normal window close), switch to, minimise, maximise and restore.
- A built-in catalogue of 65 common applications and 69 websites, configurable in `config.json`.
- Media keys and volume, screenshots (full screen or region), lock the PC.
- Open a website by name, or search the web, in the default browser or a browser of your choice.
- Open folders (14 built in plus your own), project folders in an editor, and 36 Windows Settings pages.
- Explicit Bluetooth on/off with read-back verification and a Settings fallback.
- Inspect a TCP port, and force-terminate its listener behind a confirmation.
- Local safety model: action allowlist, strict response validation, risk levels, confirmations, protected
  processes, dry-run mode and DPAPI-encrypted API keys.
- First run detects the default Windows browser. Configuration files from earlier builds are migrated automatically.
- Unit tests for validation, policy, providers, configuration, matching and the shipped example configuration.
