# Build dan platform

[English](../en/building.md) · *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*

## Kebutuhan

- .NET SDK **10.0.4xx**. `global.json` mengunci 10.0.401 dengan `rollForward: latestFeature`.
- GPU dengan DirectX 12 (Windows), Vulkan (Linux/Android), atau Metal (macOS).
- Untuk Android: workload .NET Android (`dotnet workload install android`) dan Android SDK.

Inti native Three.Net sudah ada di paket `ThreeNet.Native` untuk win-x64, linux-x64, osx-x64/arm64, dan android-arm64/x64, jadi toolchain Rust tidak diperlukan.

## Desktop (Windows, macOS, Linux)

```bash
dotnet run --project src/NusantaraColossi.Desktop -c Release
# build mandiri (self-contained)
dotnet publish src/NusantaraColossi.Desktop -c Release -r win-x64   --self-contained
dotnet publish src/NusantaraColossi.Desktop -c Release -r linux-x64 --self-contained
dotnet publish src/NusantaraColossi.Desktop -c Release -r osx-arm64 --self-contained
```

Proyek desktop menyalin `assets/models`, `assets/audio`, `assets/art`, dan `assets/textures` ke samping file executable.

Opsi baris perintah:

| Opsi | Efek |
|---|---|
| `--screenshot <folder>` | Berkeliling judul → peta → arsip → keahlian → empat perburuan dengan autopilot, menyimpan PNG, lalu keluar |
| `--lang en\|id` | Bahasa untuk tur tangkapan layar |
| env `NC_PROFILE=1` | Mencetak waktu frame ke stderr setiap 2 detik |
| env `NC_Q=0\|1\|2` | Mengganti kualitas grafis untuk satu kali jalan |

## Pengemasan

Skrip di `build/` mem-publish head desktop secara mandiri (self-contained, pemain tidak perlu memasang .NET) lalu membungkusnya untuk tiap OS. Semua hasil ditulis ke `artifacts/` (diabaikan git): hasil publish mentah ke `artifacts/publish/<rid>/`, dan paket jadi ke `artifacts/packages/`.

| OS | Perintah | Hasil |
|---|---|---|
| Windows | `pwsh build/package-windows.ps1` | `NusantaraColossi-<versi>-win-x64.zip` |
| Linux | `bash build/package-linux.sh` | `NusantaraColossi-<versi>-x86_64.AppImage` jika `appimagetool` ada di `PATH` (atau `APPIMAGETOOL=/path/ke/alat`), jika tidak `NusantaraColossi-<versi>-linux-x64.tar.gz` berisi AppDir (ekstrak lalu jalankan `./AppRun`) |
| macOS | `bash build/package-macos.sh` | `NusantaraColossi-<versi>-macos-arm64.zip` dan `-macos-x64.zip`, masing-masing berisi `Nusantara Colossi.app` |

- **Versi:** diambil dari `<Version>` di `Directory.Build.props`.
- **Setiap paket** berisi file executable, inti native Three.Net (`threenet_core.dll` / `libthreenet_core.so` / `libthreenet_core.dylib`), native SkiaSharp/HarfBuzz, dan folder `assets/`. Skrip gagal jika salah satunya tidak ada.
- **AppDir Linux:** `AppRun`, `nusantara-colossi.desktop`, dan ikon dari `assets/art/icon.png`. Game-nya sendiri ada di `usr/bin/`.
- **Bundle macOS:**
  - Bundle id-nya `com.syubadubin.nusantaracolossi`, minimal macOS 13, dan baris hak ciptanya memuat kredit.
  - File game dan `assets/` berada di `Contents/MacOS`, karena game membaca aset di samping file executable-nya.
  - `.icns` dibuat dengan `iconutil`/`sips`, dan bundle ditandatangani ad-hoc dengan `codesign`, jika alat-alat itu tersedia. Setel `RIDS="osx-arm64"` untuk membuat satu arsitektur saja, atau `CODESIGN_IDENTITY` untuk menandatangani dengan identitas sungguhan.
  - Aplikasi bertanda tangan ad-hoc yang diunduh dari internet tetap dikarantina. Jalankan `xattr -dr com.apple.quarantine "Nusantara Colossi.app"` sekali, atau klik kanan → Open. Distribusi publik memerlukan Developer ID, hardened runtime, dan notarisasi (belum disiapkan).
- **Ukuran:** sekitar 210 MB setelah diekstrak (67 MB di antaranya aset), dan sekitar 120 MB dalam zip untuk Windows. Proyek desktop membuang ~100 MB file `.pdb` native SkiaSharp dari hasil publish.

### Integrasi berkelanjutan (CI)

`.github/workflows/ci.yml` berjalan pada setiap push dan pull request:

- **Windows, Ubuntu, dan macOS (Apple Silicon):** build, menjalankan tes, lalu menjalankan skrip pengemasan yang sesuai.
- **Uji asap (smoke check):**
  - Ketiga runner memeriksa susunan hasil publish (executable, inti native, `assets/`, dan bit executable di Unix).
  - Di macOS, `.app` juga diperiksa: `Info.plist`, bundle id, `.icns`, tanda tangan `codesign`, dan dylib arm64.
  - Di Linux, ada uji upaya-terbaik yang menjalankan game selama 25 detik di bawah Xvfb dengan Vulkan perangkat lunak dari Mesa (tidak menggagalkan build).
- **Artefak:** paket diunggah sebagai `NusantaraColossi-<rid>`, dan hasil tes sebagai `test-results-<rid>`.
- **Android:** job terpisah yang opsional (`continue-on-error`) memasang workload android dan mengunggah APK bertanda tangan.

## Android

```bash
dotnet build src/NusantaraColossi.Android -c Release
adb install src/NusantaraColossi.Android/bin/Release/net10.0-android/com.syubadubin.nusantaracolossi-Signed.apk
```

- Menargetkan API 26+, arm64 dan x64, mode lanskap saja.
- Aset runtime dikemas sebagai `AndroidAsset`. Pada peluncuran pertama setiap versi aplikasi, aset diekstrak ke penyimpanan aplikasi, karena loader Three.Net membutuhkan path file.
- Kontrol sentuh, kualitas Hemat, dan skala render 0,8 aktif secara bawaan.
- Build Release dikompilasi AOT (`RunAOTCompilation`): aplikasi lebih cepat terbuka, dan kerja JIT di CPU ponsel berkurang.
- Bundle Play Store (AAB), ditandatangani dengan upload key Anda. Jangan masukkan keystore dan kata sandinya ke git:

  ```bash
  dotnet publish src/NusantaraColossi.Android -c Release -p:StoreBuild=true     -p:AndroidSigningKeyStore=upload.keystore -p:AndroidSigningKeyAlias=upload     -p:AndroidSigningKeyPass=env:NC_KEY_PASS -p:AndroidSigningStorePass=env:NC_STORE_PASS
  ```

- Bawaan mobile: kualitas Low, skala render 0,55, dan resolusi dinamis di semua layar (turun sampai 0,35). Kepala Penjaga yang jauh memakai LOD 6 ribu segitiga (`assets/models/heads/lod1`), dan kualitas Low hanya memuat LOD itu.
- Profiling di perangkat: `adb shell touch /sdcard/Android/data/com.syubadubin.nusantaracolossi/files/profile` mencatat waktu frame dan event sentuh ke logcat (tag `NC`).

> Status: sudah diuji di satu tablet kelas bawah (Mali-G51). Aplikasi terbuka dan menu merespons keyboard (sentuhan dan keluaran audio belum dipastikan), tetapi hanya sekitar 3 FPS. Butuh GPU kelas menengah ke atas. Lihat [Progress.md](../../Progress.md).

## Tes

```bash
dotnet test                                      # 54 tes
dotnet test --filter "FullyQualifiedName~Climb"  # sebagian
```

Tes mencakup:

- **Memanjat:** perpindahan kaki ke punggung, lipatan tepi, lepas dan jatuh, stamina.
- **Aturan tempur:** luka jatuh, segel, resonansi, pelat dan retas, guncangan, serangan, kemampuan, gas, amukan, alur kemenangan.
- **Penyimpanan:** simpan-muat dan pemulihan file rusak.
- **Co-op:** determinisme snapshot dan luka jarak jauh.

## Membuat ulang audio

```bash
dotnet run --project tools/NusantaraColossi.AudioForge -c Release
```

Perintah ini menulis 43 file WAV (5 loop musik, 6 latar suasana, dan 32 efek) ke `assets/audio` secara deterministik. Lihat [aset.md](aset.md).
