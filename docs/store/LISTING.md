# Microsoft Store listing draft

Draft text for Partner Center. Limits used here come from Microsoft's current Partner Center documentation: description up to 10,000
characters; short description up to 1,000 but only about 270 are shown, so keep it under that; up to 20 features of 200 characters each;
up to 7 search terms of 30 characters each; "What's new" up to 1,500 characters; additional system requirements up to 200 characters each.
Tests in `PackagingAndPolicyTests` keep this file within those limits.

Items in [square brackets] need the developer.

## Product title

FlowCommand

## Short description

A command palette for Windows. Say or type a short command and FlowCommand opens apps, folders and websites, controls windows and volume, takes screenshots and more. Works offline.

## Description

FlowCommand works on its own. To speak commands you need a separate dictation tool of your choice, such as Wispr Flow (a different product from another company; FlowCommand is not affiliated with it) or Windows voice typing. Typing needs nothing extra. An optional online mode uses live generative AI (Google Gemini or Anthropic Claude) and requires your own API key from that provider.

FlowCommand is a small command palette that lives in the notification area. Double-tap one key (the backtick key by default), say or type a short command, and press Enter. FlowCommand turns the command into one action, checks it against a strict built-in list, and runs it on your PC.

Try things like:
- "Open Chrome", "switch to Visual Studio Code", "close Discord"
- "Open my downloads folder", "open Windows Update settings"
- "Volume up", "pause the music", "mute"
- "Take a screenshot", "show desktop", "lock my computer"
- "Search for cheap flights", "open GitHub"

OFFLINE BY DEFAULT
Out of the box FlowCommand understands common commands using rules on your PC. No account, no key, and nothing is sent anywhere.

OPTIONAL ONLINE AI
For free-form wording, choose Google Gemini or Anthropic Claude in Settings and paste your own API key. Only the text of each command is sent to the provider you chose, never audio. The provider's terms, limits and charges apply. FlowCommand has no ads and no telemetry.

SAFE BY DESIGN
FlowCommand can only do what is on its fixed action list. There is no shell, script or registry access. Risky actions ask first, and a dry-run mode shows what would happen without doing it. You can switch off any action, pause the keyboard listener from the tray icon, and delete all of FlowCommand's data from Settings.

ABOUT THE KEYBOARD LISTENER
To notice a double-tap, FlowCommand watches one key. It does not record, store or send what you type. You can pause it from the tray icon or use a hotkey instead.

FlowCommand is an independent project. It is not affiliated with or endorsed by Microsoft, Wispr, Google or Anthropic.

## Features

- Opens, closes, switches to and resizes applications by name
- Opens folders, project folders, websites and 36 Windows Settings pages
- Volume, mute and media keys, screenshots, show desktop and lock
- Works offline by default: no account, no key, nothing leaves your PC
- Optional online AI with your own Gemini or Claude key for free-form wording
- Fixed action list: no shell, script or registry access
- Confirmation for risky actions and a dry-run mode
- Pause the keyboard listener from the tray icon at any time
- Delete all FlowCommand data from Settings; no ads and no telemetry
- Add your own apps, websites, folders and projects in a simple settings file

## System requirements

Minimum:
- Windows 10 version 1809 or newer, 64-bit (x64)

Recommended:
- Windows 11

Additional:
- Optional: a dictation tool such as Wispr Flow or Windows voice typing to speak commands
- Optional: your own Google Gemini or Anthropic API key for online AI
- Optional: internet connection (online AI mode only)

> The supported-version statement must only be published for versions that have actually been tested. Record them in `docs/TESTING.md` first.

## Category and subcategory

Productivity (primary). [Utilities and tools if Partner Center offers it as a better fit.]

## Search terms

<!-- search-terms:start -->
- voice commands
- command palette
- app launcher
- dictation commands
- voice control
- productivity utility
- open apps by voice
<!-- search-terms:end -->

No other company's product name appears in the search terms (policy 10.1.3).

## Links

| Field | Value |
|---|---|
| Privacy policy URL | [CONFIRM: public HTTPS address of PRIVACY.md. Default: https://github.com/LwkMoon/FlowCommand/blob/main/docs/PRIVACY.md] |
| Website | https://github.com/LwkMoon/FlowCommand |
| Support contact | [CONFIRM: https://github.com/LwkMoon/FlowCommand/issues or an email address] |
| Copyright and trademark info | Copyright (c) 2026 LwkMoon. MIT licence. |
| Developed by | [CONFIRM: publisher name as in Partner Center] |

## What's new in this version (initial release notes)

First Store release.
- Offline command understanding out of the box: no account or key needed
- Optional online AI with your own Gemini or Claude key
- Welcome screen, tray menu with pause, and one-click deletion of all FlowCommand data
- Open and control apps and windows, folders, websites, Settings pages, volume, screenshots and lock
- Optional start with Windows, off by default

## Age rating guidance (IARC questionnaire in Partner Center)

Answer honestly from the app's real behavior. Expected answers:

| Topic | Answer |
|---|---|
| Violence, blood, fear, sexual content, nudity, profanity, drugs, alcohol, tobacco, gambling | None |
| In-app purchases or real-money transactions | None (free, no purchases) |
| User-to-user communication or user-generated content shared with others | None |
| Shares the user's location with other users | No |
| Unrestricted internet or web browsing inside the app | No (it opens the system browser when asked) |
| Generative AI content | Optional online mode returns structured actions, not free-form content. If the questionnaire has an AI question, answer yes for optional live generative AI |

The resulting rating is decided by the questionnaire, so do not assume it. Re-answer it if the app gains features.

## Live generative AI (policy 11.16)

- Metadata disclosure: the description above states it in the first paragraph and in "Optional online AI".
- Partner Center: tick the live generative AI declaration during submission.
- Reporting: Settings and the tray menu include **Report a problem or get support**, which opens the support address.

## Certification notes (paste into Partner Center)

> No sign-in is required. Test steps: 1) Launch FlowCommand. A welcome screen appears; choose "Offline" and press "Get started". 2) Right-click the tray icon (notification area, it may be under the ^ arrow) and choose "Open command palette", or double-tap the backtick key (left of the 1 key) while no text box is focused. 3) Type "open calculator" and press Enter: Calculator opens. Try "volume up", "take a screenshot", "show desktop". 4) Tray, "Pause listening for the trigger" stops the keyboard listener. 5) Settings, "Delete all my FlowCommand data" resets the app.
>
> runFullTrust: FlowCommand is a classic .NET desktop (WPF) application. It needs a system-wide keyboard listener to notice a double-tap of one key and must launch and close other desktop programs, which cannot be done from an AppContainer. The listener only checks whether a key is the trigger key; it stores and transmits nothing, can be paused from the tray, and a hotkey-only mode is available.
>
> radios (device capability): used only by the optional "turn Bluetooth on or off" action, through the Windows Radio API. No microphone or camera capability is declared.
>
> Windows settings and policy 10.2.8: every action runs only when the user gives that command. Each action can be switched off in Settings. Bluetooth uses the documented Windows.Devices.Radios API. UI Automation is used read-only to detect whether a text box has focus, never to change settings.
>
> Live generative AI (policy 11.16): the optional online mode (off by default, requires the user's own Google Gemini or Anthropic API key) sends the text of a command to that provider. Its reply is a structured action that is validated against a fixed list; it cannot run arbitrary commands. Users can report problems via Settings and the tray menu. The offline mode needs no key, so no test account or key is required.
>
> The app has no ads, no telemetry and no accounts. Privacy policy: [URL].

## Screenshots

Requirements: PNG, at least 1366x768 (3840x2160 preferred), at least one required and four recommended, no personal information, real
screens of the running app. **None are included in the repository**: no Windows desktop was available to capture them, and fabricated
screens would breach policy 10.1. Capture these from the packaged build on a clean desktop at 100 or 150 percent scaling:

1. Welcome screen (shows the value, the offline or online choice and the trigger)
2. Command palette after typing "open downloads folder" with a success result
3. Command palette showing a confirmation for a moderate-risk action (for example "close Notepad" with Notepad open)
4. Settings top section: provider choice and trigger
5. Settings, About, privacy and your data section (shows delete-all-data)
6. Tray menu open, showing Pause, Dry-run, Settings and Privacy policy
7. Palette showing the dry-run indicator with a command that would change something

Hide any real API key, user name or private file names before capturing.

## Images

| Asset | Status |
|---|---|
| Store logo, 300x300 PNG | `packaging/store-listing/StoreListingIcon-300x300.png` (generated; reviewed by eye) |
| Poster art, super hero art, promotional images | Not required for an app. Add later if desired |
| In-package tile assets | `packaging/Assets` (indexed into `resources.pri` by the build) |
