# Game design

[Bahasa Indonesia](../id/desain-game.md) · *Made by Ariana Mischa Fadhila of Syubadubin Studios*

## Premise

Indonesia, 2045. **GARUDA NET**, a national AI built to manage the archipelago's infrastructure, finds patterns in temple reliefs, folklore and seismic data. It concludes that eight ancient **Penjaga** (Guardians) sleep under the land and sea, and its probing wakes them. **Arka** helped build GARUDA and now pilots its field drone. They set out to lay each guardian to rest before the cities around them fall.

The game is original IP. It works in the boss-hunter genre (climbable giants, few enemies, scale as the spectacle), and its identity comes from Indonesian material culture:

- **Majapahit** temple stone and the **kawung** batik motif.
- **Gamelan** tuned to pelog.
- Real landscapes such as Bromo, Ijen, Raja Ampat and Jatiluwih.

## Core loop

```
Map ─► choose region ─► approach & read the guardian ─► climb ─► scan seals ─► strike cores on the pulse
 ▲                                                                                         │
 └──── skills ◄── points, rank, inscription ◄── victory (all cores destroyed → collapse) ◄──┘
```

## Pillars

1. **Scale you can read.** The camera's lock-on frames Arka against the guardian. Footsteps rumble louder the closer you are. The HUD stays quiet so the body of the guardian carries the screen.
2. **Tension through rhythm.** Every core has a heartbeat. You hear it, see it in the grip ring, and the stab rewards releasing on the beat. The rhythm speeds up in rage phases, so the decision gets harder as the stakes rise.
3. **Fair danger.** Every attack is telegraphed: shake-off warnings, stomp wind-ups, gust warnings 2 s ahead. With *Hunter's Instinct*, stomp zones show as rings on the ground.
4. **Tools, not power.** GARUDA's modules create openings: unsealing cores, stunning the guardian, luring it away. They don't deal damage. The climb is always yours.

## Guardians

Heights are gameplay heights. Lore sizes are deliberately bigger: legends exaggerate, and the inscription archive keeps the legendary scale.

| Guardian | Region (biome, weather) | Height | Cores | Signature |
|---|---|---|---|---|
| Kerbau Langit / Sky Buffalo | Bromo savanna, dawn | 31 m | 2 | Tutorial guardian; mossy legs, a safe hump to rest on |
| Gedung Titan / Tower Titan | Jakarta, night rain | 72 m | 3 (2 plated) | Glass torso can't be climbed; plated cores need a hack; very hackable |
| Dewi Padi / Rice Goddess | Bali terraces, clear | 47 m | 3 | Seals stay open only 14 s; climb the skirt, then the crown |
| Komodo Agung / Great Komodo | Flores hills, heat | 24 m | 3 | Obsidian legs can't be climbed, so the route is via the tail; two rage phases |
| Orangutan Purba / Primeval Orangutan | Kalimantan rainforest, fog, rain | 42 m | 3 | Shakes every 4.5 s; arm stomps |
| Leviathan Nusantara | Raja Ampat lagoon | 12 m above water | 3 (fins) | Nine-segment serpent that rides the surface; swimming approach |
| Penjaga Api Biru / Blue Fire Warden | Ijen crater, night | 38 m | 3 | Fast pulse (1.2 s); toxic gas clouds; blue fire |
| Kapal Majapahit / Majapahit Ship | Banda Sea, storm | 48 m | 4 | A living war-ship: masts and sails to climb, gusts that throw you |

## Systems

- **Climbing.** Guardians are articulated rigid parts. Each face is marked climbable or not, and top faces are standable. Arka walks across surfaces, wraps over edges and transfers between touching parts. Grip stamina drains with movement and hazards, and refills while standing.
- **Cores.** Sealed until scanned. Plated cores also need the guardian stunned by a hack. Damage scales with charge^1.4, with ×1.5 for resonance.
- **Guardian AI.** States: Dormant → Roam → Pursue → Attack (stomp, sweep, roar, charge) / ShakeWarn → ShakeOff → Stunned / Stagger → Collapse. Annoyance builds when you stab flesh. Each rage threshold adds 20% speed and a faster pulse.
- **Hazards.** Rain makes grip drain faster. Heat slows stamina recovery. Sulfur gas hurts and blocks recovery. Storm gusts can throw off standing climbers.
- **Progression.** Regions unlock in order. Hack unlocks after the 1st victory and Decoy after the 2nd. Nine skills sit in three trees.
- **Legend mode.** 1.5× damage taken, faster guardians, no core markers.
- **Co-op.** 2–4 hunters. The host is authoritative for the guardian, and each client is authoritative for its own climber. See [architecture](architecture.md#co-op).

## Presentation

- **Palette.** Gold (*prada*) stands for myth and temples, cyan (*nadi*) for GARUDA technology, ember (*bara*) for danger. Every UI colour comes from these meanings.
- **Type.** Marcellus SC is the inscription face. Hanken Grotesk is the body face. JetBrains Mono carries GARUDA's telemetry.
- **HUD signature.** A kawung grip ring, and guardian vitality drawn as a candi whose tiers are the cores.
- **Music.** Three layered loops (explore, battle, rage) crossfade with the fight's intensity. A gong marks each gamelan cycle, and the heartbeat follows the core pulse.
