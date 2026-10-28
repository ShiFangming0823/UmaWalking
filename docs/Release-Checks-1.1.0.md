# UmaWalking 1.1.0 verification

Prepared on 2026-10-28. Tested on Windows 10 build 19045, 3840×2160, 125% scaling. Unity 2022.3.62f3, Windows x64 Mono. Local game data is excluded from the archives.

- All 12 pure logic suites passed, including orthographic geometry and avoidance at 15/30/60/120 FPS.
- Native input: 29 checks; host selection/lifecycle: 27 checks. Synthetic fixtures invoke production callbacks without physical mouse injection or restarting Explorer.
- Real-player orthographic ground: 64 checks; real-model avoidance stability: 26 checks.
- Actual Explorer Root/IconView/Auto parenting and coverage: 28 checks.
- Window-management transient fixtures: 20 checks. These simulate Alt+Tab/FancyZones classes, not physical use of third-party UI.
- Controller: 29 general, 89 options/data-safety, 977 names, 21 desktop-ground and 59 update/startup checks. Startup tests use an injected service, not the user's Run registry entry.
- Upgrade storage: 17 checks. Clean extracted runtime: 17 checks, including ordinary startup and legacy configuration inheritance.
- Package manifest hashes, ZIP CRC, required Unity runtime siblings, Markdown links, English filenames, sanitized defaults and game/personal-data exclusions verified.

The Win11 24H2 compatibility change follows Issue #1's confirmed attachment difference and the reviewed conservative policy. Actual compositor visibility on the reporting 24H2 machine has NOT been verified here. Native visibility and successful presentation are not proof of compositor visibility. Request that system's retest before closing the issue.

Runtime assembly SHA-256: `329BF21A4EECC83FE470560E59FF3B1A0FBF0AFD825C292D1D86B21421677E53`.

Controller SHA-256: `9B6B745F5DCFC83199E5E6A8CC7EC3538C7B1221734D535183C4C84D6BB8DD1E`.

User configuration and the already-running preview instance were preserved. No GitHub Release, tag, commit, push or Issue comment was created by this task.
