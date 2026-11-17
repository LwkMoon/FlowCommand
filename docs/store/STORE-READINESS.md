# Microsoft Store readiness audit

Status date: 15 November 2026. Policy source: Microsoft Store Policies version 7.20 (published 15 September 2026, effective 22 October 2026).

## Verdict

**Not ready to submit today, but the remaining work is now verification and account setup, not engineering.**
The product was already well built (strict action allowlist, DPAPI key storage, 278 passing unit tests (now 356)). The audit found five things
that would have caused a Store rejection or a broken Store install, and all five now have an implementation. None of the Windows-side
changes could be compiled or run in the environment used to make them (no Windows, no NuGet access), so the first job is to build and
test on a Windows machine. That is step 1 of [Next steps](#next-steps-for-the-developer).

### Status vocabulary used everywhere in these docs

| Label | Meaning |
|---|---|
| **Verified here** | Executed in the authoring environment, with the result recorded |
| **Implemented, not verified** | Code or files written, but never compiled or run on Windows |
| **Manual test** | Needs a person on a Windows PC |
| **Developer input** | Needs a decision or information only the developer has |
| **Partner Center** | Can only be done in the Microsoft Partner Center website |

## What was already good (kept as is)

- One-request-one-action pipeline; the AI (or any provider) only selects from a fixed catalogue and its output is strictly validated, then risk-checked locally.
- No shell, script, registry or arbitrary-file actions. URLs and paths come from configuration, never from the AI.
- API keys encrypted with DPAPI for the current user, not logged, not in `config.json`.
- No telemetry, ads, accounts or update check. No microphone code (a test enforces it).
- Runs as a normal user (`asInvoker`), single instance, clean exit (explicit shutdown, hook and hotkey released).
- Dry-run mode, confirmations, protected processes, 278 unit tests, CI build.
- Both production projects have **zero NuGet package references**. The dependency surface is the .NET runtime itself.

## Findings and what was done

| # | Finding | Priority | What was done | Status |
|---|---|---|---|---|
| 1 | Primary function needed a third-party API key. Store policy 10.8.3 lists "API secret keys" as financial information an individual account may not require for primary functionality; policy 10.1.2 wants a fully functional product and 10.3 a testable one (testers have no key); 10.1.1 wants the value to be clear on first run. | Critical | New **offline interpreter** (`LocalRuleProvider`) is the default. Online Gemini or Claude is an optional upgrade. Same validation and risk policy apply to its output. | Core logic **verified here** (78 new tests, 61 of them for the interpreter; 356 total pass). Settings and welcome UI **implemented, not verified** |
| 2 | "Start with Windows" wrote the HKCU Run key with the exe path. Under MSIX that registry write is virtualised and the path changes on every update, so the startup entry would silently not work. | Critical | Packaged installs use a declared **Windows startup task** (user-controlled, disabled by default). Zip installs keep the Run key. | **Implemented, not verified** |
| 3 | Settings, keys and logs were written under `%APPDATA%` and `%LOCALAPPDATA%`. A packaged app's writes there are redirected, so "Open config folder" would show an empty folder and uninstall would leave confusing state. | Critical | Packaged installs use the package's own `LocalState` folder via `ApplicationData`; Settings shows the exact path. | **Implemented, not verified** |
| 4 | No Store-compatible package existed (only zips). | Critical | MSIX manifest template, tile assets, `New-MsixPackage.ps1`, `Install-DevMsix.ps1`, CI job, and a manual `store-package` workflow. See [PACKAGING.md](PACKAGING.md). | Script parses and its input checks ran (**verified here**); packaging itself **not verified** (needs Windows SDK) |
| 5 | Policy 11.16 (live generative AI): disclose in metadata, note it in Partner Center, and give users a way to report inappropriate output. | Critical | Disclosure written into the [listing draft](LISTING.md); tray and Settings now have **Report a problem or get support**. | Code **implemented, not verified**. Partner Center checkbox is **Partner Center** |
| 6 | Policy 10.5.1: a privacy policy is mandatory for Win32 and desktop bridge products. | Critical | [Privacy policy draft and data inventory](../PRIVACY.md), with a test that keeps it in step with the code. | Draft **verified here** against code. Publishing it is **Developer input** |
| 7 | Single-file framework-dependent publishing assumes a .NET runtime that clean PCs do not have, and MSIX cannot declare the .NET desktop runtime as a dependency. | High | MSIX build is **self-contained, x64, not single-file** (single-file would extract native libraries to a temp folder at run time). ReadyToRun on by default for startup speed. | **Implemented, not verified** (size and start-up time unmeasured) |
| 8 | Launching a second copy showed an error box. A Store user who clicks the tile while it sits in the tray would think the app is broken. | High | Second launch now asks the running copy to show its window and exits quietly. | **Implemented, not verified** |
| 9 | No first-run experience. | High | Welcome window: what it does, how to use it, offline or online choice, trigger choice, optional start-with-Windows. | **Implemented, not verified** |
| 10 | No way to switch the keyboard listener off without exiting. | High | Tray item **Pause listening for the trigger** removes the hook and the hotkey. | **Implemented, not verified** |
| 11 | No DPI awareness declaration. | Medium | `PerMonitorV2` added to `app.manifest`. | **Implemented, not verified** (test at 100, 150, 200 percent) |
| 12 | The exe had no icon and the tray icon was a drawn placeholder. | Medium | One icon design drives the `.ico`, tray icon, MSIX tiles and Store logo (`packaging/tools/make_assets.py`). | Images **verified here** by eye; embedding **not verified** |
| 13 | No accessibility names on key Settings controls. | Medium | `AutomationProperties` names and headings added to Settings and Welcome. | **Implemented, not verified**. Screen reader test is **Manual test** |
| 14 | No in-app way to remove stored data. | Medium | Settings, **Delete all my FlowCommand data**. | **Implemented, not verified** |
| 15 | Unobserved task and non-UI thread exceptions were not logged. | Medium | Both now logged (no key or text in the message). | **Implemented, not verified** |
| 16 | NU1510 for `System.Security.Cryptography.ProtectedData`. | Medium | No package reference to it exists in any project now. The restore data shows the package is part of the .NET 10 framework (it is on the pruning list), and the existing published build compiled without it. Nothing needed removing. | Reasoned from files; **Manual test**: build on Windows and confirm no NU1510 |
| 17 | The README, project description and in-app text called Wispr Flow a requirement. FlowCommand has **no integration** with it: the palette is a text box, and anything that types works. | Medium | Wording corrected everywhere; Wispr Flow is described as an optional companion, unaffiliated. A read-only "is it running" hint is shown, never an error. | **Verified here** (text). Hint code **not verified** |
| 18 | Em-dashes and a few stale statements in existing docs. | Low | Cleaned in the files touched. | **Verified here** |

## Decisions that differ from the original brief

The brief asked for several things that the code or the policies make wrong or unnecessary. These were changed on purpose.

| The brief said | What was done instead | Why |
|---|---|---|
| Detect Wispr Flow, handle it "not running", avoid duplicate execution | A passive hint only | There is no integration to break. Making Wispr Flow a gate would add a failure mode and a dependency disclosure for no benefit |
| Prominently disclose a Wispr Flow dependency | Disclose the real dependency (online mode needs the user's own API key) and describe Wispr Flow as optional | Policy 10.2.4 requires disclosure of dependencies that deliver *primary* functionality. Offline mode removes that dependency entirely |
| Put Wispr Flow in the search terms | Not done | Policy 10.1.3 forbids other products' titles in search terms unless you publish them. A test enforces it |
| Replace the low-level keyboard hook with `RegisterHotKey` if possible | Hook kept, plus pause, plus a first-run choice to use a hotkey only | `RegisterHotKey` cannot detect a single key pressed twice, which is the signature interaction. The hotkey alternative already existed |
| Use Windows Credential Manager | DPAPI kept | Already correct (per-user, encrypted, never logged). Switching would add risk and no benefit |
| Single-file framework-dependent build | Self-contained folder inside MSIX | See finding 7 |
| Four real screenshots | A shot list, not fake images | Policy 10.1 requires accuracy, and no Windows desktop was available |
| Windows App Certification Kit in CI | Documented as a manual local step | WACK needs an installed package, administrator rights and a desktop session |
| ARM64 | x64 only | Cannot be built or tested here. x64 apps run under emulation on Windows on ARM (untested) |

## Store policy checklist (v7.20)

| Policy | Requirement in short | How it is met | Status |
|---|---|---|---|
| 10.1.1 | Accurate metadata, unique name, clear value on first run, no false relationship to other products | Listing draft; Welcome window; "not affiliated" wording | Draft **verified here**; **Developer input** on name |
| 10.1.3 | At most 7 search terms, none naming others' products | 7 terms, no third-party names; test enforces | **Verified here** |
| 10.2.4 | Disclose dependencies at the start of the description | First lines of the description | Draft **verified here** |
| 10.2.7 | Clean uninstall, clearly communicated | MSIX uninstall, data in package folder, Delete-my-data button, README | **Manual test** |
| 10.2.8 | Consent and supported methods when changing Windows settings | Every action is user-initiated and individually switchable; Bluetooth uses the documented Radio API; accessibility API is used read-only to classify focus. Explained in certification notes | **Developer input** (reviewer judgment) |
| 10.2.9 | (Only for the EXE/MSI route) signed installer, silent install, stable URL | Not used; MSIX chosen | n/a |
| 10.3 | Testable without special accounts | Offline mode needs no account | Design **verified here**; **Manual test** |
| 10.4.1, 10.4.2 | Detect incompatible OS, stay responsive, handle exceptions | Min OS in manifest; global exception handlers | **Manual test** |
| 10.5.1, 10.5.4 | Privacy policy URL, modern cryptography | [PRIVACY.md](../PRIVACY.md); DPAPI | **Developer input** (publish URL) |
| 10.6 | Capabilities must relate to function | `runFullTrust`, `internetClient`, `radios`, each justified in the manifest. No microphone or camera | **Verified here** (test) |
| 10.8.3 | Individual accounts cannot require financial information, which includes API secret keys, for primary function | API key is optional; offline mode is complete | **Developer input**: see risk below |
| 10.14 | Account type must fit the publisher name | Individual account with a personal publisher name | **Developer input** |
| 11.2 | Third-party names and logos only with permission | No Wispr, Google, Anthropic or Microsoft logos. Names appear only in plain text to state compatibility | **Verified here** |
| 11.11 | Honest IARC age rating | Guidance in the listing draft | **Partner Center** |
| 11.16 | Live generative AI: disclose, flag in Partner Center, provide reporting | Disclosure text; in-app report link | **Partner Center** for the flag |

### Residual risks to know about

1. **10.8.3 interpretation.** A strict reviewer could say an app that *offers* API-key entry is covered. The mitigation is that offline mode is a complete, key-free product. If a reviewer disagrees anyway, the fallback is a Company developer account (requires business verification), or removing the online provider from the Store build.
2. **10.2.8 interpretation.** Bluetooth, lock and volume actions change Windows state. They are triggered only by the person's own command and can each be disabled. Expect possible reviewer questions; the certification notes pre-answer them.
3. **Low-level keyboard hook.** Some security products flag hooks. Mitigations: pause, hotkey-only option, source is open, disclosure in the listing and policy.
4. **Name similarity.** "FlowCommand" shares the word "Flow" with "Wispr Flow". The description states there is no affiliation. Whether that is enough is a developer decision (see owner decisions).
5. **Individual accounts in Pakistan.** Microsoft's current page says individual accounts are free and identity verification is by ID and selfie in all supported countries. Whether Pakistan appears in the supported list could not be confirmed from the pages retrieved, so check at registration.

## What could not be done in the authoring environment

| Not possible here | Consequence |
|---|---|
| Compile the WPF project (needs the Windows Desktop targeting pack from NuGet, which was unreachable) | Every Windows-side edit is unproven until `dotnet build` runs on Windows |
| Run real xunit (needs NuGet) | The existing 278 tests plus 64 new ones were run through a small stand-in runner compiled against the real `FlowCommand.Core`. Results are genuine for the Core logic but are not an xunit run |
| Run `makeappx`, `makepri`, `signtool`, WACK | The package has never been built |
| Run Windows, Wispr Flow, or any UI | No screenshots, no behavior tests of hooks, tray, startup task, DPI |
| Reach Partner Center | No identity values, no submission |

## Owner decisions

1. **Name.** Reserve "FlowCommand" in Partner Center. If taken or if you want less similarity to "Wispr Flow", pick another name before building the Store package.
2. **Account type.** Individual (free, simplest) or Company (removes the 10.8.3 doubt, needs business verification).
3. **Publisher display name and support contact.** Needed for the listing and privacy policy.
4. **Where to host the privacy policy.** The default points at the repo's `docs/PRIVACY.md` on GitHub; a page on your own site is more conventional.
5. **Whether online AI ships in the first Store version.** Safest first submission: keep it (it is optional), but be ready to cut it if certification objects.
6. **Existing zip users.** Store installs use a separate data folder. Decide whether to document a manual copy of `config.json` (the docs currently do) or build an import button later.

## Next steps for the developer

See the end of [PACKAGING.md](PACKAGING.md#developer-checklist-in-order) for the ordered, click-by-click list, and
[../TESTING.md](../TESTING.md) for the test plan and the table to record results.
