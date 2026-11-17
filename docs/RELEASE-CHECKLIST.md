# Release checklist

Work top to bottom. Stop at the first failure.

## Before building

- [ ] `CHANGELOG.md`: move "Unreleased" to the new version with the date.
- [ ] Version raised in `Directory.Build.props` (three parts). Store versions must always increase.
- [ ] `config.example.json` matches the defaults (a test checks this) and `configVersion` is current.
- [ ] `docs/PRIVACY.md` still matches what the code does; effective date updated if it changed. Publish the new text.
- [ ] `THIRD-PARTY-NOTICES.md` still correct: `dotnet list <project> package --include-transitive` for each project.
- [ ] `docs/store/LISTING.md` description, features and release notes updated.

## Automated checks

- [ ] Clean clone. `dotnet restore`, `dotnet build FlowCommand.slnx -c Release`: no errors, **no warnings**.
- [ ] `dotnet test tests/FlowCommand.Tests/FlowCommand.Tests.csproj -c Release`: all pass.
- [ ] `dotnet list <project> package --vulnerable --include-transitive` for all three projects: nothing found.
- [ ] GitHub Actions `build` workflow is green on the commit to be released.

## Manual checks

- [ ] [TESTING.md](TESTING.md) sections A and B on the release build. Results logged.
- [ ] Section C on Windows 11 and Windows 10 clean machines for every version named in the listing.
- [ ] Windows App Certification Kit on the dev package built from the same commit.
- [ ] Upgrade test from the previous released version.
- [ ] Screenshots retaken if the UI changed.

## Store submission

- [ ] Run the **store-package** workflow with the Partner Center identity. Download `flowcommand-msix-store`. Note the SHA-256.
- [ ] Partner Center: new submission, upload the `.msix`, update the listing text, release notes and screenshots, check age rating and generative AI answers, paste certification notes, submit.
- [ ] Do not change the package after submitting. For any fix, raise the version and submit again.

## After approval

- [ ] Install from the Store on a clean PC; quick pass of A1 to A8.
- [ ] Tag the commit (`v<version>`). The `release` workflow builds the zip archives as a **draft** GitHub release: review, then publish.
- [ ] Update the README Install table (Store link once live).
- [ ] Watch Partner Center reviews and the GitHub issues for the first week.
