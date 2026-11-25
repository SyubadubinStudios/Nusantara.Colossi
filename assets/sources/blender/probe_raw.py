# Probe raw Rodin GLBs: tri count, bounds, images. Run: blender -b --factory-startup -P probe_raw.py
import bpy, os, mathutils
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "rodin")
for f in sorted(os.listdir(R)):
    if not f.endswith('.glb'): continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(R, f))
    tris = 0; mn = mathutils.Vector((1e9,)*3); mx = -mn
    for o in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
        m = o.data; m.calc_loop_triangles(); tris += len(m.loop_triangles)
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    print("PROBE", f, [(o.name, o.type) for o in bpy.context.scene.objects], 'tris', tris,
          'min', tuple(round(x, 3) for x in mn), 'max', tuple(round(x, 3) for x in mx),
          [(i.name, tuple(i.size)) for i in bpy.data.images], [m.name for m in bpy.data.materials])
