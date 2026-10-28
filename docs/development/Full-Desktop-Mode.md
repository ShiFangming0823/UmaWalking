# Full desktop orthographic ground — 1.1.0

Available in 1.1.0 alongside the original line and quadrilateral modes.

## Controls

In **Characters and grounds**, select **Full desktop · Orthographic** in the
ground selector and press **Apply full desktop**. No canvas, corners, entrances,
or exits need to be configured. The existing line and quadrilateral modes remain
available. Applying this mode preserves previously drawn line/surface data for
later editing.

The camera uses a fixed 30-degree elevation and orthographic projection. Each
actor keeps the same uniform display scale throughout the desktop: moving up,
down, left, or right does not create perspective size changes. The model-size
slider remains available. A uniform safety fit is applied when the sampled
animation envelope cannot fit in the actor's render canvas or physical screen;
this fit depends on the actor and size setting, never its desktop position.

Every actor independently picks an entrance from the four screen edges, with a
random point along that edge. Retirement picks another arbitrary edge. There
are no fixed portals. The whole actor starts outside the display, walks in with
a fade, roams, and walks out before its slot is freed. Entry/exit may temporarily
cross the display boundary; active wandering and drag placement reserve room for
the sampled full-body animation envelope, including hair and accessories.

Click specials, pose-preserving drag, outfit changes, manual entry/retirement,
Include variants, population limits, depth ordering, the optional avoidance
switch, and Explorer desktop attachment use the existing shared systems.

## Implementation

- Persisted mode: `DesktopGroundConfig.mode = "Desktop"`.
- `GroundRoute` creates an internal rectangular map and per-actor random edge
  choices. Generated gates are not written into the user's surface definitions.
- `DesktopActorGround` switches only the camera orientation, then restores its
  original transform/projection when leaving this mode.
- `DesktopActorFooting` supplies stable asymmetric whole-body navigation margins,
  a constant fit and a calibrated canvas anchor. All gait floors contribute to
  the bounds so action changes do not resize the actor.
- `GroundMap.Depth`, yaw calculation, and avoidance use the same 30-degree tilt.
- `ActorState` exports `orthographicDesktop`, `viewElevation`, `entranceEdge`,
  `exitEdge`, and `navigationRegion` to aid diagnostics.
- The controller applies the fullscreen mode without opening the drawing canvas;
  UI text and button states are localized in Chinese, English, and Japanese.

## Verification

Run `source/Test-Logic.ps1`; the additional `DesktopPlaneTests` covers four-edge
entry/exit, monotonic fades, full-body containment, dragging, aspect ratios and
landscape/portrait screen geometry. Existing geometry and scheduling suites
remain part of that run.

For native control layout, launch the controller with
`--desktop-ground-selftest --root <disposable-fixture>` and provide a local
`DesktopSettings.json`. This checks all three locales and preserves saved surface
definitions. It does not change the startup registry or the user's settings.

For real models, use a disposable complete runtime with a valid user-owned data
path and run:

```powershell
python source/Tests/verify_desktop_plane.py --build-directory <test-runtime> --evidence-directory <private-output>
```

The integration test compares rendered alpha bounds at five desktop locations,
exercises 50/100/150% scaling, freezes/resumes drag, switches through the old ground
modes, checks manual exit/queued entry, tests the larger Hishi Akebono, and reloads
the mode after a fresh player launch. It uses test-gated pointer commands and
does not inject physical OS mouse input. It restores the test settings and does
not write the shared user profile. Keep portraits and private logs outside Git.

The preview launcher sets `UMA_WALKING_PROFILE` to a separate `PreviewProfile`
folder beside the preview executable. It does not overwrite the normal user's
ground settings. Exit another UmaWalking instance before using the preview.

## Desktop avoidance heading stability

The desktop-only avoidance policy now offers the previous heading and continuous
turn candidates alongside the coarse goal-relative angle bank. It penalizes
unnecessary heading changes and still checks every candidate for predictive and
simultaneous swept collisions. It permits recovery toward the intended goal and
does not pin gait speed to the previous frame. Drawn quadrilateral grounds retain
their previous policy.

A small display-only heading hysteresis suppresses sub-step directional noise on
desktop locomotion when avoidance is enabled. Specials, paired actions and other
ground modes follow their existing orientation rules. Ground changes clear the
old navigation velocity and facing history.

`DesktopAvoidanceStabilityTests` compares the original and desktop policies on
head-on, stationary-obstacle and six-agent fixtures at 15/30/60/120 FPS, and also
checks swept safety, progress, screen bounds, yaw wrap and gait-speed changes.
At 30 FPS, those deterministic fixture peaks fall from 19/25/30 small heading
reversals per second to 1/1/2. This measures the fixtures, not a guarantee that
characters never need to turn repeatedly in every possible arrangement.

`verify_desktop_avoidance_stability.py` records real model poses every frame in a
disposable test player and checks the same encounters, held-pose preservation,
click specials, the avoidance toggle and retirement. The bounded heading audit
command is available only in explicit test mode; ordinary runs do not collect
per-frame trace buffers. Diagnostics now expose actual/requested yaw and planned
velocity to make future steering issues reproducible.
