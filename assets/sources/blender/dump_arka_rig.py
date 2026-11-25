# Debug helper: dump Arka mesh vertices and bone segments (rest) from arka.blend to .npy/.txt
import bpy, sys, numpy as np
out = sys.argv[sys.argv.index("--")+1]
ob = bpy.data.objects["Arka"]; rig = bpy.data.objects["ArkaRig"]
co = np.empty(len(ob.data.vertices)*3); ob.data.vertices.foreach_get("co", co); np.save(out + "_verts.npy", co.reshape(-1,3))
with open(out + "_bones.txt", "w") as f:
    for b in rig.data.bones: f.write("%s %f %f %f %f %f %f\n" % ((b.name,) + tuple(b.head_local) + tuple(b.tail_local)))
