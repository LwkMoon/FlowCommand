# FlowCommand privacy policy and data inventory

> **DRAFT for the developer to review.** Every statement below was written from the source code of this repository
> (see the "Where this comes from" column). Items marked **[CONFIRM]** need the developer's decision. Publish the final
> text at a public HTTPS address and put that address in Partner Center and in `src/FlowCommand/Platform/AppLinks.cs`.
> This is not legal advice and has not been reviewed by a lawyer.

**Effective date:** [CONFIRM: date of first publication]
**Publisher:** [CONFIRM: your publisher name exactly as it appears in Partner Center]
**Contact:** [CONFIRM: a monitored support email address or the GitHub issues page]

## The short version

- FlowCommand has **no ads, no analytics, no telemetry, no accounts and no update check.**
- In the default **offline mode FlowCommand sends nothing over the network.**
- If **you choose** an online AI provider (Google Gemini or Anthropic Claude) and supply **your own API key**, the text of each
  command you give is sent to that provider. Audio is never captured or sent by FlowCommand.
- FlowCommand watches **one key** to notice a double-tap. It does not record, store or send what you type.
- You can delete everything FlowCommand stores in one click (Settings, **Delete all my FlowCommand data**) or by uninstalling.

## What FlowCommand accesses on your PC

| Data | Why | Stored? | Sent anywhere? | Where this comes from |
|---|---|---|---|---|
| The command text you type or that your dictation tool inserts into the palette | To work out what you want | Only in memory (up to four recent commands for ten minutes if "recent context" is on). Written to the log **only** if you turn on `logTranscripts` | Only to your chosen online AI provider, and only in online mode | `CommandPipeline`, `FileLog` |
| Key presses, as Windows delivers them to a keyboard listener | To notice a double-tap of the trigger key | **No** | **No** | `DoubleTapKeyListener.cs` |
| Names of running programs and titles of open windows; which control has keyboard focus (its type and whether it is read-only, never its contents) | To close, switch to or resize an app, and to ignore the double-tap while you are typing in a text box | **No** | **No** | `WindowTools.cs`, `TextFocusDetector.cs` |
| Start Menu shortcuts, registered App Paths, your default browser setting | To launch programs by name | **No** (read when needed) | **No** | `AppResolver.cs`, `DefaultBrowserDetector.cs` |
| Whether a program with "wispr" in its name is running | A hint on the welcome and settings screens | **No** | **No** | `WisprFlowDetector.cs` |
| Windows Bluetooth radio state | Only for the "turn Bluetooth on or off" action | **No** | **No** | `BluetoothActions.cs` |
| Listening TCP ports and the owning program name (from the Windows `netstat` tool) | Only for the "what is using port N" action | **No** | **No** | `PortActions.cs` |

FlowCommand does **not** use the microphone, camera, location, contacts, clipboard, or your files (other than saving a screenshot
you asked for to Pictures\Screenshots and opening folders you ask for).

## What FlowCommand stores

| Item | Contents | Location | Protection |
|---|---|---|---|
| Settings | Your choices, plus the program, folder, project and website lists you configure (including folder paths you type) | Zip install: `%APPDATA%\FlowCommand\config.json`. Store install: the app's package folder, shown in Settings | Normal file permissions of your Windows account |
| API keys (online mode only) | The key you paste | `secrets` folder beside the settings file | Encrypted with Windows DPAPI for your Windows user (`DataProtectionScope.CurrentUser`). Never written to the log or `config.json` |
| Log | Timestamps, action names, results, error types. Dictated text only if `logTranscripts` is on. Rolled over at about 1 MB | Zip install: `%LOCALAPPDATA%\FlowCommand\logs`. Store install: the package folder | Normal file permissions |
| Start with Windows (optional, off by default) | A Windows startup entry | Windows | You can switch it off in FlowCommand or in Windows Settings, Apps, Startup |

Nothing is kept on any server operated by the developer. The developer has no server and receives no data.

## What is sent over the network, and to whom

**Offline mode (default): nothing.**

**Online mode (you must select it and enter your own key).** For each command FlowCommand sends an HTTPS request to the provider you chose:

| Provider | Address | Why |
|---|---|---|
| Google Gemini | `generativelanguage.googleapis.com` | To interpret the command |
| Anthropic Claude | `api.anthropic.com` | To interpret the command |

The request contains: your command text; the names and descriptions of FlowCommand's actions; the **names** (not paths or web addresses)
of the programs, folders, projects and websites you configured; up to four recent commands and results if "recent context" is on;
and your API key, which authenticates the request. It never contains audio, your files, or the contents of your screen.
The provider handles that data under its own terms and privacy policy, and may charge your account. Google states that
content sent through unpaid Gemini API use can be used to improve its products and reviewed by people, so do not send sensitive
information on an unpaid tier. Links: Google Gemini API terms (<https://ai.google.dev/gemini-api/terms>) and Anthropic privacy
policy (<https://www.anthropic.com/legal/privacy>).

**Opening websites and searches.** When you ask FlowCommand to open a website or search the web, FlowCommand hands the address to
your browser. For a search, the words you said are placed in your configured search address (Google by default). Your browser and that
site then handle the request under their own policies.

**Microsoft Store.** If you install from the Microsoft Store, Microsoft processes the download, updates and acquisition statistics under
its own privacy statement. FlowCommand itself contains no code that reports to Microsoft or to the developer.

## Your controls

- **Pause** the keyboard listener from the tray icon at any time. While paused, no keyboard listener or hotkey is active.
- Turn the double-tap off in Settings and use a hotkey or the tray icon instead.
- Switch to **offline mode** in Settings so nothing is ever sent.
- **Remove a saved API key** in Settings, or use an environment variable instead.
- **Delete all my FlowCommand data** in Settings removes settings, saved keys and the log and turns off start-with-Windows.
- **Uninstall.** Store install: Windows Settings, Apps, Installed apps. Windows removes the app's data with it. Zip install: delete
  the program folder and the two data folders listed above.
- `logTranscripts` is off by default; leave it off to keep dictated text out of the log.

## Retention

Everything stays on your PC until you delete it. FlowCommand has no retention period of its own because the developer holds no data.

## Children

FlowCommand is not directed at children. Online mode requires a Google or Anthropic API key, and those providers set their own age
requirements (Google's Gemini API terms require users to be 18 or older).

## Changes to this policy

Changes will be published at the same address with a new effective date, and in the release notes when they affect data handling.

## Where this comes from, and what to confirm

- Every network destination above is enforced by a test (`PackagingAndPolicyTests`): the test fails if the source gains an
  address that this page does not mention, or if the app project gains a package dependency.
- **[CONFIRM]** the publisher name, contact address, effective date and the public address where this page will live.
- **[CONFIRM]** you are comfortable with the statements about Google's and Anthropic's terms; they describe those terms at the time of writing and can change.
- **[CONFIRM]** whether you want a legal review before publishing (recommended if you plan to distribute in the EU or UK, where
  additional notices may apply).
