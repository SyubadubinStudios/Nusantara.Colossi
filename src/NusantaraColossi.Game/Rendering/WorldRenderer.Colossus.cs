using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;

namespace NusantaraColossi.Game.Rendering;

public sealed partial class WorldRenderer
{
    private Node[] partPivots = [];
    private readonly List<(Material Material, float BaseGlow)> skinMaterials = [];
    private Node[] coreNodes = [];
    private Node[] coreHalos = [];
    private Material[] coreMaterials = [];
    private Node[] coreLights = [];
    private float[] coreFlash = [];
    private Vector3 glowColor;

    private MaterialOptions SkinOptions(SurfaceKind surface, ColossusBlueprint bp)
    {
        var body = bp.BodyColor;
        var glow = bp.GlowColor;
        var seed = bp.Id.Length * 3.7f + (int)surface;
        return surface switch
        {
            SurfaceKind.Fur => StoneOptions(body * new Vector3(1.05f, .95f, .85f), .2f, new Vector3(.26f, .32f, .14f), .55f, seed, fur: true, glow: glow, glowIntensity: .15f),
            SurfaceKind.Moss => StoneOptions(body, .8f, new Vector3(.22f, .34f, .13f), 1.2f, seed, glow: glow, glowIntensity: .25f),
            SurfaceKind.Glass => StoneOptions(body, 1, Vector3.Zero, 0, seed, glass: true, glow: glow, glowIntensity: .7f),
            SurfaceKind.Wood => StoneOptions(body, .3f, new Vector3(.16f, .24f, .14f), .5f, seed, wood: true, glow: glow, glowIntensity: .12f),
            SurfaceKind.Coral => StoneOptions(new Vector3(.62f, .42f, .38f), .5f, new Vector3(.1f, .4f, .35f), .9f, seed, glow: glow, glowIntensity: .45f),
            SurfaceKind.Obsidian => StoneOptions(body * .55f, .6f, Vector3.Zero, 0, seed, glow: glow, glowIntensity: 1.1f) with { Custom2 = new Vector4(0, 0, 0, seed) },
            SurfaceKind.Gold => MaterialOptions.Pbr(new Vector4(1f, .76f, .36f, 1), 1, .3f) with { Shader = colossusShader, Custom0 = new Vector4(1f, .76f, .36f, .8f), Custom3 = new Vector4(glow, .4f) },
            SurfaceKind.Cloth => MaterialOptions.Pbr(new Vector4(.62f, .22f, .16f, 1), 0, .95f) with { CullMode = CullMode.None, Shader = colossusShader, Custom0 = new Vector4(.62f, .22f, .16f, .3f), Custom3 = new Vector4(glow, .35f) },
            SurfaceKind.Metal => StoneOptions(body, .4f, Vector3.Zero, 0, seed, glow: glow, glowIntensity: .5f) with { Metallic = .7f },
            _ => StoneOptions(body, 1, new Vector3(.2f, .3f, .12f), .45f, seed, glow: glow, glowIntensity: .3f)
        };
    }

    private void BuildColossus()
    {
        var bp = Encounter.Blueprint;
        glowColor = bp.GlowColor;
        var materials = new Dictionary<SurfaceKind, Material>();
        partPivots = new Node[bp.Parts.Length];
        var boxNodes = new Node[bp.Parts.Length];
        var detail = Quality switch { 0 => .45f, 1 => .7f, _ => 1f };
        for (var i = 0; i < bp.Parts.Length; i++)
        {
            var part = bp.Parts[i];
            if (!materials.TryGetValue(part.Surface, out var material))
            {
                var options = SkinOptions(part.Surface, bp);
                materials[part.Surface] = material = Scene.CreateMaterial(options);
                skinMaterials.Add((material, options.Custom3.W));
            }
            var pivot = Scene.CreateNode(name: $"{bp.Id}-{part.Id}");
            partPivots[i] = pivot;
            var roundness = part.Surface is SurfaceKind.Cloth ? .1f : MathF.Min(part.Half.X, MathF.Min(part.Half.Y, part.Half.Z)) * .55f;
            var noise = part.Surface switch { SurfaceKind.Glass or SurfaceKind.Cloth or SurfaceKind.Gold => .04f, SurfaceKind.Fur => .45f, SurfaceKind.Coral => .6f, _ => .3f };
            var mesh = Scene.AddMesh(MeshFactory.SculptedBox(Scene, part.Half, roundness, noise, i * 17 + bp.Id.Length, detail, 26), material, pivot);
            mesh.Position = part.Center;
            boxNodes[i] = mesh;
        }

        // Rodin full-body model, split per part offline. Each "part-<i>" mesh is already in its pivot's
        // frame, so it is parented under that pivot and moves with the rig. A part that gets a body mesh
        // drops its sculpted box mesh (the node stays: cores hang off it, and the box stays the collider).
        var bodyNear = Quality > 0 || !File.Exists(AssetPaths.BodyLod(bp.Id)) ? AttachBody(AssetPaths.Body(bp.Id), bodyNearParts) : 0;
        var bodyFar = File.Exists(AssetPaths.BodyLod(bp.Id)) ? AttachBody(AssetPaths.BodyLod(bp.Id), bodyFarParts) : 0;
        if (bodyNear > 0 && bodyFar > 0) SetVisible(bodyFarParts, false);
        var covered = new bool[bp.Parts.Length];
        foreach (var (index, _) in bodyNearParts.Concat(bodyFarParts)) covered[index] = true;
        // NC_SHOW_COLLIDERS=1 keeps the box meshes too, to check where the colliders stick out of the body.
        var showColliders = Environment.GetEnvironmentVariable("NC_SHOW_COLLIDERS") == "1";
        for (var i = 0; i < covered.Length; i++)
            if (covered[i] && !showColliders) boxNodes[i].DetachMesh();

        // Without a body model: the Rodin head sculpture, fitted inside the head's collision box. Low
        // quality loads only the light LOD; higher qualities load both and swap by camera distance.
        var head = bp.HeadPart;
        if (head >= 0 && !covered[head])
        {
            var nearPath = AssetPaths.Head(bp.Id);
            var farPath = AssetPaths.HeadLod(bp.Id);
            headNear = Quality > 0 || !File.Exists(farPath) ? PlaceHead(nearPath, head, bp) : null;
            headFar = File.Exists(farPath) ? PlaceHead(farPath, head, bp) : null;
            if (headNear is not null && headFar is not null) headFar.Visible = false;
            if (headNear is not null || headFar is not null)
                boxNodes[head].Scale = new Vector3(.82f); // the carved block stays as the climbable core of the head
        }

        // Cores: a glowing glyph-eye on the face, a halo ring and a light.
        var cores = bp.Cores;
        coreNodes = new Node[cores.Length];
        coreHalos = new Node[cores.Length];
        coreMaterials = new Material[cores.Length];
        coreLights = new Node[cores.Length];
        coreFlash = new float[cores.Length];
        for (var i = 0; i < cores.Length; i++)
        {
            var core = cores[i];
            var part = bp.Parts[core.Part];
            var local = ColossusRig.FacePoint(part.Half, core.FaceAxis, core.FaceSign, core.Uv);
            var normal = Mathx.Axis(core.FaceAxis, core.FaceSign);
            var anchor = boxNodes[core.Part].CreateChild($"core-{core.Id}");
            anchor.Position = local + normal * .15f;
            anchor.Rotation = RotationFromTo(Vector3.UnitY, normal);
            // Anchor sits under the (possibly scaled) head block; undo that scale.
            if (core.Part == head && boxNodes[head].Scale.X < 1) anchor.Scale = new Vector3(1 / boxNodes[head].Scale.X);
            coreMaterials[i] = Scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(glowColor, 1), .2f, .3f) with { Emissive = glowColor, EmissiveIntensity = 2 });
            coreNodes[i] = Mesh(sphere, coreMaterials[i], Vector3.Zero, new Vector3(1.1f, .45f, 1.1f), anchor);
            coreNodes[i].CastShadow = false;
            var ringMat = Mat("core-ring", () => MaterialOptions.Pbr(new Vector4(.15f, .13f, .1f, 1), .8f, .4f));
            Mesh(torus, ringMat, new Vector3(0, .05f, 0), new Vector3(1.6f, 1.6f, 1.6f), anchor).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            var haloMat = Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(glowColor, .6f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 2 });
            coreHalos[i] = Mesh(torus, haloMat, new Vector3(0, .3f, 0), new Vector3(2.4f), anchor);
            coreHalos[i].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            coreHalos[i].CastShadow = false;
            coreLights[i] = Scene.AddLight(Light.Point(glowColor, 0, 16), anchor);
            coreLights[i].Position = new Vector3(0, 1.2f, 0);
        }
        UpdateColossus(0);
    }

    internal static Quaternion RotationFromTo(Vector3 from, Vector3 to)
    {
        var d = Vector3.Dot(from, to);
        if (d > .9999f) return Quaternion.Identity;
        if (d < -.9999f) return Quaternion.CreateFromAxisAngle(MathF.Abs(from.X) < .9f ? Vector3.UnitX : Vector3.UnitZ, MathF.PI);
        var axis = Vector3.Normalize(Vector3.Cross(from, to));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(Math.Clamp(d, -1, 1)));
    }

    /// <summary>Called on a core hit to flash its glow.</summary>
    public void FlashCore(int index)
    {
        if ((uint)index < (uint)coreFlash.Length) coreFlash[index] = 1;
    }

    private Node? headNear, headFar;
    private bool headFarShown, bodyFarShown;
    private readonly List<(int Part, Node Node)> bodyNearParts = [], bodyFarParts = [];

    /// <summary>
    /// Loads a split body model and moves each "part-&lt;i&gt;" node under part pivot i. Returns how many
    /// parts it found (0 when the file is missing or fails to load).
    /// </summary>
    private int AttachBody(string path, List<(int Part, Node Node)> into)
    {
        if (!File.Exists(path)) return 0;
        try
        {
            var import = Scene.LoadModelCached(path, partPivots[0]);
            var stack = new Stack<Node>();
            stack.Push(import.Root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.Name is { } name && name.StartsWith("part-", StringComparison.Ordinal)
                    && int.TryParse(name.AsSpan(5), out var index) && index >= 0 && index < partPivots.Length)
                {
                    node.Parent = partPivots[index];
                    node.SetTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
                    node.SetShadowsRecursive(true, true);
                    into.Add((index, node));
                    continue;
                }
                foreach (var child in node.Children) stack.Push(child);
            }
            import.Root.Remove();
            return into.Count;
        }
        catch (ThreeNetException ex)
        {
            AssetWarning = $"{Path.GetFileName(path)}: {ex.Message}";
            return 0;
        }
    }

    private static void SetVisible(List<(int Part, Node Node)> parts, bool visible)
    {
        foreach (var (_, node) in parts) SetVisibleRecursive(node, visible);
    }

    private static void SetVisibleRecursive(Node node, bool visible)
    {
        node.Visible = visible;
        foreach (var child in node.Children) SetVisibleRecursive(child, visible);
    }

    /// <summary>Swaps the full body for its LOD beyond about three guardian heights (15% hysteresis).</summary>
    private void UpdateBodyLod()
    {
        if (bodyNearParts.Count == 0 || bodyFarParts.Count == 0) return;
        var distance = Vector3.Distance(Camera.WorldPosition, partPivots[0].WorldPosition);
        var threshold = MathF.Max(60, Encounter.Blueprint.Height * 3) * (bodyFarShown ? .85f : 1f);
        var far = distance > threshold;
        if (far == bodyFarShown) return;
        bodyFarShown = far;
        SetVisible(bodyNearParts, !far);
        SetVisible(bodyFarParts, far);
    }

    /// <summary>Loads a head model under the head pivot, scaled and centred inside the head's box.</summary>
    private Node? PlaceHead(string path, int head, ColossusBlueprint bp)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var import = Scene.LoadModelCached(path, partPivots[head]);
            var bounds = Scene.GetBounds(import.Root);
            if (bounds.IsEmpty) return null;
            var half = bp.Parts[head].Half;
            var scale = MathF.Min(half.X * 2.3f / MathF.Max(bounds.Size.X, .01f), MathF.Min(half.Y * 2.3f / MathF.Max(bounds.Size.Y, .01f), half.Z * 2.3f / MathF.Max(bounds.Size.Z, .01f)));
            import.Root.Scale = new Vector3(scale);
            // Models face -Z; colossi face +Z.
            import.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
            var center = (bounds.Min + bounds.Max) * .5f * scale;
            import.Root.Position = bp.Parts[head].Center - new Vector3(-center.X, center.Y, -center.Z);
            import.Root.SetShadowsRecursive(true, true);
            return import.Root;
        }
        catch (ThreeNetException ex)
        {
            AssetWarning = $"{Path.GetFileName(path)}: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Shows the light head beyond about eight head-heights from the camera, with 15% hysteresis so
    /// the swap does not flicker at the threshold.
    /// </summary>
    private void UpdateHeadLod()
    {
        if (headNear is null || headFar is null) return;
        var bp = Encounter.Colossus.Blueprint;
        var size = bp.Parts[bp.HeadPart].Half.Y * 2;
        var distance = Vector3.Distance(Camera.WorldPosition, partPivots[bp.HeadPart].WorldPosition);
        var threshold = MathF.Max(40, size * 8) * (headFarShown ? .85f : 1f);
        var far = distance > threshold;
        if (far == headFarShown) return;
        headFarShown = far;
        headNear.Visible = !far;
        headFar.Visible = far;
    }

    private void UpdateColossus(float dt)
    {
        UpdateHeadLod();
        UpdateBodyLod();
        var colossus = Encounter.Colossus;
        var world = colossus.PartWorld;
        for (var i = 0; i < partPivots.Length; i++)
        {
            if (Matrix4x4.Decompose(world[i], out var scale, out var rotation, out var translation))
                partPivots[i].SetTransform(translation, rotation, scale);
        }
        var pulse = colossus.CorePulse(colossus.Pose.Time);
        var rage = colossus.Rage;
        var dying = colossus.Pose.Collapse;
        // Seams glow with the heartbeat, hotter in rage, and fade as the guardian falls.
        var seam = (.55f + pulse * .9f) * (1 + rage * .5f) * (1 - dying);
        foreach (var (material, glow) in skinMaterials)
        {
            if (glow <= 0) continue;
            var w = glow * seam;
            material.Update(o => o with { Custom3 = o.Custom3 with { W = w } });
        }
        var legend = Encounter.Options.Legend;
        for (var i = 0; i < coreNodes.Length; i++)
        {
            var state = colossus.Cores[i];
            coreFlash[i] = MathF.Max(0, coreFlash[i] - dt * 2.5f);
            var open = state.SealOpen > 0;
            float intensity;
            Vector3 color;
            if (state.Destroyed) { intensity = .05f; color = new Vector3(.15f, .12f, .1f); }
            else if (open) { intensity = 2.2f + pulse * 9 + coreFlash[i] * 20; color = glowColor; }
            else { intensity = legend ? .25f : .6f + pulse * .6f; color = Vector3.Lerp(glowColor, new Vector3(.45f, .45f, .5f), .6f); }
            coreMaterials[i].Update(o => o with { Emissive = color, EmissiveIntensity = intensity });
            coreHalos[i].Visible = !state.Destroyed && open;
            var haloScale = 2.2f + pulse * .8f + coreFlash[i] * 2;
            coreHalos[i].Scale = new Vector3(haloScale);
            // Sealed and destroyed cores read through emissive alone; a point light costs every pixel.
            coreLights[i].Light = !state.Destroyed && (open || coreFlash[i] > 0)
                ? Light.Point(color, 30 + pulse * 120 + coreFlash[i] * 300, 18)
                : null;
        }
    }
}
