# Far-distance / low-quality LODs for the 8 colossus heads.
# Opens each head_<id>.blend (already rotated and normalised by process_static.py), collapses it to
# LOD_TRIS triangles, downsizes textures to LOD_TEX and writes assets/models/heads/lod1/<id>.glb.
# Run: blender -b --factory-startup -P process_lods.py [-- only_name ...]
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *
# Same list as process_static.py (importing that script would rerun its jobs).
HEADS = ["gedung-titan", "kerbau-langit", "dewi-padi", "leviathan",
         "orangutan-purba", "komodo-agung", "penjaga-api-biru", "kapal-majapahit"]

LOD_TRIS = 6000
LOD_TEX = 512
only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for h in HEADS:
    if only and h not in only:
        continue
    bpy.ops.wm.open_mainfile(filepath=os.path.join(BLEND, "head_%s.blend" % h))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    ob = meshes[0]
    t0 = tri_count(ob)
    t1 = decimate(ob, LOD_TRIS)
    downscale_images(LOD_TEX)
    out = os.path.join(MODELS, "heads", "lod1", h + ".glb")
    export_glb(out, image_format='JPEG', jpeg_quality=80)
    print("LOD", h, "tris", t0, "->", t1, "bytes", os.path.getsize(out))
