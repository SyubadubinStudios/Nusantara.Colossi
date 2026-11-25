using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>Mounts from the original design: a trail motorbike on land, a jet ski on the water.</summary>
public enum VehicleKind : byte { Trail, JetSki }

/// <summary>A mount parked in (or ridden around) the arena. Plain data; the rider's simulation moves it.</summary>
public struct Vehicle
{
    public VehicleKind Kind;
    public Vector3 Position;
    public float Yaw;
    /// <summary>Forward speed, m/s (negative when bouncing back off an obstacle).</summary>
    public float Speed;
    /// <summary>Slot of the hunter riding it, or -1.</summary>
    public int Rider;

    public static float MaxSpeed(VehicleKind kind) => kind == VehicleKind.Trail ? 17f : 15f;
    /// <summary>How close a hunter must be to mount.</summary>
    public const float MountRange = 3.4f;
}

/// <summary>Tuning for the GARUDA glider wing that opens mid-air.</summary>
public static class Glider
{
    public const float SinkSpeed = 2.4f;
    public const float CruiseSpeed = 9f;
    public const float DiveBoost = 5f;
    public const float TurnRate = 1.9f;
    public const float StaminaDrain = 2.5f;
    /// <summary>Seconds after leaving the ground before the wing may open.</summary>
    public const float MinAirTime = .25f;
}
