# Re-author Arka's clips on the existing rig in arka.blend (keeps mesh and skin weights), then export.
# Run: blender -b assets/sources/blender/arka.blend -P assets/sources/blender/reanimate_arka.py
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, importlib
from common import *
import arka_anim
importlib.reload(arka_anim)

bpy.context.scene.render.fps = arka_anim.FPS
rig = bpy.data.objects["ArkaRig"]
report = arka_anim.build_all(rig)
print("CLIPS", report)
rig.animation_data.action = bpy.data.actions["Idle"]
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 180
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, "arka.blend"), compress=True)
export_glb(os.path.join(BLEND, "hires", "arka.glb"), animations=True, image_format='AUTO')
downscale_images(1024)
out = os.path.join(MODELS, "arka.glb")
export_glb(out, animations=True, image_format='JPEG', jpeg_quality=85)
validate(out)
