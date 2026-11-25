# Arka: normalize the Rodin mesh (shallow A-pose), build a simple humanoid armature fitted to it,
# skin it (automatic weights, envelope fallback), then author all gameplay clips with arka_anim.py.
# Run: blender -b --factory-startup -P process_arka.py
# Character faces Blender +Y (= glTF -Z). Character's right side = Blender +X.
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Matrix, Vector
from common import *

FPS = 60
reset()
bpy.context.scene.render.fps = FPS
ob = import_joined(os.path.join(RODIN, "arka.glb"), "Arka")
t0 = tri_count(ob)
rotate_deg(ob, 'Z', 180)            # Rodin faces -Y -> now faces +Y (glTF -Z)
t1 = decimate(ob, 40000)
normalize(ob, 'height', 1.8)        # feet at Z=0, centred on X/Y
# textures stay at source 2048 px in arka.blend; the game GLB is downscaled at export time
print("ARKA tris", t0, "->", t1)

# ---------------------------------------------------------------- landmarks
co = verts(ob)
x, y, z = co[:, 0], co[:, 1], co[:, 2]
H = 1.8
tip = float(np.abs(x).max())
arm_z = float(np.median(z[np.abs(x) > 0.45]))
band = (z > 1.12) & (z < 1.25) & (np.abs(x) < 0.4)
torso_hw = float(np.percentile(np.abs(x[band]), 98))
mid = (np.abs(x) < 0.025) & (z > 0.45) & (z < 1.1)
crotch = float(z[mid].min()) if mid.any() else 0.82
legs = z < 0.45
leg_x = float(np.median(np.abs(x[legs])))
foot_front = float(y[(z < 0.1)].max())
foot_back = float(y[(z < 0.1)].min())
print("LANDMARKS tip %.3f arm_z %.3f torso_hw %.3f crotch %.3f leg_x %.3f foot y %.3f..%.3f"
      % (tip, arm_z, torso_hw, crotch, leg_x, foot_back, foot_front))

# The Rodin mesh is a shallow A-pose: fit the arm centre line z = a + b*|x| from cross-section
# midpoints between |x| 0.4..0.7 (both sides pooled).
xs, zs = [], []
ax_ = np.abs(x)
for b0 in np.arange(0.40, 0.70, 0.02):
    k = (ax_ >= b0) & (ax_ < b0 + 0.02) & (z > 0.95)
    if k.sum() > 5:
        xs.append(b0 + 0.01); zs.append((z[k].min() + z[k].max()) / 2)
arm_b, arm_a = np.polyfit(xs, zs, 1)
ARM_REST = math.degrees(math.atan(-arm_b))          # degrees below horizontal
print("ARM LINE z = %.3f + %.3f*|x|  (%.1f deg below horizontal)" % (arm_a, arm_b, ARM_REST))
arm_line = lambda ax: float(arm_a + arm_b * ax)
# Proportions measured from the silhouette (see dump_arka_rig.py / plot): leg gap closes ~0.70 m,
# shoulders' top ~1.45 m, neck base ~1.45 m.
hip_z = 0.82
sh_x, sh_z = 0.17, 1.37
wrist_x = tip - 0.17
elbow_x = 0.45
knee_z = 0.50
ankle_z = 0.09
ycen = float(np.median(y[(z > 0.9) & (z < 1.4) & (np.abs(x) < 0.15)]))

# ---------------------------------------------------------------- armature
arm_data = bpy.data.armatures.new("ArkaRig")
rig = bpy.data.objects.new("ArkaRig", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None, connect=False, roll=0.0):
    b = eb.new(name); b.head = Vector(head); b.tail = Vector(tail); b.roll = roll
    if parent:
        b.parent = eb[parent]; b.use_connect = connect
    return b


chest_z = 1.18
neck_z = 1.45
head_z = 1.55
bone("hips", (0, ycen, hip_z + 0.03), (0, ycen, 0.97))
bone("spine", (0, ycen, 0.97), (0, ycen, chest_z), "hips", True)
bone("chest", (0, ycen, chest_z), (0, ycen, neck_z), "spine", True)
bone("neck", (0, ycen, neck_z), (0, ycen, head_z), "chest", True)
bone("head", (0, ycen, head_z), (0, ycen, H), "neck", True)
for s, sx in (("R", 1), ("L", -1)):
    # shoulder joint sits slightly above the extrapolated centre line (deltoid); elbow/wrist on the line
    bone("upper_arm." + s, (sx * sh_x, ycen, sh_z), (sx * elbow_x, ycen, arm_line(elbow_x)), "chest")
    bone("lower_arm." + s, (sx * elbow_x, ycen, arm_line(elbow_x)), (sx * wrist_x, ycen, arm_line(wrist_x)), "upper_arm." + s, True)
    bone("hand." + s, (sx * wrist_x, ycen, arm_line(wrist_x)), (sx * tip, ycen, arm_line(tip)), "lower_arm." + s, True)
    bone("upper_leg." + s, (sx * leg_x, ycen, hip_z), (sx * leg_x, ycen + 0.01, knee_z), "hips")
    bone("lower_leg." + s, (sx * leg_x, ycen + 0.01, knee_z), (sx * leg_x, ycen - 0.02, ankle_z), "upper_leg." + s, True)
    bone("foot." + s, (sx * leg_x, ycen - 0.02, ankle_z), (sx * leg_x, foot_front - 0.02, 0.02), "lower_leg." + s, True)
# Right-hand weapon socket (non-deforming): palm centre, pointing along the hand.
_hd = (eb["hand.R"].tail - eb["hand.R"].head).normalized()
_w = eb.new("weapon.R"); _w.head = eb["hand.R"].head + _hd * 0.08; _w.tail = _w.head + _hd * 0.10
_w.parent = eb["hand.R"]; _w.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- skinning
bpy.ops.object.select_all(action='DESELECT')
ob.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
skin_method = "automatic"
bpy.ops.object.parent_set(type='ARMATURE_AUTO')


def unweighted_ratio():
    n = 0
    for v in ob.data.vertices:
        if not any(g.weight > 1e-4 for g in v.groups):
            n += 1
    return n / len(ob.data.vertices)


ur = unweighted_ratio()
print("AUTO WEIGHTS unweighted ratio %.4f" % ur)
if ur > 0.02:
    # Bone-heat failed on parts of the mesh: fall back to envelope weights.
    skin_method = "envelope"
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    ob.parent = None
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
    for b in arm_data.bones:
        b.envelope_distance = 0.12; b.head_radius = b.tail_radius = 0.06
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')
    ur = unweighted_ratio()
    print("ENVELOPE WEIGHTS unweighted ratio %.4f" % ur)

# Any remaining unweighted vertices: give them to the nearest bone segment (keeps export valid).
vg = {g.name: g for g in ob.vertex_groups}
segs = [(b.name, np.array(b.head_local), np.array(b.tail_local)) for b in arm_data.bones]
fixed = 0
for v in ob.data.vertices:
    if any(g.weight > 1e-4 for g in v.groups):
        continue
    p = np.array(v.co); best = None
    for n, a, b in segs:
        ab = b - a; t = np.clip(np.dot(p - a, ab) / np.dot(ab, ab), 0, 1); d = np.linalg.norm(p - (a + t * ab))
        if best is None or d < best[0]:
            best = (d, n)
    if best[1] not in vg:
        vg[best[1]] = ob.vertex_groups.new(name=best[1])
    vg[best[1]].add([v.index], 1.0, 'REPLACE'); fixed += 1
print("SKIN", skin_method, "nearest-bone fixups", fixed)

# ---------------------------------------------------------------- animation (arka_anim.py)
import importlib, arka_anim
importlib.reload(arka_anim)
report = arka_anim.build_all(rig, ob)

rig.animation_data.action = bpy.data.actions["Idle"]
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 150
print("ACTIONS", [(a.name, tuple(a.frame_range)) for a in bpy.data.actions])
print("MESH VALIDATE changed:", ob.data.validate(verbose=False))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, "arka.blend"), compress=True)
export_glb(os.path.join(BLEND, "hires", "arka.glb"), animations=True, image_format='AUTO')  # 2048 px reference
downscale_images(1024)                                   # mobile budget: 1024 px JPEG, <= 3 MB
out = os.path.join(MODELS, "arka.glb")
export_glb(out, animations=True, image_format='JPEG', jpeg_quality=85)
validate(out)
