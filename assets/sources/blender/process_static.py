# Process the static Rodin models (GARUDA drone + 8 colossus heads) into game-ready GLBs.
# Run: blender -b --factory-startup -P process_static.py [-- only_name ...]
# Mobile budget: <=25k tris (drone <=15k), textures 1024 px, JPEG inside the GLB, target <= 3 MB.
# The .blend saved to assets/sources/blender keeps the 2048 px source textures; a 2048 px PNG GLB is
# also written to assets/sources/blender/hires/ for reference.
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

HEADS = ["gedung-titan", "kerbau-langit", "dewi-padi", "leviathan",
         "orangutan-purba", "komodo-agung", "penjaga-api-biru", "kapal-majapahit"]

# name, source, output, pre-rotations [(axis, deg)], normalize mode, size, max tris
JOBS = [("garuda-drone", "garuda-drone.glb", os.path.join(MODELS, "garuda-drone.glb"),
         [('X', -32), ('Z', 180)], 'width', 0.6, 15000)]
for h in HEADS:
    # A "_v2" regeneration replaces the first take (fully modelled in the round, not a flat relief).
    v2 = "head_%s_v2.glb" % h
    JOBS.append((h, v2 if os.path.exists(os.path.join(RODIN, v2)) else "head_%s.glb" % h, os.path.join(MODELS, "heads", h + ".glb"),
                 [('Z', 180)], 'height', 1.0, 25000))

MOBILE_TEX = 1024
only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for name, src, out, rots, mode, size, max_tris in JOBS:
    if only and name not in only:
        continue
    reset()
    ob = import_joined(os.path.join(RODIN, src), name)
    t0 = tri_count(ob)
    for axis, deg in rots:
        rotate_deg(ob, axis, deg)
    t1 = decimate(ob, max_tris)
    normalize(ob, mode, size)
    mn, mx = bounds(ob)
    print("PROCESSED", name, "tris", t0, "->", t1, "bounds", mn.round(3).tolist(), mx.round(3).tolist())
    stem = ("head_" if name in HEADS else "") + name
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, stem + ".blend"), compress=True)  # full-res textures
    export_glb(os.path.join(BLEND, "hires", stem + ".glb"), image_format='AUTO')
    downscale_images(MOBILE_TEX)
    export_glb(out, image_format='JPEG', jpeg_quality=85)
    validate(out)
