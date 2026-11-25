# Find X-axis rotation that makes the (pitched) Rodin drone flat: minimal Z extent.
import bpy, sys, numpy as np, math
f = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=f)
o = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
co = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", co); co = co.reshape(-1,3)
M = np.array(o.matrix_world)[:3,:3]; co = co @ M.T
res = []
for d in range(-90, 91, 1):
    t = math.radians(d); c, s = math.cos(t), math.sin(t)
    z = co[:,1]*s + co[:,2]*c; y = co[:,1]*c - co[:,2]*s
    res.append((z.max()-z.min(), d, y.max()-y.min()))
res.sort(); print("TILT", res[:3])
