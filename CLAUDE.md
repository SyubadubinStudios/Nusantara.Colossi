# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Nusantara Colossi** is an original action-adventure boss-hunter set in an alternate Indonesia (2045). GARUDA NET wakes eight ancient guardians, and the player is Arka, an ex-AI engineer and drone pilot. `Nusantara.Colossi.txt` (Indonesian) is the authoritative brief. `solution-design.md` is an older design whose stack table is superseded.

**Stack (decided with the user):**
- **.NET 10 / C#**, **Avalonia UI** on every platform (Windows, macOS, Linux, Android), and **Three.Net** (`ThreeNet`, `ThreeNet.Avalonia` 0.12.x) for rendering, audio, gamepads and UDP networking.
- Rust only through Three.Net's native core.
- Do **not** use Blazor, WPF, MAUI, Babylon or SignalR.

## Commands

```bash
dotnet build NusantaraColossi.slnx                      # everything (Android needs the android workload)
dotnet test                                              # 81 xUnit tests (Core simulation)
dotnet test --filter "FullyQualifiedName~Climb"          # single test / subset
dotnet run --project src/NusantaraColossi.Desktop        # play
dotnet run --project src/NusantaraColossi.Desktop -- --screenshot docs/images --lang en   # regenerate doc screenshots (also --lang id → *-id.png)
dotnet run --project tools/NusantaraColossi.AudioForge -c Release                         # regenerate assets/audio/*.wav
dotnet build src/NusantaraColossi.Android               # APK (RunAOTCompilation=false; no device tested yet)
```

Environment variables:
- `NC_PROFILE=1` prints frame timings: tick/render ms, draw calls, lights, GPU ms.
- `NC_Q=0|1|2` overrides graphics quality for one run.

## Architecture (big picture)

- **`src/NusantaraColossi.Core`**: engine-free, AOT-compatible simulation. Everything gameplay lives here and is unit-tested.
  - A guardian is a `ColossusBlueprint` (articulated boxes + cores + tuning). `ColossusRig.Evaluate(bp, pose, Span<Matrix4x4>)` is a **pure function**. The same part matrices drive rendering, climbing collision and co-op, so never add per-machine state to the rig.
  - Face bitmask convention: bit = axis*2 + (positive ? 1 : 0). Guardians face +Z, and forward = (sin yaw, 0, cos yaw). Matrices are row-vector (local × parentWorld).
  - `ClimbWorld` does surface-walking over oriented boxes (static obstacles + dynamic guardian part slots). Faces with normal.y > 0.7 are standable.
  - `Encounter.Step(inputs, dt, firstLocalSlot)` runs at a fixed 60 Hz. It **clears its event list first**, then emits `GameEvent`s that the presentation consumes.
  - Co-op (`Coop.cs`): the host is authoritative for the guardian, and clients own their climber. Clients queue `PendingRemoteStabs/Abilities`, and the host queues `RemoteHurts`.
- **`src/NusantaraColossi.Game`**: shared by all heads.
  - `UI/Shell` owns one `ThreeNetView` (`IsRendering=false`) and an 8 ms `DispatcherTimer` frame loop. It also handles FPS capping, dynamic resolution and gamepad menu navigation.
  - Screens derive from `Screen`, and the UI is code-built (no XAML).
  - `UI/Stage` holds exactly one `WorldRenderer` at a time (menu showcase or hunt).
  - `Rendering/WorldRenderer*.cs` contains the scene build (environment / colossus / effects partials), plus `Shaders.cs` (WGSL surface hooks) and `CameraRig`.
  - `Audio/GameAudio` is the layered adaptive score. `Net/CoopSession` wraps `ThreeNet.Networking.NetworkSession`.
- **`src/NusantaraColossi.Desktop` / `src/NusantaraColossi.Android`**: thin heads. Android extracts APK assets to app storage, because Three.Net loads from file paths.
- **Assets** (`assets/`): optional at runtime, and every model has a procedural fallback. The path contract is in `docs/en/assets.md` (`AssetPaths` in `GameSettings.cs`).

## Gotchas

- Inside Avalonia controls, `Theme` resolves to `StyledElement.Theme`. UI files alias the palette as `using Palette = NusantaraColossi.Game.UI.Theme;`. `Key` and `MouseButton` are ambiguous with ThreeNet, so alias them to `Avalonia.Input`.
- `SceneEnvironment.Default` has **linear fog 10–100 m**. WorldRenderer pushes `FogStart/FogEnd` past the far plane. Keep `AmbientIntensity` small, because the procedural sky already provides IBL.
- Avalonia transitions need an attached visual. The first screen is created in `AttachedToVisualTree`.
- Lights cost every pixel on integrated GPUs. Set `node.Light = null` to remove a light rather than setting its intensity to 0.
- A `TextBlock` with `MaxWidth` inside a stretched panel centers itself. Set `HorizontalAlignment.Left`.

## Deliverable rules from the brief

- All docs bilingual (English and Bahasa Indonesia) with screenshots: `README.md`, `docs/en`, `docs/id`, `docs/images`.
- Credit in the docs **and** the app: "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios". It appears on the title screen and in credits.
- Keep **`PLAN.md`** (roadmap) and **`Progress.md`** (tracking) current. Update Progress.md as work lands, and report gaps honestly.
- Performance and memory matter: the game targets mobile. Keep the hot paths (`Step`, `Evaluate`, `ClimbWorld.Move`) allocation-free.

## Asset and UI tooling (`.mcp.json`, `.claude/skills/`)

- UI/UX: the project-local `frontend-design` skill. The direction is "temple foundation stone × GARUDA telemetry": gold = myth, cyan = GARUDA tech, ember = danger.
- 2D art: Rodin `GenerateImageWithGptImageFlare`.
- 3D: try Rodin Hunyuan (`Generate3DFrom*WithHunyuan`). **If Hunyuan fails, use Rodin 3D** (`Generate3DFromPrompt` / `Generate3DFromImagePath`). This is the user's instruction, and Hunyuan has been failing on Replicate.
- Blender MCP for sculpting, rigging and animation. Blender sources go in `assets/sources/blender`.
