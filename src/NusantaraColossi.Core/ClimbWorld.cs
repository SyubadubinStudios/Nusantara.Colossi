using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>An oriented box the player can collide with, stand on or (per face) climb.</summary>
public struct ClimbBox
{
    public Matrix4x4 World;
    public Matrix4x4 Inverse;
    public Vector3 Half;
    public Vector3 Center;
    public float Radius;
    public byte Faces;
    public SurfaceKind Surface;
    /// <summary>-1 for static arena props, otherwise the colossus slot.</summary>
    public int Owner;
    /// <summary>Part index (colossus) or obstacle index (static).</summary>
    public int Index;
    public bool Active;

    public readonly bool IsClimbable(int axis, float sign) => (Faces & (1 << (axis * 2 + (sign > 0 ? 1 : 0)))) != 0;
    public readonly Vector3 ToLocal(Vector3 world) => Vector3.Transform(world, Inverse);
    public readonly Vector3 ToWorld(Vector3 local) => Vector3.Transform(local, World);
    public readonly Vector3 NormalWorld(int axis, float sign) => Vector3.Normalize(Vector3.TransformNormal(Mathx.Axis(axis, sign), World));
}

/// <summary>Where a climber is attached: a box, a point on its surface (box-local) and the face.</summary>
public struct ClimbContact
{
    public int Box;
    public Vector3 Local;
    public int Axis;
    public float Sign;
}

/// <summary>
/// The set of climbable/collidable boxes for one arena: static props first, then one slot per colossus part.
/// Slots never move, so a <see cref="ClimbContact"/> survives the per-frame pose refresh.
/// </summary>
public sealed class ClimbWorld
{
    public const float SurfaceOffset = .42f;
    private const float CoverShrink = .06f;
    private readonly ClimbBox[] boxes;
    public int StaticCount { get; }
    public int Count => boxes.Length;

    public ClimbWorld(IReadOnlyList<Obstacle> obstacles, int dynamicCapacity)
    {
        boxes = new ClimbBox[obstacles.Count + dynamicCapacity];
        StaticCount = obstacles.Count;
        for (var i = 0; i < obstacles.Count; i++)
        {
            var o = obstacles[i];
            var world = Matrix4x4.CreateRotationY(o.Yaw) * Matrix4x4.CreateTranslation(o.Center);
            Set(i, world, o.Half, o.Climbable ? PartBuilder.All : PartBuilder.Faces(negX: false, posX: false, negY: false, negZ: false, posZ: false), o.Surface, -1, i);
        }
    }

    public ref readonly ClimbBox this[int i] => ref boxes[i];

    public void Set(int slot, in Matrix4x4 world, Vector3 half, byte faces, SurfaceKind surface, int owner, int index)
    {
        Matrix4x4.Invert(world, out var inverse);
        boxes[slot] = new ClimbBox
        {
            World = world, Inverse = inverse, Half = half, Faces = faces, Surface = surface, Owner = owner, Index = index,
            Center = world.Translation, Radius = half.Length(), Active = true
        };
    }

    public void Deactivate(int slot) => boxes[slot].Active = false;

    public Vector3 WorldPosition(in ClimbContact c) => boxes[c.Box].ToWorld(c.Local);
    public Vector3 WorldNormal(in ClimbContact c) => boxes[c.Box].NormalWorld(c.Axis, c.Sign);
    public Vector3 BodyPosition(in ClimbContact c) => WorldPosition(c) + WorldNormal(c) * SurfaceOffset;

    /// <summary>True when the point is inside some other box (a face buried in a joint).</summary>
    public bool IsCovered(Vector3 world, int exclude)
    {
        for (var j = 0; j < boxes.Length; j++)
        {
            if (j == exclude) continue;
            ref readonly var b = ref boxes[j];
            if (!b.Active || Vector3.DistanceSquared(b.Center, world) > b.Radius * b.Radius) continue;
            var l = b.ToLocal(world);
            if (MathF.Abs(l.X) < b.Half.X - CoverShrink && MathF.Abs(l.Y) < b.Half.Y - CoverShrink && MathF.Abs(l.Z) < b.Half.Z - CoverShrink)
                return true;
        }
        return false;
    }

    /// <summary>Index of a box containing the point (expanded by <paramref name="margin"/>), or -1.</summary>
    public int FindContaining(Vector3 world, float margin, int exclude = -1)
    {
        for (var j = 0; j < boxes.Length; j++)
        {
            if (j == exclude) continue;
            ref readonly var b = ref boxes[j];
            var r = b.Radius + margin;
            if (!b.Active || Vector3.DistanceSquared(b.Center, world) > r * r) continue;
            var l = b.ToLocal(world);
            if (MathF.Abs(l.X) <= b.Half.X + margin && MathF.Abs(l.Y) <= b.Half.Y + margin && MathF.Abs(l.Z) <= b.Half.Z + margin)
                return j;
        }
        return -1;
    }

    /// <summary>
    /// Closest uncovered face point of box <paramref name="box"/> to a world point.
    /// <paramref name="requireClimbable"/> filters faces that cannot be gripped; top faces are always standable.
    /// </summary>
    public bool Project(int box, Vector3 world, bool requireClimbable, out ClimbContact contact, out float distance)
    {
        ref readonly var b = ref boxes[box];
        contact = default;
        distance = float.MaxValue;
        if (!b.Active) return false;
        var local = b.ToLocal(world);
        var clamped = Vector3.Clamp(local, -b.Half, b.Half);
        for (var axis = 0; axis < 3; axis++)
            for (var s = -1; s <= 1; s += 2)
            {
                var standable = axis == 1 && s > 0 && b.NormalWorld(1, 1).Y > .7f;
                if (requireClimbable && !b.IsClimbable(axis, s) && !standable) continue;
                var candidate = Mathx.WithComponent(clamped, axis, s * Mathx.Component(b.Half, axis));
                var d = Vector3.Distance(candidate, local);
                if (d >= distance) continue;
                var normal = b.NormalWorld(axis, s);
                if (IsCovered(b.ToWorld(candidate) + normal * .3f, box)) continue;
                distance = d;
                contact = new ClimbContact { Box = box, Local = candidate, Axis = axis, Sign = s };
            }
        return distance < float.MaxValue;
    }

    /// <summary>Is this contact a walkable top surface rather than a wall?</summary>
    public bool IsStanding(in ClimbContact c) => WorldNormal(c).Y > .7f;

    public enum MoveResult { Moved, Blocked, SteppedOff }

    /// <summary>
    /// Slides a contact across its surface by a world-space displacement, wrapping over edges onto
    /// neighbouring faces and transferring onto touching boxes (leg → belly → flank).
    /// </summary>
    public MoveResult Move(ref ClimbContact contact, Vector3 worldDelta, bool gripping)
    {
        if (worldDelta.LengthSquared() < 1e-10f) return MoveResult.Moved;
        ref readonly var b = ref boxes[contact.Box];
        var standing = IsStanding(contact);
        var d = Vector3.TransformNormal(worldDelta, b.Inverse);
        d = Mathx.WithComponent(d, contact.Axis, 0);
        var q = contact.Local + d;
        var next = contact;
        for (var k = 1; k <= 2; k++)
        {
            var i = (contact.Axis + k) % 3;
            var h = Mathx.Component(b.Half, i);
            var qi = Mathx.Component(q, i);
            if (MathF.Abs(qi) <= h) continue;
            var sign = MathF.Sign(qi);
            var over = MathF.Abs(qi) - h;
            var targetClimbable = b.IsClimbable(i, sign);
            if (standing && !gripping)
            {
                // Walking off a ledge: the caller turns this into a fall with momentum.
                if (TryTransfer(ref next, b.ToWorld(Mathx.WithComponent(q, i, sign * (h + .05f))), worldDelta, false)) { contact = next; return MoveResult.Moved; }
                return MoveResult.SteppedOff;
            }
            if (!targetClimbable)
            {
                q = Mathx.WithComponent(q, i, sign * h);
                continue;
            }
            q = Mathx.WithComponent(q, i, sign * h);
            var ha = Mathx.Component(b.Half, contact.Axis);
            q = Mathx.WithComponent(q, contact.Axis, Math.Clamp(contact.Sign * ha - contact.Sign * over, -ha, ha));
            next.Axis = i;
            next.Sign = sign;
            break;
        }
        q = Vector3.Clamp(q, -b.Half, b.Half);
        next.Local = q;

        // A face buried inside another box hands the climber to that box.
        var normal = b.NormalWorld(next.Axis, next.Sign);
        var probe = b.ToWorld(q) + normal * .3f;
        var covering = FindCovering(probe, contact.Box);
        if (covering >= 0)
        {
            if (Project(covering, probe, true, out var t, out _) && (gripping || IsStanding(t)))
            {
                contact = t;
                return MoveResult.Moved;
            }
            return MoveResult.Blocked;
        }
        // Moving toward a neighbouring box: step across the gap.
        if (TryTransfer(ref next, b.ToWorld(q) + Vector3.Normalize(worldDelta) * .55f, worldDelta, gripping))
        {
            contact = next;
            return MoveResult.Moved;
        }
        contact = next;
        return MoveResult.Moved;
    }

    private int FindCovering(Vector3 world, int exclude)
    {
        for (var j = 0; j < boxes.Length; j++)
        {
            if (j == exclude) continue;
            ref readonly var o = ref boxes[j];
            if (!o.Active || Vector3.DistanceSquared(o.Center, world) > o.Radius * o.Radius) continue;
            var l = o.ToLocal(world);
            if (MathF.Abs(l.X) < o.Half.X - CoverShrink && MathF.Abs(l.Y) < o.Half.Y - CoverShrink && MathF.Abs(l.Z) < o.Half.Z - CoverShrink)
                return j;
        }
        return -1;
    }

    private bool TryTransfer(ref ClimbContact contact, Vector3 probe, Vector3 worldDelta, bool gripping)
    {
        var j = FindContaining(probe, .2f, contact.Box);
        if (j < 0) return false;
        if (!Project(j, probe, true, out var t, out var distance) || distance > 1.2f) return false;
        if (!gripping && !IsStanding(t)) return false;
        // Do not "transfer" backwards into the face we are leaving.
        if (Vector3.Dot(WorldNormal(t), worldDelta) > .5f * worldDelta.Length()) return false;
        contact = t;
        return true;
    }

    /// <summary>Find the best grip near a world point (used when grabbing from the air or the ground).</summary>
    public bool FindGrip(Vector3 world, float reach, bool requireClimbable, out ClimbContact contact)
    {
        contact = default;
        var best = float.MaxValue;
        for (var j = 0; j < boxes.Length; j++)
        {
            ref readonly var b = ref boxes[j];
            var r = b.Radius + reach;
            if (!b.Active || Vector3.DistanceSquared(b.Center, world) > r * r) continue;
            if (Project(j, world, requireClimbable, out var c, out var d) && d <= reach && d < best)
            {
                best = d;
                contact = c;
            }
        }
        return best < float.MaxValue;
    }
}
