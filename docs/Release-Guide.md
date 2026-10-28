# GitHub publication guide

## Repository versus release

GitHub does not compile a desktop application automatically. Creating a release automatically provides source ZIP/tar archives for its tag; users cannot run those as a compiled Windows application. Upload the prepared Windows runtime ZIP as a release asset. Uploading only `UmaDesktop.exe` is insufficient because it needs UnityPlayer, Mono, the data folder and controller alongside it.

See GitHub's official [about releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases) and [managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) documentation.

## Prepared files

| File under `dist` | Purpose |
|---|---|
| `UmaWalking-v1.1.0-Windows-x64.zip` | User download: tested Windows application (creates or restores settings at launch), README, notices and documentation |
| `UmaWalking-v1.1.0-Source.zip` | Authored overlay, tests, setup/build tools and documentation; fetches upstream separately |
| `UmaWalking-v1.1.0-Publication.zip` | Entire prepared directory except `dist`; convenient all-in-one handoff without nested duplicate archives |
| `SHA256SUMS.txt` | Checksums for the three archives |

The repository should track source, tools, README, AGENTS and docs. `.gitignore` excludes compiled runtime folders, local data, logs and generated archives. Prefer uploading compiled binaries through Releases rather than committing them to Git history.

## Manual publication

1. Resolve and document the actual permission items in [License-Status.md](License-Status.md); no overall license was assumed during packaging.
2. Create your own GitHub repository and commit the prepared source files. Do not commit personal settings, diagnostic logs or any original/extracted game data.
3. Create tag `v1.1.0` from the intended source revision, then create a release for that tag.
4. Attach `UmaWalking-v1.1.0-Windows-x64.zip` and `SHA256SUMS.txt`. The source/publication archives are optional additional attachments. Explain that users must obtain official game data separately.
5. Verify a fresh download, checksum, full extraction, data-path selection and launch. Include supported platform, version, known limitations and data requirements in the release notes.

No workflow has been configured to publish automatically. Future GitHub Actions builds need an explicitly configured Windows Unity workflow, the matching Unity editor and appropriate Unity license activation/secrets. Do not commit licensing credentials. Automated building does not settle copyright permissions.

The update checker and first-upgrade procedure are documented in [Updates-and-Settings.md](Updates-and-Settings.md). Windows runtime ZIPs omit the live DesktopSettings.json so extraction cannot overwrite an older local configuration. Future public versions use one DesktopProduct.Version constant.
