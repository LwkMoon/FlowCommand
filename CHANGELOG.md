# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

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
