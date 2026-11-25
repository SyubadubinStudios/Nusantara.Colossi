# Building and platforms

[Bahasa Indonesia](../id/build.md) · *Made by Ariana Mischa Fadhila of Syubadubin Studios*

## Requirements

- .NET SDK **10.0.4xx**. `global.json` pins 10.0.401 with `rollForward: latestFeature`.
- A GPU with DirectX 12 (Windows), Vulkan (Linux/Android) or Metal (macOS).
- For Android: the .NET Android workload (`dotnet workload install android`) and the Android SDK.

Three.Net's native core ships in the `ThreeNet.Native` package for win-x64, linux-x64, osx-x64/arm64 and android-arm64/x64, so no Rust toolchain is needed.

## Desktop (Windows, macOS, Linux)

```bash
dotnet run --project src/NusantaraColossi.Desktop -c Release
# self-contained builds
dotnet publish src/NusantaraColossi.Desktop -c Release -r win-x64   --self-contained
dotnet publish src/NusantaraColossi.Desktop -c Release -r linux-x64 --self-contained
dotnet publish src/NusantaraColossi.Desktop -c Release -r osx-arm64 --self-contained
```

The desktop project copies `assets/models`, `assets/audio`, `assets/art` and `assets/textures` next to the executable.

Command-line options:

| Option | Effect |
|---|---|
| `--screenshot <folder>` | Tours title → map → codex → skills → four hunts on autopilot, saves PNGs, then exits |
| `--lang en\|id` | Language for the screenshot tour |
| env `NC_PROFILE=1` | Prints frame timings to stderr every 2 s |
| env `NC_Q=0\|1\|2` | Overrides graphics quality for one run |

## Packaging

Scripts in `build/` publish the desktop head self-contained (no .NET install needed on the player's machine) and wrap it for each OS. Everything is written to `artifacts/` (git-ignored): the raw publish output goes to `artifacts/publish/<rid>/`, and the finished packages go to `artifacts/packages/`.

| OS | Command | Output |
|---|---|---|
| Windows | `pwsh build/package-windows.ps1` | `NusantaraColossi-<version>-win-x64.zip` |
| Linux | `bash build/package-linux.sh` | `NusantaraColossi-<version>-x86_64.AppImage` if `appimagetool` is on `PATH` (or `APPIMAGETOOL=/path/to/it`), otherwise `NusantaraColossi-<version>-linux-x64.tar.gz` of the AppDir (extract it and run `./AppRun`) |
| macOS | `bash build/package-macos.sh` | `NusantaraColossi-<version>-macos-arm64.zip` and `-macos-x64.zip`, each holding `Nusantara Colossi.app` |

- **Version:** taken from `<Version>` in `Directory.Build.props`.
- **Every package** contains the executable, Three.Net's native core (`threenet_core.dll` / `libthreenet_core.so` / `libthreenet_core.dylib`), SkiaSharp/HarfBuzz natives and the `assets/` folder. The scripts fail if any of these is missing.
- **Linux AppDir:** `AppRun`, `nusantara-colossi.desktop` and the icon from `assets/art/icon.png`. The game itself is in `usr/bin/`.
- **macOS bundle:**
  - The bundle id is `com.syubadubin.nusantaracolossi`, the minimum is macOS 13, and the copyright line carries the credit.
  - The game files and `assets/` live in `Contents/MacOS`, because the game reads assets next to its executable.
  - `.icns` is made with `iconutil`/`sips`, and the bundle is ad-hoc signed with `codesign`, when those tools exist. Set `RIDS="osx-arm64"` to build one architecture only, or `CODESIGN_IDENTITY` to sign with a real identity.
  - An ad-hoc signed app downloaded from the internet is still quarantined. Run `xattr -dr com.apple.quarantine "Nusantara Colossi.app"` once, or right-click → Open. Public distribution needs a Developer ID, the hardened runtime and notarization (not set up yet).
- **Size:** about 210 MB unpacked (of which 67 MB is assets), and about 120 MB zipped on Windows. The desktop project drops SkiaSharp's ~100 MB of native `.pdb` files from publish output.

### Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request:

- **Windows, Ubuntu and macOS (Apple Silicon):** build, run the tests, then run the matching packaging script.
- **Smoke checks:**
  - All three runners check the publish layout (executable, native core, `assets/`, and the executable bit on Unix).
  - On macOS, the `.app` is also checked: `Info.plist`, bundle id, `.icns`, the `codesign` signature and an arm64 dylib.
  - On Linux, a best-effort test launches the game for 25 s under Xvfb with Mesa's software Vulkan (it does not fail the build).
- **Artifacts:** packages are uploaded as `NusantaraColossi-<rid>` and test results as `test-results-<rid>`.
- **Android:** a separate, optional job (`continue-on-error`) installs the android workload and uploads the signed APK.

## Android

```bash
dotnet build src/NusantaraColossi.Android -c Release
adb install src/NusantaraColossi.Android/bin/Release/net10.0-android/com.syubadubin.nusantaracolossi-Signed.apk
```

- Targets API 26+, arm64 and x64, landscape only.
- Runtime assets ship as `AndroidAsset`s. On first launch for each app version, they are extracted to app storage, because Three.Net's loaders take file paths.
- Touch controls, Low quality and a 0.8 render scale are on by default.
- Release builds are AOT-compiled (`RunAOTCompilation`), which gives faster start-up and less JIT work on phone CPUs.
- Play Store bundle (AAB), signed with your upload key. Keep the keystore and passwords out of git:

  ```bash
  dotnet publish src/NusantaraColossi.Android -c Release -p:StoreBuild=true     -p:AndroidSigningKeyStore=upload.keystore -p:AndroidSigningKeyAlias=upload     -p:AndroidSigningKeyPass=env:NC_KEY_PASS -p:AndroidSigningStorePass=env:NC_STORE_PASS
  ```

- Mobile defaults: Low quality, render scale 0.55, and dynamic resolution on every screen (down to 0.35). Far guardian heads use the 6k-triangle LOD (`assets/models/heads/lod1`), and Low quality loads only that LOD.
- Profiling on a device: `adb shell touch /sdcard/Android/data/com.syubadubin.nusantaracolossi/files/profile` logs frame timings and pointer events to logcat (tag `NC`).

> Status: tested on one low-end tablet (Mali-G51): it starts and its menus respond to a keyboard (touch and audio output not yet confirmed), but it runs at about 3 FPS there. A mid-range or better GPU is needed. See [Progress.md](../../Progress.md).

## Tests

```bash
dotnet test                                   # all 54 tests
dotnet test --filter "FullyQualifiedName~Climb"  # a subset
```

The tests cover:

- **Climbing:** leg-to-back transfer, edge wrap, release and fall, stamina.
- **Combat rules:** fall damage, seals, resonance, plating and hack, shake-off, attacks, abilities, gas, rage, the victory flow.
- **Saves:** save round-trip and corruption recovery.
- **Co-op:** snapshot determinism and remote hurts.

## Regenerating audio

```bash
dotnet run --project tools/NusantaraColossi.AudioForge -c Release
```

This writes 43 WAV files (5 music loops, 6 ambience beds and 32 effects) to `assets/audio` deterministically. See [assets.md](assets.md).
