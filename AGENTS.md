# UmaWalking — contributor and coding-agent guide

This file describes the prepared publication tree. Read README.md, docs/development/Build.md, docs/License-Status.md, and docs/development/Architecture.md before changing code. User instructions take precedence. Continue ordinary authorized implementation and verification without repeatedly requesting routine approval.

## Layout

- `source/Assets/Scripts/DesktopPrototype/`: authored Unity runtime partials, pure geometry, scheduling, selection, expressions, settings, Windows desktop integration.
- `source/Assets/Editor/UmaDesktopBuild.cs`: creates the desktop scene, configures the user-provided startup image, builds Windows x64 Mono, then restores upstream PlayerSettings.
- `source/Assets/Resources/UmaDesktop/`: transparent presentation shader.
- `source/Controller/`: WinForms control panel, localization, model limit, data extraction, diagnostics and restart helper.
- `source/Tests/`: pure C# and opt-in real-player Python integration tests. Player commands beginning with `test` must remain gated by test mode.
- `source/*.ps1`: source application, build and verification helpers.
- `tools/`: portable project setup, build and publication verification entry points.
- `docs/`: current guides; `docs/archive/` contains historical Chinese notes under English filenames. Root README and this guide are the only root Markdown files.
- `licenses/`: preserved third-party license/notice texts. They do not grant a blanket license to upstream code, Unity, or game assets.
- `work/UmaViewer/`: locally acquired, pinned upstream checkout plus applied overlay; ignored by Git and omitted from archives.
- `build/UmaWalking/`: local compiled output; ignored by Git. The root publication folder also contains the tested runtime for local packaging.

Do not check game `dat`, `meta`, `master.mdb`, extracted mini datasets, credentials, user settings, logs, Unity Library/Temp/UserSettings, or raw model/motion payloads into the repository. Avoid committing generated portraits or gameplay captures as test evidence. English filenames are required for new publication files; localized prose and UI strings are allowed.

## Build and test

Use Unity **2022.3.62f3**, Windows x64 and Mono. Windows support and a valid Unity license are required; Web Build Support, Visual Studio and Python are not runtime prerequisites. The controller uses the Windows .NET Framework C# compiler. Python is optional for integration tests; some tests need Pillow.

From repository root in PowerShell:

```powershell
.\tools\Setup-Project.ps1
.\tools\Build-Local.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
.\source\Test-Logic.ps1
```

Setup obtains UmaViewer revision `07f82e9fa08f23c1cda3a9be3045a09372eae8bb` from `https://github.com/katboi01/UmaViewer.git`; it does not download game resources. `-UpstreamRepository` can point at a local Git clone. Preserve the pinned revision until source anchors, duplicate-source repair and regression tests are verified against a replacement. Do not replace an existing unrelated project directory or update the maintainer's running installation implicitly.

For controller self-tests, prepare a disposable fixture containing the catalog, state and settings expected by each test, then run its `--selftest`, `--options-selftest` or `--names-selftest` entry point with `--root`. Do not run extraction/deletion fixtures against real game-data folders. Python tests accept `--build-directory` and `--evidence-directory`; pass both explicitly. Tests can change settings, send local commands and create desktop windows. Back up original settings and restore exact bytes in `finally`; stop only the instance the test started. Avoid running destructive or competing tests concurrently.

Run checks appropriate to the change. Pure logic is sufficient for pure policy edits, but rendering, native input, window layering, ground coordinates, queues and runtime settings require a real Windows player check. Existing assertions are not proof of a new binary until rerun against that binary. Record SHA-256 identity in evidence; never relabel an old report as a test of a new build. GUI DrawToBitmap can omit native combo-box text: also assert selected values and layout bounds.

## Required behavior

- Brand the public project and control-panel caption **UmaWalking**. Existing internal executable/config/data names remain `UmaDesktop*` for compatibility. Renaming them requires updating Unity data-folder lookup, restart identity checks, controller paths, launchers, scripts and manifests together.
- Windows primary-monitor desktop only. Rendered actors are Explorer desktop child windows, never globally topmost. A genuinely covered desktop pauses rendering, movement, scheduling and arrivals; Alt+Tab and FancyZones window-management overlays are exceptions. Never bypass this in a production path.
- Hard maximum: **10** live actors, with ten distinct valid Unity render layers **20–29**. Configured limit: **1–10**, default **6**. Arriving and departing actors occupy slots. Manual and automatic arrivals share the cap.
- Lowering a configured cap is a graceful transition: existing total may temporarily exceed the new cap only while excess actors leave. Compute additional departures from non-retiring actors; counting already-retiring actors again drains survivors. Prefer retaining manual choices, then older arrivals. No new model may use a slot until the previous actor actually releases it.
- Include duplicates are allowed only when the valid selected-name count is positive and smaller than the configured cap. Never use the same character/outfit combination twice, including retiring instances. All/Exclude and Include rosters at or above the cap keep unique names. Empty Include generates no random characters; explicit manual choices may still be requested.
- Queued manual entry is FIFO, respects the cap, and resets the next-entry timer only after successful admission. Raising a cap may release a waiting request. Outfit replacement reuses a slot and preserves instance selection, ordinal and other actors.
- Experimental plane collision avoidance is **OFF by default**. Keep the experimental label in all three UI languages. It applies only to same-surface plane agents, not lines/default floor. Preserve click-special, interaction, drag and portal behavior. Never use the full transparent sprite rectangle as a physical floor footprint.
- The model cap must persist through normal and force restart. Capture pending UI values for forced restart rather than restoring an older saved cap. Localization must not write translated/original strings into NumericUpDown values or other numeric controls.
- A single release-click on opaque model pixels requests a special immediately. A drag freezes only the held actor's exact pose/timers and resumes on release; it can cross grounds. Low-level `WM_MOUSEMOVE` must be forwarded, not swallowed, or the OS cursor can freeze.
- Ground projection positions feet; it must not shear or squash a 3D body. Keep uniform body scaling. Boundary erosion constrains horizontal shoe footprint, not raised shoe screen Y. Preserve stable ground-fit scale; do not reintroduce per-stride snapping or fading along ordinary boundaries.
- Temporary avoidance must not overwrite an exit goal/stage. Entering agents may backtrack outside their portal to clear a queue. Preserve the facing tangent after a short avoidance waypoint is reached.
- Walk/run probability is 3:1, movement episodes 10–24 seconds, per-actor sampled speeds, one-second early special cutoff, and approximately 0.5-second fades. Costume category weights are 50/30/20, renormalized when absent.
- Walk expressions: WaraiA–E group 80%, SleepA/B group 20%; uniform within each group. Run expressions: KiriA–C group 90%, GuruguruA/FightA group 10%. Re-select on state entry. Specials use smiles except default-face IDs 1018, 1040 and 1105. Preserve optional facial/ear tracks and atlas overrides.
- Default ordinary base height is 380px and Hishi Akebono 570px: a **1.5× linear display-size ratio**, before the global size slider and ground perspective. Do not describe it as a measured 1.5× physical volume.
- Retain UI locales zh/ja/en and character-name localization. Keep outfit names Japanese. Support 15/30/60/120 FPS with 30 default and anti-aliasing toggle. Do not expose implementation details unnecessarily in user flows.

## Data safety and diagnostics

The app reads the user's own DMM game data. Current mini extraction includes mini character/motion roots, shader and recursive prerequisites, plus meta and master. Validate actual files and dependencies, not merely index entries or a folder's apparent size. The 18GB sufficiency result applies to the audited version, not all future versions.

Extraction is copy-and-verify, to a separate empty destination outside the source. Never silently delete, repair in place, or redownload game assets. Original deletion requires the existing explicit user dialog after successful independent startup from the extracted dataset; default response stays No. Future additions need a fresh official download/extraction. Do not turn confirmation into an automated Yes branch.

Diagnostics are local text with path/user redaction. Preserve version, binary hashes, settings, ground, actor, queue, pause, hardware and recent-log evidence while avoiding credentials and raw asset payloads. Do not automatically upload reports, create Issues, send messages or publish Releases.

## Publication and provenance

Use English archive/document filenames, relative links, and sanitized defaults. Never include local absolute paths, reports with usernames, runtime user settings, caches, extracted assets or upstream backup sources in a public archive. Keep historical performance measurements labeled with version/hardware; a ten-model functional pass is not a low-end performance guarantee.

The inspected upstream revision has no top-level license. Preserve existing notices and flag unresolved redistribution permissions; do not invent an MIT/Apache/GPL grant for the entire project. Unity and bundled plugins retain separate terms. The startup image was supplied locally by the maintainer; confirm publication rights before public use. The prepared ZIPs are local artifacts, not legal clearance or an instruction to publish.

Before delivery: verify ZIP CRC and manifest hashes, ensure all required Unity runtime siblings are present, scan for excluded game-data and personal files, validate local Markdown links and English filenames, test a clean extracted runtime, and preserve the user's running configuration. GitHub Release does not compile automatically: upload the full runtime ZIP; automated Unity builds need an explicitly configured workflow and licensing setup.

## Public release 1.0.0 additions

Use DesktopProduct.Version as the sole release-number constant. Preserve the per-user profile in LocalApplicationData/UmaWalking across application-directory changes. Root DesktopSettings.json is a compatibility mirror; do not ship or reset a user profile. Explicit test modes must not overwrite the durable profile. Update preferences are separate from game settings. Startup registration is per-user and opt-in; use an injected startup service in tests, never toggle the maintainer's actual Run entry. Update checks are asynchronous, public GitHub stable-release reads; manual checks bypass skip choices, while no branch downloads or executes installers. Keep same-repository URL validation, request limits, timeouts and offline behavior. See docs/Updates-and-Settings.md.

Default path correction: use DesktopProduct.DefaultDataPath for new-user prefill. Repair only missing legacy GameData placeholders; preserve valid datasets and arbitrary custom paths, including temporarily unavailable drives. Never ship an expanded application-directory path or a user profile. See docs/Default-Data-Path.md.

## Version 1.1.0 desktop integration

Keep Host as the Explorer top-level root and ChildHost as the actor parent. Never replace the root used by coverage enumeration with SHELLDLL_DefView. Re-resolve the leaf on every poll and handle destruction without a topmost fallback. DesktopHostMode defaults to Auto; persist and localize its manual overrides. See docs/development/Desktop-Compatibility.md. Full-desktop mode uses a fixed orthographic projection and all four display edges; preserve old drawn grounds and stable avoidance headings. Tests of native visibility do not prove Win11 compositor output.
