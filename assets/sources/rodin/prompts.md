# Rodin MCP generation log — Nusantara Colossi

Every generation was run once. All 2D, 3D and voice items succeeded except the first 3D batch:
**all 10 Hunyuan 3D jobs failed** with `API Error (ResourceInsufficient): Resources are insufficient`.
At the user's instruction they were **not retried on Hunyuan**. All 10 were regenerated once with
**Rodin 3D (`Generate3DFromPrompt`)** using the same prompts.

Temp outputs from the tool (`%TEMP%\...`) were copied into the project; the source file column gives the project path.

## 2D art — `mcp__rodin__GenerateImageWithGptImageFlare` (outputFormat png, background auto)

| Item | Size / quality | Project file |
|---|---|---|
| keyart | 1536x1024 / high | `assets/art/keyart.png` |
| arka | 1024x1536 / medium | `assets/art/arka.png` |
| gedung-titan | 1536x1024 / medium | `assets/art/colossi/gedung-titan.png` |
| kerbau-langit | 1536x1024 / medium | `assets/art/colossi/kerbau-langit.png` |
| dewi-padi | 1536x1024 / medium | `assets/art/colossi/dewi-padi.png` |
| leviathan | 1536x1024 / medium | `assets/art/colossi/leviathan.png` |
| orangutan-purba | 1536x1024 / medium | `assets/art/colossi/orangutan-purba.png` |
| komodo-agung | 1536x1024 / medium | `assets/art/colossi/komodo-agung.png` |
| penjaga-api-biru | 1536x1024 / medium | `assets/art/colossi/penjaga-api-biru.png` |
| kapal-majapahit | 1536x1024 / medium | `assets/art/colossi/kapal-majapahit.png` |
| icon | 1024x1024 / medium | `assets/art/icon.png` |

### Prompts

**keyart** — Epic cinematic title key art, painterly-realistic AAA video game concept painting. At dawn, a tiny lone explorer stands on the edge of a rocky cliff in the foreground, seen from behind, with a small white-and-gold drone hovering beside him emitting faint cyan light. He faces a colossal stone water buffalo guardian rising from a misty volcanic savanna like Mount Bromo's sea of sand: its body carved from grey andesite with intricate ancient Javanese temple relief panels (Borobudur and Prambanan style) as armor plates, curving massive horns, moss and ferns growing on its back, thin glowing cyan circuit lines tracing the carvings, and a radiant amber-gold glyph core blazing on its chest. Smoking volcano crater behind, golden sunrise light breaking through mist, volumetric god rays, birds. Very faint silhouette of a modern Indonesian city skyline on the far hazy horizon. Sense of immense scale, awe and solitude. Original fantasy design. No text, no logo, no letters.

**arka** — Full-body character concept portrait, painterly-realistic AAA game art. Arka, a young Indonesian man in his late twenties, warm brown skin, short black hair, determined focused expression, lean athletic climber build. He wears a practical explorer-engineer outfit: a dark teal technical softshell jacket with subtle gold batik parang and kawung pattern accents on the shoulders and cuffs, a climbing harness with carabiners and rope, reinforced cargo trousers, fingerless climbing gloves, sturdy trail boots, a translucent cyan AR visor over his eyes showing faint holographic readouts. At his hip hangs a kris-inspired energy blade with a wavy luk blade edged in glowing cyan light and an ornate carved gold hilt. Small tech pouches, a wrist drone-control unit. Standing three-quarter view against a soft misty temple-ruin and jungle background with warm rim light. Original character design. No text, no logo, no letters.

**gedung-titan** — Cinematic establishing shot, painterly-realistic AAA game concept art. At night in a dense Jakarta-like modern business district, a 400-meter-tall living skyscraper titan walks between office towers: a humanoid colossus whose body is built from glass curtain walls, concrete floors and steel, fused with ancient carved andesite stone relief panels in Javanese temple style, its head a skyscraper crown with a carved stone mask and glowing cyan eyes, cyan circuit lines pulsing across the glass, a glowing amber-gold glyph core in its chest. Neon signs, rain-slick streets, traffic light trails, helicopters with searchlights, tiny people below for scale. Low-angle view emphasizing enormous height. Original design. No text, no readable signage, no logo.

**kerbau-langit** — Cinematic establishing shot, painterly-realistic AAA game concept art. A 250-meter colossal sky buffalo guardian charging across a volcanic savanna of golden grass and grey volcanic sand like the Bromo Tengger caldera, with the smoking cone of a volcano behind. The buffalo is carved from grey andesite stone with ancient Javanese temple relief armor plates, huge sweeping horns, thick moss and ferns on its back, dust clouds kicked up by its hooves, thin glowing cyan circuit lines along the carvings, a glowing amber-gold glyph core on its chest. Late afternoon light, dramatic clouds, tiny horse rider in foreground for scale. Original design. No text, no logo.

**dewi-padi** — Cinematic establishing shot, painterly-realistic AAA game concept art. A towering, graceful rice-goddess guardian colossus rising over cascading Balinese rice terraces at golden morning: her body formed of layered terraced earth and stone like stepped paddies with water channels, robes of green rice stalks swaying, ornate gold Balinese headdress and jewelry, a serene carved stone face with glowing gold eyes, an amber-gold glyph core on her chest, faint cyan circuit lines in the carvings. Palm trees, a split temple gate (candi bentar) and small shrines below, mist in the valleys, tiny farmers for scale. Original design. No text, no logo.

**leviathan** — Cinematic establishing shot, painterly-realistic AAA game concept art. A kilometer-long serpentine sea dragon guardian surfacing among the mushroom-shaped green karst islands of Raja Ampat in turquoise water: its coiling body made of ancient carved stone encrusted with colorful coral, barnacles and sea fans, with Javanese naga motifs and a crowned naga head, glowing cyan circuit lines along its scales and an amber-gold glyph core glowing beneath its jaw, waterfalls cascading off its coils, sea spray, a tiny speedboat in the foreground for scale. Bright tropical daylight with dramatic clouds. Original design. No text, no logo.

**orangutan-purba** — Cinematic establishing shot, painterly-realistic AAA game concept art. A primeval giant orangutan guardian colossus crouching in the misty canopy of a Kalimantan (Borneo) rainforest: its massive body formed of ancient weathered stone wrapped in thick tree roots, vines and moss, long arms gripping giant dipterocarp trees, reddish-orange moss like fur, a wise carved stone face with glowing amber-gold eyes, a glowing gold glyph core in its chest, faint cyan circuit lines in the cracks. Morning mist, shafts of light through the canopy, hornbills flying, a tiny explorer on a branch for scale. Original design. No text, no logo.

**komodo-agung** — Cinematic establishing shot, painterly-realistic AAA game concept art. A mountain-sized komodo dragon guardian prowling over the dry savanna hills of Flores and Komodo island near the sea: its body armored with plates of dark volcanic basalt rock, glowing orange molten lava seams between the plates, forked tongue of fire, ancient carved glyphs on its scales, an amber-gold glyph core in its chest, faint cyan circuit lines. Golden hour, steam and ash, bays and islands in the background, tiny deer fleeing for scale. Original design. No text, no logo.

**penjaga-api-biru** — Cinematic establishing shot, painterly-realistic AAA game concept art at night. Inside the Ijen volcano crater, a giant humanoid guardian colossus made of jagged black sulfur-stained rock rises from a vivid turquoise acid lake, wreathed in eerie electric-blue flames, with toxic yellow sulfur gas billowing around, yellow sulfur crystal deposits on its shoulders, ancient carved temple relief on its chest plates, glowing cyan circuit lines, a blazing amber-gold glyph core. Steep crater walls, tiny figures with gas masks and headlamps for scale. Original design. No text, no logo.

**kapal-majapahit** — Cinematic establishing shot, painterly-realistic AAA game concept art. A colossal ancient Majapahit-era sailing ship that is alive, rising out of a stormy Banda Sea: a massive multi-masted vessel with tanja sails and outriggers, its hull made of dark carved teak wood and ancient stone temple relief, a living naga-garuda figurehead prow with glowing gold eyes, barnacles and seaweed hanging, glowing cyan circuit lines running along the planks and an amber-gold glyph core burning on the deck. Huge waves, torrential rain, forked lightning, dark clouds, a tiny modern patrol boat for scale. Original design. No text, no logo.

**icon** — Mobile game app icon, centered emblem, stylized polished gold garuda wings spreading symmetrically, merged with a carved grey andesite stone colossus eye in the middle with intricate Javanese relief patterns, the iris glowing amber-gold with thin cyan circuit lines, on a dark deep teal background with subtle radial glow. Clean, bold, readable at small size, premium AAA look. No text, no letters.

## 3D models

### Attempt 1 — `mcp__rodin__Generate3DFromPromptWithHunyuan` — ALL FAILED
Settings: faceCount 60000 (arka, drone) / 40000 (heads), enablePbr true, generateType Normal.
Result for all 10 items: `3D Asset generation failed ... API Error (ResourceInsufficient): Resources are insufficient.`
No file produced. Not retried on Hunyuan (user instruction).

### Attempt 2 — `mcp__rodin__Generate3DFromPrompt` (Rodin 3D) — ALL SUCCEEDED
Settings: the tool only takes `prompt` (no face count / PBR options). Each output came with ~33–40k tris and
three 2048 px PBR textures (base color, metallic-roughness, normal). Same prompts as attempt 1:

| Item | Raw source file (Rodin 3D) | Raw tris | Prompt |
|---|---|---|---|
| arka | `assets/sources/rodin/arka.glb` | 34,075 | Full-body 3D game character, standing straight in T-pose with arms stretched out horizontally to the sides, legs slightly apart, facing forward, no base, no plinth, no stand. Young Indonesian man explorer-engineer, lean athletic build, short black hair, warm brown skin, translucent cyan AR visor over eyes. Dark teal technical jacket with gold batik pattern accents on shoulders and cuffs, climbing harness with carabiners, cargo trousers, fingerless gloves, trail boots, kris-inspired energy blade sheathed at the hip with ornate gold hilt and cyan glowing edge. Realistic proportions, clean separated limbs, game-ready. |
| garuda-drone | `assets/sources/rodin/garuda-drone.glb` | 35,525 | Compact quad-rotor drone shaped like a stylized garuda bird, seen as a single object, no base. Central streamlined body like a bird torso with a sharp beaked garuda head at the front, four rotor arms shaped like swept feathered wings each ending in a ducted propeller ring, fan-shaped feather tail at the back. Glossy white shell panels with polished gold trim and engraved Javanese ornament lines, glowing cyan light strips and cyan eye lenses, small camera sensor under the chin. Clean sci-fi hard-surface design. |
| head gedung-titan | `assets/sources/rodin/head_gedung-titan.glb` | 36,289 | Monumental head sculpture only, front-facing, no body, no neck stand, no base. Head of a giant skyscraper titan: the top of the head is a stepped art-deco skyscraper crown with spire and glass curtain-wall panels, the face is a carved grey andesite stone mask in ancient Javanese temple style with stern brow and ornate relief, two glowing cyan eyes, cyan circuit lines etched across the glass and stone, concrete and steel jaw segments. |
| head kerbau-langit | `assets/sources/rodin/head_kerbau-langit.glb` | 32,610 | Monumental head sculpture only, front-facing, no body, no base. Giant water buffalo head carved from grey mossy andesite stone, huge sweeping curved horns, intricate ancient Javanese temple relief carvings (Borobudur style) on the forehead and cheeks, patches of green moss and small ferns, glowing amber-gold eyes, thin cyan glowing circuit lines in the carved grooves, weathered ancient statue. |
| head dewi-padi | `assets/sources/rodin/head_dewi-padi.glb` | 37,848 | Monumental head sculpture only, front-facing, no body, no base. Serene goddess head of a rice-goddess guardian carved from terraced earth-toned stone, layered stepped terraces like rice paddies forming her hair, ornate golden Balinese crown headdress with flames and flower motifs, green rice stalk ornaments, calm closed-lip face with glowing gold eyes, gold earrings, ancient temple statue style. |
| head leviathan | `assets/sources/rodin/head_leviathan.glb` | 39,702 | Monumental head sculpture only, front-facing, no body, no base. Javanese naga sea-dragon head carved from ancient stone, crowned with an ornate tiered naga crown, open jaws with fangs, flowing carved whisker fins, encrusted with colorful coral, barnacles and sea fans, teal-green weathered stone, glowing cyan eyes and cyan circuit lines along the scales. |
| head orangutan-purba | `assets/sources/rodin/head_orangutan-purba.glb` | 32,988 | Monumental head sculpture only, front-facing, no body, no base. Primeval giant orangutan head carved from ancient weathered stone, broad flanged cheek pads, wise deep-set eyes glowing amber-gold, wrapped in thick twisting tree roots and vines, covered in reddish-orange and green moss like fur, small carved glyphs on the brow, ancient jungle temple statue. |
| head komodo-agung | `assets/sources/rodin/head_komodo-agung.glb` | 33,252 | Monumental head sculpture only, front-facing, no body, no base. Giant komodo dragon head made of dark volcanic basalt rock plates, glowing orange molten lava seams between the plates, heavy brow ridges, slightly open jaw with serrated teeth, carved ancient glyphs on the scales, glowing amber-gold eyes, rugged lizard snout. |
| head penjaga-api-biru | `assets/sources/rodin/head_penjaga-api-biru.glb` | 33,428 | Monumental head sculpture only, front-facing, no body, no base. Fierce guardian head carved from jagged black volcanic rock stained with bright yellow sulfur crystals, crown of stylized blue flame shapes, stern demonic temple guardian (dvarapala) face with bulging glowing electric-blue eyes and fangs, carved ancient relief on the cheeks. |
| head kapal-majapahit | `assets/sources/rodin/head_kapal-majapahit.glb` | 33,890 | Monumental ship prow figurehead only, front-facing, no hull, no base. Combined naga and garuda head carved from ancient dark teak wood and grey stone, garuda beak and feathered crest merged with naga crown and curling serpent scales, ornate Majapahit carving, weathered with barnacles and seaweed, gold inlay trim, glowing amber-gold eyes. |

Note: Rodin returned the Arka body in a shallow **A-pose** (arms ~19.6° below horizontal), not a strict T-pose.
The rig compensates for this (see `assets/sources/blender/process_arka.py`).

## Voice — `mcp__rodin__GenerateVoiceWithElevenLabsV3` (voiceId Aria, LangCode id, Speed 1.0)

MP3 output converted with `ffmpeg -i in.mp3 -ar 44100 -ac 1 -c:a pcm_s16le out.wav`.

| Name | Text | File | Duration |
|---|---|---|---|
| intro | Arka, sinyal GARUDA NET mendeteksi kebangkitan Penjaga Nusantara. Kita harus bergerak. | `assets/audio/voice/intro.wav` | 6.51 s |
| scan | Pemindaian selesai. Titik inti terdeteksi. | `assets/audio/voice/scan.wav` | 3.29 s |
| grip | Stamina genggaman menipis! Cari pijakan! | `assets/audio/voice/grip.wav` | 3.76 s |
| core | Inti hancur. Penjaga itu melemah. | `assets/audio/voice/core.wav` | 3.21 s |
| victory | Penjaga telah tenang. Sejarah Nusantara terbuka kembali. | `assets/audio/voice/victory.wav` | 4.81 s |
| enrage | Hati-hati! Ia mengamuk! | `assets/audio/voice/enrage.wav` | 1.93 s |
