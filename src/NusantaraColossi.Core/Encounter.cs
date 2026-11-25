using System.Numerics;

namespace NusantaraColossi.Core;

public enum EncounterPhase : byte { Fight, Victory, Defeat }

[Flags]
public enum DroneAbilities : byte
{
    None = 0,
    Scan = 1,
    Hack = 2,
    Decoy = 4,
    All = Scan | Hack | Decoy
}

public enum Rank : byte { C, B, A, S }

/// <summary>Per-player GARUDA drone cooldowns.</summary>
public struct DroneState
{
    public float ScanCooldown;
    public float HackCooldown;
    public float DecoyCooldown;
    public float ScanPulse;
}

public sealed record EncounterOptions
{
    public bool Legend { get; init; }
    public DroneAbilities Abilities { get; init; } = DroneAbilities.All;
    public bool IsAuthority { get; init; } = true;
    public int Seed { get; init; } = 1;
}

public readonly record struct GasCloud(Vector3 Center, float Radius, float Height);

/// <summary>
/// One colossus hunt: region, colossus, players, drones and hazards. Deterministic given inputs, so the
/// same class runs single player, the co-op host (authority) and co-op clients (pose fed by snapshots).
/// </summary>
public sealed class Encounter : IPlayerEnvironment
{
    private readonly List<GameEvent> events = [];
    private readonly GasCloud[] gas;
    private readonly bool[] gasActive;
    private float gustClock;
    private bool gustWarned;
    private bool victoryEmitted;
    private readonly Strike[] strikes = new Strike[32];
    private int lastVolley = -1;
    private readonly float floodBase, floodDepth;
    private bool floodWarned, floodRising, diveWarned, diving;

    public RegionDefinition Region { get; }
    public ColossusBlueprint Blueprint { get; }
    public Colossus Colossus { get; }
    public ClimbWorld Climb { get; }
    public List<Player> Players { get; } = [];
    public DroneState[] Drones { get; } = new DroneState[4];
    public EncounterOptions Options { get; internal set; } // internal set: co-op host migration (Coop.PromoteToAuthority)
    public EncounterPhase Phase { get; private set; } = EncounterPhase.Fight;
    public float Elapsed { get; private set; }
    public float DamageTaken { get; private set; }
    public int Resonances { get; private set; }
    public float GustStrength { get; private set; }
    public Vector3 GustDirection { get; private set; } = Vector3.UnitX;
    public bool GustWarning { get; private set; }
    public IReadOnlyList<GameEvent> Events => events;
    public ReadOnlySpan<GasCloud> GasClouds => gas;
    /// <summary>GARUDA's scan pings the whole arena.</summary>
    public float ScanRange => Region.ArenaRadius * 1.6f;
    public float DamageScale => Options.Legend ? 1.5f : 1;
    public float RespawnTimer { get; private set; }
    /// <summary>Signature projectiles in flight (inactive entries are free slots).</summary>
    public ReadOnlySpan<Strike> Strikes => strikes;
    /// <summary>Mounts parked in the arena: one per hunter.</summary>
    public Vehicle[] Vehicles { get; } = new Vehicle[4];
    public int VehicleCount { get; private set; }
    /// <summary>Clock the signature schedules run on (synced to co-op peers through the guardian pose).</summary>
    public float SignatureTime => Colossus.Pose.Time;

    /// <summary>Current water surface: the region's sea, or Dewi Padi's flood. NaN when the arena is dry.</summary>
    public float WaterLevel => Blueprint.Signature == Signature.Flood
        ? floodBase + Signatures.FloodAmount(Colossus.Pose.Time) * floodDepth * (1 - Colossus.Pose.Collapse)
        : Region.WaterLevel;

    /// <summary>0..1 flood for presentation (Dewi Padi only).</summary>
    public float Flood => Blueprint.Signature == Signature.Flood ? Signatures.FloodAmount(Colossus.Pose.Time) * (1 - Colossus.Pose.Collapse) : 0;

    public Encounter(RegionDefinition region, ColossusBlueprint blueprint, EncounterOptions? options = null)
    {
        Region = region;
        Blueprint = blueprint;
        Options = options ?? new EncounterOptions();
        Colossus = new Colossus(blueprint, region, Options.Seed, Options.Legend);
        Climb = new ClimbWorld(region.Obstacles, blueprint.Parts.Length);
        SyncColossusBoxes();
        var cloudCount = region.Hazard == Hazard.ToxicGas ? 6 : 0;
        gas = new GasCloud[cloudCount];
        gasActive = new bool[cloudCount];
        UpdateHazards(0);
        if (blueprint.Signature == Signature.Flood)
        {
            // Sample the arena floor once: the flood fills the lower terraces and leaves the high steps dry.
            float min = float.MaxValue, max = float.MinValue;
            for (var x = -1f; x <= 1f; x += .1f)
                for (var z = -1f; z <= 1f; z += .1f)
                {
                    if (x * x + z * z > 1) continue;
                    var h = region.Height(x * region.ArenaRadius * .75f, z * region.ArenaRadius * .75f);
                    min = MathF.Min(min, h);
                    max = MathF.Max(max, h);
                }
            floodBase = min - .6f;
            floodDepth = MathF.Max(3.2f, (max - min) * .45f) + .6f;
        }
    }

    public Player AddPlayer(string name, PlayerStats? stats = null)
    {
        var p = new Player(Players.Count, name) { Stats = stats ?? new PlayerStats() };
        var spawn = SpawnFor(p.Slot);
        p.Reset(spawn, 0);
        Players.Add(p);
        EnsureVehicle(p.Slot);
        return p;
    }

    private Vector3 SpawnFor(int slot)
    {
        var spawn = Region.SpawnPoint + new Vector3(slot * 2.5f - 3.75f * (slot > 0 ? 1 : 0), 0, 0);
        spawn.Y = Region.Height(spawn.X, spawn.Z);
        return spawn;
    }

    /// <summary>
    /// Vehicles are indexed by hunter slot, so every peer agrees which bike is whose even when players
    /// joined in a different order. Parks one for every slot up to <paramref name="slot"/>.
    /// </summary>
    public void EnsureVehicle(int slot)
    {
        while (VehicleCount <= slot && VehicleCount < Vehicles.Length) AddVehicle(SpawnFor(VehicleCount), VehicleCount);
    }

    /// <summary>Co-op: mirror the vehicle a remote hunter reports riding.</summary>
    public void ApplyRemoteVehicle(int slot, int riding, Vector3 position, float yaw, float speed)
    {
        for (var i = 0; i < VehicleCount; i++)
            if (Vehicles[i].Rider == slot && i != riding) { Vehicles[i].Rider = -1; Vehicles[i].Speed = 0; }
        if (riding < 0) return;
        EnsureVehicle(riding);
        if (riding >= VehicleCount) return;
        ref var v = ref Vehicles[riding];
        v.Rider = slot;
        v.Position = position;
        v.Yaw = yaw;
        v.Speed = speed;
    }

    private void AddVehicle(Vector3 spawn, int slot)
    {
        if (VehicleCount >= Vehicles.Length) return;
        var water = Region.WaterLevel;
        var seaborne = Region.HasWater && water - Region.Height(spawn.X, spawn.Z) > .6f;
        // Park beside the hunter, a few steps toward the arena.
        var side = (slot % 2 == 0 ? 1 : -1) * (3.5f + slot / 2 * 3f);
        var position = spawn + new Vector3(side, 0, 3);
        position.Y = seaborne ? water : Region.Height(position.X, position.Z);
        Vehicles[VehicleCount++] = new Vehicle
        {
            Kind = seaborne ? VehicleKind.JetSki : VehicleKind.Trail,
            Position = position,
            Yaw = 0,
            Rider = -1
        };
    }

    /// <summary>Frees any vehicle the slot was riding (respawn, disconnect).</summary>
    public void ReleaseVehicle(int slot)
    {
        for (var i = 0; i < VehicleCount; i++)
            if (Vehicles[i].Rider == slot) { Vehicles[i].Rider = -1; Vehicles[i].Speed = 0; }
    }

    public void Emit(GameEvent e) => events.Add(e);

    public float ShakeOf(int owner) => owner == 0 ? Colossus.ShakeIntensity : 0;

    public float StaminaRegenScale => Region.Hazard == Hazard.Heat ? .6f : 1;

    public float GripDrainHazard => Region.Hazard == Hazard.Rain ? 1.25f : 1;

    public bool InGas(Vector3 position)
    {
        for (var i = 0; i < gas.Length; i++)
        {
            var c = gas[i];
            if (position.Y > c.Center.Y + c.Height) continue;
            if (Vector3.DistanceSquared(Mathx.Flat(position), Mathx.Flat(c.Center)) < c.Radius * c.Radius) return true;
        }
        return false;
    }

    /// <summary>Copy the colossus pose into the climb world's dynamic slots.</summary>
    public void SyncColossusBoxes()
    {
        var parts = Blueprint.Parts;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var world = ColossusRig.BoxWorld(part, Colossus.PartWorld[i]);
            Climb.Set(Climb.StaticCount + i, world, part.Half, part.ClimbFaces, part.Surface, 0, i);
        }
    }

    /// <summary>Advance the hunt. <paramref name="inputs"/> holds one input per locally simulated player (by slot).</summary>
    public void Step(ReadOnlySpan<PlayerInput> inputs, float dt, int firstLocalSlot = 0)
    {
        events.Clear();
        RemoteHurts.Clear();
        PendingRemoteStabs.Clear();
        PendingRemoteAbilities.Clear();
        if (dt <= 0) return;
        dt = MathF.Min(dt, 1 / 20f);
        if (Phase == EncounterPhase.Fight) Elapsed += dt;
        UpdateHazards(dt);

        if (Options.IsAuthority) Colossus.Think(this, dt);
        SyncColossusBoxes();
        UpdateSignature();

        for (var i = 0; i < inputs.Length; i++)
        {
            var slot = firstLocalSlot + i;
            if (slot >= Players.Count) break;
            var player = Players[slot];
            var input = inputs[i];
            if (Phase != EncounterPhase.Fight) input = input with { Attack = false, ScanPressed = false, HackPressed = false, DecoyPressed = false };
            player.Update(input, this, dt);
            UpdateDrone(player, input, dt);
            if (player.PendingStab >= 0)
            {
                var charge = player.PendingStab;
                player.PendingStab = -1;
                if (Options.IsAuthority) ResolveStab(player.BodyCenter, player.IsAttachedTo(0, Climb), charge, player.Slot, Colossus.Pose.Time);
                else PendingRemoteStabs.Add(new(player.Slot, player.BodyCenter, player.IsAttachedTo(0, Climb), charge, Colossus.Pose.Time));
            }
        }

        UpdateOutcome(dt);
    }

    /// <summary>Stabs a co-op client made this tick; the network layer forwards them to the host.</summary>
    public List<StabRequest> PendingRemoteStabs { get; } = [];
    /// <summary>Drone abilities a co-op client used this tick; forwarded to the host.</summary>
    public List<AbilityRequest> PendingRemoteAbilities { get; } = [];

    private void UpdateDrone(Player player, in PlayerInput input, float dt)
    {
        ref var drone = ref Drones[player.Slot];
        drone.ScanCooldown = MathF.Max(0, drone.ScanCooldown - dt);
        drone.HackCooldown = MathF.Max(0, drone.HackCooldown - dt);
        drone.DecoyCooldown = MathF.Max(0, drone.DecoyCooldown - dt);
        drone.ScanPulse = MathF.Max(0, drone.ScanPulse - dt);
        if (player.Mode == PlayerMode.Dead || Phase != EncounterPhase.Fight) return;
        if (input.ScanPressed) UseAbility(player, DroneAbilities.Scan, input.CameraForward);
        if (input.HackPressed) UseAbility(player, DroneAbilities.Hack, input.CameraForward);
        if (input.DecoyPressed) UseAbility(player, DroneAbilities.Decoy, input.CameraForward);
    }

    /// <summary>Uses a GARUDA ability. Returns false (and emits a "denied" event) when unavailable.</summary>
    public bool UseAbility(Player player, DroneAbilities ability, Vector3 aim)
    {
        ref var drone = ref Drones[player.Slot];
        var stats = player.Stats;
        var distance = Vector3.Distance(player.Position, Colossus.Pose.Position);
        var unlocked = (Options.Abilities & ability) != 0;
        switch (ability)
        {
            case DroneAbilities.Scan:
                if (!unlocked || drone.ScanCooldown > 0) { Emit(new(GameEventKind.ScanDenied, player.Position, drone.ScanCooldown, player.Slot)); return false; }
                drone.ScanCooldown = stats.ScanCooldown;
                drone.ScanPulse = 1.4f;
                Emit(new(GameEventKind.Scan, player.Position, 0, player.Slot));
                if (!Options.IsAuthority) { PendingRemoteAbilities.Add(new(player.Slot, ability, player.Position, aim)); return true; }
                if (distance < ScanRange) Colossus.Scan(Blueprint.SealOpenSeconds * stats.SealScale);
                return true;
            case DroneAbilities.Hack when Blueprint.Signature == Signature.Firewall:
                // GARUDA cannot hack GARUDA NET: it refuses, and says so (Value -1 marks immunity).
                Emit(new(GameEventKind.HackDenied, player.Position, -1, player.Slot));
                return false;
            case DroneAbilities.Hack:
                if (!unlocked || drone.HackCooldown > 0 || distance > 95 || Colossus.IsDefeated)
                { Emit(new(GameEventKind.HackDenied, player.Position, drone.HackCooldown, player.Slot)); return false; }
                drone.HackCooldown = stats.HackCooldown;
                Emit(new(GameEventKind.Hack, Colossus.Pose.Position, 0, player.Slot));
                if (!Options.IsAuthority) { PendingRemoteAbilities.Add(new(player.Slot, ability, player.Position, aim)); return true; }
                Colossus.Stun(4.2f * Blueprint.HackSusceptibility + stats.HackBonusSeconds);
                Emit(new(GameEventKind.Stunned, Colossus.Pose.Position));
                return true;
            case DroneAbilities.Decoy:
                if (!unlocked || drone.DecoyCooldown > 0) { Emit(new(GameEventKind.DecoyDenied, player.Position, drone.DecoyCooldown, player.Slot)); return false; }
                drone.DecoyCooldown = stats.DecoyCooldown;
                var point = player.Position + Mathx.SafeNormalize(Mathx.Flat(aim), Mathx.Forward(player.Yaw)) * 16;
                point.Y = Region.Height(point.X, point.Z);
                Emit(new(GameEventKind.Decoy, point, 0, player.Slot));
                if (!Options.IsAuthority) { PendingRemoteAbilities.Add(new(player.Slot, ability, player.Position, aim)); return true; }
                Colossus.Lure(point, 7);
                return true;
        }
        return false;
    }

    /// <summary>Host-side ability application for a remote co-op player (cooldowns are checked on the client).</summary>
    public void ApplyRemoteAbility(AbilityRequest request)
    {
        var player = request.Slot < Players.Count ? Players[request.Slot] : null;
        switch (request.Ability)
        {
            case DroneAbilities.Scan when Vector3.Distance(request.Position, Colossus.Pose.Position) < ScanRange:
                Colossus.Scan(Blueprint.SealOpenSeconds * (player?.Stats.SealScale ?? 1));
                break;
            case DroneAbilities.Hack when !Colossus.IsDefeated && Blueprint.Signature != Signature.Firewall:
                Colossus.Stun(4.2f * Blueprint.HackSusceptibility + (player?.Stats.HackBonusSeconds ?? 0));
                Emit(new(GameEventKind.Stunned, Colossus.Pose.Position));
                break;
            case DroneAbilities.Decoy:
                var point = request.Position + Mathx.SafeNormalize(Mathx.Flat(request.Aim), Vector3.UnitZ) * 16;
                point.Y = Region.Height(point.X, point.Z);
                Colossus.Lure(point, 7);
                break;
        }
    }

    /// <summary>Authority-side stab resolution. Returns the core index hit, or -1.</summary>
    public int ResolveStab(Vector3 body, bool onColossus, float charge, int slot, float pulseTime)
    {
        if (!onColossus || Colossus.IsDefeated) return -1;
        var bp = Blueprint;
        var best = -1;
        var bestDistance = 3.1f;
        for (var i = 0; i < bp.Cores.Length; i++)
        {
            if (Colossus.Cores[i].Destroyed) continue;
            var (position, _) = ColossusRig.CoreWorld(bp, bp.Cores[i], Colossus.PartWorld);
            var d = Vector3.Distance(position, body);
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        var power = slot < Players.Count ? Players[slot].Stats.StabPower : 1;
        if (best < 0)
        {
            Colossus.AddAnnoyance(.8f);
            Emit(new(GameEventKind.StabFlesh, body, charge, slot));
            return -1;
        }
        ref var core = ref Colossus.Cores[best];
        var (corePosition, _) = ColossusRig.CoreWorld(bp, bp.Cores[best], Colossus.PartWorld);
        if (core.SealOpen <= 0)
        {
            Emit(new(GameEventKind.StabSealed, corePosition, best, slot));
            return best;
        }
        if (bp.Cores[best].Plated && Colossus.State != ColossusState.Stunned)
        {
            Emit(new(GameEventKind.StabDeflected, corePosition, best, slot));
            return best;
        }
        var resonance = Colossus.CorePulse(pulseTime) > .5f;
        var damage = (10 + 46 * MathF.Pow(charge, 1.4f)) * power * (resonance ? 1.5f : 1);
        core.Health -= damage;
        Colossus.AddAnnoyance(2.2f);
        if (resonance)
        {
            Resonances++;
            Emit(new(GameEventKind.CoreResonance, corePosition, damage, slot));
        }
        Emit(new(GameEventKind.CoreHit, corePosition, damage, slot));
        if (core.Destroyed)
        {
            core.Health = 0;
            Emit(new(GameEventKind.CoreDestroyed, corePosition, best, slot));
            Colossus.OnCoreDestroyed(this);
        }
        return best;
    }

    /// <summary>Hits the host landed on remote co-op players this tick; forwarded to their owners.</summary>
    public List<(int Slot, float Amount, Vector3 Knockback)> RemoteHurts { get; } = [];

    internal bool HurtPlayer(Player player, float amount, Vector3 knockback)
    {
        if (player.IsRemote)
        {
            if (player.Mode == PlayerMode.Dead) return false;
            RemoteHurts.Add((player.Slot, amount, knockback));
            return true;
        }
        if (!player.Hurt(amount, knockback, this)) return false;
        DamageTaken += amount;
        return true;
    }

    // ---------------------------------------------------------------- signature mechanics

    private void UpdateSignature()
    {
        var signature = Blueprint.Signature;
        if (signature == Signature.None) return;
        var t = Colossus.Pose.Time;
        var fighting = Phase == EncounterPhase.Fight && !Colossus.IsDefeated;
        switch (signature)
        {
            case Signature.Flood:
            {
                var warn = fighting && Signatures.FloodWarning(t);
                if (warn && !floodWarned) Emit(new(GameEventKind.FloodWarn, Colossus.Pose.Position));
                floodWarned = warn;
                var rising = fighting && Signatures.FloodAmount(t) > .02f;
                if (rising && !floodRising) Emit(new(GameEventKind.FloodRise, Colossus.Pose.Position, floodDepth));
                floodRising = rising;
                break;
            }
            case Signature.Dive:
            {
                var warn = fighting && Signatures.DiveWarning(t);
                if (warn && !diveWarned) Emit(new(GameEventKind.DiveWarn, Colossus.Pose.Position));
                diveWarned = warn;
                var under = fighting && Signatures.DiveAmount(t) > .05f;
                if (under && !diving) Emit(new(GameEventKind.DiveStart, Colossus.Pose.Position));
                if (!under && diving) Emit(new(GameEventKind.Surface, Colossus.Pose.Position));
                diving = under;
                break;
            }
            default:
            {
                var period = Signatures.VolleyPeriod(signature, Colossus.Rage);
                var active = fighting && Colossus.State is not (ColossusState.Dormant or ColossusState.Stunned or ColossusState.Stagger);
                var volley = (int)MathF.Floor(t / period);
                if (!active || lastVolley < 0) lastVolley = volley; // the first volley comes one full period after waking
                else if (volley != lastVolley)
                {
                    lastVolley = volley;
                    FireVolley(signature, volley, t);
                }
                break;
            }
        }
        ResolveStrikes(t);
    }

    private void FireVolley(Signature signature, int volley, float t)
    {
        var kind = signature switch { Signature.GlassShards => StrikeKind.Shard, Signature.Cannons => StrikeKind.Cannonball, _ => StrikeKind.DataBolt };
        var rage = Colossus.Rage;
        var pose = Colossus.Pose;
        var forward = Mathx.Forward(pose.Yaw);
        var right = Vector3.Cross(Vector3.UnitY, forward);
        var extra = kind switch { StrikeKind.Shard => 4 + 2 * rage, StrikeKind.Cannonball => 2 + rage, _ => rage };
        Emit(new(GameEventKind.Volley, pose.Position, (float)kind));

        // Aimed shots first: two shards or one cannonball per hunter in the open; data bolts hit everyone, climbers too.
        var fired = 0;
        foreach (var p in Players)
        {
            if (p.Mode == PlayerMode.Dead) continue;
            var attached = p.Mode == PlayerMode.Attached;
            if (attached && kind != StrikeKind.DataBolt) continue;
            var shots = kind == StrikeKind.Shard ? 2 : 1;
            for (var s = 0; s < shots; s++)
            {
                var spread = kind == StrikeKind.DataBolt ? 0 : s == 0 ? 2.5f : 6f;
                var a = Signatures.Hash01(volley, p.Slot, s) * Mathx.Tau;
                var target = (attached ? p.BodyCenter : p.Position) + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * spread * Signatures.Hash01(volley, p.Slot, s + 7);
                if (!attached) target.Y = SurfaceAt(target);
                Launch(kind, OriginFor(kind, target, fired), target, t);
                fired++;
            }
        }
        // The rest land around the guardian, so standing near it is never fully safe.
        for (var i = 0; i < extra; i++)
        {
            var a = Signatures.Hash01(volley, 31, i) * Mathx.Tau;
            var r = kind == StrikeKind.Cannonball ? 18 + Signatures.Hash01(volley, 37, i) * 40 : 7 + Signatures.Hash01(volley, 41, i) * Colossus.SweepRadius;
            var target = pose.Position + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * r;
            target.Y = SurfaceAt(target);
            Launch(kind, OriginFor(kind, target, fired + i), target, t);
        }

        Vector3 OriginFor(StrikeKind k, Vector3 target, int i)
        {
            switch (k)
            {
                case StrikeKind.Shard:
                    // Panes break off the tower's glass torso high above.
                    return pose.Position + Vector3.UnitY * Blueprint.Height * (.55f + .25f * Signatures.Hash01(volley, 43, i))
                           + Mathx.SafeNormalize(Mathx.Flat(target - pose.Position), forward) * 6;
                case StrikeKind.Cannonball:
                {
                    // A gun port on the side facing the target.
                    var side = Vector3.Dot(target - pose.Position, right) >= 0 ? 1 : -1;
                    return pose.Position + right * side * 8.5f + forward * (-14 + 28 * Signatures.Hash01(volley, 47, i)) + Vector3.UnitY * 5;
                }
                default:
                {
                    var head = Blueprint.HeadPart;
                    return head >= 0 ? Colossus.PartWorld[head].Translation : pose.Position + Vector3.UnitY * Blueprint.Height;
                }
            }
        }
    }

    private float SurfaceAt(Vector3 p)
    {
        var ground = Region.Height(p.X, p.Z);
        var water = WaterLevel;
        return float.IsNaN(water) ? ground : MathF.Max(ground, water);
    }

    private void Launch(StrikeKind kind, Vector3 origin, Vector3 target, float t)
    {
        for (var i = 0; i < strikes.Length; i++)
        {
            if (strikes[i].Active) continue;
            strikes[i] = new Strike
            {
                Kind = kind, Origin = origin, Target = target, Radius = Signatures.StrikeRadius(kind),
                LaunchTime = t, LandTime = t + Signatures.FlightTime(kind), Active = true
            };
            return;
        }
    }

    private void ResolveStrikes(float t)
    {
        for (var i = 0; i < strikes.Length; i++)
        {
            ref var s = ref strikes[i];
            if (!s.Active || t < s.LandTime) continue;
            s.Active = false;
            Emit(new(GameEventKind.StrikeImpact, s.Target, (float)s.Kind));
            if (!Options.IsAuthority || Phase != EncounterPhase.Fight) continue;
            foreach (var p in Players)
            {
                if (p.Mode == PlayerMode.Dead) continue;
                bool hit;
                if (s.Kind == StrikeKind.DataBolt) hit = Vector3.Distance(p.BodyCenter, s.Target) < s.Radius + .4f;
                else hit = p.Mode != PlayerMode.Attached
                           && Vector3.Distance(Mathx.Flat(p.Position), Mathx.Flat(s.Target)) < s.Radius
                           && MathF.Abs(p.Position.Y - s.Target.Y) < 4;
                if (!hit) continue;
                var away = Mathx.SafeNormalize(Mathx.Flat(p.Position - s.Target), Vector3.UnitX);
                HurtPlayer(p, Signatures.StrikeDamage(s.Kind) * DamageScale, s.Kind == StrikeKind.DataBolt ? Vector3.Zero : away * 8 + Vector3.UnitY * 5);
            }
        }
    }


    private void UpdateHazards(float dt)
    {
        var t = Colossus.Pose.Time;
        for (var i = 0; i < gas.Length; i++)
        {
            var a = i / (float)gas.Length * Mathx.Tau + t * (.035f + i * .006f) * (i % 2 == 0 ? 1 : -1);
            var r = Region.ArenaRadius * (.22f + .12f * (i % 3));
            var x = MathF.Sin(a) * r;
            var z = MathF.Cos(a) * r;
            // Clouds breathe: each one fades in and out so safe paths keep changing.
            var phase = MathF.Sin(t * .11f + i * 1.7f);
            gasActive[i] = phase > -.35f;
            gas[i] = new GasCloud(new Vector3(x, Region.Height(x, z), z), gasActive[i] ? 15 + 5 * phase : 0, 12);
        }

        if (Region.Hazard == Hazard.Wind)
        {
            gustClock += dt;
            const float cycle = 15f;
            var c = gustClock % cycle;
            GustWarning = c is >= 10.5f and < 12.5f;
            if (GustWarning && !gustWarned) { Emit(new(GameEventKind.GustWarn, Vector3.Zero)); gustWarned = true; }
            if (!GustWarning && c < 10.5f) gustWarned = false;
            var target = c is >= 12.5f and < 15f ? 1f : 0f;
            if (target > 0 && GustStrength < .05f) Emit(new(GameEventKind.Gust, Vector3.Zero));
            GustStrength = Mathx.Damp(GustStrength, target, 5, dt);
            var angle = gustClock * .05f;
            GustDirection = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        }
    }

    private void UpdateOutcome(float dt)
    {
        if (Phase != EncounterPhase.Fight) return;
        if (Colossus.State == ColossusState.Defeated && !victoryEmitted)
        {
            victoryEmitted = true;
            Phase = EncounterPhase.Victory;
            Emit(new(GameEventKind.Victory, Colossus.Pose.Position, Elapsed));
            return;
        }
        var alive = 0;
        foreach (var p in Players) if (p.Mode != PlayerMode.Dead) alive++;
        if (Players.Count > 0 && alive == 0)
        {
            Phase = EncounterPhase.Defeat;
            return;
        }
        // Co-op: a fallen hunter rejoins at camp after a short wait.
        if (Players.Count > 1)
        {
            foreach (var p in Players)
            {
                if (p.Mode != PlayerMode.Dead || p.IsRemote) continue;
                RespawnTimer += dt;
                if (RespawnTimer > 8)
                {
                    RespawnTimer = 0;
                    ReleaseVehicle(p.Slot);
                    p.Reset(Region.SpawnPoint, 0);
                    Emit(new(GameEventKind.Retry, p.Position, 0, p.Slot));
                }
            }
        }
    }

    /// <summary>Client side: authoritative clock and outcome from the host.</summary>
    public void SetElapsed(float elapsed, EncounterPhase phase)
    {
        Elapsed = elapsed;
        if (phase != Phase && phase == EncounterPhase.Victory) victoryEmitted = true;
        Phase = phase;
    }

    /// <summary>Client side: wind from the host.</summary>
    public void SetWeather(float gust, Vector3 direction, bool warning)
    {
        GustStrength = gust;
        GustDirection = direction;
        GustWarning = warning;
    }

    /// <summary>Client side: apply a hit the host landed on this peer's player.</summary>
    public void ApplyHurt(int slot, float amount, Vector3 knockback)
    {
        if (slot < Players.Count && !Players[slot].IsRemote) HurtPlayer(Players[slot], amount, knockback);
    }

    public Rank Rank => RankFor(Elapsed, DamageTaken);

    public Rank RankFor(float seconds, float damage)
    {
        var par = 150 + Blueprint.TotalCoreHealth * .9f;
        var score = seconds / par + damage / 400f;
        return score < .8f ? Rank.S : score < 1.15f ? Rank.A : score < 1.7f ? Rank.B : Rank.C;
    }
}

public readonly record struct StabRequest(int Slot, Vector3 Body, bool OnColossus, float Charge, float PulseTime);

public readonly record struct AbilityRequest(int Slot, DroneAbilities Ability, Vector3 Position, Vector3 Aim);
