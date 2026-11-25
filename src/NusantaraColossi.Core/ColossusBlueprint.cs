using System.Numerics;

namespace NusantaraColossi.Core;

public enum SurfaceKind { Stone, Moss, Fur, Glass, Wood, Coral, Obsidian, Metal, Cloth, Gold }

public enum Archetype { Quadruped, Biped, Serpent, Ship }

public enum PartRole
{
    Body, Torso, Pelvis, Neck, Head, Horn, Hump, Tail,
    LegFrontLeft, LegFrontRight, LegBackLeft, LegBackRight,
    LegLeft, LegRight, ArmLeft, ArmRight,
    Segment, Fin, Mast, Sail, Ornament
}

public enum AttackKind { None, Stomp, Sweep, Roar, Charge, ShakeOff }

/// <summary>
/// One rigid, box-shaped piece of a colossus. <see cref="Joint"/> is the pivot in the parent's frame,
/// <see cref="Center"/> is the box centre relative to that pivot and <see cref="Half"/> its half size.
/// Boxes are what the climber grips, so they are deliberately the authoritative shape; the renderer
/// sculpts its mesh inside the same volume.
/// </summary>
public sealed record PartBlueprint(
    string Id, int Parent, PartRole Role, Vector3 Joint, Vector3 Center, Vector3 Half,
    SurfaceKind Surface, Vector3 Tilt, float GaitPhase, float GaitAmplitude, byte ClimbFaces)
{
    /// <summary>Face index = axis * 2 + (positive ? 1 : 0).</summary>
    public bool IsClimbable(int axis, float sign) => (ClimbFaces & (1 << (axis * 2 + (sign > 0 ? 1 : 0)))) != 0;
    public bool AnyClimbable => ClimbFaces != 0;
}

/// <summary>A weak point (inti) sitting on one face of a part.</summary>
public sealed record CoreBlueprint(string Id, int Part, int FaceAxis, float FaceSign, Vector2 Uv, float Health, bool Plated = false);

public sealed class ColossusBlueprint
{
    public required string Id { get; init; }
    public required LocText Name { get; init; }
    public required LocText Epithet { get; init; }
    public required Archetype Archetype { get; init; }
    public required PartBlueprint[] Parts { get; init; }
    public required CoreBlueprint[] Cores { get; init; }
    public required AttackKind[] Attacks { get; init; }
    /// <summary>Height of the root pivot above the ground (or water) when standing.</summary>
    public required float RootHeight { get; init; }
    /// <summary>Approximate standing height, used for camera framing and attack ranges.</summary>
    public required float Height { get; init; }
    public required float WalkSpeed { get; init; }
    public required float TurnRate { get; init; }
    public required float StrideLength { get; init; }
    public float ShakeInterval { get; init; } = 7f;
    public float ShakeStrength { get; init; } = 1f;
    public float AttackCooldown { get; init; } = 4.5f;
    /// <summary>Vitality fractions at which the colossus enters a new rage phase.</summary>
    public float[] RageThresholds { get; init; } = [.5f];
    /// <summary>Seconds a GARUDA scan keeps a core's ancient seal open.</summary>
    public float SealOpenSeconds { get; init; } = 24f;
    public float CorePulsePeriod { get; init; } = 1.6f;
    /// <summary>True for colossi that ride the water surface (ship, serpent).</summary>
    public bool Floats { get; init; }
    /// <summary>Mechanical colossi can be stunned for longer by a GARUDA hack.</summary>
    public float HackSusceptibility { get; init; } = 1f;
    /// <summary>The guardian's own mechanic on top of the shared attacks.</summary>
    public Signature Signature { get; init; }
    public required LocText Lore { get; init; }
    public required LocText Hint { get; init; }
    public required LocText Inscription { get; init; }
    public required Vector3 GlowColor { get; init; }
    public required Vector3 BodyColor { get; init; }

    public int HeadPart => Array.FindIndex(Parts, p => p.Role == PartRole.Head);
    public float TotalCoreHealth => Cores.Sum(c => c.Health);
}

/// <summary>Fluent builder that keeps the blueprint tables readable.</summary>
internal sealed class PartBuilder
{
    public const byte All = 0b111111;
    public const byte None = 0;
    private readonly List<PartBlueprint> parts = [];

    public int Add(string id, string? parent, PartRole role, Vector3 joint, Vector3 center, Vector3 half, SurfaceKind surface,
        byte climb = All, Vector3 tilt = default, float gaitPhase = 0, float gaitAmplitude = 0)
    {
        var parentIndex = parent is null ? -1 : parts.FindIndex(p => p.Id == parent);
        if (parent is not null && parentIndex < 0) throw new InvalidOperationException($"Unknown parent part '{parent}'.");
        parts.Add(new(id, parentIndex, role, joint, center, half, surface, tilt, gaitPhase, gaitAmplitude, climb));
        return parts.Count - 1;
    }

    public int this[string id] => parts.FindIndex(p => p.Id == id);
    public PartBlueprint[] Build() => [.. parts];

    public static byte Faces(bool negX = true, bool posX = true, bool negY = true, bool posY = true, bool negZ = true, bool posZ = true) =>
        (byte)((negX ? 1 : 0) | (posX ? 2 : 0) | (negY ? 4 : 0) | (posY ? 8 : 0) | (negZ ? 16 : 0) | (posZ ? 32 : 0));
}
