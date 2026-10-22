# Default game-data path

The default prefill is `C:\DMMGames\Umamusume\umamusume_Data\Persistent`. It is a generic DMM installation example, not a developer-specific path and not a requirement to install on C:. Users who chose another game installation directory or an extracted mini pack should select that actual directory.

Research checked on 2026-10-22:

- The [UmaTL project's setup instructions](https://github.com/UmaTL/hachimi-tl-en/releases/tag/config) list `C:\DMMGames\Umamusume` as the default DMM game installation directory.
- [UmaViewer's own installation instructions](https://github.com/katboi01/UmaViewer#requirementsinstallation) describe the `umamusume_Data\Persistent` layout for fresh installations, and note that older installations may still use the user's LocalLow directory. In every case check for `meta`, `master/master.mdb`, and `dat`.

The previous relative default `GameData` expanded to the program's own installation directory. On a maintainer machine this could display a path containing a personal directory name even though no such absolute path was embedded in the published default. The compiled default, control-panel fallback and packaged default JSON now use the generic DMM path above.

Only missing old placeholder paths are migrated. A structurally valid selected dataset, including a valid GameData subfolder, is retained. Arbitrary custom paths are retained even if their drive is temporarily unavailable. Profile changes remain backed up and private; no user profile or runtime logs are added to the release archives.
