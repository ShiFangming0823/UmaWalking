# Architecture and maintenance map

UmaWalking combines a Unity renderer with a separate WinForms control panel. The authored overlay is under `source`, and setup applies it to a pinned external UmaViewer checkout.

| Area | Main source |
|---|---|
| Main loop, character lifecycle, animation transitions | `source/Assets/Scripts/DesktopPrototype/DesktopPrototype*.cs`, `DesktopActor*.cs`, `MiniClipPlayer.cs` |
| Official local data index, dependencies and availability | `DesktopAssetPlan.cs`, `MiniMotionCatalog.cs`, controller `UmaDesktopData.cs` |
| Model cap, Include variants, manual entries | `DesktopPopulationLimit.cs`, `DesktopIncludeVariants.cs`, `DesktopManualEntries.cs`, controller `UmaWalkingPopulation.cs` |
| Desktop windows, coverage pause, drag and hit testing | `WindowsOverlay.cs`, `DesktopEnvironment.cs`, `DesktopMouseInput.cs`, `DesktopGroundDrag.cs` |
| Ground geometry, footwear footprint and avoidance | `DesktopGroundMath.cs`, `DesktopFootprintMath.cs`, `DesktopAvoidance.cs`, actor grounding partials |
| Expressions | `MiniExpressionController.cs`, `MiniExpressionPolicy.cs` |
| Names, language, settings and diagnostics | `DesktopCharacterNames.cs`, `DesktopSettings.cs`, controller `UmaDesktopLanguage.cs`, `UmaDesktopOptions.cs` |
| Build generation | `source/Assets/Editor/UmaDesktopBuild.cs`, `source/Build-*.ps1` |

The runtime resolves game bundles from the selected local `Persistent` or extracted directory through UmaViewer's asset manager and builder. It creates mini character instances with compatible outfits and body/face animations. Spring/secondary-motion handling remains based on the existing model hierarchy; avoid arbitrary transform scaling of individual bones or screen-space mesh distortion.

Each visible actor is rendered to an offscreen target and composed into its own Windows layered window on the Explorer desktop. Unity's own player window is not the final visible pet window. The control panel exchanges JSON commands/state through `DesktopCommands`; these files and `DesktopLogs` are local, transient, and excluded from publication. Never promote actor windows above foreground applications to work around spawn issues.

Three-dimensional custom ground uses a quadrilateral mapping for positions and uniformly scaled upright actors. Entry and exit have explicit phases, and foot occupancy is constrained during movement/drag. A drag must cancel stale entry/exit path state before normal movement resumes. Actor capacity counts entering and departing actors, and manual queues must not exceed the configured cap by loading an extra actor early.

The current runtime uses character render layers 20–29 to support a maximum of ten actors. Changes to the cap must consider masks, actor pooling, queues, Include outfit uniqueness, UI labels, diagnostics and tests together. Source filenames may retain UmaDesktop for compatibility; public project branding is UmaWalking.
