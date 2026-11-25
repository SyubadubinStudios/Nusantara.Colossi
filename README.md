# Nusantara Colossi

**English** · [Bahasa Indonesia](#bahasa-indonesia)

> *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*
> *Made by Ariana Mischa Fadhila of Syubadubin Studios*

![Title screen](docs/images/title.png)

An action-adventure boss hunter set in an alternate Indonesia, 2045. The national AI network **GARUDA NET** has woken eight ancient Guardians across the archipelago. You play **Arka**, a former AI engineer and drone pilot. You climb the Guardians themselves, read the pulse of their cores, and lay them to rest one at a time with your drone, GARUDA.

Built with **.NET 10**, **Avalonia UI** and **Three.Net** (a Rust/wgpu renderer), for **Windows, macOS, Linux and Android**.

| | |
|---|---|
| ![Archipelago map](docs/images/map.png) | ![Tower Titan, Jakarta](docs/images/hunt-jakarta.png) |
| ![Sky Buffalo, Bromo](docs/images/hunt-bromo.png) | ![Blue Fire Warden, Ijen](docs/images/hunt-ijen.png) |
| ![Inscription archive](docs/images/codex.png) | ![Skills](docs/images/skills.png) |

## What you do

- **Climb living giants.** Every guardian is an articulated body you can grip, climb, stand on and cross between limbs. Grip stamina drains while you hang. Guardians try to shake you off and warn you just before they do.
- **Read the heartbeat.** Each core beats. Hold the stab, then release it on the pulse for a **resonance** strike that deals ×1.5 damage. The HUD's kawung ring and the audio heartbeat both keep time.
- **Work with GARUDA.** **Scan** to break a core's ancient seal for a few seconds. **Hack** to stun mechanical guardians and open plated cores. **Deploy a hologram decoy** to pull the guardian away. These unlock as you progress.
- **Eight regions, eight guardians**, each with its own hazard:

  | Region | Guardian | Hazard |
  |---|---|---|
  | Bromo | Sky Buffalo | — |
  | Jakarta | Tower Titan | rain on glass |
  | Bali | Rice Goddess | — |
  | Flores | Great Komodo | heat |
  | Kalimantan | Primeval Orangutan | rain |
  | Raja Ampat | Leviathan | — |
  | Ijen | Blue Fire Warden | toxic sulfur gas |
  | Banda Sea | Majapahit Ship | storm gusts |

- **Rage phases, ranks and Legend mode.** Guardians grow faster as they weaken. Each hunt earns an S/A/B/C rank. Legend mode makes guardians faster and stronger and removes the core markers.
- **Light RPG.** Three skill trees (Explorer, Engineer, Survivalist) with three tiers each. You earn points from first victories, S ranks and Legend clears.
- **2–4 player co-op** over LAN or direct IP. The host simulates the guardian, and every hunter climbs with their own local control.
- **Fully bilingual**: Bahasa Indonesia and English, switchable at any time.

## Quick start

Requirements: .NET SDK 10.0.4xx and a GPU with DirectX 12, Vulkan or Metal.

```bash
dotnet run --project src/NusantaraColossi.Desktop -c Release
```

| Command | Purpose |
|---|---|
| `dotnet build NusantaraColossi.slnx` | Build everything, including the Android head when its workload is installed |
| `dotnet test` | Run the 54 simulation tests |
| `dotnet run --project tools/NusantaraColossi.AudioForge` | Regenerate all music and sound effects into `assets/audio` |
| `dotnet run --project src/NusantaraColossi.Desktop -- --screenshot docs/images --lang en` | Capture the documentation screenshots |
| `dotnet build src/NusantaraColossi.Android -c Release` | Build the Android APK (needs the .NET Android workload) |

### Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Camera | Right-drag or arrow keys | Right stick |
| Grip / climb, sprint | Hold Shift | Hold RT |
| Jump | Space | A |
| Dodge | Ctrl | B |
| Stab (hold to charge) | Left click / F | X |
| Lock view on guardian | Tab | Y |
| Scan · Hack · Decoy | Q · E · R | LB · RB · LT |
| Pause | Esc | Start |

On Android, the left half of the screen is a floating stick and the right half controls the look. The thumb buttons are Grip, Stab, Jump, Dodge and the three GARUDA modules.

## Documentation

| | English | Bahasa Indonesia |
|---|---|---|
| Player guide | [docs/en/player-guide.md](docs/en/player-guide.md) | [docs/id/panduan-pemain.md](docs/id/panduan-pemain.md) |
| Game design | [docs/en/game-design.md](docs/en/game-design.md) | [docs/id/desain-game.md](docs/id/desain-game.md) |
| Architecture | [docs/en/architecture.md](docs/en/architecture.md) | [docs/id/arsitektur.md](docs/id/arsitektur.md) |
| Building & platforms | [docs/en/building.md](docs/en/building.md) | [docs/id/build.md](docs/id/build.md) |
| Assets & audio pipeline | [docs/en/assets.md](docs/en/assets.md) | [docs/id/aset.md](docs/id/aset.md) |
| Marketing kit (social posts) | [docs/marketing/posts-en.md](docs/marketing/posts-en.md) | [docs/marketing/posts-id.md](docs/marketing/posts-id.md) |

See also [PLAN.md](PLAN.md) for the roadmap and [Progress.md](Progress.md) for development tracking.

## Project layout

```
src/NusantaraColossi.Core      Engine-free simulation: guardians, climbing, combat, co-op protocol, saves
src/NusantaraColossi.Game      Avalonia UI + Three.Net rendering, audio, input, networking (shared by all heads)
src/NusantaraColossi.Desktop   Windows / macOS / Linux head
src/NusantaraColossi.Android   Android head
tests/NusantaraColossi.Core.Tests   xUnit tests for the simulation
tools/NusantaraColossi.AudioForge   Offline gamelan-based music and SFX synthesiser
assets/                        Models, art, audio (runtime files)
```

---

## Bahasa Indonesia

[English](#nusantara-colossi) · **Bahasa Indonesia**

> *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*

Game aksi-petualangan pemburu raksasa berlatar Indonesia alternatif tahun 2045. Jaringan AI nasional **GARUDA NET** membangunkan delapan Penjaga kuno di seluruh Nusantara. Kamu adalah **Arka**, mantan insinyur AI dan pilot drone. Kamu memanjat tubuh para Penjaga, membaca denyut inti mereka, lalu menenangkan mereka satu per satu bersama drone GARUDA.

Dibuat dengan **.NET 10**, **Avalonia UI**, dan **Three.Net** (renderer Rust/wgpu) untuk **Windows, macOS, Linux, dan Android**.

| | |
|---|---|
| ![Layar judul](docs/images/title-id.png) | ![Peta Nusantara](docs/images/map-id.png) |
| ![Arsip prasasti](docs/images/codex-id.png) | ![Keahlian](docs/images/skills-id.png) |

### Yang kamu lakukan

- **Memanjat raksasa hidup.** Setiap Penjaga adalah tubuh bersendi. Kamu bisa menggenggamnya, memanjatnya, berdiri di atasnya, dan berpindah antaranggota tubuhnya. Stamina genggaman terkuras saat bergantung. Penjaga akan berusaha mengguncangmu lepas, dan selalu ada peringatan sesaat sebelumnya.
- **Membaca detak jantung.** Setiap inti berdenyut. Tahan tusukan, lalu lepaskan tepat pada denyutnya untuk serangan **resonansi** dengan kerusakan ×1,5. Cincin kawung di HUD dan detak jantung di audio sama-sama menandai iramanya.
- **Bekerja sama dengan GARUDA.** **Pindai** untuk membuka segel kuno inti selama beberapa detik. **Retas** untuk melumpuhkan Penjaga mekanis dan membuka inti berpelat. **Pasang umpan hologram** untuk memancing Penjaga menjauh. Ketiganya terbuka seiring kemajuanmu.
- **Delapan wilayah, delapan Penjaga**, masing-masing dengan bahayanya sendiri:

  | Wilayah | Penjaga | Bahaya |
  |---|---|---|
  | Bromo | Kerbau Langit | — |
  | Jakarta | Gedung Titan | hujan di kaca |
  | Bali | Dewi Padi | — |
  | Flores | Komodo Agung | panas |
  | Kalimantan | Orangutan Purba | hujan |
  | Raja Ampat | Leviathan Nusantara | — |
  | Ijen | Penjaga Api Biru | gas belerang |
  | Laut Banda | Kapal Majapahit | hembusan badai |

- **Fase amukan, peringkat, dan Mode Legenda.** Penjaga makin cepat ketika melemah. Setiap perburuan mendapat peringkat S/A/B/C. Mode Legenda membuat Penjaga lebih cepat dan lebih kuat, tanpa penanda inti.
- **RPG ringan.** Tiga pohon keahlian (Penjelajah, Insinyur, Penyintas), masing-masing tiga tingkat. Poin didapat dari kemenangan pertama, peringkat S, dan Mode Legenda.
- **Co-op 2–4 pemain** lewat LAN atau IP langsung. Host menjalankan simulasi Penjaga, dan setiap pemburu memanjat dengan kendali lokalnya sendiri.
- **Dua bahasa penuh**: Bahasa Indonesia dan English, bisa diganti kapan saja.

### Mulai cepat

Kebutuhan: .NET SDK 10.0.4xx dan GPU dengan DirectX 12, Vulkan, atau Metal.

```bash
dotnet run --project src/NusantaraColossi.Desktop -c Release
```

| Perintah | Fungsi |
|---|---|
| `dotnet build NusantaraColossi.slnx` | Build semua proyek, termasuk Android jika workload-nya terpasang |
| `dotnet test` | Jalankan 54 tes simulasi |
| `dotnet run --project tools/NusantaraColossi.AudioForge` | Buat ulang semua musik dan efek suara ke `assets/audio` |
| `dotnet run --project src/NusantaraColossi.Desktop -- --screenshot docs/images --lang id` | Ambil tangkapan layar untuk dokumentasi |
| `dotnet build src/NusantaraColossi.Android -c Release` | Build APK Android (butuh workload .NET Android) |

### Kontrol

| Aksi | Keyboard / mouse | Gamepad |
|---|---|---|
| Bergerak | WASD | Stik kiri |
| Kamera | Seret klik kanan atau tombol panah | Stik kanan |
| Genggam / panjat, lari | Tahan Shift | Tahan RT |
| Lompat | Spasi | A |
| Menghindar | Ctrl | B |
| Tusuk (tahan untuk mengisi) | Klik kiri / F | X |
| Kunci pandangan ke Penjaga | Tab | Y |
| Pindai · Retas · Umpan | Q · E · R | LB · RB · LT |
| Jeda | Esc | Start |

Di Android, separuh kiri layar berfungsi sebagai stik mengambang dan separuh kanan untuk memutar pandangan. Tombol jempol: Genggam, Tusuk, Lompat, Menghindar, dan tiga modul GARUDA.

### Dokumentasi

[Panduan pemain](docs/id/panduan-pemain.md) · [Desain game](docs/id/desain-game.md) · [Arsitektur](docs/id/arsitektur.md) · [Build & platform](docs/id/build.md) · [Pipeline aset & audio](docs/id/aset.md) · [Paket marketing](docs/marketing/README.md#bahasa-indonesia) · [PLAN.md](PLAN.md) · [Progress.md](Progress.md)
