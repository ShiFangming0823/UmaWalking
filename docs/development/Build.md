# Build UmaWalking locally

## Requirements

- Windows x64; Git; PowerShell.
- Unity Editor **2022.3.62f3** with Windows Build Support and an activated license. Unity Hub alone is not the editor.
- The controller build and pure C# logic tests use Windows .NET Framework's `csc.exe`; Visual Studio is not required for this Mono build.
- Python 3 is optional for integration and packaging verification scripts.
- Obtain game data separately for real model/rendering tests. It must not enter source control or release archives.

## Source overlay layout

This is an overlay for the pinned [UmaViewer](https://github.com/katboi01/UmaViewer) revision `07f82e9fa08f23c1cda3a9be3045a09372eae8bb`. The entire upstream project is intentionally not redistributed here. Confirm [license status](../License-Status.md) before publishing a derivative.

From the publication root:

```powershell
.\tools\Setup-Project.ps1
.\source\Test-Logic.ps1
.\tools\Build-Local.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
```

Setup clones into `work/UmaViewer`, verifies the pinned commit and applies `source/Apply-Prototype.ps1`. It never edits the maintainer's original project. Optional `-UpstreamRepository` can point to a local Git repository for an offline clone; the pinned commit must be present. Existing nonmatching checkouts are refused rather than reset or deleted.

Build output defaults to `build/UmaWalking`. Unity generates the desktop scene, builds `UmaDesktop.exe` and its companion files, and the controller compiler creates `UmaDesktopController.exe`. Internal names are retained for runtime and restart compatibility. `DesktopSettings.json` can be copied from the publication's default config when testing a fresh build. Keep every generated runtime sibling together.

The packaged overlay replaces the original machine-specific default paths with publication-relative build paths and the generic DMM data-path default; runtime logic is unchanged. The packaged controller and Unity runtime were rebuilt for public version 1.1.0. This is not a claim of bit-for-bit reproducible output from every machine.

## Tests

`source/Test-Logic.ps1` compiles and runs twelve independent logic suites without Unity or game data. `source/Build-Controller.ps1 -OutputDirectory <empty-test-output>` validates controller compilation. Python integration tests under `source/Tests` accept `--build-directory`; use a disposable runtime copy with a selected local data path. Several tests intentionally adjust settings, inject desktop input, create transient windows, or restart the test process. Do not run them against a user's normal application session.

`tools/Verify-Publication.py <publication-root>` checks manifest hashes, required siblings, default settings and filename/data exclusions. It does not replace real visual desktop tests or certify redistribution rights.

See [Architecture.md](Architecture.md), the root [AGENTS.md](../../AGENTS.md) and the historical [Source-Guide.md](Source-Guide.md) for implementation context. Private diagnostic logs, former fixture snapshots and actual game indexes are not distributed.

Run `source/Test-Profile.ps1` for upgrade storage tests. The controller `--release-selftest --root <disposable-fixture>` covers release comparison, skip behavior, startup service injection and localized update dialogs. Never write a real Startup Run entry in UI fixtures. Set a disposable `UMA_WALKING_PROFILE` for real normal-mode upgrade tests. Use `tools/Set-Version.ps1` before a future build and `tools/Package-Release.py` after staging sanitized defaults and the complete runtime.

Additional native integration fixtures: `source/Test-DesktopHost.ps1` exercises root/leaf host selection and leaf recreation. The real-player `verify_visibility.py` checks actual Explorer Root/IconView/Auto parenting and coverage; `Test-TransientVisibility.ps1` checks window-management exceptions.
