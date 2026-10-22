# Publication verification - 2026-10-21

Version: UmaWalking 3.16.0. This records local packaging checks, not public release or legal clearance.

- Ten pure C# logic suites passed: 1,093,654 assertions across scheduling, fleet/filter rules, desktop coverage, spacing, expressions, ground geometry, drag, footwear boundaries, avoidance and population limits.
- The portable controller source compiled successfully. Disposable control-panel fixtures passed 29 controller, 86 options/data-guard, and 977 localized-name assertions. No original game data was deleted.
- Setup cloned the pinned upstream revision from an existing local repository into an independent directory and applied the packaged overlay successfully. This verifies local setup, not future remote availability.
- A freshly extracted runtime archive started with the initial-download game data, loaded two actors at 15 FPS and attached their windows to the Explorer desktop. The test ran for about 35 seconds and exited successfully. Test settings and Unity's local profile were restored. The user's existing program was not stopped.
- All publication filenames are ASCII. The 21 original runtime Markdown documents were gathered under docs using English filenames; personal paths and obsolete local attachments were removed. Historical notes are labeled.
- Local Markdown link targets, default configuration and required runtime siblings passed validation. Every archive was checked for ZIP CRC errors and given a SHA-256 checksum. The publication manifest records each file's size and SHA-256 hash.
- Game asset packs, extracted models/animations, game databases, private logs, user settings, original upstream source backups and Unity caches are excluded.

Runtime assembly SHA-256: `C5BC24CE5EDD8D442CD3216B6A2337D1F3A70E4C485DD450C66E2522AFD91DCB`.

The runtime binaries are the previously tested 3.16.0 build; packaging did not rebuild or change runtime behavior. The overlay only changes machine-specific default paths to portable defaults. Current ten-actor functionality was checked during 3.16.0 development; this short publication smoke does not add a ten-actor performance benchmark or low-end-hardware certification. Rights still require the confirmations in [License-Status.md](../License-Status.md).
