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

    public void Update(Player player, Colossus colossus, RegionDefinition region, Vector2 look, float dt)
    {
        time += dt;
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
