# Pipeline aset dan audio

[English](../en/assets.md) · *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*

Game tidak pernah bergantung pada keberadaan aset. Setiap model punya cadangan prosedural, dan audio yang hilang hanya berarti senyap. Aset memperindah tampilan, tetapi tidak pernah menghalangi build.

## Kontrak folder

| Path | Isi | Dipakai oleh |
|---|---|---|
| `assets/art/keyart.png`, `arka.png` | Key art dan konsep Arka | Dokumentasi |
| `assets/art/colossi/<id>.png` | Potret Penjaga | Panel peta, arsip prasasti |
| `assets/art/icon.png`, `icon.ico` | Ikon aplikasi | Jendela, exe, peluncur Android |
| `assets/models/arka.glb` | Arka, dengan klip opsional `Idle`, `Run`, `Climb`, `Jump`, `Stab`, `Hang` | `CharacterVisual` |
| `assets/models/garuda-drone.glb` | Drone GARUDA | `DroneVisual` |
| `assets/models/bodies/<id>.glb`, `bodies/lod1/<id>.glb` | Tubuh penuh Penjaga, dipotong per bagian rig (sekitar 35 ribu / 9 ribu segitiga) | `WorldRenderer.Colossus`: setiap node `part-<i>` dipasang di bawah pivot bagian i |
| `assets/models/heads/<id>.glb`, `heads/lod1/<id>.glb` | Kepala Penjaga (24 ribu / 6 ribu segitiga), hanya dipakai jika Penjaga tidak punya model tubuh | `WorldRenderer.Colossus`, disesuaikan dengan bagian kepala |
| `assets/audio/*.wav` | Musik, suasana, SFX | `GameAudio` |
| `assets/audio/voice/<baris>-<bahasa>.wav` | Rekaman suara GARUDA opsional (`intro-id.wav`, …) | `GameAudio.SayLine` |
| `assets/sources/{rodin,blender}` | File sumber, tidak dikirim | — |

Id Penjaga: `kerbau-langit`, `gedung-titan`, `dewi-padi`, `komodo-agung`, `orangutan-purba`, `leviathan`, `penjaga-api-biru`, `kapal-majapahit`.

## Seni 2D: Rodin GPT Image Flare

Potret dan key art dibuat dengan `GenerateImageWithGptImageFlare`. Setiap prompt memakai arahan seni yang sama:

- Andesit Majapahit, lumut, dan prada emas untuk sisi mitos.
- Sirkuit biru tipis untuk GARUDA.
- Lanskap Indonesia yang nyata.
- Sosok manusia kecil sebagai pembanding skala.

## Model 3D: Rodin dan Blender

- Model detail (Arka, drone, kepala Penjaga) pertama kali dicoba dengan Hunyuan 3D (`Generate3DFrom*WithHunyuan`). Jika Hunyuan gagal, pembuatannya beralih ke Rodin 3D (`Generate3DFromPrompt` / `Generate3DFromImagePath`).
- Di build ini, semua 10 job Hunyuan gagal di Replicate, jadi setiap model yang dikirim berasal dari Rodin 3D. Anggaran mobile-nya:
  - Arka: 34 ribu segitiga, 1,5 MB.
  - Drone: 15 ribu segitiga, 0,6 MB.
  - Kepala: 24 ribu segitiga, sekitar 1 MB per kepala.

  Teksturnya JPEG 1K di dalam setiap GLB. Sumber 2K disimpan di `assets/sources/blender/hires/`, dan prompt dicatat di `assets/sources/rodin/prompts.md`.
- Blender (lewat Blender MCP) dipakai untuk merapikan, menyetel skala, rigging, dan animasi. Ekspornya GLB dengan nama klip di atas.
- **Tubuh penuh** (`process_bodies.py`):
  1. `dotnet run tools/rigdump/rigdump.cs -- assets/sources/rodin/rigs.json` menulis matriks setiap bagian rig dalam pose diam.
  2. Skrip menyesuaikan setiap model tubuh Rodin (`assets/sources/rodin/bodies/<id>.glb`) ke batas pose diam rig. Yaw 0 menjadi bawaan karena model Rodin menghadap depan; putaran seperempat hanya dicoba jika sumbu panjangnya tidak cocok.
  3. Mesh di-decimate, lalu setiap face diberikan ke kotak bagian yang paling dekat dengan pusatnya. Bagian yang tidak kebagian face mengambil face di dalam kotaknya sendiri.
  4. Tubuh direntangkan mendekati kotak collider supaya Arka berpegangan pada permukaan yang terlihat. Rentangan tiap bagian (skala dibatasi 0,6–1,6, sejauh 75%) dicampur ke seluruh mesh menurut jarak ke tiap kotak, sehingga sambungan antarbagian tetap tertutup. Perbaikan per Penjaga juga ada di sini: `MATTE` menaikkan batas bawah kekasaran model yang terlalu mengilap (Penjaga Ijen).
  5. Skrip menulis satu objek `part-<i>` per bagian, dengan verteks di ruang pivot bagian itu.
  6. Di game, setiap node bagian dipindah ke bawah pivotnya dan mesh kotak pahatannya dilepas. Node kotak tetap ada, karena inti menempel di sana dan kotak tetap menjadi collider panjat.
  7. Kualitas Low hanya memuat LOD. Kualitas yang lebih tinggi beralih ke LOD di luar sekitar tiga kali tinggi Penjaga.
- Tanpa model tubuh, GLB kepala diputar agar menghadap +Z dan diskalakan seragam ke dalam kotak kepala. Balok kepala prosedural mengecil menjadi 82% dan tetap menjadi inti kepala yang cocok dengan tabrakan.

## Audio: AudioForge

`tools/NusantaraColossi.AudioForge` mensintesis seluruh soundtrack secara offline dan deterministik. Hasilnya WAV mono 16-bit 32 kHz.

- **Suara gamelan.** Dimodelkan dari parsial inharmonik:
  - Saron: bilah sekitar 1 : 2,76 : 5,4 : 8,93.
  - Bonang: gong pencon dengan mode 1,5× dan 2×.
  - Gong ageng: *ombak* yang berdenyut pelan.
  - Kendang dan taiko: membran dengan nada menurun plus tepukan.

  Semuanya ditala ke laras **pelog** yang sedikit direntang.
- **Musik.** `music-title` (seremonial), `music-explore` (lengang), serta `music-battle` dan `music-rage` (132 bpm, berlapis), ditambah `music-victory`. Dalam game, jelajah, tempur, dan amukan diputar bersamaan dan dicampur sesuai intensitas, jadi transisi tidak pernah mengulang loop.
- **Suasana.** Latar angin, hujan, laut, hutan, kota, dan api, dibuat bisa diulang tanpa sambungan dengan mencampurkan bagian akhir ke awal.
- **Efek.** Langkah, panjat, tusuk, inti terkena, resonansi dan hancur, injak, raung, sapu, terjang, runtuh, pindai, retas, umpan, hembusan, petir, detak jantung, dan suara UI.
- **Mastering.** Reverb Schroeder ("ruang candi batu"), lalu normalisasi dengan pembatas tanh yang lembut.
