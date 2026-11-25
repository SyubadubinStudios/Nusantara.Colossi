using System.Numerics;

namespace NusantaraColossi.Core;

public enum ColossusState : byte { Dormant, Roam, Pursue, Attack, ShakeWarn, ShakeOff, Stunned, Stagger, Collapse, Defeated }

public struct CoreState
{
    public float Health;
    /// <summary>Seconds the ancient seal stays open after a GARUDA scan. Sealed cores take no damage.</summary>
    public float SealOpen;
    public bool Revealed;
    public bool Destroyed => Health <= 0;
}

/// <summary>Runtime state and behaviour of one colossus. Only the authority (single player or co-op host) runs <see cref="Think"/>.</summary>
public sealed class Colossus
{
    public ColossusBlueprint Blueprint { get; }
    public ColossusPose Pose;
    public ColossusState State { get; internal set; } = ColossusState.Dormant;
    public CoreState[] Cores { get; }
    public Matrix4x4[] PartWorld { get; }
    public int Rage { get; internal set; }
    public float StateTime { get; internal set; }
    public bool Legend { get; init; }
    /// <summary>Where the colossus is heading (player, decoy or patrol point).</summary>
    public Vector3 Target { get; internal set; }
    public float DecoyTime { get; internal set; }
    public Vector3 DecoyPoint { get; internal set; }
    public float Annoyance { get; internal set; }
    private float attackCooldown = 3;
    private float chargeYaw;
    private bool attackLanded;
    private readonly HashSet<int> hitThisAttack = [];
    private float stunDuration;
    private Vector3 roamPoint;
    private readonly Random random;
    private readonly int stompPart;
    private readonly RegionDefinition region;

    public Colossus(ColossusBlueprint blueprint, RegionDefinition region, int seed, bool legend = false)
    {
        Blueprint = blueprint;
        this.region = region;
        Legend = legend;
        random = new Random(seed);
        Cores = blueprint.Cores.Select(c => new CoreState { Health = c.Health * (legend ? 1.4f : 1) }).ToArray();
        PartWorld = new Matrix4x4[blueprint.Parts.Length];
        var limb = ColossusRig.StompLimb(blueprint);
        stompPart = Math.Max(0, Array.FindIndex(blueprint.Parts, p => p.Role == limb));
        Pose.Position = GroundAt(new Vector3(0, 0, region.ArenaRadius * .2f));
        Pose.Yaw = MathF.PI;
        roamPoint = Pose.Position;
        Evaluate();
    }

    public float Vitality
    {
        get
        {
            float total = 0, left = 0;
            for (var i = 0; i < Cores.Length; i++)
            {
                var max = Blueprint.Cores[i].Health * (Legend ? 1.4f : 1);
                total += max;
                left += MathF.Max(0, Cores[i].Health);
            }
            return total <= 0 ? 0 : left / total;
        }
    }

    public bool IsDefeated => State is ColossusState.Collapse or ColossusState.Defeated;
    public float SpeedScale => (1 + .2f * Rage) * (Legend ? 1.15f : 1);
    public float CorePulsePeriod => Blueprint.CorePulsePeriod / (1 + .25f * Rage);

    /// <summary>0..1 heartbeat of the cores. Stabs released on the beat resonate for bonus damage.</summary>
    public float CorePulse(float time)
    {
        var c = (MathF.Cos(Mathx.Tau * time / CorePulsePeriod) + 1) * .5f;
        return MathF.Pow(c, 6);
    }

    public float ShakeIntensity => State switch
    {
        ColossusState.ShakeOff => Blueprint.ShakeStrength,
        ColossusState.ShakeWarn => .25f,
        ColossusState.Stagger => .3f,
        ColossusState.Collapse => .6f,
        _ => Pose.Attack == AttackKind.Stomp && Pose.AttackProgress > ColossusRig.StompImpact && Pose.AttackProgress < .72f ? .35f : 0
    };

    public float AttackRange => Blueprint.Archetype switch
    {
        Archetype.Quadruped => Blueprint.Height * .75f,
        Archetype.Serpent => 26,
        Archetype.Ship => 40,
        _ => Blueprint.Height * .5f
    };

    private Vector3 GroundAt(Vector3 p) => new(p.X, Blueprint.Floats && region.HasWater ? region.WaterLevel - DiveOffset : region.Height(p.X, p.Z), p.Z);

    /// <summary>How far below the surface a diving guardian (Leviathan) currently is.</summary>
    public float DiveOffset => Blueprint.Signature == Signature.Dive ? Signatures.DiveAmount(Pose.Time) * Signatures.DiveDepth * (1 - Pose.Collapse) : 0;

    /// <summary>Recompute part matrices from <see cref="Pose"/>. Peers call this after applying a snapshot.</summary>
    public void Evaluate() => ColossusRig.Evaluate(Blueprint, Pose, PartWorld);

    public void Scan(float duration)
    {
        for (var i = 0; i < Cores.Length; i++)
        {
            if (Cores[i].Destroyed) continue;
            Cores[i].SealOpen = MathF.Max(Cores[i].SealOpen, duration);
            Cores[i].Revealed = true;
        }
    }

    public void Stun(float seconds)
    {
        if (IsDefeated) return;
        stunDuration = seconds;
        SetState(ColossusState.Stunned);
        Pose.Attack = AttackKind.None;
    }

    public void Lure(Vector3 point, float seconds)
    {
        DecoyPoint = point;
        DecoyTime = seconds;
        if (State == ColossusState.Dormant) SetState(ColossusState.Pursue);
    }

    internal void AddAnnoyance(float amount) => Annoyance += amount;

    private void SetState(ColossusState state)
    {
        State = state;
        StateTime = 0;
    }

    internal void OnCoreDestroyed(Encounter encounter)
    {
        var vitality = Vitality;
        if (vitality <= 0)
        {
            SetState(ColossusState.Collapse);
            Pose.Attack = AttackKind.None;
            encounter.Emit(new(GameEventKind.Collapse, Pose.Position));
            return;
        }
        var thresholds = Blueprint.RageThresholds;
        while (Rage < thresholds.Length && vitality <= thresholds[Rage])
        {
            Rage++;
            encounter.Emit(new(GameEventKind.Rage, Pose.Position, Rage));
        }
        SetState(ColossusState.Stagger);
        Pose.Attack = AttackKind.None;
        encounter.Emit(new(GameEventKind.Staggered, Pose.Position));
    }

    /// <summary>Authority-side AI and locomotion.</summary>
    public void Think(Encounter encounter, float dt)
    {
        StateTime += dt;
        Pose.Time += dt;
        DecoyTime = MathF.Max(0, DecoyTime - dt);
        attackCooldown -= dt;
        for (var i = 0; i < Cores.Length; i++) Cores[i].SealOpen = MathF.Max(0, Cores[i].SealOpen - dt);

        // Who are we after?
        Player? prey = null;
        var climbers = 0;
        var bestDistance = float.MaxValue;
        foreach (var p in encounter.Players)
        {
            if (p.Mode == PlayerMode.Dead) continue;
            if (p.IsAttachedTo(0, encounter.Climb)) { climbers++; continue; }
            var d = Vector3.Distance(Mathx.Flat(p.Position), Mathx.Flat(Pose.Position));
            if (d < bestDistance) { bestDistance = d; prey = p; }
        }
        if (DecoyTime > 0) { Target = DecoyPoint; bestDistance = Vector3.Distance(Mathx.Flat(DecoyPoint), Mathx.Flat(Pose.Position)); }
        else if (prey is not null) Target = prey.Position;
        if (climbers > 0) Annoyance += dt * (1 + .35f * Rage) * climbers; else Annoyance = MathF.Max(0, Annoyance - dt * .5f);

        var moveSpeed = 0f;
        var desiredYaw = Pose.Yaw;
        var toTarget = Mathx.Flat(Target - Pose.Position);
        if (toTarget.LengthSquared() > 1) desiredYaw = Mathx.YawOf(toTarget);

        switch (State)
        {
            case ColossusState.Dormant:
                Pose.GaitWeight = Mathx.Damp(Pose.GaitWeight, 0, 3, dt);
                if (bestDistance < region.ArenaRadius * .55f || climbers > 0 || Annoyance > 0)
                {
                    SetState(ColossusState.Pursue);
                    encounter.Emit(new(GameEventKind.Awaken, Pose.Position));
                    attackCooldown = 2.5f;
                }
                break;
            case ColossusState.Roam:
            case ColossusState.Pursue:
            {
                if (climbers > 0 && Annoyance > Blueprint.ShakeInterval * (1 - .18f * Rage) * (Legend ? .8f : 1))
                {
                    SetState(ColossusState.ShakeWarn);
                    encounter.Emit(new(GameEventKind.ShakeWarn, Pose.Position));
                    break;
                }
                var hasPrey = prey is not null || DecoyTime > 0;
                if (!hasPrey)
                {
                    // Everyone is on its back: wander and try to scrape them off against the terrain.
                    if (Vector3.Distance(Mathx.Flat(roamPoint), Mathx.Flat(Pose.Position)) < 12)
                    {
                        var a = random.NextSingle() * Mathx.Tau;
                        var r = region.ArenaRadius * (.15f + random.NextSingle() * .45f);
                        roamPoint = new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r);
                    }
                    desiredYaw = Mathx.YawOf(Mathx.Flat(roamPoint - Pose.Position));
                    moveSpeed = Blueprint.WalkSpeed * .6f;
                }
                else
                {
                    var facing = MathF.Abs(Mathx.WrapAngle(desiredYaw - Pose.Yaw));
                    if (attackCooldown <= 0 && TryChooseAttack(encounter, bestDistance, facing, out var attack))
                    {
                        StartAttack(attack, desiredYaw, encounter);
                        break;
                    }
                    moveSpeed = bestDistance > AttackRange * .7f ? Blueprint.WalkSpeed : Blueprint.WalkSpeed * .25f;
                }
                break;
            }
            case ColossusState.Attack:
                UpdateAttack(encounter, dt, ref moveSpeed, ref desiredYaw);
                break;
            case ColossusState.ShakeWarn:
                if (StateTime > (Legend ? .6f : .9f))
                {
                    SetState(ColossusState.ShakeOff);
                    Pose.Attack = AttackKind.ShakeOff;
                    encounter.Emit(new(GameEventKind.ShakeStart, Pose.Position));
                }
                break;
            case ColossusState.ShakeOff:
                Pose.AttackProgress = StateTime / ColossusRig.Duration(AttackKind.ShakeOff);
                if (StateTime > ColossusRig.Duration(AttackKind.ShakeOff))
                {
                    Annoyance = 0;
                    Pose.Attack = AttackKind.None;
                    SetState(ColossusState.Pursue);
                    attackCooldown = 1.5f;
                }
                break;
            case ColossusState.Stunned:
                Pose.Kneel = Mathx.Damp(Pose.Kneel, StateTime < stunDuration ? 1 : 0, 3, dt);
                if (StateTime > stunDuration + .8f) { SetState(ColossusState.Pursue); attackCooldown = 2; }
                break;
            case ColossusState.Stagger:
                Pose.Kneel = Mathx.Damp(Pose.Kneel, StateTime < 2.4f ? .55f : 0, 4, dt);
                if (StateTime > 3.2f) { SetState(ColossusState.Pursue); Annoyance += 3; attackCooldown = 1.2f; }
                break;
            case ColossusState.Collapse:
                Pose.Collapse = Mathx.Saturate(StateTime / 6f);
                Pose.Collapse = Pose.Collapse * Pose.Collapse * (3 - 2 * Pose.Collapse);
                if (StateTime > 7) { SetState(ColossusState.Defeated); encounter.Emit(new(GameEventKind.Defeated, Pose.Position)); }
                break;
        }
        if (State is not (ColossusState.Stunned or ColossusState.Stagger) && Pose.Kneel > 0) Pose.Kneel = Mathx.Damp(Pose.Kneel, 0, 3, dt);

        // Locomotion.
        moveSpeed *= SpeedScale;
        if (!IsDefeated)
        {
            var turn = Blueprint.TurnRate * SpeedScale * dt * (State == ColossusState.Attack && Pose.Attack == AttackKind.Charge ? .15f : 1);
            if (State is ColossusState.Pursue or ColossusState.Roam or ColossusState.Attack) Pose.Yaw = Mathx.TurnToward(Pose.Yaw, desiredYaw, turn);
            // Stay inside the arena.
            var next = Pose.Position + Mathx.Forward(Pose.Yaw) * moveSpeed * dt;
            var r = MathF.Sqrt(next.X * next.X + next.Z * next.Z);
            if (r > region.ArenaRadius * .78f) next *= region.ArenaRadius * .78f / r;
            Pose.Position = GroundAt(Vector3.Lerp(Pose.Position, next, 1));
            Pose.GaitPhase += moveSpeed * dt / Blueprint.StrideLength;
            Pose.GaitWeight = Mathx.Damp(Pose.GaitWeight, moveSpeed > .1f ? MathF.Min(1.6f, moveSpeed / (Blueprint.WalkSpeed * SpeedScale)) : 0, 4, dt);
        }
        else Pose.GaitWeight = Mathx.Damp(Pose.GaitWeight, 0, 4, dt);

        // Look at what we hunt.
        var look = toTarget.LengthSquared() > 1 ? Math.Clamp(Mathx.WrapAngle(desiredYaw - Pose.Yaw), -.7f, .7f) : 0;
        Pose.HeadYaw = Mathx.Damp(Pose.HeadYaw, IsDefeated ? 0 : look, 2.5f, dt);
        Pose.Shake = Mathx.Damp(Pose.Shake, ShakeIntensity, 10, dt);
        Evaluate();
    }

    private bool TryChooseAttack(Encounter encounter, float distance, float facingError, out AttackKind attack)
    {
        attack = AttackKind.None;
        var options = Blueprint.Attacks;
        bool Has(AttackKind k) => Array.IndexOf(options, k) >= 0;
        var close = distance < AttackRange;
        if (Has(AttackKind.Charge) && distance > AttackRange * 1.8f && distance < region.ArenaRadius && facingError < .35f && random.NextSingle() < .5f)
            attack = AttackKind.Charge;
        else if (close && Has(AttackKind.Roar) && random.NextSingle() < .28f)
            attack = AttackKind.Roar;
        else if (close && Has(AttackKind.Sweep) && (facingError > .9f || random.NextSingle() < .35f))
            attack = AttackKind.Sweep;
        else if (close && Has(AttackKind.Stomp) && facingError < .8f)
            attack = AttackKind.Stomp;
        else if (close && Has(AttackKind.Sweep))
            attack = AttackKind.Sweep;
        else if (close && Has(AttackKind.Roar))
            attack = AttackKind.Roar;
        return attack != AttackKind.None;
    }

    private void StartAttack(AttackKind attack, float yaw, Encounter encounter)
    {
        SetState(ColossusState.Attack);
        Pose.Attack = attack;
        Pose.AttackProgress = 0;
        attackLanded = false;
        hitThisAttack.Clear();
        chargeYaw = yaw;
        var kind = attack switch
        {
            AttackKind.Stomp => GameEventKind.StompWindup,
            AttackKind.Sweep => GameEventKind.SweepStart,
            AttackKind.Roar => GameEventKind.RoarStart,
            _ => GameEventKind.ChargeStart
        };
        encounter.Emit(new(kind, attack == AttackKind.Stomp ? ColossusRig.LimbTip(Blueprint, stompPart, PartWorld) : Pose.Position, (float)attack));
    }

    /// <summary>Where the current attack will land; the HUD draws a warning ring there (Hunter's Instinct).</summary>
    public (Vector3 Center, float Radius)? Telegraph()
    {
        if (State != ColossusState.Attack) return null;
        return Pose.Attack switch
        {
            AttackKind.Stomp when Pose.AttackProgress < ColossusRig.StompImpact => (Mathx.Flat(ColossusRig.LimbTip(Blueprint, stompPart, PartWorld)) with { Y = Pose.Position.Y }, StompRadius),
            AttackKind.Roar when Pose.AttackProgress < ColossusRig.RoarImpact => (Pose.Position, RoarRadius),
            AttackKind.Sweep when Pose.AttackProgress < ColossusRig.SweepEnd => (Pose.Position, SweepRadius),
            _ => null
        };
    }

    public float StompRadius => 6 + Blueprint.Height * .08f;
    public float RoarRadius => Blueprint.Height * 1.1f + 10;
    public float SweepRadius => Blueprint.Height * .85f + 6;

    private void UpdateAttack(Encounter encounter, float dt, ref float moveSpeed, ref float desiredYaw)
    {
        var kind = Pose.Attack;
        var duration = ColossusRig.Duration(kind) * (Legend ? .9f : 1) / (1 + .1f * Rage);
        Pose.AttackProgress = Mathx.Saturate(StateTime / duration);
        var p = Pose.AttackProgress;
        switch (kind)
        {
            case AttackKind.Stomp:
                if (p < .4f) moveSpeed = Blueprint.WalkSpeed * .3f;
                if (!attackLanded && p >= ColossusRig.StompImpact)
                {
                    attackLanded = true;
                    var tip = ColossusRig.LimbTip(Blueprint, stompPart, PartWorld);
                    var ground = GroundAt(tip);
                    encounter.Emit(new(GameEventKind.StompImpact, ground, StompRadius));
                    foreach (var pl in encounter.Players)
                    {
                        if (!pl.IsOnGround && pl.Mode != PlayerMode.Air) continue;
                        var d = Vector3.Distance(Mathx.Flat(pl.Position), Mathx.Flat(ground));
                        if (d > StompRadius * 2.2f) continue;
                        var falloff = 1 - Mathx.Saturate((d - StompRadius * .5f) / (StompRadius * 1.7f));
                        var away = Mathx.SafeNormalize(Mathx.Flat(pl.Position - ground), Vector3.UnitX);
                        encounter.HurtPlayer(pl, 45 * falloff * encounter.DamageScale, away * 9 * falloff + Vector3.UnitY * 6 * falloff);
                    }
                }
                break;
            case AttackKind.Sweep:
                if (p >= ColossusRig.SweepStart && p <= ColossusRig.SweepEnd)
                {
                    foreach (var pl in encounter.Players)
                    {
                        if (hitThisAttack.Contains(pl.Slot) || pl.Mode is PlayerMode.Attached or PlayerMode.Dead) continue;
                        var d = Vector3.Distance(Mathx.Flat(pl.Position), Mathx.Flat(Pose.Position));
                        if (d > SweepRadius || d < 4) continue;
                        hitThisAttack.Add(pl.Slot);
                        var away = Mathx.SafeNormalize(Mathx.Flat(pl.Position - Pose.Position), Vector3.UnitX);
                        if (encounter.HurtPlayer(pl, 30 * encounter.DamageScale, away * 12 + Vector3.UnitY * 5))
                            encounter.Emit(new(GameEventKind.SweepHit, pl.Position));
                    }
                }
                break;
            case AttackKind.Roar:
                if (!attackLanded && p >= ColossusRig.RoarImpact)
                {
                    attackLanded = true;
                    encounter.Emit(new(GameEventKind.RoarWave, Pose.Position, RoarRadius));
                    foreach (var pl in encounter.Players)
                    {
                        if (pl.Mode == PlayerMode.Dead) continue;
                        if (pl.IsAttachedTo(0, encounter.Climb)) { pl.SpendStamina(22); continue; }
                        var d = Vector3.Distance(Mathx.Flat(pl.Position), Mathx.Flat(Pose.Position));
                        if (d > RoarRadius) continue;
                        var away = Mathx.SafeNormalize(Mathx.Flat(pl.Position - Pose.Position), Vector3.UnitX);
                        encounter.HurtPlayer(pl, 14 * encounter.DamageScale, away * 10 + Vector3.UnitY * 4);
                    }
                }
                break;
            case AttackKind.Charge:
                desiredYaw = chargeYaw;
                moveSpeed = p is > .15f and < .85f ? Blueprint.WalkSpeed * 3.2f : Blueprint.WalkSpeed * .8f;
                if (p is > .15f and < .85f)
                {
                    var front = Pose.Position + Mathx.Forward(Pose.Yaw) * Blueprint.Height * .35f;
                    foreach (var pl in encounter.Players)
                    {
                        if (hitThisAttack.Contains(pl.Slot) || !pl.IsOnGround) continue;
                        if (Vector3.Distance(Mathx.Flat(pl.Position), Mathx.Flat(front)) > Blueprint.Height * .45f) continue;
                        hitThisAttack.Add(pl.Slot);
                        var side = Vector3.Cross(Mathx.Forward(Pose.Yaw), Vector3.UnitY);
                        if (Vector3.Dot(pl.Position - Pose.Position, side) < 0) side = -side;
                        if (encounter.HurtPlayer(pl, 38 * encounter.DamageScale, side * 14 + Mathx.Forward(Pose.Yaw) * 8 + Vector3.UnitY * 7))
                            encounter.Emit(new(GameEventKind.ChargeHit, pl.Position));
                    }
                }
                break;
        }
        if (StateTime >= duration)
        {
            Pose.Attack = AttackKind.None;
            Pose.AttackProgress = 0;
            SetState(ColossusState.Pursue);
            attackCooldown = Blueprint.AttackCooldown * (1 - .18f * Rage) * (Legend ? .8f : 1) * (.8f + random.NextSingle() * .4f);
        }
    }
}
