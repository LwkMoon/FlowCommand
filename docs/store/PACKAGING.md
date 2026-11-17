# Packaging and deployment

Everything here is **implemented, not verified** unless it says otherwise: the package has never been built, because building needs
Windows and the Windows SDK. The PowerShell scripts were parse-checked and their input guards were run (**verified here**).

## Which route: MSIX or EXE/MSI listing

| | **MSIX package (chosen)** | EXE or MSI download listing |
|---|---|---|
| Who signs | **Microsoft signs it for free** after certification | You must buy a code-signing certificate from a trusted CA and sign the installer **and every PE file** in it (policy 10.2.9) |
| Updates | Store delivers them; you submit a higher version | You host a new versioned HTTPS URL for every release and update it in Partner Center |
| Install and uninstall | Clean, transactional, no installer to write | You write and maintain a silent standalone installer and uninstaller |
| SmartScreen | Not an issue for Store installs | Depends on your certificate's reputation |
| Fit for FlowCommand | Everything the app does works in a full-trust MSIX (see below) | Also works, but costs money and effort with no user benefit |

**Decision: MSIX.** No part of FlowCommand needed the unpackaged route. The zip on GitHub stays as a separate, optional distribution.

### What full-trust MSIX changes for this app

| Area | Behavior in MSIX | What was done |
|---|---|---|
| Global keyboard hook, hotkey, tray icon, hidden background process | Work unchanged in a `runFullTrust` desktop app | Nothing. Pause item added |
| Writing to the install folder | Not allowed (read-only) | The app never wrote there |
| `%APPDATA%` and HKCU writes | Redirected into a private per-package copy | Data now uses the package `LocalState` folder explicitly; startup uses a startup task |
| Start with Windows | The Run key would not work | `windows.startupTask` extension plus `StartupTask` API |
| Launch programs, open settings pages, `netstat`, window control | Unchanged for a full-trust process | Nothing |
| Bluetooth (Windows Radio API) | Packaged apps must declare the capability | `DeviceCapability radios` |
| Network to AI providers | Full-trust is not sandboxed | `internetClient` declared for transparency |
| DPAPI | Works, per Windows user | Nothing |
| Single instance | Named kernel objects work across launches | Second launch now signals the first |
| Update | Windows replaces the app folder; `LocalState` is kept | Test it (see below) |
| Uninstall | Windows removes the app and its `LocalState` | Documented in README and policy |
| Windows older than 10.0.17763 | Not installable | `MinVersion` in the manifest |

No microphone, camera, location or broad file capability is declared, and a test fails if one appears.

## .NET deployment

| Option | Verdict |
|---|---|
| Framework-dependent single file (the old zip) | Fine for developers, wrong for the Store: MSIX has no way to depend on the .NET desktop runtime, so a clean PC would fail to start |
| **Self-contained, not single-file (chosen for MSIX)** | Works on a clean Windows install. MSIX compresses the folder, so the download is smaller than the on-disk size |
| Self-contained single file | Rejected for MSIX: extracts native libraries to a temp folder on every start and bypasses package integrity |
| Trimming | **Not used.** WPF is not trim-safe; the saving is not worth the crash risk |
| ReadyToRun | **On** for faster start. Pass `-NoReadyToRun` to the script if it ever causes trouble |
| ARM64 | Not built. x64 apps are expected to run under emulation on Windows on ARM, but that is **untested** |

Measure the real package size and cold start time on your machine and record them in [TESTING.md](../TESTING.md).

## Building

Prerequisites on Windows: .NET 10 SDK and the Windows 10/11 SDK (provides `makeappx`, `makepri`, `signtool`).

```powershell
# Development build: throw-away identity, signed with a self-signed certificate, for testing on your own PC
.\scripts\New-MsixPackage.ps1

# Install it (administrator PowerShell, trusts the test certificate once)
.\scripts\Install-DevMsix.ps1

# Store build: unsigned, with the identity Partner Center gave you
.\scripts\New-MsixPackage.ps1 -Target store `
    -IdentityName "<Package/Identity/Name>" `
    -Publisher "<Package/Identity/Publisher>" `
    -PublisherDisplayName "<Package/Properties/PublisherDisplayName>"
```

The same Store build is available without a local SDK as the manual **store-package** workflow (GitHub, Actions tab, Run workflow). The
`build` workflow also produces a development MSIX on every push and checks its contents.

### Versioning

The version comes from `Directory.Build.props` (`1.0.0`). The package version is that plus `.0` (`1.0.0.0`) because the Store reserves the
fourth part. **Every submission must have a higher version than the last one**, including after a rejection that you re-submit as a new package.

### Identity placeholders (cannot be invented)

| Manifest value | Where to get it | Placeholder in the repo |
|---|---|---|
| `Identity Name` | Partner Center, your app, Product management, Product identity | none: the script refuses to build a Store package without it |
| `Identity Publisher` | Same page (a string beginning `CN=`) | same |
| `PublisherDisplayName` | Same page | same |

The development build uses `LwkMoon.FlowCommand.Dev` and `CN=FlowCommand Dev`, which can never be uploaded to the Store, so a dev build
can sit on your PC next to the Store version.

### Signing

- **Development:** self-signed certificate created by the script, exported as `FlowCommand-dev.cer`. Trusted only on machines where you import it.
- **Store:** upload the **unsigned** `.msix`. Microsoft signs it after certification. Do not sign a Store package yourself.

## Verifying a package

1. `scripts\New-MsixPackage.ps1` finishes without errors and prints size and SHA-256.
2. CI step **Verify package contents** unpacks it and checks the manifest, `resources.pri`, the runtime and the logo.
3. Install the dev package. Start it from the Start menu. Run the **Packaged install** section of [TESTING.md](../TESTING.md).
4. Windows App Certification Kit, from an administrator prompt on the test PC:
   ```powershell
   & "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" reset
   & "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" test -appxpackagepath "artifacts\msix\FlowCommand_1.0.0.0_x64_dev.msix" -reportoutputpath "$env:USERPROFILE\Desktop\wack.xml"
   ```
   Fix every failure it reports (the dev package's self-signed publisher may produce signing-related notes that do not apply to the Store build; read each one).
5. **Upgrade test:** build version 1.0.1 with the **same** dev identity and certificate, `Add-AppxPackage` over the installed one, and confirm settings, saved key and the startup choice survived.
6. **Removal test:** uninstall from Windows Settings, then confirm the `LocalState` folder under `%LOCALAPPDATA%\Packages\` for FlowCommand is gone.

## If you ever need the EXE/MSI route

Policy 10.2.9 requires: an `.msi` or `.exe` standalone installer (not a web stub); the installer and **all** PE files inside signed by a CA in the
Microsoft Trusted Root Program; a stable, versioned HTTPS URL whose file never changes after submission; a new URL for every update; silent install
(a UAC prompt is allowed); PC only. FlowCommand would additionally need an installer project (WiX or similar) and a startup-entry change back to the Run key.

## Developer checklist, in order

1. **Windows PC, build and test the code.** Install the .NET 10 SDK and the Windows 11 SDK. Clone the repo.
   `dotnet build FlowCommand.slnx -c Release` then `dotnet test tests/FlowCommand.Tests/FlowCommand.Tests.csproj -c Release`.
   Because the Windows-side edits were never compiled, expect to fix a few small compile errors. Confirm there is **no NU1510 warning**.
2. **Run it unpackaged** (`dotnet run --project src/FlowCommand`) and work through the first part of [TESTING.md](../TESTING.md): welcome screen, offline commands, tray menu, pause, second launch.
3. **Build and install the dev MSIX** (commands above). Work through the packaged section of the test plan. Run WACK. Do the upgrade and removal tests.
4. **Take screenshots** (shot list in [LISTING.md](LISTING.md#screenshots)) from the real running app on a clean desktop.
5. **Publish the privacy policy** at a public HTTPS address. Edit `docs/PRIVACY.md` placeholders first. If the address is not the GitHub one, update `AppLinks.cs`.
6. **Create the developer account.** Go to <https://developer.microsoft.com/store/register>, sign in with a Microsoft account, choose the **Windows and Xbox** program, choose **Individual**, and complete identity verification (government ID and a selfie). Check that your country is supported when you pick it.
7. **Reserve the name.** Partner Center, **Apps and games**, **New product**, **MSIX or PWA app**, type the name, reserve it. If it is taken, choose another and update the listing text.
8. **Copy the identity.** Open the new app, **Product management**, **Product identity**. Copy the three values.
9. **Build the Store package.** GitHub, **Actions**, **store-package**, **Run workflow**, paste the three values, run. Download the artifact (`flowcommand-msix-store`). Labels in Partner Center change over time, so match by meaning if a name differs.
10. **Start a submission.** In the app: **Pricing and availability** (free, pick markets), **Properties** (category Productivity; add the privacy policy URL and support contact; tick that the app uses generative AI if prompted, and describe it as in LISTING.md), **Age ratings** (answer the questionnaire using the guidance in LISTING.md), **Packages** (upload the `.msix`), **Store listings** (paste text from LISTING.md, upload logo and screenshots), **Submission options** (paste the certification notes; Partner Center will ask you to justify the `runFullTrust` restricted capability, and LISTING.md has the wording).
11. **Submit.** Watch email and the Partner Center status. If certification fails, the report names the policy number; fix, **raise the version**, rebuild, resubmit.
12. **After approval:** install it from the Store on a clean PC and rerun the quick checks in TESTING.md. Then tag a GitHub release for the zip build if you still want one, and update the README Install table.
