# Arsitektur

[English](../en/architecture.md) · *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*

## Lapisan

```
┌──────────────────────────────────────────────────────────────────┐
│ Head: NusantaraColossi.Desktop (Win/macOS/Linux)                 │
│       NusantaraColossi.Android                                   │
├──────────────────────────────────────────────────────────────────┤
│ NusantaraColossi.Game                                            │
│  UI      App · Shell (frame loop) · Screens · kontrol HUD        │
│          · InputMapper                                           │
│  Render  WorldRenderer (+Environment/Colossus/Effects)           │
│          · CameraRig · shader WGSL                               │
│  Audio   GameAudio (musik adaptif, SFX spasial, suara)           │
│  Net     CoopSession (ThreeNet UDP NetworkSession)               │
├──────────────────────────────────────────────────────────────────┤
│ NusantaraColossi.Core  (tanpa referensi engine/UI, aman AOT)     │
│  Regions · blueprint Colossi · ColossusRig · ClimbWorld          │
│  · Player · AI Colossus · Encounter · Progression/Save           │
│  · protokol Coop                                                 │
└──────────────────────────────────────────────────────────────────┘
          Three.Net (inti Rust wgpu): DX12 · Vulkan · Metal · GLES
```

Alur runtime: **UI → layanan game → simulasi (Core) → rendering (Three.Net)**. Simulasi tidak pernah mereferensikan rendering. Renderer membaca status simulasi dan mengonsumsi `GameEvent`.

## Simulasi (Core)

- **Penjaga adalah blueprint plus rig murni.** `ColossusBlueprint` adalah daftar kotak `PartBlueprint` (sendi, pusat, setengah ukuran, parameter langkah, dan bitmask sisi yang bisa dipanjat) ditambah inti dan penyetelan. `ColossusRig.Evaluate(blueprint, pose, Span<Matrix4x4>)` adalah fungsi murni dari `ColossusPose` yang ringkas ke matriks bagian. Matriks yang sama menggerakkan rendering, tabrakan panjat, dan co-op, sehingga snapshot pose mereproduksi Penjaga persis sama di setiap mesin. Ada tes yang memeriksa hal ini.
- **`ClimbWorld`** menjalankan gerak di permukaan kotak berorientasi: rintangan statis ditambah satu slot dinamis per bagian Penjaga. Kontak berupa (kotak, titik lokal, sisi). Gerakan diproyeksikan pada sisi, melipat melewati tepi, dan berpindah ke kotak yang bersentuhan. Sisi yang tertutup kotak lain dilewati. Sisi yang normalnya mengarah ke atas bisa dipijak.
- **`Encounter`** memegang satu perburuan: wilayah, Penjaga, pemain dan drone, bahaya, dan hasil akhir. `Step(inputs, dt)` berjalan tetap 60 Hz dan menulis `GameEvent` (langkah kaki, hantaman injakan, inti terkena, dan sebagainya) yang diubah lapisan presentasi menjadi VFX, audio, guncangan kamera, dan baris HUD.
- **`Profile` / `SaveStore`.** JSON dengan serialisasi hasil source generator. Penyimpanan atomik (file sementara lalu dipindahkan), dan file yang rusak disimpan sebagai `.corrupt`, tidak dibuang.

## Frame loop

`Shell` memegang satu `ThreeNetView` dengan `IsRendering = false` dan `DispatcherTimer` 8 ms. Setiap tik:

1. Kembali lebih awal jika jatah waktu FPS belum terpenuhi. Timer hanya memeriksa.
2. Baca gamepad. Di menu, D-pad memindahkan fokus.
3. `Screen.Tick(dt)`. Layar perburuan memakai akumulator langkah tetap (1/60 detik), membaca input per langkah, menjalankan `Encounter`, menyebarkan event, lalu memperbarui kamera dan `WorldRenderer`.
4. `GameAudio.Update` memperbarui pendengar, volume lapisan musik, dan sinkronisasi detak jantung.
5. `viewport.RenderFrame()`.
6. Saat berburu, **resolusi dinamis** menyetel `RenderScale` agar batas FPS tetap tercapai.

Hanya ada satu dunia 3D pada satu waktu. Menu menampilkan dunia "pameran" (Penjaga yang sedang diam), dan perburuan menggantikannya. Ini menjaga memori GPU tetap stabil di ponsel.

## Rendering

- **Bagian Penjaga** adalah mesh `MeshFactory.SculptedBox`: kotak membulat dengan perpindahan noise, tetap di dalam kotak tabrakannya, dengan UV dalam meter. Jika `assets/models/heads/<id>.glb` ada, bagian kepala memakai GLB itu, diskalakan agar pas dengan kotak kepala.
- **Shader permukaan WGSL** (`Shaders.cs`) mengubah permukaan PBR sebelum pencahayaan, sehingga bayangan, kabut, dan tone mapping tetap berlaku. Shader Penjaga bekerja di ruang UV: panel ukir, motif kawung, lumut, bulu, kayu, kaca, dan cahaya sambungan yang berdenyut dari C#. Shader terrain bekerja di ruang dunia, dengan detail per bioma seperti jalan dan genangan, sawah tergenang, kerak belerang, dan garis pantai. Kualitas Hemat membuang dua oktaf noise.
- **Efek.** Vegetasi instanced, emitter partikel GPU (percikan, debu, cairan inti, cipratan, bara, asap), `FireEffect`, `WaterSurface`, dan `CloudLayer` dari Three.Net, serta petir saat badai.
- **Cahaya dibatasi.** Cahaya inti hanya ada saat inti terbuka atau berkilat. Cahaya drone hanya ada untuk pemain aktif. Cahaya api, jalan, dan pura mengikuti tingkat kualitas.

## Co-op

| Pesan | Arah | Keandalan | Isi |
|---|---|---|---|
| Hello → Welcome / Full | klien ↔ host | andal | versi protokol, nama → slot, Penjaga, legenda, kemampuan, seed |
| PlayerState | klien → host, 20 Hz | tak andal | pemanjat milik klien |
| Snapshot | host → semua, 20 Hz | tak andal | waktu/fase, pose dan status Penjaga, inti, cuaca, semua pemain |
| Stab, Ability | klien → host | andal | diselesaikan di host (cek segel, pelat, resonansi) |
| Hurt | host → pemilik | andal | luka dari Penjaga pada pemanjat jarak jauh |
| Event | host → semua | andal | event Penjaga untuk audio dan VFX |

Klien mensimulasikan pemanjatnya sendiri tanpa tambahan latensi dan menjalankan rig Penjaga dari snapshot. Karena rig bersifat deterministik, tabrakan panjat di klien sama dengan di host. Format paket ada di `Core/Coop.cs`, dengan field little-endian dan string UTF-8 berawalan panjang. Paket yang rusak dibuang.

## Catatan performa

- Jalur panas (`Step`, `ColossusRig.Evaluate`, `ClimbWorld.Move`) tidak mengalokasikan memori per frame. Semuanya memakai span dan array yang sudah dialokasikan.
- Saat pertama kali dijalankan, renderer offscreen sekali pakai membaca kelas GPU. GPU terintegrasi mulai di kualitas Hemat.
- Di Intel UHD 620 (terintegrasi), perburuan berjalan sekitar 30–65 FPS pada kualitas Hemat dengan resolusi dinamis. Variabel lingkungan `NC_PROFILE=1` mencetak waktu per frame: tik, render, draw call, segitiga, cahaya, dan ms GPU.
