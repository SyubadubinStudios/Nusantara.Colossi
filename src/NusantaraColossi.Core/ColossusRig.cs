using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>Everything needed to reproduce a colossus pose. Small enough to replicate every network tick.</summary>
public struct ColossusPose
{
    /// <summary>Ground (or water) point under the root pivot.</summary>
    public Vector3 Position;
    public float Yaw;
    /// <summary>Gait cycles travelled (one cycle = one full stride).</summary>
    public float GaitPhase;
    /// <summary>0 when standing, 1 when walking, above 1 when charging.</summary>
    public float GaitWeight;
    public float Time;
    public AttackKind Attack;
    public float AttackProgress;
    public float Kneel;
    public float Shake;
    public float Collapse;
    /// <summary>Head turn toward the target, radians, relative to the body.</summary>
    public float HeadYaw;
}

/// <summary>
/// Pure function from <see cref="ColossusPose"/> to part transforms. Gameplay collision (climbing,
/// cores, attack contacts) and rendering both read these matrices, so what you see is what you grip.
/// </summary>
public static class ColossusRig
{
    public const float StompImpact = .62f;
    public const float RoarImpact = .5f;
    public const float SweepStart = .42f;
    public const float SweepEnd = .68f;

    public static float Duration(AttackKind kind) => kind switch
    {
        AttackKind.Stomp => 2.6f,
        AttackKind.Sweep => 2.4f,
        AttackKind.Roar => 2.8f,
        AttackKind.Charge => 4f,
        AttackKind.ShakeOff => 2.6f,
        _ => 0
    };

    /// <summary>Limb that delivers a stomp for this blueprint.</summary>
    public static PartRole StompLimb(ColossusBlueprint blueprint) => blueprint.Archetype switch
    {
        Archetype.Quadruped => PartRole.LegFrontLeft,
        Archetype.Serpent => PartRole.Head,
        Archetype.Ship => PartRole.Head,
        _ => blueprint.Id == "orangutan-purba" ? PartRole.ArmRight : PartRole.LegRight
    };

    /// <summary>Raise-then-slam curve: rises to 1 by 55% then drops to 0 at the impact moment.</summary>
    public static float Lift(float p) => p < .55f ? Mathx.SmoothStep(0, .55f, p) : p < StompImpact ? 1 - (p - .55f) / (StompImpact - .55f) : 0;

    private static float Bell(float p, float start, float end) =>
        p <= start || p >= end ? 0 : MathF.Sin((p - start) / (end - start) * MathF.PI);

    public static void Evaluate(ColossusBlueprint bp, in ColossusPose pose, Span<Matrix4x4> world)
    {
        if (world.Length < bp.Parts.Length) throw new ArgumentException("Pose buffer too small.", nameof(world));
        var parts = bp.Parts;
        var gaitAngle = Mathx.Tau * pose.GaitPhase;
        var walk = MathF.Min(pose.GaitWeight, 1.6f);
        var p = pose.AttackProgress;
        var stomp = pose.Attack == AttackKind.Stomp ? Lift(p) : 0;
        var sweep = pose.Attack == AttackKind.Sweep ? Bell(p, .15f, .9f) : 0;
        var sweepSwing = pose.Attack == AttackKind.Sweep ? MathF.Cos(Mathx.SmoothStep(SweepStart - .1f, SweepEnd + .1f, p) * MathF.PI) : 0; // 1 → -1
        var roar = pose.Attack == AttackKind.Roar ? Bell(p, .1f, .85f) : 0;
        var charge = pose.Attack == AttackKind.Charge ? Bell(p, 0, 1) : 0;
        var stompLimb = StompLimb(bp);

        // ---- root ----
        float pitch = 0, roll = 0, lift = 0;
        var breath = MathF.Sin(pose.Time * 1.1f);
        switch (bp.Archetype)
        {
            case Archetype.Quadruped:
                lift -= MathF.Abs(MathF.Sin(gaitAngle * 2)) * .35f * walk;
                pitch -= stomp * .16f;
                pitch += charge * .08f;
                lift -= pose.Kneel * bp.RootHeight * .3f;
                pitch += pose.Kneel * .12f;
                break;
            case Archetype.Biped:
                lift -= MathF.Abs(MathF.Sin(gaitAngle * 2)) * .45f * walk;
                roll += MathF.Sin(gaitAngle) * .025f * walk;
                lift -= pose.Kneel * bp.RootHeight * .38f;
                pitch += pose.Kneel * .18f;
                break;
            case Archetype.Serpent:
                lift += MathF.Sin(pose.Time * .6f) * .4f - pose.Kneel * 2.5f;
                roll += MathF.Sin(pose.Time * .45f) * .03f;
                pitch -= roar * .2f;
                break;
            case Archetype.Ship:
                roll += MathF.Sin(pose.Time * .7f) * .05f;
                pitch += MathF.Sin(pose.Time * .53f + 1) * .025f;
                lift += MathF.Sin(pose.Time * .9f) * .35f - pose.Kneel * 1.6f;
                roll += sweep * .1f;
                break;
        }
        lift += breath * .12f;
        roll += pose.Shake * (MathF.Sin(pose.Time * 9.3f) * .07f + MathF.Sin(pose.Time * 15.1f) * .025f) * (bp.Archetype == Archetype.Ship ? 2.2f : 1);
        pitch += pose.Shake * MathF.Sin(pose.Time * 7.1f + .6f) * .045f;
        roll += pose.Collapse * 1.15f;
        lift -= pose.Collapse * bp.RootHeight * .62f;

        var rootLocal = Quaternion.CreateFromYawPitchRoll(0, pitch, roll);
        world[0] = Matrix4x4.CreateFromQuaternion(rootLocal) * Matrix4x4.CreateRotationY(pose.Yaw) *
                   Matrix4x4.CreateTranslation(pose.Position + new Vector3(0, bp.RootHeight + lift, 0));

        // ---- children (parents always precede children in the blueprint) ----
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            float rx = 0, ry = 0, rz = 0;
            var swing = MathF.Sin(gaitAngle + part.GaitPhase * Mathx.Tau);
            switch (part.Role)
            {
                case PartRole.LegFrontLeft or PartRole.LegFrontRight or PartRole.LegBackLeft or PartRole.LegBackRight
                    or PartRole.LegLeft or PartRole.LegRight:
                    rx = part.GaitAmplitude * swing * walk;
                    if (part.Role == stompLimb) rx -= stomp * 1.05f;
                    if (pose.Kneel > 0)
                        rx += part.Role is PartRole.LegFrontLeft or PartRole.LegFrontRight or PartRole.LegLeft or PartRole.LegRight
                            ? -pose.Kneel * .55f : pose.Kneel * .3f;
                    break;
                case PartRole.ArmLeft or PartRole.ArmRight:
                    rx = part.GaitAmplitude * swing * walk;
                    if (part.Role == stompLimb) rx -= stomp * 1.9f;
                    if (part.Role == PartRole.ArmRight)
                    {
                        rz += sweep * 1.1f;
                        ry += sweep * sweepSwing * .9f;
                    }
                    rx -= roar * .35f;
                    rz += (part.Role == PartRole.ArmLeft ? -1 : 1) * roar * .45f;
                    break;
                case PartRole.Tail:
                    ry = part.GaitAmplitude * MathF.Sin(pose.Time * 1.3f + part.GaitPhase * Mathx.Tau) + part.GaitAmplitude * swing * walk * .6f;
                    ry += sweep * sweepSwing * 1.25f;
                    break;
                case PartRole.Segment:
                    ry = part.GaitAmplitude * MathF.Sin(gaitAngle * .5f + pose.Time * .35f - part.GaitPhase * Mathx.Tau);
                    ry += sweep * sweepSwing * .22f;
                    rx = MathF.Sin(pose.Time * .5f - part.GaitPhase * 4) * .025f;
                    break;
                case PartRole.Head:
                    rx = MathF.Sin(gaitAngle * 2) * .035f * walk + breath * .02f - roar * .55f + charge * .28f + pose.Kneel * .3f;
                    if (part.Role == stompLimb) rx -= stomp * .8f; // serpent rears its head, then slams it down
                    ry = pose.HeadYaw;
                    break;
                case PartRole.Neck:
                    rx = -roar * .25f + charge * .15f + pose.Kneel * .2f;
                    break;
                case PartRole.Torso:
                    ry = MathF.Sin(gaitAngle) * .05f * walk + sweep * sweepSwing * .25f;
                    rx = roar * -.12f;
                    break;
                case PartRole.Mast:
                    rz = part.GaitAmplitude * MathF.Sin(pose.Time * .8f) + pose.Shake * MathF.Sin(pose.Time * 6) * .05f;
                    break;
                case PartRole.Sail:
                    ry = part.GaitAmplitude * MathF.Sin(pose.Time * .45f) + sweep * sweepSwing * .6f;
                    break;
            }
            var local = Matrix4x4.CreateFromYawPitchRoll(ry, rx, rz) *
                        Matrix4x4.CreateFromYawPitchRoll(part.Tilt.Y, part.Tilt.X, part.Tilt.Z) *
                        Matrix4x4.CreateTranslation(part.Joint);
            world[i] = local * world[part.Parent];
        }
    }

    /// <summary>World transform of a part's collision box (centre-origin, no scale).</summary>
    public static Matrix4x4 BoxWorld(PartBlueprint part, in Matrix4x4 partWorld) => Matrix4x4.CreateTranslation(part.Center) * partWorld;

    /// <summary>Point on a box face in box-local coordinates. uv ∈ [-1, 1] spans the face.</summary>
    public static Vector3 FacePoint(Vector3 half, int axis, float sign, Vector2 uv)
    {
        var u = (axis + 1) % 3;
        var v = (axis + 2) % 3;
        var point = Mathx.WithComponent(Vector3.Zero, axis, sign * Mathx.Component(half, axis));
        point = Mathx.WithComponent(point, u, uv.X * Mathx.Component(half, u));
        return Mathx.WithComponent(point, v, uv.Y * Mathx.Component(half, v));
    }

    public static (Vector3 Position, Vector3 Normal) CoreWorld(ColossusBlueprint bp, CoreBlueprint core, ReadOnlySpan<Matrix4x4> world)
    {
        var part = bp.Parts[core.Part];
        var box = BoxWorld(part, world[core.Part]);
        var local = FacePoint(part.Half, core.FaceAxis, core.FaceSign, core.Uv);
        return (Vector3.Transform(local, box), Vector3.Normalize(Vector3.TransformNormal(Mathx.Axis(core.FaceAxis, core.FaceSign), box)));
    }

    /// <summary>World position of the far end of a limb (foot, fist, head tip) used for stomp contacts.</summary>
    public static Vector3 LimbTip(ColossusBlueprint bp, int partIndex, ReadOnlySpan<Matrix4x4> world)
    {
        var part = bp.Parts[partIndex];
        var tip = part.Center + Vector3.Normalize(part.Center == Vector3.Zero ? Vector3.UnitZ : part.Center) * MathF.Max(part.Half.Y, part.Half.Z) * 1.0f;
        if (part.Role is PartRole.LegLeft or PartRole.LegRight or PartRole.LegFrontLeft or PartRole.LegFrontRight
            or PartRole.LegBackLeft or PartRole.LegBackRight or PartRole.ArmLeft or PartRole.ArmRight)
            tip = part.Center + new Vector3(0, -part.Half.Y, 0);
        return Vector3.Transform(tip, world[partIndex]);
    }
}
