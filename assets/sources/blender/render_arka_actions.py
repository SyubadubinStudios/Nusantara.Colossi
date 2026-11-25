# QA: render each Arka action at 4 phases from a 3/4 front view (character faces +Y) and a side view.
# Run: blender -b arka.blend -P render_arka_actions.py -- <outdir>
import bpy, sys, os, mathutils
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
sc = bpy.context.scene; rig = bpy.data.objects["ArkaRig"]
try: sc.render.engine = 'BLENDER_WORKBENCH'
except TypeError as e: print(e)
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = sc.render.resolution_y = 220
for t in rig.animation_data.nla_tracks: t.mute = True
cam = bpy.data.objects.new("qa_cam", bpy.data.cameras.new("qa_cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.6
ctr = mathutils.Vector((0, 0, 0.95))
for vname, d in (("front", (0.7, 1, 0.25)), ("side", (1, 0, 0.1))):
    d = mathutils.Vector(d).normalized(); cam.location = ctr + d * 6
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    for a in bpy.data.actions:
        rig.animation_data.action = a
        n = int(a.frame_range[1])
        for i, f in enumerate([0, n // 4, n // 2, 3 * n // 4]):
            sc.frame_set(f)
            sc.render.filepath = os.path.join(out, "%s_%s_%d.png" % (a.name, vname, i))
            bpy.ops.render.render(write_still=True)
