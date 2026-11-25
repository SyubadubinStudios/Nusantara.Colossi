# Shared helpers for the Nusantara Colossi asset pipeline (Blender 5.2, run headless:
#   blender -b --factory-startup -P <script>.py
# Conventions: Blender is Z-up; the glTF exporter (+Y up) maps Blender +Y -> glTF -Z.
# So "faces glTF -Z" == "faces Blender +Y". Rodin outputs face Blender -Y, so we turn them 180 deg.
import bpy, math, os, tempfile
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", ".."))
RODIN = os.path.join(ASSETS, "sources", "rodin")
MODELS = os.path.join(ASSETS, "models")
BLEND = os.path.join(ASSETS, "sources", "blender")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_joined(path, name):
    """Import a GLB, drop non-mesh helpers, join meshes, apply all transforms."""
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            # keep children's world transform before deleting empties
            for c in o.children:
                mw = c.matrix_world.copy(); c.parent = None; c.matrix_world = mw
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name; ob.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


def verts(ob):
    co = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def bounds(ob):
    co = verts(ob)
    return co.min(0), co.max(0)


def tri_count(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


def transform(ob, M):
    ob.data.transform(M)
    ob.data.update()


def rotate_deg(ob, axis, deg):
    transform(ob, Matrix.Rotation(math.radians(deg), 4, axis))


def normalize(ob, mode, size):
    """mode 'height': Z extent = size; mode 'width': X extent = size.
    Then centre on X/Y and put the bottom at Z=0."""
    mn, mx = bounds(ob)
    ext = mx - mn
    s = size / (ext[2] if mode == 'height' else ext[0])
    transform(ob, Matrix.Scale(s, 4))
    mn, mx = bounds(ob)
    c = (mn + mx) / 2
    transform(ob, Matrix.Translation(Vector((-c[0], -c[1], -mn[2]))))


def decimate(ob, max_tris):
    t = tri_count(ob)
    if t <= max_tris:
        return t
    ratio = (max_tris * 0.97) / t
    m = ob.modifiers.new("Decimate", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'; m.ratio = ratio; m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=m.name)
    return tri_count(ob)


def downscale_images(max_size, tmpdir=None):
    """Downscale every image to max_size and replace it by a freshly packed PNG
    (so the glTF exporter writes the reduced pixels)."""
    tmpdir = tmpdir or tempfile.mkdtemp(prefix="nc_tex_")
    for img in list(bpy.data.images):
        w, h = img.size
        if max(w, h) <= max_size or w == 0:
            continue
        nw, nh = (max_size, max(1, round(h * max_size / w))) if w >= h else (max(1, round(w * max_size / h)), max_size)
        img.scale(nw, nh)
        px = np.empty(nw * nh * 4, dtype=np.float32); img.pixels.foreach_get(px)
        new = bpy.data.images.new(img.name + "_ds", nw, nh, alpha=True)
        new.colorspace_settings.name = img.colorspace_settings.name
        new.pixels.foreach_set(px)
        p = os.path.join(tmpdir, bpy.path.clean_name(img.name) + ".png")
        new.filepath_raw = p; new.file_format = 'PNG'; new.save()
        new.pack()
        img.user_remap(new)
        bpy.data.images.remove(img)
        new.name = new.name[:-3]


def export_glb(path, animations=False, image_format='JPEG', jpeg_quality=85):
    """Mobile budget default: all textures re-encoded as JPEG inside the GLB."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    kw = dict(filepath=path, export_format='GLB', use_selection=False, export_yup=True,
              export_apply=True, export_image_format=image_format)
    if image_format == 'JPEG':
        kw['export_jpeg_quality'] = jpeg_quality
    if animations:
        kw.update(export_animations=True, export_animation_mode='ACTIONS', export_force_sampling=True,
                  export_frame_range=False, export_anim_single_armature=True, export_skins=True,
                  export_def_bones=False)
    else:
        kw.update(export_animations=False)
    bpy.ops.export_scene.gltf(**kw)


def validate(path):
    """Re-import an exported GLB into an empty scene and report tris / bounds / actions."""
    reset()
    bpy.ops.import_scene.gltf(filepath=path)
    tris = 0; mn = np.full(3, 1e9); mx = np.full(3, -1e9)
    for o in bpy.context.scene.objects:
        # skip the importer's bone display shape (Icosphere) - it is not part of the file's meshes
        if o.type != 'MESH' or o.name.startswith("Icosphere") or not o.visible_get():
            continue
        m = o.data; m.calc_loop_triangles(); tris += len(m.loop_triangles)   # rest (bind) pose
        co = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
        W = np.array(o.matrix_world); co = co @ W[:3, :3].T + W[:3, 3]
        mn = np.minimum(mn, co.min(0)); mx = np.maximum(mx, co.max(0))
    # Blender Z-up -> glTF Y-up: gltf (x, y, z) = (x, z, -y)
    gmn = (mn[0], mn[2], -mx[1]); gmx = (mx[0], mx[2], -mn[1])
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    acts = [(a.name, "%.2fs" % ((a.frame_range[1] - a.frame_range[0]) / fps)) for a in bpy.data.actions]
    imgs = [(i.name, tuple(i.size)) for i in bpy.data.images]
    arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    print("VALIDATE", os.path.basename(path), "tris", tris,
          "gltf_min", tuple(round(float(v), 3) for v in gmn), "gltf_max", tuple(round(float(v), 3) for v in gmx),
          "bones", len(arm[0].data.bones) if arm else 0, "actions", acts, "images", imgs,
          "size_MB", round(os.path.getsize(path) / 1e6, 2))
