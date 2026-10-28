# Explorer desktop compatibility — 1.1.0

Issue [#1](https://github.com/ShiFangming0823/UmaWalking/issues/1) reports invisible layered children under Progman on Windows 11 IoT Enterprise LTSC 2024 / 24H2 build 26100. The reporter verified visible children under SHELLDLL_DefView. Native visibility and successful UpdateLayeredWindow alone do not establish compositor visibility.

`DesktopEnvironment.Host` remains the Explorer-owned top-level Progman/WorkerW with its direct SHELLDLL_DefView. Coverage enumeration stops at Host, never at a child. `IconView` is that direct child; `ChildHost` is the actual actor parent. Auto chooses IconView if the root has WS_EX_NOREDIRECTIONBITMAP and the view is visible; otherwise it retains Root. This style is a conservative heuristic, not a universal compositor capability test. Settings offers Auto, Root and IconView overrides, persisted in DesktopHostMode.

Every poll resolves both handles again. Overlay attachment/recreation uses ChildHost, including when the leaf changes while the root remains unchanged. Creation uses the selected parent's DPI context; movement/presentation convert physical coordinates into its client space. A missing or hidden host pauses/hides the actors without a top-level fallback. No shell restart, undocumented shell message or desktop-icon preference change is performed.

Diagnostics preserve desktopHost and add desktopChildHost, desktopIconView, requested/selected strategy, handle/class/rectangle/style/process/DPI information and per-actor lastNativeError. windowVisible still means native visibility, not a screenshot/compositor result.

Run Test-NativeInput.ps1, Test-DesktopHost.ps1 and Test-Logic.ps1. Native fixtures verify root/leaf parentage, non-topmost style, nonzero-origin coordinates, click/drag queues, hide/resume and leaf destruction/recreation. Real-player tests must also exercise the actual Explorer Root and IconView choices, full coverage, partial coverage, transient window management and profile preservation. Fixture checks cannot emulate Windows 11 composition. A real retest on the reporting 24H2 system is required before declaring that environment verified.
