using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;

namespace NusantaraColossi.Game.Rendering;

/// <summary>
/// Arka. Uses the rigged model (assets/models/arka.glb with Idle/Run/Climb/Jump/Stab/Hang clips) when it
/// exists, otherwise a procedural figure animated in code. Either way, orientation comes from the
/// player's surface frame so he hugs walls, hangs under bellies and stands on backs.
/// </summary>
internal sealed class CharacterVisual
{
    public Node Root { get; }
    private readonly Node body;
    private readonly Node? hips, torso, head, armL, armR, legL, legR, blade;
    private readonly Dictionary<string, AnimationPlayer> clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> durations = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Clips that play once from the start and hold their last frame (all clips are loaded looping).</summary>
    private static readonly HashSet<string> OneShots = new(StringComparer.OrdinalIgnoreCase) { "Death", "Dodge", "StabRelease" };
    private readonly AnimationMixer mixer = new();
    private string currentClip = "";
    private float phase;
    private Quaternion smoothedRotation = Quaternion.Identity;
    private Vector3 smoothedPosition;
    private bool first = true;

    public bool HasModel => clips.Count > 0 || modelLoaded;
    private readonly bool modelLoaded;

    public CharacterVisual(WorldRenderer world, Scene scene, string name, Vector3 jacket, bool hologram = false)
    {
        Root = scene.CreateNode(name: name);
        body = Root.CreateChild("body");
        var path = AssetPaths.Model("arka");
        if (!hologram && File.Exists(path))
        {
            try
            {
                var before = scene.Animations.Count;
                var import = scene.LoadModel(path, body);
                import.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI); // model faces -Z
                import.Root.SetShadowsRecursive(true, false);
                for (var i = before; i < scene.Animations.Count; i++)
                {
                    var clip = scene.Animations[i];
                    var player = clip.Play(loop: true, weight: 0);
                    clips[clip.Name] = player;
                    durations[clip.Name] = clip.Duration;
                }
                modelLoaded = true;
                Play("Idle", 0);
                return;
            }
            catch (ThreeNetException) { /* fall back to the procedural figure */ }
        }

        MaterialOptions Look(Vector3 c, float rough = .8f) => hologram
            ? MaterialOptions.Basic(new Vector4(.25f, .85f, .82f, .35f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 1.5f }
            : MaterialOptions.Pbr(new Vector4(c, 1), 0, rough);
        var jacketMat = scene.CreateMaterial(Look(jacket));
        var skin = scene.CreateMaterial(Look(new Vector3(.55f, .36f, .24f), .6f));
        var dark = scene.CreateMaterial(Look(new Vector3(.08f, .09f, .1f)));
        var batik = scene.CreateMaterial(Look(new Vector3(.78f, .55f, .2f), .7f));
        var visor = scene.CreateMaterial(hologram ? Look(Vector3.One) : MaterialOptions.Pbr(new Vector4(.1f, .5f, .5f, 1), .5f, .2f) with { Emissive = new Vector3(.25f, .85f, .82f), EmissiveIntensity = 2 });
        var bladeMat = scene.CreateMaterial(hologram ? Look(Vector3.One) : MaterialOptions.Pbr(new Vector4(1f, .75f, .35f, 1), 1, .2f) with { Emissive = new Vector3(1f, .65f, .25f), EmissiveIntensity = 1.5f });
        var capsule = scene.CreateCapsuleGeometry(.5f, 1, 12, 4);
        var ball = scene.CreateSphereGeometry(1, 14, 10);
        var cube = scene.CreateBoxGeometry();

        Node Part(Node parent, Geometry g, Material m, Vector3 p, Vector3 s)
        {
            var n = scene.AddMesh(g, m, parent);
            n.Position = p;
            n.Scale = s;
            n.CastShadow = !hologram;
            return n;
        }
        hips = body.CreateChild("hips");
        hips.Position = new Vector3(0, .95f, 0);
        torso = hips.CreateChild("torso");
        Part(torso, capsule, jacketMat, new Vector3(0, .3f, 0), new Vector3(.4f, .5f, .26f));
        Part(torso, cube, batik, new Vector3(0, .58f, .02f), new Vector3(.36f, .08f, .28f)); // batik scarf
        Part(torso, cube, dark, new Vector3(0, .32f, -.18f), new Vector3(.3f, .38f, .16f));  // climbing pack
        head = torso.CreateChild("head");
        head.Position = new Vector3(0, .72f, 0);
        Part(head, ball, skin, new Vector3(0, .02f, 0), new Vector3(.12f, .14f, .13f));
        Part(head, ball, dark, new Vector3(0, .08f, -.01f), new Vector3(.125f, .1f, .13f)); // hair
        Part(head, cube, visor, new Vector3(0, .03f, .11f), new Vector3(.2f, .045f, .03f));
        Node Limb(Node parent, Vector3 pivot, Material m, float length, float width)
        {
            var joint = parent.CreateChild();
            joint.Position = pivot;
            Part(joint, capsule, m, new Vector3(0, -length * .5f, 0), new Vector3(width, length, width));
            return joint;
        }
        armL = Limb(torso, new Vector3(-.24f, .55f, 0), jacketMat, .62f, .11f);
        armR = Limb(torso, new Vector3(.24f, .55f, 0), jacketMat, .62f, .11f);
        legL = Limb(hips, new Vector3(-.1f, 0, 0), dark, .92f, .14f);
        legR = Limb(hips, new Vector3(.1f, 0, 0), dark, .92f, .14f);
        blade = Part(armR, cube, bladeMat, new Vector3(0, -.68f, .12f), new Vector3(.03f, .05f, .42f)); // keris-shaped energy blade
    }

    private float stabRelease, dodgeHold, oneShotElapsed;

    private string Pick(string preferred, string fallback) => clips.ContainsKey(preferred) ? preferred : fallback;

    private void Play(string name, float fade)
    {
        if (currentClip == name || !clips.TryGetValue(name, out var next)) return;
        if (OneShots.Contains(name)) { next.Time = 0; next.Speed = 1; oneShotElapsed = 0; }
        if (clips.TryGetValue(currentClip, out var previous)) mixer.CrossFade(previous, next, fade);
        else { next.Weight = 1; next.Time = 0; }
        currentClip = name;
    }

    public void Update(Player p, float dt, bool visible = true)
    {
        Root.Visible = visible && (p.Mode != PlayerMode.Dead || (modelLoaded && clips.ContainsKey("Death")));
        var up = Mathx.SafeNormalize(p.Up, Vector3.UnitY);
        var forward = p.Facing - up * Vector3.Dot(p.Facing, up);
        forward = Mathx.SafeNormalize(forward, Vector3.Cross(Vector3.UnitX, up));
        var right = Vector3.Cross(up, forward);
        var basis = new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1);
        var target = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(basis));
        if (first) { smoothedRotation = target; smoothedPosition = p.Position; first = false; }
        smoothedRotation = Quaternion.Slerp(smoothedRotation, target, 1 - MathF.Exp(-14 * dt));
        smoothedPosition = Vector3.Distance(smoothedPosition, p.Position) > 3 ? p.Position : Mathx.Damp(smoothedPosition, p.Position, 25, dt);
        Root.Position = smoothedPosition;
        Root.Rotation = smoothedRotation;

        var climbing = p.Mode == PlayerMode.Attached && !p.IsStanding;
        var hanging = climbing && p.Up.Y < .1f && Vector3.Dot(p.Facing, Vector3.UnitY) > .5f;
        if (clips.Count > 0)
        {
            // Preferred clip first, then fallbacks for older rigs that lack it.
            var horizontal = new Vector2(p.Velocity.X, p.Velocity.Z).Length();
            if (p.PendingStabRelease) stabRelease = .4f;
            stabRelease = MathF.Max(0, stabRelease - dt);
            if (p.DodgeTimer > 0) dodgeHold = .5f;
            dodgeHold = MathF.Max(0, dodgeHold - dt);
            var clip = p.Mode == PlayerMode.Dead ? Pick("Death", "Idle")
                : p.IsRiding ? Pick("Ride", "Idle")
                : p.Gliding ? Pick("Glide", "Hang")
                : p.Charging ? "Stab"
                : stabRelease > 0 ? Pick("StabRelease", "Stab")
                : dodgeHold > 0 && p.Mode != PlayerMode.Attached ? Pick("Dodge", "Run")
                : p.Mode == PlayerMode.Swim ? Pick("Swim", "Run")
                : p.Mode == PlayerMode.Air ? (p.Velocity.Y < -6 ? Pick("Fall", "Jump") : "Jump")
                : hanging ? "Hang"
                : climbing ? (p.IsMoving ? "Climb" : Pick("ClimbIdle", "Hang"))
                : p.IsMoving ? (horizontal > 7 ? Pick("Sprint", "Run") : "Run")
                : "Idle";
            Play(clip, clip is "Dodge" or "StabRelease" ? .08f : .18f);
            mixer.Update(dt);
            // Count elapsed time ourselves: the looping player would wrap past the end between frames.
            if (OneShots.Contains(currentClip) && clips.TryGetValue(currentClip, out var shot) && durations.TryGetValue(currentClip, out var length)
                && (oneShotElapsed += dt) >= length - 1 / 60f)
            {
                shot.Time = length - 1 / 60f; // hold the final pose instead of looping
                shot.Speed = 0;
            }
            if (clips.TryGetValue("Climb", out var c)) c.Speed = p.IsMoving ? 1 : 0;
            if (clips.TryGetValue("Run", out var run)) run.Speed = Math.Clamp(horizontal / 5.5f, .6f, 1.6f);
            return;
        }
        if (hips is null) return;
        // ---- procedural animation ----
        var speed = p.Mode == PlayerMode.Attached ? (p.IsMoving ? 1 : 0) : MathF.Min(1, new Vector2(p.Velocity.X, p.Velocity.Z).Length() / 8);
        phase += dt * (climbing ? 5 : 4 + speed * 7) * (p.IsMoving || speed > .1f ? 1 : 0);
        var s = MathF.Sin(phase);
        float lArm, rArm, lLeg, rLeg, lean = 0, spread = 0;
        if (p.Charging)
        {
            var c = p.StabCharge;
            lArm = rArm = -2.6f - c * .4f;
            lLeg = .2f; rLeg = -.2f;
            lean = -.15f * c;
        }
        else if (climbing)
        {
            lArm = -2.4f + s * .5f; rArm = -2.4f - s * .5f;
            lLeg = .5f - s * .4f; rLeg = .5f + s * .4f;
            spread = .25f;
        }
        else if (p.Mode == PlayerMode.Air)
        {
            lArm = -1.2f; rArm = -1.4f; lLeg = .9f; rLeg = .3f;
        }
        else
        {
            var stride = .9f * speed;
            lArm = -s * stride * .8f; rArm = s * stride * .8f;
            lLeg = s * stride; rLeg = -s * stride;
            lean = .18f * speed;
        }
        armL!.Rotation = Quaternion.CreateFromYawPitchRoll(0, lArm, -spread);
        armR!.Rotation = Quaternion.CreateFromYawPitchRoll(0, rArm, spread);
        legL!.Rotation = Quaternion.CreateFromYawPitchRoll(0, lLeg, -spread * .4f);
        legR!.Rotation = Quaternion.CreateFromYawPitchRoll(0, rLeg, spread * .4f);
        torso!.Rotation = Quaternion.CreateFromYawPitchRoll(0, lean, 0);
        hips.Position = new Vector3(0, .95f + MathF.Abs(MathF.Cos(phase)) * .05f * speed, climbing ? -.15f : 0);
        blade!.Visible = p.Charging || p.Mode == PlayerMode.Attached;
    }
}

/// <summary>GARUDA: the drone companion. Follows over the shoulder, rises to scan, beams to hack.</summary>
internal sealed class DroneVisual
{
    public Node Root { get; }
    private readonly Node[] rotors = new Node[4];
    private readonly Node light;
    private Vector3 position;
    private Vector3 velocity;
    private float rise;
    private bool initialised;

    public DroneVisual(Scene scene, string name)
    {
        Root = scene.CreateNode(name: name);
        var path = AssetPaths.Model("garuda-drone");
        var loaded = false;
        if (File.Exists(path))
        {
            try
            {
                var import = scene.LoadModelCached(path, Root);
                import.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
                loaded = true;
            }
            catch (ThreeNetException) { }
        }
        var ball = scene.CreateSphereGeometry(1, 16, 10);
        var cube = scene.CreateBoxGeometry();
        var disc = scene.CreateCylinderGeometry(.5f, .5f, .02f, 16);
        var shell = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(.92f, .9f, .86f, 1), .1f, .35f));
        var gold = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(.95f, .72f, .3f, 1), 1, .3f));
        var eye = scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.25f, .9f, .85f, 1)) with { EmissiveIntensity = 4 });
        var blur = scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.7f, .9f, .9f, .25f)) with { AlphaMode = AlphaMode.Blend, DepthWrite = false });
        if (!loaded)
        {
            scene.AddMesh(ball, shell, Root).Scale = new Vector3(.18f, .1f, .24f);
            var wingL = scene.AddMesh(cube, gold, Root);
            wingL.Position = new Vector3(-.26f, .02f, -.02f);
            wingL.Scale = new Vector3(.32f, .02f, .12f);
            wingL.Rotation = Quaternion.CreateFromYawPitchRoll(-.3f, 0, .15f);
            var wingR = scene.AddMesh(cube, gold, Root);
            wingR.Position = new Vector3(.26f, .02f, -.02f);
            wingR.Scale = new Vector3(.32f, .02f, .12f);
            wingR.Rotation = Quaternion.CreateFromYawPitchRoll(.3f, 0, -.15f);
            scene.AddMesh(ball, eye, Root).Position = new Vector3(0, 0, .22f);
            Root.Children[^1].Scale = new Vector3(.05f);
        }
        for (var i = 0; i < 4; i++)
        {
            rotors[i] = scene.AddMesh(disc, blur, Root);
            rotors[i].Position = new Vector3(i % 2 == 0 ? -.3f : .3f, .08f, i < 2 ? .18f : -.18f);
            rotors[i].Scale = new Vector3(.22f, 1, .22f);
            rotors[i].CastShadow = false;
        }
        light = scene.AddLight(Light.Point(new Vector3(.3f, .9f, .85f), 4, 6), Root);
        light.Light = null; // Lit only while its hunter is in the hunt.
    }

    /// <summary>Removes the drone's light while its slot is empty (lights cost every pixel).</summary>
    public void Hide()
    {
        Root.Visible = false;
        light.Light = null;
    }

    public Vector3 Position => position;

    public void Scan() => rise = 1.4f;

    public void Update(Player p, float dt, float time)
    {
        Root.Visible = p.Mode != PlayerMode.Dead;
        rise = MathF.Max(0, rise - dt);
        var side = Vector3.Cross(Vector3.UnitY, Mathx.SafeNormalize(Mathx.Flat(p.Facing), Vector3.UnitZ));
        var target = p.BodyCenter + Vector3.UnitY * (1.3f + MathF.Sin(time * 1.7f) * .12f + rise * 2.5f) - side * .9f + (p.Mode == PlayerMode.Attached ? -p.Facing * 1.6f : Vector3.Zero);
        if (!initialised) { position = target; initialised = true; }
        // Critically damped spring: drifts like a hovering bird, never overshoots into walls.
        var k = 9f;
        var accel = (target - position) * k * k - velocity * 2 * k;
        velocity += accel * dt;
        position += velocity * dt;
        Root.Position = position;
        var heading = Mathx.YawOf(Mathx.Flat(p.Facing));
        Root.Rotation = Quaternion.CreateFromYawPitchRoll(heading, Math.Clamp(velocity.Z * .02f, -.3f, .3f), Math.Clamp(-velocity.X * .03f, -.4f, .4f));
        foreach (var r in rotors) r.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, time * 40);
        light.Light = p.Mode == PlayerMode.Dead ? null : Light.Point(new Vector3(.3f, .9f, .85f), 4 + rise * 30, 6 + rise * 10);
    }
}
