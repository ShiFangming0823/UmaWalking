# Updates, publishing, and configuration persistence

Public version: **1.0.0**. Repository: [ShiFangming0823/UmaWalking](https://github.com/ShiFangming0823/UmaWalking).

## 用户：更新程序

1. 程序默认在每次启动时异步检查公开的 GitHub 正式 Release。检查失败不会停止桌宠。预发布和草稿不参与正式版提醒。
2. 有新版时显示该 Release 的更新说明。点击「前往下载更新」只会打开对应 GitHub 页面，不会下载、运行安装器或覆盖程序文件。
3. 「跳过当前版本」只记录该版；更高版本仍会提醒。「跳过所有更新」关闭以后启动时的自动检查；「设置与诊断」中的手动「检查更新」始终有效，可以重新查看被跳过的新版说明。重新勾选「启动时自动检查更新」可恢复自动检查。
4. 下载 Windows-x64 ZIP，退出旧程序，解压新版到新的可写文件夹，然后启动。保留游戏或精简数据目录。不要删除 `%LOCALAPPDATA%\UmaWalking`。
5. 勾选「随系统启动」后，在本用户登录 Windows 时运行，不需要管理员权限。取消勾选会删除本程序的启动项。若更换安装文件夹，先启动新位置一次，程序会刷新原本已启用的启动项；删除旧文件夹前完成此步骤。Windows策略或用户在系统设置中禁用启动项仍可能阻止自动运行。

首次从3.x开发版迁移到1.0.0时，先将旧程序的 `DesktopSettings.json` 复制到1.0.0程序目录，再首次启动1.0.0。此前没有用户配置目录时，程序会将该旧配置导入持久目录。以后版本无需再次复制。

## Configuration storage

| Location | Contents |
|---|---|
| `%LOCALAPPDATA%\UmaWalking\DesktopSettings.json` | Durable game path, FPS/AA, language, model cap, size, Include/Exclude selection, ground geometry and other pet settings |
| Same file with `.bak` suffix | Previous saved configuration for recovery |
| `%LOCALAPPDATA%\UmaWalking\UserPreferences.json` | Automatic-update toggle and skipped release version |
| Application folder `DesktopSettings.json` | Compatible local mirror; imported only when no durable profile exists |
| Current user's Windows `Run` entry named `UmaWalking` | Optional Windows sign-in startup command |

After the first successful 1.0.0 launch, a new application directory reads the durable profile before its shipped defaults. Atomic writes and a previous-file backup reduce the risk of partial writes; if the primary profile is invalid, a valid backup is used. If both copies are damaged, the program reports the problem and preserves the files rather than silently resetting everything. Exit the app before restoring a backup. Retain the selected full/compact data directory separately: configuration preservation does not copy or redownload game assets.

Development tests use disposable profiles via `UMA_WALKING_PROFILE`; explicit runtime test modes stay local and do not overwrite a real user's settings. This environment override is a test/development feature, not needed for normal users.

## Maintainer: how a new version is detected

The program reads the public, unauthenticated endpoint:

```text
https://api.github.com/repos/ShiFangming0823/UmaWalking/releases/latest
```

It compares the release `tag_name`, such as `v1.0.1`, with the app's embedded semantic version. The dialog uses the release title and `body` as plain-text release notes; only the same repository's HTTPS release page can be opened. There is no separate model server, update manifest, account token, or executable auto-installation step. The request uses a timeout and runs off the UI thread. Rate limiting, unavailable networks, and a repository with no visible stable release are handled as nonfatal results. Manual checks display a status message when no update is available.

At validation on 2026-10-22, this endpoint returned HTTP 404; that does not distinguish an unpublished release from a repository that is private or not yet accessible. The program handles this state. Update dialogs and skip behavior were separately verified with controlled release fixtures; an actual newer public release cannot be demonstrated until the maintainer publishes one.

See GitHub's [latest release REST documentation](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).

## Maintainer: publishing 1.0.0 and later versions

1. This package is labeled **1.0.0**. Create a Git tag **`v1.0.0`** from the intended source commit; create a GitHub Release for that tag.
2. Fill the release description with meaningful update notes. The description becomes the in-app update text. Chinese, English and Japanese notes may be included in the same body; they are displayed as provided, without automatic translation.
3. Upload the complete **`UmaWalking-v1.0.0-Windows-x64.zip`** and **`SHA256SUMS.txt`**. Do not upload only the EXE. Source and Publication ZIPs are optional additional attachments. Do not upload game data, extracted mini packs, personal configuration or logs.
4. Publish it as a public **stable** release and mark the intended stable build as **Latest**. A draft, prerelease, plain Git tag, or private repository is not a public stable update channel.
5. For the next build, for example **1.0.1**, use `tools/Set-Version.ps1 -Version 1.0.1`, apply the overlay, build, rerun tests, and package. The shared version constant is `source/Assets/Scripts/DesktopPrototype/DesktopProduct.cs`, used by Unity, the controller, diagnostics and the update checker. `tools/Package-Release.py` reads that constant for archive names.
6. Publish tag **`v1.0.1`**, release notes and the matching binary ZIP. Version 1.0.0 users will see it at their next eligible automatic check or manual check. Do not reuse a tag/version for changed binaries: users who skipped it would otherwise continue to skip it.

The configuration schema version is separate from the app version. Do not reset it to 1 or overwrite user profiles when changing release numbers. Add migrations for future settings changes, preserve valid old values, and test a new-folder upgrade before publishing. Existing permissions and third-party notices in [License-Status.md](License-Status.md) still apply. No GitHub release has been created or modified by this local packaging work.
