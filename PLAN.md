# PLAN — Nusantara Colossi roadmap / peta jalan

*Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios · Made by Ariana Mischa Fadhila of Syubadubin Studios*

Stack (final): **.NET 10 · C#**, **Avalonia UI** (every platform), **Three.Net** (rendering, audio, input, UDP networking on a Rust wgpu core). No Blazor, WPF, MAUI or Babylon. Rust is used only through Three.Net's native core.

Status legend: ✅ done · 🟡 partial · ⬜ not started

## Milestone 1: Vertical slice (current)

| # | Deliverable | Status |
|---|---|---|
| 1.1 | Solution layout (Core / Game / Desktop / Android / tests / AudioForge), central package management | ✅ |
| 1.2 | Simulation core: regions, 8 guardian blueprints, deterministic rig, surface climbing, player, guardian AI, encounter, hazards | ✅ |
| 1.3 | Progression: skills (3×3), ranks, Legend mode, unlocks, atomic saves | ✅ |
| 1.4 | Co-op protocol (2–4 players), host-authoritative guardian, client-owned climbers | ✅ |
| 1.5 | Rendering: sculpted guardian parts, WGSL stone/terrain/building/foliage shaders, 8 biomes, water, clouds, fire, particles, lightning | ✅ |
| 1.6 | Camera: orbit, lock-on, trauma shake, showcase orbit | ✅ |
| 1.7 | Adaptive gamelan score + SFX (AudioForge), spatial audio, heartbeat sync, subtitles | ✅ |
| 1.8 | UI: title, map, dossier, hunt HUD (grip ring, candi vitality, ability dock, telemetry), pause / victory / defeat, codex, skills, settings, credits, co-op lobby | ✅ |
| 1.9 | Input: keyboard/mouse, gamepad (menus + rumble), touch | ✅ |
| 1.10 | Desktop head, screenshot tour, dynamic resolution, GPU-based default quality | ✅ |
| 1.11 | Android head (asset extraction, touch, landscape) | 🟡 builds; not yet run on a device |
| 1.12 | 2D art (portraits, key art, icon) with Rodin GPT Image Flare | ✅ |
| 1.13 | 3D models via Rodin (Hunyuan failed on Replicate) + Blender processing: rigged Arka, drone, 8 heads at mobile weight | ✅ |
| 1.14 | Bilingual docs (README, player guide, design, architecture, build, assets) with screenshots | ✅ |

## Milestone 2: Feel and fidelity

- 🟡 Arka has 15 procedural clips (`arka_anim.py`): Idle, Run, Sprint, Jump, Fall, Climb, ClimbIdle, Hang, Stab, StabRelease, Dodge, Swim, Death, Ride, Glide. Still to do: hand IK onto grip points, and motion-captured or hand-keyed polish.
- ⬜ Full guardian body meshes (Rodin → Blender retopology), skinned to the rig's part pivots; keep the box colliders.
- 🟡 Head GLBs are slimmed to about 1 MB (24k tris, 1K textures); komodo, leviathan and kapal were regenerated as full 3D heads. Still to do: LODs.
- ✅ Collapse cinematic (a wide orbit scaled to each guardian's height, HUD hidden) and a post-victory close-up on the head behind the result card.
- ✅ GARUDA voice lines (ElevenLabs) for all 11 lines in Indonesian and English.
- ⬜ Terrain edge walls dressed as cliffs/karst. Raja Ampat's edge currently reads as a pale wall.
- ✅ Photo mode (pause menu, solo): free orbit within 40 m, pan, height, zoom; PNG saved to Pictures/Nusantara Colossi.

## Milestone 3: Content and tension

- ✅ Guardian-specific mechanics:
  - Dewi Padi floods terraces mid-fight.
  - Gedung Titan sheds glass shards.
  - Leviathan dives with a breath meter.
  - Kapal Majapahit fires its cannons.
- ✅ Vehicles from the original design (motor trail, jet ski, glider), as in-arena mounts, synced in co-op (protocol 3).
- ✅ A ninth, secret guardian tied to GARUDA NET itself (Garuda Agung at Nusantara Point Zero, with an epilogue).
- ✅ Time-attack leaderboards (local, top five per guardian).

## Milestone 4: Platforms and release

- ⬜ Android device QA: performance pass on mid-range phones, AOT build, Play Store packaging.
- 🟡 macOS and Linux packaging scripts plus CI smoke checks. Windows installer (Inno Setup) tested. Linux and macOS not yet run on real machines.
- 🟡 Co-op: LAN discovery, reconnect and host migration are implemented and unit-tested, but not tried across machines.
- ✅ Accessibility: remappable controls, hold-to-toggle grip, high-contrast HUD, non-colour cues and UI scale (checked in screenshots).
- ✅ CI: build plus tests on Windows, Linux and macOS, and a release workflow that attaches every platform's package.
