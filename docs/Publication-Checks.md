# UmaWalking 1.0.0 - default-path correction checks

Date: 2026-10-22. Rebuilt Windows x64 Mono runtime and controller with Unity 2022.3.62f3.

- New-user compiled default, control-panel fallback and sanitized default JSON use `C:\DMMGames\Umamusume\umamusume_Data\Persistent`.
- The old relative GameData default was not an embedded personal directory; Windows expanded it beneath the program folder. Only missing old placeholders are now migrated. Valid datasets and custom paths remain unchanged.
- Seventeen storage/migration assertions passed, including valid relative and absolute GameData directories and temporarily unavailable custom directories.
- Final controller binary passed 29 controller, 86 options/data-guard, 977 name/localization and 59 update/default-prefill assertions. The new assertions check missing/old-prefill replacement and custom-path preservation in the real control panel.
- Two normal-mode runtime launches with disposable profiles passed: a fresh profile displayed the generic DMM data path, and an old expanded placeholder was repaired to that path without changing any other setting. Both reported version 1.0.0 and exited normally. The generic path is not required to exist on a custom-installed system; folder selection remains available.
- Archives are rebuilt with version 1.0.0, hashed and ZIP-CRC checked. No private profile, local runtime logs or game resources are included. The Windows runtime ZIP continues to omit the live configuration file.

Runtime assembly SHA-256: `AB2640AF5480A658A6705038508A3915EFC5F9E1A1A0621C44F5EBC2BE4CDB95`.

Controller SHA-256: `DCB1D735990AD6EC49E1206DCAF98DCFDE41A3F7A44E2CF42C2805C8EB9FD669`.

The original expression/upgrade checks are retained as [historical evidence](archive/Publication-Checks-Initial-1.0.0.md), with their original binary hashes. Game rendering, update mechanics and animation rules are unchanged by this correction. Source references and installation-layout caveats are in [Default-Data-Path.md](Default-Data-Path.md).
