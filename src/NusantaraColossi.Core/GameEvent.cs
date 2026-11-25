using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>Things that happened this tick. Drives audio, VFX, camera shake, HUD toasts and co-op replication.</summary>
public enum GameEventKind : byte
{
    Footstep, ClimbStep, Jump, Land, Dodge, Splash,
    Grab, Release, GripLost, ThrownOff, StaminaLow, SecondWind,
    PlayerHurt, PlayerDied,
    StabFlesh, StabSealed, StabDeflected, CoreHit, CoreResonance, CoreDestroyed,
    Awaken, ShakeWarn, ShakeStart, StompWindup, StompImpact, SweepStart, SweepHit, RoarStart, RoarWave, ChargeStart, ChargeHit,
    Stunned, Staggered, Rage, Collapse, Defeated,
    Scan, ScanDenied, Hack, HackDenied, Decoy, DecoyDenied,
    GustWarn, Gust,
    Victory, Retry,
    // Appended (wire values of earlier kinds are unchanged).
    FloodWarn, FloodRise, DiveWarn, DiveStart, Surface, Volley, StrikeImpact, BreathLow,
    Mount, Dismount, GliderOpen, GliderClose
}

public readonly record struct GameEvent(GameEventKind Kind, Vector3 Position, float Value = 0, int Slot = 0);
