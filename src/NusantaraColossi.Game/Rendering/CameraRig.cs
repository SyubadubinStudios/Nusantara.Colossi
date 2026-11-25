using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;

namespace NusantaraColossi.Game.Rendering;

/// <summary>
/// Third-person orbit camera. Lock-on swings the view so the guardian fills the frame behind Arka,
/// the classic boss-hunter framing that makes the scale legible. Shake uses trauma² for punchy hits.
/// </summary>
public sealed class CameraRig
{
    public float Yaw { get; set; }
    public float Pitch { get; set; } = -.18f;
    public bool LockOn { get; set; }
    public float ShakeScale { get; set; } = 1;
    public float FieldOfView { get; private set; } = 58;
    public Vector3 Position { get; private set; }
    public Vector3 Focus { get; private set; }
    public Vector3 Forward => new(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));

    private float distance = 7;
    private float trauma;
    private float time;
    private bool initialised;
    private Vector3 smoothedFocus;
    private Vector3 shakeOffset;
    private float shakeRoll;

    public void AddTrauma(float amount) => trauma = MathF.Min(1, trauma + amount);

    /// <summary>Cinematic orbit used behind the title screen.</summary>
    public void Showcase(Colossus colossus, RegionDefinition region, float dt)
    {
        time += dt;
        var head = colossus.Blueprint.HeadPart;
        var target = colossus.PartWorld[Math.Max(0, head)].Translation * .6f + colossus.Pose.Position * .4f;
        var radius = colossus.Blueprint.Height * 1.6f + 25;
        var angle = time * .05f + 2.2f;
        var position = target + new Vector3(MathF.Sin(angle) * radius, colossus.Blueprint.Height * .1f, MathF.Cos(angle) * radius);
        position.Y = MathF.Max(position.Y, region.Height(position.X, position.Z) + 3);
        Position = position;
        Focus = target;
        FieldOfView = 46;
        shakeOffset = Vector3.Zero;
        shakeRoll = 0;
    }

    private float cineTime, cineStart;
    private bool cineActive;
    private Vector3 photoFocus, photoOrigin;
    private float photoDistance, photoFov;

    /// <summary>
    /// The guardian has fallen: a slow wide orbit while it collapses (framing scaled to its height), then,
    /// with <paramref name="inscription"/>, a close low orbit on the head where its inscription glows.
    /// Starts from the current view and eases there, so there is no hard cut.
    /// </summary>
    public void Cinematic(Colossus colossus, RegionDefinition region, bool inscription, float dt)
    {
        if (!cineActive)
        {
            cineActive = true;
            cineTime = 0;
            cineStart = Yaw + MathF.PI * .7f;
        }
        time += dt;
        cineTime += dt;
        var bp = colossus.Blueprint;
        var head = colossus.PartWorld[Math.Max(0, bp.HeadPart)].Translation;
        var body = colossus.Pose.Position + Vector3.UnitY * bp.Height * .3f;
        var k = Math.Clamp(colossus.Pose.Collapse, 0, 1);
        Vector3 focus;
        float radius, height, angle, fov;
        if (!inscription)
        {
            focus = Vector3.Lerp(head, body, .35f + .35f * k);
            radius = bp.Height * 1.1f + 16;
            height = bp.Height * (.5f - .3f * k) + 3;
            angle = cineStart + cineTime * .1f;
            fov = 50;
        }
        else
        {
            focus = head + Vector3.UnitY * .5f;
            radius = MathF.Max(7, bp.Height * .22f);
            height = 1.5f + bp.Height * .04f;
            angle = cineStart + .9f + cineTime * .035f;
            fov = 40;
        }
        var want = focus + new Vector3(MathF.Sin(angle) * radius, height, MathF.Cos(angle) * radius);
        var ground = region.Height(want.X, want.Z) + 1.2f;
        if (region.HasWater) ground = MathF.Max(ground, region.WaterLevel + 1);
        want.Y = MathF.Max(want.Y, ground);
        Position = Mathx.Damp(Position, want, 1.6f, dt);
        Focus = Mathx.Damp(Focus, focus, 2.5f, dt);
        FieldOfView = Mathx.Damp(FieldOfView, fov, 1.5f, dt);
        trauma = MathF.Max(0, trauma - dt * .9f);
        var shake = trauma * trauma * ShakeScale * .5f;
        shakeOffset = new Vector3(Noise.Value(time * 18, 0, 3) - .5f, Noise.Value(time * 18, 5, 3) - .5f, 0) * shake;
        shakeRoll = 0;
    }

    public void BeginPhoto()
    {
        photoFocus = photoOrigin = Focus;
        photoDistance = Math.Clamp(Vector3.Distance(Position, Focus), 3, 30);
        photoFov = FieldOfView;
        var to = Focus - Position;
        Yaw = Mathx.YawOf(Mathx.Flat(to));
        Pitch = Math.Clamp(MathF.Atan2(to.Y, Mathx.Flat(to).Length()), -1.4f, 1.4f);
        shakeOffset = Vector3.Zero;
        shakeRoll = 0;
    }

    /// <summary>Photo mode: a free orbit camera that stays within 40 m of where the hunt was paused.</summary>
    public void Photo(RegionDefinition region, Vector2 look, Vector2 pan, float rise, float zoom, float dt)
    {
        Yaw = Mathx.WrapAngle(Yaw + look.X);
        Pitch = Math.Clamp(Pitch + look.Y, -1.4f, 1.4f);
        var flat = Mathx.Forward(Yaw);
        var right = Vector3.Cross(flat, Vector3.UnitY);
        photoFocus += (right * pan.X + flat * pan.Y) * 8 * dt + Vector3.UnitY * rise * 5 * dt;
        var offset = photoFocus - photoOrigin;
        if (offset.Length() > 40) photoFocus = photoOrigin + Vector3.Normalize(offset) * 40;
        photoFov = Math.Clamp(photoFov - zoom * 30 * dt, 15, 90);
        var position = photoFocus - Forward * photoDistance;
        var ground = region.Height(position.X, position.Z) + .4f;
        if (region.HasWater) ground = MathF.Max(ground, region.WaterLevel + .3f);
        position.Y = MathF.Max(position.Y, ground);
        Position = position;
        Focus = photoFocus;
        FieldOfView = photoFov;
        shakeOffset = Vector3.Zero;
        shakeRoll = 0;
    }

    public void Update(Player player, Colossus colossus, RegionDefinition region, Vector2 look, float dt)
    {
        time += dt;
        cineActive = false;
        var attached = player.Mode == PlayerMode.Attached;
        var climbing = attached && !player.IsStanding;
        var playerFocus = player.BodyCenter + Vector3.UnitY * (climbing ? .4f : .7f);
        if (!initialised)
        {
            smoothedFocus = playerFocus;
            Yaw = player.Yaw;
            initialised = true;
        }
        smoothedFocus = Vector3.Distance(smoothedFocus, playerFocus) > 12 ? playerFocus : Mathx.Damp(smoothedFocus, playerFocus, 12, dt);

        Yaw += look.X;
        Pitch = Math.Clamp(Pitch + look.Y, -1.25f, 1.05f);
        if (LockOn && !colossus.IsDefeated)
        {
            var head = colossus.Blueprint.HeadPart;
            var target = colossus.PartWorld[Math.Max(0, head)].Translation;
            var to = target - smoothedFocus;
            var flat = Mathx.Flat(to);
            if (flat.LengthSquared() > 4)
            {
                Yaw = Mathx.TurnToward(Yaw, Mathx.YawOf(flat), 3.2f * dt * MathF.Max(.3f, MathF.Abs(Mathx.WrapAngle(Mathx.YawOf(flat) - Yaw))));
                var wantPitch = Math.Clamp(MathF.Atan2(to.Y, flat.Length()) * .7f, -.6f, .9f);
                Pitch = Mathx.Damp(Pitch, wantPitch, 3, dt);
            }
        }
        Yaw = Mathx.WrapAngle(Yaw);

        var wantDistance = climbing ? 8.5f : attached ? 9f : 6.8f;
        if (colossus.State is ColossusState.Attack or ColossusState.ShakeOff) wantDistance += 2;
        distance = Mathx.Damp(distance, wantDistance, 3, dt);
        var forward = Forward;
        var position = smoothedFocus - forward * distance;
        // Never dip under the ground.
        var ground = region.Height(position.X, position.Z) + .7f;
        if (region.HasWater) ground = MathF.Max(ground, region.WaterLevel + .5f);
        if (position.Y < ground) position.Y = ground;
        Position = position;
        Focus = smoothedFocus;

        var sprint = player.IsOnGround && new Vector2(player.Velocity.X, player.Velocity.Z).Length() > 7;
        FieldOfView = Mathx.Damp(FieldOfView, sprint ? 64 : LockOn ? 54 : 58, 4, dt);

        trauma = MathF.Max(0, trauma - dt * .9f);
        var shake = trauma * trauma * ShakeScale;
        shakeOffset = new Vector3(Noise.Value(time * 18, 0, 3) - .5f, Noise.Value(time * 18, 5, 3) - .5f, Noise.Value(time * 18, 9, 3) - .5f) * 1.2f * shake;
        shakeRoll = (Noise.Value(time * 15, 13, 3) - .5f) * .12f * shake;
        // Being on a shaking colossus rattles the view too.
        if (attached && colossus.ShakeIntensity > .3f) shakeOffset += new Vector3(MathF.Sin(time * 41), MathF.Sin(time * 37), 0) * .05f * colossus.ShakeIntensity * ShakeScale;
    }

    public void Apply(Node camera)
    {
        camera.Position = Position + shakeOffset;
        var up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(Mathx.SafeNormalize(Focus - Position, Vector3.UnitZ), shakeRoll));
        camera.LookAt(Focus + shakeOffset * .5f, up);
        camera.Camera = ThreeNet.Camera.Perspective(FieldOfView * MathF.PI / 180, .2f, 2600);
    }
}
