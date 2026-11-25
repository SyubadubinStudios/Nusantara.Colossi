# Assets and audio pipeline

[Bahasa Indonesia](../id/aset.md) · *Made by Ariana Mischa Fadhila of Syubadubin Studios*

The game never depends on an asset being present. Every model has a procedural fallback, and missing audio is silent. Assets improve the look; they never gate the build.

## Folder contract

| Path | Content | Used by |
|---|---|---|
| `assets/art/keyart.png`, `arka.png` | Key art and Arka concept | Docs |
| `assets/art/colossi/<id>.png` | Guardian portraits | Map dossier, codex |
| `assets/art/icon.png`, `icon.ico` | App icon | Window, exe, Android launcher |
| `assets/models/arka.glb` | Arka, with optional clips `Idle`, `Run`, `Climb`, `Jump`, `Stab`, `Hang` | `CharacterVisual` |
| `assets/models/garuda-drone.glb` | GARUDA drone | `DroneVisual` |
| `assets/models/bodies/<id>.glb`, `bodies/lod1/<id>.glb` | Full guardian bodies, split per rig part (about 35k / 9k tris) | `WorldRenderer.Colossus`: each `part-<i>` node is parented under part pivot i |
| `assets/models/heads/<id>.glb`, `heads/lod1/<id>.glb` | Guardian heads (24k / 6k tris), used only when a guardian has no body model | `WorldRenderer.Colossus`, fitted to the head part |
| `assets/audio/*.wav` | Music, ambience, SFX | `GameAudio` |
| `assets/audio/voice/<line>-<lang>.wav` | Optional recorded GARUDA lines (`intro-en.wav`, …) | `GameAudio.SayLine` |
| `assets/sources/{rodin,blender}` | Source files, not shipped | — |

Guardian ids: `kerbau-langit`, `gedung-titan`, `dewi-padi`, `komodo-agung`, `orangutan-purba`, `leviathan`, `penjaga-api-biru`, `kapal-majapahit`.

## 2D art: Rodin GPT Image Flare

Portraits and key art are generated with `GenerateImageWithGptImageFlare`. Every prompt uses the same art direction:

- Majapahit andesite, moss and gold leaf for the myth.
- Thin cyan circuitry for GARUDA.
- Real Indonesian landscapes.
- A tiny human figure for scale.

## 3D models: Rodin and Blender

- Detailed models (Arka, the drone, guardian heads) were first tried with Hunyuan 3D (`Generate3DFrom*WithHunyuan`). When Hunyuan failed, they fall back to Rodin 3D (`Generate3DFromPrompt` / `Generate3DFromImagePath`).
- In this build, all 10 Hunyuan jobs failed on Replicate, so every shipped model is from Rodin 3D. The mobile budget is:
  - Arka: 34k tris, 1.5 MB.
  - Drone: 15k tris, 0.6 MB.
  - Heads: 24k tris, about 1 MB each.

  Textures are 1K JPEG inside each GLB. The 2K sources are kept in `assets/sources/blender/hires/`, and the prompts are recorded in `assets/sources/rodin/prompts.md`.
- Blender (through Blender MCP) is used to clean up, scale, rig and animate. It exports GLB with the clip names above.
- **Full bodies** (`process_bodies.py`):
  1. `dotnet run tools/rigdump/rigdump.cs -- assets/sources/rodin/rigs.json` writes every rig's rest-pose part matrices.
  2. The script fits each Rodin body (`assets/sources/rodin/bodies/<id>.glb`) onto the rig's rest bounds. Yaw 0 is the default, because Rodin models face front; a quarter turn is tried only when the long axis disagrees.
  3. It decimates the mesh and gives every face to the part box nearest its centre. A part left with no faces takes the faces inside its own box.
  4. It stretches the body toward the collider boxes, so Arka grips a visible surface. Each part's stretch (scale clamped to 0.6–1.6, 75% of the way) is blended over the whole mesh by distance to each box. That keeps the seams between parts closed. Per-guardian fixes live here too: `MATTE` raises the roughness floor of a model that came out too glossy (the Ijen warden).
  5. It writes one `part-<i>` object per part, with vertices in that part's pivot frame.
  6. In game, each part node is moved under its pivot and the sculpted box mesh is detached. The box node stays, because cores hang off it and the box is still the climbing collider.
  7. Low quality loads only the LOD. Higher qualities swap to the LOD beyond about three guardian heights.
- Without a body model, a head GLB is rotated to face +Z and uniformly scaled into its head box. The procedural head block shrinks to 82% and stays as the collision-matching core of the head.

## Audio: AudioForge

`tools/NusantaraColossi.AudioForge` synthesises the whole soundtrack offline and deterministically. The output is 32 kHz mono 16-bit WAV.

- **Gamelan voices.** They are modelled from inharmonic partials:
  - Saron: bars at about 1 : 2.76 : 5.4 : 8.93.
  - Bonang: kettle gong with 1.5× and 2× modes.
  - Gong ageng: slow *ombak* beating.
  - Kendang and taiko: pitch-swept membranes plus a slap.

  Everything is tuned to a stretched **pelog** scale.
- **Music.** `music-title` (ceremonial), `music-explore` (sparse), and `music-battle` with `music-rage` (132 bpm, layered), plus `music-victory`. In-game, explore, battle and rage play together and crossfade by fight intensity, so a transition never restarts a loop.
- **Ambience.** Wind, rain, sea, jungle, city and fire beds, made loopable by crossfading the tail into the head.
- **Effects.** Footsteps, climbing, stab, core hit, resonance and destroy, stomp, roar, sweep, charge, collapse, scan, hack, decoy, gust, thunder, heartbeat and UI sounds.
- **Mastering.** A Schroeder reverb (the "stone temple"), then normalisation with soft tanh limiting.
