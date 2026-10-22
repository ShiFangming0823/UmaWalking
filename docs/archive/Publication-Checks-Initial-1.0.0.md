> Historical checks for the initial 1.0.0 build. See the current Publication-Checks.md for the default-path correction.

# UmaWalking 1.0.0 publication checks

Date: 2026-10-22. The runtime was rebuilt with Unity 2022.3.62f3 for Windows x64 Mono.

- Ten existing pure C# logic suites passed, covering scheduling, selection, desktop coverage, spacing, expressions, grounds, drag, footwear, avoidance and population limits.
- Twelve profile-storage assertions passed: legacy import, atomic saving, backups, new-folder restoration, invalid-profile recovery and test isolation.
- The real control panel passed 29 controller, 86 options/data-guard, 977 localized-name and 56 release/update assertions. Update fixtures cover semantic versions, prerelease/draft rejection, same-repository URL checks, skipped-version/all-skip persistence, manual bypass, re-enabling checks, concurrent clicks, network failure, no release, localized controls and update-dialog layouts. Startup changes were injected into a fake service; the maintainer's real Windows startup entry was not changed.
- Two independent normal-mode installations inherited all **51** saved configuration entries without changing any value. Game path, performance, language, Include/Exclude lists and ground settings matched exactly. Update preferences were retained. Both tests loaded a character and attached it to the desktop; the existing user instance was not stopped.
- Final face sampling ran at 30 FPS, 150% size, MSAA and FXAA enabled: three characters (including Sakura Laurel), twelve expressions each, 36 original-versus-fixed comparisons. It preserves opaque depth/stencil rules and expression identity while clamping atlas edges and correcting near-skin DXT backing-color differences. Existing game textures are not modified. Native and enlarged views were reviewed privately; game-resource screenshots are not bundled.
- The normal expression audit passed five models, 245 action-state transitions and twelve preset portraits; atlas-index checks remained valid.
- The public GitHub latest-release endpoint returned HTTP 404 during verification. No available newer public release was asserted. The updater's controlled-fixture tests verify the future update dialog and choices; network/unavailable-release behavior remains nonfatal.
- A fresh Windows ZIP extraction passed both first-launch cases: no data opens a live 1.0.0 panel with a nonfatal no-release status; an existing isolated user profile restores its settings and renders a desktop-attached model. The real Windows startup entry remained unchanged.
- Final archives use **v1.0.0** names, carry SHA-256 checksums, and are checked for ZIP CRC errors. The Windows ZIP omits a live settings file so extracting over an old directory does not overwrite its settings.
- Game data, extracted asset packs, private paths/logs, test fixtures, Unity caches, debug-only build folders and local profiles are excluded. Root Markdown files remain README.md and AGENTS.md; publication filenames are English.

Runtime assembly SHA-256: `18B480215D1DD36FD301A8BA44F61CA6DA2EE4861AE854E5B9FAD404EFE654AD`.

Controller SHA-256: `F7DDC45776AF15556987F729FBA3D6841965049FF55BDAC3963289BDE6BBBD45`.

This is technical verification, not confirmation of the permissions discussed in [License-Status.md](../License-Status.md). See [Updates-and-Settings.md](../Updates-and-Settings.md) for publishing and upgrading. Historical development checks are in [archive](Publication-Checks-Development.md).
