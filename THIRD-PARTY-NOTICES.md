# Third-party notices

FlowCommand's own code is released under the [MIT License](LICENSE).

## What ships inside the application

| Component | Used for | License | Notes |
|---|---|---|---|
| .NET 10 runtime and libraries (Microsoft) | Runs the app. Included in the self-contained zip and in the Store package | MIT | <https://github.com/dotnet/runtime> |
| Windows Presentation Foundation and Windows Forms (Microsoft) | Windows, and the tray icon | MIT | <https://github.com/dotnet/wpf>, <https://github.com/dotnet/winforms> |
| Windows SDK .NET projections for WinRT APIs (`Windows.Devices.Radios`, `Windows.Storage`, `Windows.ApplicationModel`) | Bluetooth control, package-aware data folder, startup task | Part of the .NET Windows target framework (`net10.0-windows10.0.17763.0`) | Not a separate NuGet package in this repo |

FlowCommand's two production projects contain **no NuGet package references**, so there are no other bundled libraries. Tests
(`PackagingAndPolicyTests`, `NoMicrophoneCodeTests`) fail if a package reference is added to either production project, which is the prompt to update this file.

When a Store or self-contained build is produced, the .NET runtime files are copied into the package. Their license text is in the
`LICENSE.txt` and `THIRD-PARTY-NOTICES.TXT` files that the .NET publish step places beside the executable.

## Used only to build and test (not shipped)

| Component | License |
|---|---|
| xUnit (`xunit`, `xunit.runner.visualstudio`) | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| Windows 10/11 SDK tools (`makeappx`, `makepri`, `signtool`) | Microsoft SDK license; used by the packaging script, not redistributed |
| Pillow (Python), `packaging/tools/make_assets.py` | HPND (MIT-CMU style) |

## Services FlowCommand can talk to (optional, user-initiated)

These are not bundled code. They are online services that the person chooses to use with their own account.

| Service | When | Terms |
|---|---|---|
| Google Gemini API | Only if the person selects it and supplies their own key | <https://ai.google.dev/gemini-api/terms> |
| Anthropic API | Only if the person selects it and supplies their own key | <https://www.anthropic.com/legal/terms> |

## Names and logos

Wispr, Wispr Flow, Google, Gemini, Anthropic, Claude, Microsoft and Windows are trademarks of their respective owners. They are
named in plain text only, to say what FlowCommand works with. No third-party logo is used. FlowCommand is not affiliated with,
sponsored by or endorsed by any of them.

## Keeping this file correct

Re-check versions and licenses before each release: run `dotnet list package --include-transitive` on each project and compare with the
tables above. The release checklist includes this step.
