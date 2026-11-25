# Nusantara Colossi — Assets / Aset

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios (Made by Ariana Mischa Fadhila of Syubadubin Studios).

**English** | [Bahasa Indonesia](#bahasa-indonesia)

All art is original to Nusantara Colossi. Full generation prompts are in
[`sources/rodin/prompts.md`](sources/rodin/prompts.md). The Blender pipeline is in
[`sources/blender/`](sources/blender/) and runs headless:
`blender -b --factory-startup -P assets/sources/blender/process_static.py` and `... -P process_arka.py`.

The game loads assets by the exact paths below. If a file is missing, it falls back to procedural geometry.

## Conventions

- glTF 2.0 binary (`.glb`), +Y up, **front faces −Z**, metres.
- Arka and the drone stand on Y = 0, centred on X/Z. Heads are centred on X/Z with the bottom at Y = 0 and are **1.0 m tall**, so the game scales them.
- Mobile budget: textures are 1024 px and stored as **JPEG** inside the GLB (base color, metallic-roughness and normal maps). Every game GLB is ≤ 1.5 MB.
- Full-resolution versions are kept: `sources/blender/*.blend` holds 2048 px textures, and `sources/blender/hires/*.glb` holds 2048 px PNG exports with the same geometry and orientation.

## 2D art — Rodin `GenerateImageWithGptImageFlare`

| File | Size | Quality | Prompt ref. |
|---|---|---|---|
| `art/keyart.png` | 1536×1024 | high | prompts.md › keyart |
| `art/arka.png` | 1024×1536 | medium | › arka |
| `art/icon.png` | 1024×1024 | medium | › icon |
| `art/colossi/gedung-titan.png` | 1536×1024 | medium | › gedung-titan |
| `art/colossi/kerbau-langit.png` | 1536×1024 | medium | › kerbau-langit |
| `art/colossi/dewi-padi.png` | 1536×1024 | medium | › dewi-padi |
| `art/colossi/leviathan.png` | 1536×1024 | medium | › leviathan |
| `art/colossi/orangutan-purba.png` | 1536×1024 | medium | › orangutan-purba |
| `art/colossi/komodo-agung.png` | 1536×1024 | medium | › komodo-agung |
| `art/colossi/penjaga-api-biru.png` | 1536×1024 | medium | › penjaga-api-biru |
| `art/colossi/kapal-majapahit.png` | 1536×1024 | medium | › kapal-majapahit |

(`art/icon.ico` was created by another part of the pipeline, not by this asset pass.)

## 3D models — generated with **Rodin 3D** (`Generate3DFromPrompt`)

Hunyuan 3D was tried first, and **all 10 jobs failed** (`ResourceInsufficient`). Following the user's instruction, every model was regenerated once with Rodin 3D. **None of the shipped models came from Hunyuan.**

Bounds are in glTF space (X, Y, Z), in metres, as measured by re-importing each exported GLB.

| File | Generator | Blender step | Tris (raw → final) | Bounds min → max | Size |
|---|---|---|---|---|---|
| `models/arka.glb` | Rodin 3D | `process_arka.py` | 34,075 → 34,075 | (−0.82, 0, −0.185) → (0.82, 1.80, 0.185) | 1.47 MB |
| `models/garuda-drone.glb` | Rodin 3D | `process_static.py` | 35,525 → 14,549 | (−0.30, 0, −0.325) → (0.30, 0.211, 0.325) | 0.64 MB |
| `models/heads/gedung-titan.glb` | Rodin 3D | `process_static.py` | 36,289 → 24,249 | (−0.277, 0, −0.311) → (0.277, 1, 0.311) | 0.84 MB |
| `models/heads/kerbau-langit.glb` | Rodin 3D | `process_static.py` | 32,610 → 24,250 | (−0.506, 0, −0.345) → (0.506, 1, 0.345) | 0.90 MB |
| `models/heads/dewi-padi.glb` | Rodin 3D | `process_static.py` | 37,848 → 24,250 | (−0.41, 0, −0.324) → (0.41, 1, 0.324) | 0.99 MB |
| `models/heads/leviathan.glb` | Rodin 3D | `process_static.py` | 39,702 → 24,250 | (−0.497, 0, −0.298) → (0.497, 1, 0.298) | 1.03 MB |
| `models/heads/orangutan-purba.glb` | Rodin 3D | `process_static.py` | 32,988 → 24,250 | (−0.352, 0, −0.365) → (0.352, 1, 0.365) | 0.97 MB |
| `models/heads/komodo-agung.glb` | Rodin 3D | `process_static.py` | 33,252 → 24,250 | (−0.486, 0, −0.512) → (0.486, 1, 0.512) | 0.97 MB |
| `models/heads/penjaga-api-biru.glb` | Rodin 3D | `process_static.py` | 33,428 → 24,250 | (−0.349, 0, −0.43) → (0.349, 1, 0.43) | 0.91 MB |
| `models/heads/kapal-majapahit.glb` | Rodin 3D | `process_static.py` | 33,890 → 24,250 | (−0.407, 0, −0.166) → (0.407, 1, 0.166) | 0.95 MB |

What the Blender processing does:

1. Imports the GLB, joins the meshes and applies transforms.
2. Rotates the model 180° about the vertical axis, because Rodin outputs face glTF +Z. The drone first gets a −32° pitch to level it, because Rodin delivered it nose-up.
3. Applies a collapse decimate where the mesh is over budget.
4. Normalises size: Arka is 1.8 m tall, the drone is 0.6 m wide, and heads are 1.0 m tall. It then centres the model.
5. Saves the `.blend`, exports the hi-res GLB, downscales textures to 1024 px and exports the JPEG GLB.
6. Validates each GLB by re-importing it into an empty scene (`common.validate`).

The heads' front orientation was checked in rendered previews. Some heads are relief-like and have flat backs (komodo-agung, leviathan, kapal-majapahit). That is fine for a front-facing reveal shot, but they are not fully sculpted in the round.

### Arka rig and animations

- **Rig:** a simple 17-bone humanoid named `ArkaRig`. Bones: `hips`, `spine`, `chest`, `neck`, `head`, `upper_arm.L/R`, `lower_arm.L/R`, `hand.L/R`, `upper_leg.L/R`, `lower_leg.L/R`, `foot.L/R`. The bones are fitted to the mesh, and the arm line is fitted from the vertices.
- **Rest pose:** Rodin delivered a shallow **A-pose**, with arms about 19.6° below horizontal. The rig's rest pose matches the mesh, and the actions compensate for it.
- **Skinning:** automatic (bone-heat) weights succeeded, leaving 0.2% of vertices unweighted. Those 40 vertices were assigned to the nearest bone. The exporter keeps the 4 strongest influences per vertex.
- **Animations** were authored procedurally at 30 fps and exported as glTF animations (`export_animation_mode='ACTIONS'`). Re-importing the GLB confirms all six:

| Name | Length | Type | Notes |
|---|---|---|---|
| `Idle` | 2.0 s | loop | breathing, slight arm sway |
| `Run` | 0.8 s | loop | in place, arms opposite legs, forward lean |
| `Climb` | 1.0 s | loop | hand-over-hand, facing the wall (−Z), legs stepping |
| `Jump` | 0.6 s | one-shot | crouch → launch → tuck → land-ready; hips rise and fall (root offset on `hips`) |
| `Stab` | 0.7 s | one-shot | two-handed overhead wind-up, then a downward stab with forward lean |
| `Hang` | 1.5 s | loop | both arms overhead gripping, legs dangling with sway |

All motion is in place: the hips bob, but there is no root translation. The animations are functional placeholders authored in code, not motion capture, so expect to polish them later.

## Voice — Rodin `GenerateVoiceWithElevenLabsV3` (voice "Aria", language `id`)

The ElevenLabs MP3s were converted with ffmpeg to 44.1 kHz, mono, 16-bit PCM WAV.

| File | Line | Duration |
|---|---|---|
| `audio/voice/intro.wav` | Arka, sinyal GARUDA NET mendeteksi kebangkitan Penjaga Nusantara. Kita harus bergerak. | 6.51 s |
| `audio/voice/scan.wav` | Pemindaian selesai. Titik inti terdeteksi. | 3.29 s |
| `audio/voice/grip.wav` | Stamina genggaman menipis! Cari pijakan! | 3.76 s |
| `audio/voice/core.wav` | Inti hancur. Penjaga itu melemah. | 3.21 s |
| `audio/voice/victory.wav` | Penjaga telah tenang. Sejarah Nusantara terbuka kembali. | 4.81 s |
| `audio/voice/enrage.wav` | Hati-hati! Ia mengamuk! | 1.93 s |
| `audio/voice/flood.wav`, `dive.wav`, `breath.wav`, `immune.wav`, `finale.wav` | The five signature/finale lines (Indonesian, text in `Loc.cs`) | 4–5 s |
| `audio/voice/<line>-en.wav` | English recordings of all 11 lines (voice "Aria", language `en`) | 2–6 s |

`GameAudio.SayLine` plays `<line>-<lang>.wav` first and falls back to the Indonesian `<line>.wav`.

(The other files in `audio/`, such as the music, ambience and SFX, come from a different part of the pipeline.)

## Failures and caveats

- **Hunyuan 3D:** all 10 generations failed (`ResourceInsufficient`). They were replaced by Rodin 3D and not retried on Hunyuan.
- **Arka pose:** Rodin returned an A-pose, not the T-pose that was requested. The rig handles it.
- **Blender MCP live connection:** the live session's MCP addon was disconnected during processing, because `read_factory_settings` unloaded it. All processing was therefore done with headless Blender 5.2 using the scripts in `sources/blender/`. Visual checks were done with headless workbench renders instead of `look`. Re-enable the MCP addon in the running Blender to use the live tools again.

### Pipeline scripts (`sources/blender/`)

| Script | Purpose |
|---|---|
| `common.py` | import, join, normalise, decimate, texture downscale, export and validate helpers |
| `process_static.py` | builds the drone and the 8 heads |
| `process_arka.py` | normalises Arka, builds the rig and weights, authors the 6 actions and exports |
| `probe_raw.py`, `drone_tilt_probe.py`, `preview.py`, `dump_arka_rig.py`, `render_arka_actions.py` | QA and analysis helpers |

---

## Bahasa Indonesia

Semua karya seni adalah orisinal untuk Nusantara Colossi. Prompt lengkap ada di
[`sources/rodin/prompts.md`](sources/rodin/prompts.md). Pipeline Blender ada di
[`sources/blender/`](sources/blender/) dan dijalankan tanpa GUI (headless):
`blender -b --factory-startup -P assets/sources/blender/process_static.py` dan `... -P process_arka.py`.

Game memuat aset dari path persis di atas. Jika sebuah file tidak ada, game memakai geometri prosedural sebagai gantinya.

### Konvensi
- glTF 2.0 biner (`.glb`), sumbu +Y ke atas, **bagian depan menghadap −Z**, satuan meter.
- Arka dan drone berdiri di Y = 0, di tengah sumbu X/Z. Kepala colossus di tengah X/Z, alasnya di Y = 0, dan **tingginya 1,0 m**, jadi game yang menskalakannya.
- Anggaran mobile: tekstur 1024 px dalam format **JPEG** di dalam GLB. Setiap GLB game ≤ 1,5 MB.
- Versi resolusi penuh tetap disimpan: `sources/blender/*.blend` (tekstur 2048 px) dan `sources/blender/hires/*.glb` (ekspor PNG 2048 px dengan geometri dan orientasi yang sama).

### Ringkasan aset
- **Seni 2D (GPT Image Flare):** key art, potret Arka, ikon aplikasi, dan 8 gambar establishing shot colossus. Lihat tabel di bagian English untuk ukuran dan kualitasnya.
- **Model 3D (Rodin 3D, bukan Hunyuan):** `arka.glb` (34.075 segitiga, tinggi 1,8 m, ber-rig), `garuda-drone.glb` (14.549 segitiga, lebar 0,6 m), dan 8 kepala di `models/heads/` (masing-masing sekitar 24.250 segitiga, tinggi 1,0 m). Jumlah segitiga, batas (bounds) dan ukuran file per model ada di tabel bagian English.
- **Rig Arka:** humanoid sederhana dengan 17 tulang. Bobot otomatis (bone heat) berhasil; 0,2% vertex tanpa bobot diberikan ke tulang terdekat. Mesh dari Rodin berpose A (lengan sekitar 19,6° di bawah horizontal), dan animasi mengompensasinya.
- **Animasi (glTF):** `Idle` (2,0 s, loop), `Run` (0,8 s, loop), `Climb` (1,0 s, loop, menghadap dinding −Z), `Jump` (0,6 s), `Stab` (0,7 s, tusukan dua tangan ke bawah), `Hang` (1,5 s, loop). Semua animasi di tempat (in place), tanpa translasi root. Animasi ini dibuat lewat kode sebagai placeholder fungsional, bukan motion capture.
- **Suara (ElevenLabs v3, suara "Aria", bahasa `id`):** `intro`, `scan`, `grip`, `core`, `victory`, `enrage` di `audio/voice/`, dalam format WAV 44,1 kHz mono 16-bit.

### Kegagalan dan catatan
- **Hunyuan 3D:** ke-10 generasi gagal (`ResourceInsufficient`). Semuanya diganti dengan Rodin 3D dan tidak dicoba ulang dengan Hunyuan.
- **Pose Arka:** Rodin menghasilkan pose A, bukan pose T seperti yang diminta. Rig sudah menyesuaikannya.
- **Koneksi live Blender MCP:** addon MCP di sesi Blender yang berjalan terputus karena `read_factory_settings`. Semua pemrosesan dilakukan dengan Blender 5.2 headless. Aktifkan kembali addon MCP di Blender agar alat live bisa dipakai lagi.
- Beberapa kepala berbentuk relief dengan bagian belakang datar (komodo-agung, leviathan, kapal-majapahit). Ini cocok untuk tampilan dari depan, tetapi kepala-kepala itu tidak dipahat penuh ke segala sisi.
