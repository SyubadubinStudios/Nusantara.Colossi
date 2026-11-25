# Progress — development tracking / catatan perkembangan

*Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios · Made by Ariana Mischa Fadhila of Syubadubin Studios*

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

### Known gaps (honest status)

- **Asset polish was interrupted** (the asset agent hit a usage limit). Arka still has the placeholder clips. The flat-backed heads (komodo-agung, leviathan, kapal-majapahit) are unchanged; only raw Rodin v2 regenerations exist for komodo and leviathan. The game already maps the new clip names (Sprint, Fall, Dodge, ClimbIdle, StabRelease, Swim, Death, Ride, Glide) and falls back to the current ones until they exist.
- **The accessibility and co-op work was also cut short by the usage limit.** It builds, and its unit tests pass. But the settings screen hasn't been looked at in a screenshot, and LAN discovery, reconnect and migration haven't been tried across two machines.
- **Vehicle and glider state isn't in the co-op packets yet.** Remote players appear on foot while riding.
- **New GARUDA voice lines are subtitles only.** English recordings don't exist yet; the six existing recordings are Indonesian.
- **The CI and release workflows have never run on GitHub** before this push. Linux and macOS packages are untested on real machines.
- **Milestone 2 visual items are still open:** collapse cinematics, photo mode, cliff edge walls, hand IK and full guardian body meshes.
- **Heavy 3D sources stay out of git:** the `.blend` and 2K/raw GLB files in `assets/sources`. They are attached to the release as `source-assets` instead.

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
