# Render orthographic previews of GLBs from several directions (workbench, textured).
# Usage: blender -b --factory-startup -P preview.py -- <outdir> <views comma list e.g. -y,+y,+x> <glb> [<glb>...]
import bpy, sys, os, mathutils
argv = sys.argv[sys.argv.index("--")+1:]
out, views, files = argv[0], argv[1].split(","), argv[2:]
os.makedirs(out, exist_ok=True)
D = {"-y": (0,-1,0), "+y": (0,1,0), "+x": (1,0,0), "-x": (-1,0,0), "+z": (0,0,1), "-z": (0,0,-1)}
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    sc = bpy.context.scene
    try: sc.render.engine = 'BLENDER_WORKBENCH'
    except TypeError as e: print(e)
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = sc.render.resolution_y = 320
    sc.render.film_transparent = False
    bpy.context.view_layer.update()
    mn = mathutils.Vector((1e9,)*3); mx = -mn
    for o in sc.objects:
        if o.type != 'MESH': continue
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c); mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    ctr = (mn+mx)/2; size = max(mx-mn)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = size*1.1
    for v in views:
        d = mathutils.Vector(D[v]); cam.location = ctr + d*size*3
        up = mathutils.Vector((0,0,1)) if abs(d.z) < 0.9 else mathutils.Vector((0,1,0))
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y' if abs(d.z) < 0.9 else 'Y').to_euler()
        cam.data.clip_end = size*10
        sc.render.filepath = os.path.join(out, os.path.splitext(os.path.basename(f))[0] + "_" + v.replace("+","p").replace("-","m") + ".png")
        bpy.ops.render.render(write_still=True)
