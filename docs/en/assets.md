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
| `assets/models/heads/<id>.glb` | Guardian heads | `WorldRenderer.Colossus`, fitted to the head part |
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
- In-game, a head GLB is rotated to face +Z and uniformly scaled into its head box. The procedural head block shrinks to 82% and stays as the collision-matching core of the head.

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
