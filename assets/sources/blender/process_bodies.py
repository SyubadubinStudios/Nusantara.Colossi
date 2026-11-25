# Full-body guardian meshes: Rodin body model -> fitted to the rig's rest pose -> split per part.
#
# Run: blender -b --factory-startup -P process_bodies.py [-- only_id ...]
# Inputs : assets/sources/rodin/bodies/<id>.glb  (one Rodin model per guardian)
#          assets/sources/rodin/rigs.json        (rest-pose part matrices: dotnet run tools/rigdump/rigdump.cs)
# Outputs: assets/models/bodies/<id>.glb         (BODY_TRIS triangles, 1024 px textures)
#          assets/models/bodies/lod1/<id>.glb    (LOD_TRIS triangles, 512 px textures)
#
# Each output holds one mesh object per rig part, named "part-<index>". Its vertices are in that
# part's pivot frame, so the game parents the object straight under the part pivot. The meshes then
# move with the rig exactly like the old sculpted boxes did. The boxes stay the climbable colliders.
#
# Game space is glTF space: Y up, guardians face +Z. Blender imports glTF as (x, -z, y).
import sys, os, json, math, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from common import *

BODIES = os.path.join(RODIN, "bodies")
OUT = os.path.join(MODELS, "bodies")
BODY_TRIS, LOD_TRIS = 36000, 9000
RIGS = json.load(open(os.path.join(RODIN, "rigs.json")))


def to_game(b):   # Blender (x, y, z) -> game (x, z, -y)
    return np.c_[b[:, 0], b[:, 2], -b[:, 1]]


def to_blender(g):  # game (x, y, z) -> Blender (x, -z, y)
    return np.c_[g[:, 0], -g[:, 2], g[:, 1]]


def rig_boxes(rig):
    boxes = []
    for p in rig["parts"]:
        M = np.array(p["world"]).reshape(4, 4)  # row-vector: world = [local, 1] @ M
        boxes.append((M, np.linalg.inv(M), np.array(p["center"]), np.array(p["half"])))
    return boxes


def rig_bounds(boxes):
    pts = []
    for M, _, c, h in boxes:
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    pts.append(np.r_[c + h * np.array([sx, sy, sz]), 1] @ M)
    pts = np.array(pts)[:, :3]
    return pts.min(0), pts.max(0)


def signed_distances(points, boxes):
    """(n_points, n_parts) signed distance of game-space points to every part box."""
    hom = np.c_[points, np.ones(len(points))]
    out = np.empty((len(points), len(boxes)))
    for j, (_, inv, c, h) in enumerate(boxes):
        local = (hom @ inv)[:, :3]
        d = np.abs(local - c) - h
        outside = np.linalg.norm(np.maximum(d, 0), axis=1)
        inside = np.minimum(d.max(axis=1), 0)
        out[:, j] = outside + inside
    return out


def yaw(points, deg):
    a = math.radians(deg)
    R = np.array([[math.cos(a), 0, -math.sin(a)], [0, 1, 0], [math.sin(a), 0, math.cos(a)]])
    return points @ R


def fit(points, mn, mx):
    """Non-uniform fit of the point cloud's bounds onto the rig's rest bounds."""
    pmn, pmx = points.min(0), points.max(0)
    return (points - pmn) / np.maximum(pmx - pmn, 1e-6) * (mx - mn) + mn


def score(points, boxes, rng):
    """Lower is better: model points should sit near boxes, and every box should have model nearby."""
    sample = points[rng.choice(len(points), min(4000, len(points)), replace=False)]
    sd = signed_distances(sample, boxes)
    model_to_rig = np.maximum(sd.min(axis=1), 0).mean()
    rig_to_model = np.mean([np.maximum(sd[:, j], 0).min() for j in range(len(boxes))])
    return model_to_rig + rig_to_model


# Roughness floor per guardian. Rodin gave the Ijen warden a near-mirror roughness map (mean 0.27,
# non-metal), which reads as wet plastic under the sky light. Volcanic stone should be matte.
MATTE = {"penjaga-api-biru": 0.55}


def roughen(floor):
    """Remap the glTF roughness channel (green of the metallic-roughness map) to floor..1."""
    for img in list(bpy.data.images):
        if "roughness" not in img.name.lower() or img.size[0] == 0:
            continue
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(px)
        px[1::4] = floor + px[1::4] * (1 - floor)
        new = bpy.data.images.new(img.name + "_mt", w, h, alpha=True)
        new.colorspace_settings.name = img.colorspace_settings.name
        new.pixels.foreach_set(px)
        new.filepath_raw = os.path.join(tempfile.mkdtemp(prefix="nc_rough_"), "rough.png")
        new.file_format = 'PNG'; new.save(); new.pack()
        img.user_remap(new); bpy.data.images.remove(img)
        new.name = new.name[:-3]
        print("ROUGHEN", new.name, "floor", floor)


FILL = 0.75  # how far each part's mesh is stretched toward filling its collider box (0 = untouched)


def fill_box(local, centre, half):
    """
    Stretch and shift a part's mesh toward its box, so the climbable box faces sit close to the
    visible surface (otherwise Arka can hang in mid-air off a thin limb). Scale per axis is clamped
    to 0.6–1.6 so a part never distorts badly; FILL blends between the raw fit and a full box fit.
    """
    lo, hi = local.min(0), local.max(0)
    ext = np.maximum(hi - lo, 1e-4)
    scale = np.clip((2 * half) / ext, 0.6, 1.6)
    scale = 1 + (scale - 1) * FILL
    mid = (lo + hi) / 2
    target = mid + (centre - mid) * FILL
    return (local - mid) * scale + target


def split_export(ob, assign, boxes, path, max_tex):
    """One object per part, vertices moved into the part's pivot frame; export all as one GLB."""
    parts = []
    for j, (M, inv, _, _) in enumerate(boxes):
        faces = np.nonzero(assign == j)[0]
        if len(faces) == 0:
            continue
        part = ob.copy(); part.data = ob.data.copy(); part.name = part.data.name = "part-%d" % j
        bpy.context.collection.objects.link(part)
        bm = bmesh.new(); bm.from_mesh(part.data); bm.faces.ensure_lookup_table()
        keep = set(faces.tolist())
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
        bm.to_mesh(part.data); bm.free()
        co = verts(part)
        local = (np.c_[to_game(co), np.ones(len(co))] @ inv)[:, :3]
        local = fill_box(local, boxes[j][2], boxes[j][3])
        part.data.vertices.foreach_set("co", to_blender(local).ravel())
        part.data.update()
        parts.append(part)
    bpy.data.objects.remove(ob)
    downscale_images(max_tex)
    export_glb(path, image_format='JPEG', jpeg_quality=82)
    return len(parts)


def face_assignment(ob, boxes):
    me = ob.data
    centres = np.empty(len(me.polygons) * 3); me.polygons.foreach_get("center", centres)
    sd = signed_distances(to_game(centres.reshape(-1, 3)), boxes)
    assign = sd.argmin(axis=1)
    # A small part buried in a bigger one (a komodo leg inside the body's box) can end up with no
    # faces, which would leave its bare box showing. Give it the faces inside its own box, or failing
    # that the nearest few.
    for j in range(len(boxes)):
        if np.any(assign == j):
            continue
        inside = np.nonzero(sd[:, j] <= 0)[0]
        if len(inside) == 0:
            inside = np.argsort(sd[:, j])[:max(50, len(sd) // 200)]
        assign[inside] = j
    return assign


def process(gid):
    src = os.path.join(BODIES, gid + ".glb")
    if not os.path.exists(src):
        print("SKIP", gid, "(no Rodin body)"); return
    rig = RIGS[gid]; boxes = rig_boxes(rig); mn, mx = rig_bounds(boxes)
    rng = np.random.default_rng(7)
    reset(); ob = import_joined(src, gid)
    if gid in MATTE:
        roughen(MATTE[gid])
    t0 = tri_count(ob)
    g = to_game(verts(ob))
    # Rodin models face +Z in game space (common.py), so yaw 0 is the default. The non-uniform fit
    # hides width/depth, so the score cannot tell front from back or side on near-symmetric bodies.
    # Only when the model's long horizontal axis clearly disagrees with the rig's (a ship or serpent
    # generated side-on) is a quarter turn chosen, by score.
    ext, rext = g.max(0) - g.min(0), mx - mn
    model_long_x, rig_long_x = ext[0] / ext[2], rext[0] / rext[2]
    candidates = (90, 270) if (model_long_x > 1.3 and rig_long_x < 1 / 1.3) or (model_long_x < 1 / 1.3 and rig_long_x > 1.3) else (0,)
    best = min(((score(fit(yaw(g, d), mn, mx), boxes, rng), d) for d in candidates))
    fitted = fit(yaw(g, best[1]), mn, mx)
    ob.data.vertices.foreach_set("co", to_blender(fitted).ravel()); ob.data.update()
    t1 = decimate(ob, BODY_TRIS)
    path = os.path.join(OUT, gid + ".glb")
    # Keep a copy for the LOD before splitting consumes the object.
    # (Mesh data only: an unlinked datablock is not exported with the full body.)
    lod_mesh = ob.data.copy()
    n = split_export(ob, face_assignment(ob, boxes), boxes, path, 1024)
    validate_counts = os.path.getsize(path)
    # LOD: clear the scene, rebuild an object from the saved mesh, decimate, split, export.
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    lod = bpy.data.objects.new(gid, lod_mesh); bpy.context.collection.objects.link(lod)
    t2 = decimate(lod, LOD_TRIS)
    lod_path = os.path.join(OUT, "lod1", gid + ".glb")
    split_export(lod, face_assignment(lod, boxes), boxes, lod_path, 512)
    print("BODY", gid, "yaw", best[1], "score %.2f" % best[0], "tris", t0, "->", t1, "lod", t2,
          "parts", n, "/", len(boxes), "bytes", validate_counts, os.path.getsize(lod_path))


only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for gid in RIGS:
    if not only or gid in only:
        process(gid)
