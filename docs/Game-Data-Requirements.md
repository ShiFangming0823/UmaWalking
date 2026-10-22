# Game data requirements

## What is and is not distributed

UmaWalking does not include extracted character models, costumes, animation bundles, facial resources, game metadata or the master database. Obtain them through the official Japanese DMM game client, then select its `umamusume_Data/Persistent` directory. See the [three-language download instructions](../README.md).

The folder must contain `meta`, `master/master.mdb`, and `dat`. Selecting only `dat` is not sufficient. Extraction runs locally; the program does not download a game-data pack from our server.

## Local comparison, 2026-10-21

Two local snapshots of the same game resource version were compared: the initial required download (about 18GB) and the initial download plus the optional additional download (about 30GB more). Paths and raw game indexes are omitted from this public report.

| Check | Result |
|---|---|
| Resource definitions in each index | 370,090; names, hashes and dependencies equivalent |
| Required recursive mini dependency closure | 11,656 asset files |
| Missing required assets in the initial download | 0 |
| Required asset bytes | 434,863,204 |
| Initial versus full required files | Every required file SHA-256 verified identical |
| Available mini character identities | 136 |
| Character/costume combinations | 4,080 |
| Shared costume types | 28 |
| Locomotion resources | Two walk and three run variants |
| Paired motion sets | Three matching A/B sets |
| Initial `dat` size | 19,843,189,027 bytes |
| Full `dat` size | 52,544,084,019 bytes |
| Extracted initial mini pack, including databases | 11,658 files; 647,261,284 bytes (about 617MiB) |

The additional download was mainly audio, videos and resources outside this desktop pet's mini dependency closure. For the tested version, the initial download is enough; the optional additional download is unnecessary. Future game versions can change this division. Availability of all 4,080 combinations was checked; it is not a claim that each combination was individually observed on screen. A separate runtime sample loaded five characters and exercised 245 action transitions and twelve expression states.

## Extract a compact local pack

1. Select the official source `Persistent` directory in the control panel.
2. Choose Extract mini resources and an empty destination directory.
3. Wait for recursive dependencies, databases and byte verification to finish. Do not close the program or delete the source during extraction.
4. After verification and successful switch to the compact destination, the program offers an explicit deletion confirmation for the original data. Declining keeps it. Retain the compact directory.
5. To obtain new models after a game update, download the current official data again and extract a fresh pack. A compact pack is not a self-updating game installation.

Deleting original game data can prevent the game from running until repaired or downloaded again. Extraction is a private local convenience, not permission to redistribute the resulting pack. Do not attach a full or compact data pack to GitHub issues or releases.
