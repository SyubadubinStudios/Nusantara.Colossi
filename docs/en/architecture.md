# Architecture

[Bahasa Indonesia](../id/arsitektur.md) · *Made by Ariana Mischa Fadhila of Syubadubin Studios*

## Layers

```
┌──────────────────────────────────────────────────────────────────┐
│ Heads: NusantaraColossi.Desktop (Win/macOS/Linux)                │
│        NusantaraColossi.Android                                  │
├──────────────────────────────────────────────────────────────────┤
│ NusantaraColossi.Game                                            │
│  UI      App · Shell (frame loop) · Screens · HUD controls       │
│          · InputMapper                                           │
│  Render  WorldRenderer (+Environment/Colossus/Effects)           │
│          · CameraRig · WGSL Shaders                              │
│  Audio   GameAudio (adaptive score, spatial SFX, voice)          │
│  Net     CoopSession (ThreeNet UDP NetworkSession)               │
├──────────────────────────────────────────────────────────────────┤
│ NusantaraColossi.Core  (no engine or UI references, AOT-safe)    │
│  Regions · Colossi blueprints · ColossusRig · ClimbWorld          │
│  · Player · Colossus AI · Encounter · Progression/Save           │
│  · Coop protocol                                                 │
└──────────────────────────────────────────────────────────────────┘
          Three.Net (Rust wgpu core): DX12 · Vulkan · Metal · GLES
```

Runtime flow: **UI → Game services → simulation (Core) → rendering (Three.Net)**. The simulation never references rendering. The renderer reads simulation state and consumes `GameEvent`s.

## The simulation (Core)

- **Guardians are blueprints plus a pure rig.** A `ColossusBlueprint` is a list of `PartBlueprint` boxes (joint, centre, half-extents, gait parameters, and a climbable-face bitmask) plus cores and tuning. `ColossusRig.Evaluate(blueprint, pose, Span<Matrix4x4>)` is a pure function from a compact `ColossusPose` to part matrices. The same matrices drive rendering, climbing collision and co-op, so a snapshot of the pose reproduces the guardian exactly on every machine. A test checks this.
- **`ClimbWorld`** walks the surface over oriented boxes: static obstacles plus one dynamic slot per guardian part. A contact is (box, local point, face). Movement projects across a face, wraps over edges and transfers to touching boxes. Faces covered by another box are skipped. Any face whose normal points mostly up can be stood on.
- **`Encounter`** owns one hunt: the region, the guardian, the players and drones, the hazards and the outcome. `Step(inputs, dt)` runs at a fixed 60 Hz and writes `GameEvent`s (footstep, stomp impact, core hit and so on) that the presentation layer turns into VFX, audio, camera shake and HUD lines.
- **`Profile` / `SaveStore`.** JSON with source-generated serialization. Saves are atomic (temp file then move), and a corrupt file is kept as `.corrupt` rather than lost.

## Frame loop

`Shell` owns one `ThreeNetView` with `IsRendering = false` and an 8 ms `DispatcherTimer`. Each tick:

1. Return early unless the FPS budget has elapsed. The timer only polls.
2. Poll gamepads. In menus, the D-pad moves focus.
3. `Screen.Tick(dt)`. The hunt runs a fixed-step accumulator (1/60 s), samples input per step, steps the `Encounter`, dispatches events, then updates the camera and `WorldRenderer`.
4. `GameAudio.Update` updates the listener, music layer gains and the heartbeat sync.
5. `viewport.RenderFrame()`.
6. During hunts, **dynamic resolution** adjusts `RenderScale` to hold the FPS cap.

Only one 3D world exists at a time. Menus show a "showcase" world (a guardian at rest), and a hunt replaces it. This keeps GPU memory flat on mobile.

## Rendering

- **Guardian parts** use the full-body model `assets/models/bodies/<id>.glb` when it exists. It holds one mesh per part (`part-<i>`), each in its part's pivot frame, so the meshes are reparented under the pivots and move with the rig (see [assets](assets.md)). A far LOD (`bodies/lod1`) is swapped in by distance. Without a body model, parts fall back to `MeshFactory.SculptedBox` meshes: rounded, noise-displaced boxes kept inside their collision boxes, and the head uses `assets/models/heads/<id>.glb` scaled to fit the head box. In every case the boxes stay the climbing colliders.
- **WGSL surface shaders** (`Shaders.cs`) hook the PBR surface before lighting, so shadows, fog and tone mapping still apply. The colossus shader works in UV space: carved panels, the kawung motif, moss, fur, wood, glass, and seam glow pulsed from C#. The terrain shader works in world space, with per-biome details such as roads and puddles, flooded paddies, sulfur crusts and shorelines. Low quality drops two noise octaves.
- **Effects.** Instanced foliage, GPU particle emitters (sparks, dust, ichor, splash, embers, plumes), Three.Net `FireEffect`, `WaterSurface` and `CloudLayer`, plus lightning in storms.
- **Lights are budgeted.** A core light exists only while the core is open or flashing. Drone lights exist only for active players. Fire, street and shrine lights scale with quality.

## Co-op

| Message | Direction | Reliability | Content |
|---|---|---|---|
| Hello → Welcome / Full | client ↔ host | reliable | protocol version, name → slot, guardian, legend, abilities, seed |
| PlayerState | client → host, 20 Hz | unreliable | the client's own climber |
| Snapshot | host → all, 20 Hz | unreliable | elapsed/phase, guardian pose and state, cores, weather, every player |
| Stab, Ability | client → host | reliable | resolved on the host (seal, plating, resonance checks) |
| Hurt | host → owner | reliable | damage the guardian dealt to a remote climber |
| Event | host → all | reliable | replicated guardian events for audio and VFX |

Clients simulate their own climber with zero added latency and run the guardian rig from snapshots. Since the rig is deterministic, the climbing collision matches the host's. The packet format lives in `Core/Coop.cs`, with little-endian fields and length-prefixed UTF-8. Malformed packets are dropped.

## Performance notes

- The hot paths (`Step`, `ColossusRig.Evaluate`, `ClimbWorld.Move`) allocate nothing per frame. They use spans and preallocated arrays.
- On first launch, a throwaway offscreen renderer reads the GPU class. Integrated GPUs start on Low quality.
- On an Intel UHD 620 (integrated), hunts run at about 30–65 FPS on Low with dynamic resolution. The `NC_PROFILE=1` environment variable prints per-frame timings: tick, render, draw calls, triangles, lights and GPU ms.
