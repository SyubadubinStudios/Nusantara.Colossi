using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>Each guardian's signature mechanic, on top of the shared attacks.</summary>
public enum Signature : byte
{
    None,
    /// <summary>Dewi Padi floods the terraces in waves; only high ground and her body stay dry.</summary>
    Flood,
    /// <summary>Gedung Titan sheds glass panes that rain down on marked spots.</summary>
    GlassShards,
    /// <summary>Leviathan dives with climbers on its back; they must hold their breath.</summary>
    Dive,
    /// <summary>Kapal Majapahit fires broadsides at anyone in the water.</summary>
    Cannons,
    /// <summary>GARUDA NET's avatar strikes every hunter with data bolts, even climbers; it cannot be hacked.</summary>
    Firewall
}

public enum StrikeKind : byte { Shard, Cannonball, DataBolt }

/// <summary>A telegraphed projectile: it launches from <see cref="Origin"/> and lands on <see cref="Target"/>.</summary>
public struct Strike
{
    public StrikeKind Kind;
    public Vector3 Origin;
    public Vector3 Target;
    public float Radius;
    public float LaunchTime;
    public float LandTime;
    public bool Active;

    /// <summary>0 at launch, 1 on impact.</summary>
    public readonly float Progress(float time) => Mathx.Saturate((time - LaunchTime) / MathF.Max(.01f, LandTime - LaunchTime));

    /// <summary>Ballistic position for rendering: cannonballs arc, shards and bolts fall straight.</summary>
    public readonly Vector3 PositionAt(float time)
    {
        var t = Progress(time);
        var p = Vector3.Lerp(Origin, Target, t);
        if (Kind == StrikeKind.Cannonball) p.Y += MathF.Sin(t * MathF.PI) * Vector3.Distance(Origin, Target) * .28f;
        return p;
    }
}

/// <summary>
/// Pure, time-driven schedules for the signature mechanics. Everything is a function of the guardian's
/// clock (<see cref="ColossusPose.Time"/>) and rage, which co-op snapshots already carry. Every peer therefore
/// sees the same flood level, dive depth and volleys without extra network traffic. Only the authority
/// applies damage.
/// </summary>
public static class Signatures
{
    public const float FloodCycle = 36f;
    public const float DiveCycle = 30f;
    public const float DiveDepth = 9f;

    /// <summary>0 = dry, 1 = full flood.</summary>
    public static float FloodAmount(float time)
    {
        var c = Mod(time, FloodCycle);
        if (c < 20) return 0;
        if (c < 26) return Mathx.SmoothStep(20, 26, c);
        if (c < 32) return 1;
        return 1 - Mathx.SmoothStep(32, 36, c);
    }

    /// <summary>True during the seconds before the water starts rising, when GARUDA warns.</summary>
    public static bool FloodWarning(float time) => Mod(time, FloodCycle) is >= 16 and < 20;

    /// <summary>0 = surfaced, 1 = fully submerged.</summary>
    public static float DiveAmount(float time)
    {
        var c = Mod(time, DiveCycle);
        if (c < 18) return 0;
        if (c < 21) return Mathx.SmoothStep(18, 21, c);
        if (c < 27) return 1;
        return 1 - Mathx.SmoothStep(27, 30, c);
    }

    public static bool DiveWarning(float time) => Mod(time, DiveCycle) is >= 14.5f and < 18;

    /// <summary>Seconds between volleys for strike-based signatures. Rage shortens the interval.</summary>
    public static float VolleyPeriod(Signature signature, int rage) => signature switch
    {
        Signature.GlassShards => 11f - 2.5f * rage,
        Signature.Cannons => 9f - 2.5f * rage,
        Signature.Firewall => 8f - 1.5f * rage,
        _ => 0
    };

    public static float FlightTime(StrikeKind kind) => kind switch
    {
        StrikeKind.Shard => 1.8f,
        StrikeKind.Cannonball => 2.2f,
        _ => 1.4f
    };

    public static float StrikeRadius(StrikeKind kind) => kind switch
    {
        StrikeKind.Shard => 3.2f,
        StrikeKind.Cannonball => 4.5f,
        _ => 2.6f
    };

    public static float StrikeDamage(StrikeKind kind) => kind switch
    {
        StrikeKind.Shard => 22,
        StrikeKind.Cannonball => 28,
        _ => 18
    };

    /// <summary>Deterministic hash in [0, 1) for (a, b, c), so peers derive the same spread without sharing a Random.</summary>
    public static float Hash01(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    private static float Mod(float x, float m) => x - MathF.Floor(x / m) * m;
}
