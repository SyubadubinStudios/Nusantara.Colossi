# Progress — development tracking / catatan perkembangan

*Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios · Made by Ariana Mischa Fadhila of Syubadubin Studios*

## 2026-11-25 (night): v0.3.0, guardian bodies

### Done

- **Full-body guardians.** All nine guardians now have a full Rodin 3D body instead of sculpted boxes. The pipeline:
  - `tools/rigdump` writes each rig's rest pose to `assets/sources/rodin/rigs.json`.
  - `assets/sources/blender/process_bodies.py` fits the Rodin model to the rig's bounds (yaw 0, with a quarter turn only for side-on models). It then assigns every face to its nearest part box and writes one mesh per part in that part's pivot frame.
  - Each piece is stretched part of the way toward its collider box (`FILL` 0.75, scale clamped to 0.6–1.6), so Arka grips a visible surface instead of hanging in the air off a thin limb.
  - Output: 36k triangles with 1024 px textures (about 1.2 MB), plus a 9k-triangle LOD (about 0.4 MB) swapped in by distance. Low quality loads only the LOD.
  - The game parents each piece under its part pivot, so bodies move with the rig, in co-op too. The box colliders are unchanged, so gameplay and the 85 tests are unaffected.
- The Kerbau Langit body took three Rodin tries: a biped, then a charging pose, then a standing quadruped.
- Checked in a gallery tour of all nine guardians and in Bromo/Kalimantan hunt captures.

### Known gaps (honest status)

- Bodies are rigid per part, not skinned. A seam or overlap can show at a joint when a limb swings far.
- The collider boxes and the mesh still do not match exactly. On some parts Arka's hands can sit a little off the visible surface.
- ~~The Ijen guardian's material is very glossy.~~ Fixed after the release: `process_bodies.py` now applies a per-guardian roughness floor (`MATTE`, 0.55 for Penjaga Api Biru), so its roughness averages 0.67 instead of 0.27.
- Everything else from v0.2.0 below is still open: Android on a mid-range device, co-op across machines, Linux/macOS hardware, hand IK by eye, the blank first 3D frame.

## 2026-11-25 (evening): v0.2.0

### Done

- **Android**:
  - Audio no longer crashes at launch. It mixes offline and streams through `AudioTrack`.
  - Release builds are AOT-compiled. `-p:StoreBuild=true` produces a Play Store AAB, signed with an upload key passed in as MSBuild properties (never committed).
  - Mobile render preset: render scale 0.55, and dynamic resolution on every screen down to 0.35. The frame timer runs at Background priority on mobile so touch and layout are not starved.
  - Device profiling: a `profile` file in the app's external folder logs frame timings and pointer events to logcat (tag `NC`).
- **Head LODs**: `process_lods.py` builds a 6k-triangle, 512 px version of each head (about 270 KB). It is swapped in beyond about eight head-heights, with hysteresis. Low quality loads only the LOD. Checked in Bromo screenshots at Low and Balanced.
- **Credits**: a cinematic scrolling roll (see below).
- **README**: a download section linking every installer, with first-launch steps.
- **Marketing kit**: in `docs/marketing`.
- **Version**: 0.2.0 (Android versionCode 2).

### Known gaps (honest status)

- **Guardian bodies** were still sculpted boxes in v0.2.0 (done in v0.3.0, above).
- **Android** has only run on a Mali-G51 tablet, at about 3 FPS, measured before the mobile preset. The new preset has not been measured on a device yet. Touch and audio output there are also unconfirmed. Next step: a mid-range phone.
- **Co-op** has still only been tested over loopback. No second machine was available.
- **Linux and macOS** builds have not been run on real hardware. This machine has no WSL and there is no Mac.
- **Arka's hand IK** has not been tuned by eye in play.
- **The blank first 3D frame** seen once in a screenshot run has not been reproduced.

## 2026-11-25 (later): Milestones 3–4, repo move, first release

The project moved to `C:\experiment\syubadubin\Nusantara.Colossi` (GitHub: SyubadubinStudios/Nusantara.Colossi).

### Done

- **Signature mechanics (M3).** They are pure functions of the guardian clock, so co-op peers agree without new packets:
  - Dewi Padi floods the terraces in waves.
  - Gedung Titan sheds telegraphed glass shards.
  - Leviathan dives about 9 m with climbers on its back, and a **breath meter** appears.
  - Kapal Majapahit fires broadsides.
- **Ninth guardian (M3).** Garuda Agung, GARUDA NET's own body, waits at the secret region *Nusantara Point Zero*, which unlocks after all eight. Data bolts strike even climbers, it can't be hacked, and its wings are walkable. Its victory card carries an epilogue.
- **Vehicles (M3).** A trail bike or jet ski is parked at camp (mount, ride, leap off). The **GARUDA glider** opens mid-air.
- **Local time-attack boards (M3).** Each guardian keeps its top five times in the profile, shown on the victory card.
- **Audio.** Mixes for every new event, plus 5 new GARUDA lines with subtitles. Voice files now fall back to `<line>.wav` when `<line>-<lang>.wav` is missing (before this fix, the recorded lines never played).
- **Tests.** 81/81 pass. 13 are new: signatures, breath, vehicles, glider, leaderboard and the secret unlock.
- **CI and packaging (M4).**
  - `.github/workflows/ci.yml` builds and tests on Windows, Linux and macOS, and packages each one.
  - `release.yml` attaches the Windows installer and zip, the Linux AppImage, the macOS .app zips and the Android APK to a published release.
  - Inno Setup installer (`build/installer`). It was tested here with a silent install, launch and uninstall.
- **Accessibility (M4).**
  - Remappable keyboard and gamepad bindings; HUD labels follow the bindings.
  - Hold-to-toggle grip.
  - High-contrast HUD; non-colour cues for core states.
  - UI scale setting.
- **Co-op (M4).**
  - LAN discovery: a UDP beacon, plus a list of sessions in the co-op screen.
  - Reconnect with slot reclaim by token.
  - Host migration to the lowest client slot.

### Closing the gaps (same day, after v0.1.0)

- **Co-op**:
  - Vehicles and the glider are synced (protocol 3). Vehicles are now indexed by hunter slot, so peers agree on whose bike is whose.
  - `tests/NusantaraColossi.Net.Tests` runs real UDP sessions over loopback: join, snapshots, walking, a ridden bike, and host migration (about 12 s). These tests run in CI on all three OSes.
- **Map**: the secret region stays hidden until all eight guardians fall, and the pager labels it "Unknown signal".
- **Voice**: the 5 new GARUDA lines are recorded in Indonesian, and English versions exist for all 11 lines (ElevenLabs "Aria").
- **Settings**: Fluent sliders and toggles use the prada-gold accent. Checked in screenshots, including high contrast at 130 %.
- **Heads**: komodo, leviathan and kapal were regenerated with Rodin as full 3D heads and processed to about 1 MB each.
- **Arka**:
  - 15 procedural clips (`arka_anim.py`, applied by `reanimate_arka.py`), checked on Blender contact sheets and in game. Death, Dodge and StabRelease hold their last frame.
  - Climbing hand IK (two-bone) plants wrists near the surface onto it.
- **Victory**: a collapse cinematic (wide orbit, HUD hidden), then an inscription close-up behind the result card.
- **Photo mode**: in the pause menu for solo hunts. Saves PNGs to `Pictures/Nusantara Colossi`.
- **Arena rim**: instanced cliff outcrops in every biome except the city. Raja Ampat's karst towers now stand on the rim.
- **Screenshot tours** (`--screenshot`) gained new environment options:
  - `NC_SHOT_REGIONS=Flores,Banda` captures only those hunts, and `NC_SHOT_LATE=1` adds later frames.
  - `NC_SHOT_CINEMA=1` captures the victory cinematic and photo mode; `NC_SHOT_SETTINGS=1` captures the settings screens.
  - `NC_DATA_DIR` keeps test runs out of the real profile.

- **Credits**: a cinematic scrolling roll over the 3D stage that names every guardian and comes to rest on a gold kawung seal. Hold Enter, Space, touch or gamepad South to speed it up.
- **Marketing kit** (`docs/marketing`): 7 social posts in Indonesian and English (12 images per language, in 16:9, 4:5, 9:16 and 1:1) with captions per platform and a two-week plan.
  - The 2D concept art comes from Rodin GPT Image Flare and is labelled "CONCEPT ART" on every image. Post 07 is a real screenshot.
  - `compose_posts.py` rebuilds every image.

### Known gaps (honest status)

- **Guardian bodies** are still procedural sculpted boxes; only the heads are generated models. Full body meshes (Rodin → retopology → skinned to the part pivots) are the largest open item.
- **Arka's clips are procedural keyframes**, not motion capture or hand-keyed animation. Hand IK has been seen only in autopilot screenshots (no broken limbs) and has not been tuned by eye in play.
- **Co-op has not been tried across two machines** or through NAT. Only loopback has been tested.
- **Linux and macOS packages** build in CI and are attached to the release, but have not been run on real hardware.
- **Android** was run on a Huawei MatePad (Mali-G51, Android 10, 1920×1200):
  - Fixed: ThreeNet's device audio aborted natively at launch ("android context was not initialized"). Audio now mixes offline and streams through `AudioTrack`.
  - The title screen and map render correctly, and keyboard Enter navigates.
  - The game is not playable on this GPU: about 2.8 FPS, with ~320 ms render time per frame at 1344×811 and ~270k triangles on the title screen. Touch taps are not picked up at this frame rate.
  - Next: retest on a stronger device. A mobile quality preset is needed (lower resolution, fewer triangles and props).
  - Profiling: `adb shell touch /sdcard/Android/data/com.syubadubin.nusantaracolossi/files/profile` logs frame timings and pointer events to logcat (tag `NC`).
- **Heavy 3D sources stay out of git**: the `.blend` files and raw GLBs in `assets/sources`. They are attached to releases as `source-assets`.

## 2026-11-25: Vertical slice

### Done

- **Solution scaffolded** on .NET 10 with central package management: Core, Game, Desktop, Android, tests, AudioForge. The stack follows the brief (Avalonia + Three.Net only). Blazor, WPF, MAUI and Babylon were dropped at the user's request.
- **Simulation (Core)** is complete for 8 regions and 8 guardians: deterministic rig, surface climbing, guardian AI with telegraphed attacks and rage phases, cores with seals/plating/resonance, hazards (rain, heat, gas, gusts), progression and saves, and the co-op protocol.
  **54/54 tests pass.**
- **Rendering:** sculpted guardians with carved-stone WGSL shaders, 8 biome environments, water, clouds, fire, particles, lightning, characters and drone, and a cinematic camera.
- **Audio:** AudioForge synthesises 43 deterministic WAVs (gamelan/pelog score, ambience, SFX). GameAudio crossfades explore, battle and rage layers by intensity and syncs the heartbeat to the core pulse.
- **UI (frontend-design direction "temple foundation stone × GARUDA telemetry"):** title, archipelago chart, dossier, hunt HUD (kawung grip ring, candi vitality), overlays, codex, skills, settings, credits, co-op lobby. Fully bilingual.
- **Input:** keyboard/mouse, gamepad with menu navigation and rumble, and touch controls.
- **Performance:** measured on an Intel UHD 620 (integrated GPU).

  | Metric | Before | After |
  |---|---|---|
  | Hunt FPS | 9–16 | 30–69 |
  | Lights per frame | 15 | 3–4 |
  | GPU time per frame | ~45 ms | 6–10 ms |

  The gains came from:
  - Budgeting lights, so core, drone and fire lights exist only when they matter.
  - Low-quality shader variants with two fewer noise octaves.
  - Default quality picked from the GPU class on first launch.
  - Dynamic resolution during hunts.
- **Fixed visual bugs:**
  - Three.Net's default linear fog (10–100 m) was erasing distant guardians. It is now pushed past the far plane.
  - Ambient light was 15× the default, which washed out every scene.
  - The Bromo crater floated above a pointed cone. Bromo is now a caldera.
  - The title screen rendered black because transitions started before the shell was attached.
- **Assets:** guardian portraits, key art, the Arka concept and the icon were generated with Rodin GPT Image Flare. `icon.ico` was made for the Windows exe and a launcher icon for Android. Hunyuan 3D jobs all failed on Replicate, so the 3D work was switched to Rodin 3D: Arka, the GARUDA drone and all 8 guardian heads were processed in headless Blender 5.2 (scripts in `assets/sources/blender/`) and exported to `assets/models` at mobile weight:

  | Model | Triangles | Size |
  |---|---|---|
  | Arka (17-bone rig; Idle/Run/Climb/Jump/Stab/Hang clips) | 34k | 1.5 MB |
  | Drone | 14.5k | 0.6 MB |
  | Each of the 8 heads | 24k | 0.8–1.0 MB |

  All use 1K textures. The 2K sources are kept in `assets/sources/blender/hires/`.
- **GARUDA voice lines** (`intro`, `scan`, `grip`, `core`, `victory`, `enrage`) are generated into `assets/audio/voice/`.
- **Docs:** bilingual README with screenshots (EN and ID sets); player guide, design, architecture, build and asset docs in `docs/en` and `docs/id`; PLAN.md; CLAUDE.md updated.

### Known gaps (honest status)

- **Android:** the APK builds (`com.syubadubin.nusantaracolossi`), but it has **not** been run on a device or emulator, because none was available. AOT is disabled because the Mono AOT workload isn't installed on the build machine.
- **macOS/Linux:** they share all code with Windows, but only Windows has been run.
- **Co-op:** the protocol and snapshot determinism are unit-tested, but no networked session between two machines has been played yet.
- **Arka's animations** are code-keyed placeholders that play in place, built on Rodin's A-pose. They need hand polish and hand IK onto grip points.
- **Three heads** (komodo-agung, leviathan, kapal-majapahit) have flat backs and read well only from the front.
- One screenshot run produced a blank 3D frame at the start of a hunt (HUD only). A rerun was fine. It may be a first-frame load hitch and has not been investigated.
- **Guardian bodies** are still procedural sculpted boxes. Only the heads use generated models.
- Raja Ampat's arena edge renders as a pale wall rather than cliffs.

### Next

See [PLAN.md](PLAN.md) Milestone 2, starting with polishing Arka's animations and running Android on a device.
