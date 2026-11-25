using System.Numerics;

namespace NusantaraColossi.Core;

public enum PlayerMode : byte { Ground, Air, Attached, Swim, Dead }

/// <summary>One tick of player intent. Buttons marked "pressed" are edge-triggered by the input layer.</summary>
public struct PlayerInput
{
    /// <summary>X = right, Y = forward, length ≤ 1.</summary>
    public Vector2 Move;
    /// <summary>Camera look direction; movement is camera-relative.</summary>
    public Vector3 CameraForward;
    public bool JumpPressed;
    public bool DodgePressed;
    public bool Grip;
    public bool Attack;
    public bool Sprint;
    public bool ScanPressed;
    public bool HackPressed;
    public bool DecoyPressed;
    /// <summary>Mount or dismount a vehicle; open the glider in the air.</summary>
    public bool InteractPressed;
}

/// <summary>Derived from skills; read every tick so upgrades apply immediately.</summary>
public sealed record PlayerStats
{
    public float MaxHealth { get; init; } = 100;
    public float MaxStamina { get; init; } = 100;
    public float RunSpeed { get; init; } = 5.4f;
    public float SprintSpeed { get; init; } = 8.8f;
    public float ClimbSpeed { get; init; } = 2.7f;
    public float GripDrainScale { get; init; } = 1;
    public float FallDamageScale { get; init; } = 1;
    public float DodgeCost { get; init; } = 18;
    public float StabPower { get; init; } = 1;
    public bool SecondWind { get; init; }
    public bool HunterInstinct { get; init; }
    public float ScanCooldown { get; init; } = 14;
    public float HackCooldown { get; init; } = 35;
    public float HackBonusSeconds { get; init; }
    public float SealScale { get; init; } = 1;
    public float DecoyCooldown { get; init; } = 22;
}

/// <summary>Everything the player simulation needs from the encounter around it.</summary>
public interface IPlayerEnvironment
{
    RegionDefinition Region { get; }
    ClimbWorld Climb { get; }
    /// <summary>0..1 shake intensity of whatever owns the box (colossus slot), 0 for static props.</summary>
    float ShakeOf(int owner);
    float GustStrength { get; }
    Vector3 GustDirection { get; }
    bool InGas(Vector3 position);
    float StaminaRegenScale { get; }
    float GripDrainHazard { get; }
    /// <summary>Water surface height right now (sea or flood), NaN when dry.</summary>
    float WaterLevel { get; }
    Vehicle[] Vehicles { get; }
    int VehicleCount { get; }
    void Emit(GameEvent e);
}

public sealed class Player
{
    public const float Gravity = 19f;
    public const float BodyHeight = 1.8f;
    public const float Radius = .42f;
    public const float StabChargeSeconds = 1.1f;

    public int Slot { get; }
    public string Name { get; set; }
    /// <summary>Co-op peer simulated elsewhere; its state arrives over the network.</summary>
    public bool IsRemote { get; set; }
    public PlayerStats Stats { get; set; } = new();
    public PlayerMode Mode { get; internal set; } = PlayerMode.Ground;

    /// <summary>Marks a remote co-op player whose connection dropped.</summary>
    public void Disconnect() { Mode = PlayerMode.Dead; Health = 0; }
    /// <summary>Feet position.</summary>
    public Vector3 Position { get; internal set; }
    public Vector3 Velocity { get; internal set; }
    public float Yaw { get; internal set; }
    public ClimbContact Contact;
    public float Health { get; internal set; }
    public float Stamina { get; internal set; }
    public float StabCharge { get; internal set; }
    public bool Charging { get; internal set; }
    public float Invulnerable { get; internal set; }
    public float DodgeTimer { get; internal set; }
    /// <summary>Surface "up" (for rendering orientation while climbing).</summary>
    public Vector3 Up { get; internal set; } = Vector3.UnitY;
    /// <summary>Facing direction (into the wall while climbing).</summary>
    public Vector3 Facing { get; internal set; } = Vector3.UnitZ;
    public bool IsStanding { get; internal set; }
    public bool IsMoving { get; internal set; }
    public float AirTime { get; internal set; }
    /// <summary>Index into the encounter's vehicles while riding, else -1.</summary>
    public int Riding { get; internal set; } = -1;
    public bool IsRiding => Riding >= 0;
    /// <summary>The GARUDA glider wing is open.</summary>
    public bool Gliding { get; internal set; }

    /// <summary>Last state of the ridden vehicle, sent to co-op peers.</summary>
    public Vehicle VehicleState { get; private set; }

    /// <summary>Co-op: a remote hunter's travel mode, as reported in its player state.</summary>
    public void SetRemoteTravel(int riding, bool gliding)
    {
        Riding = riding;
        Gliding = gliding;
    }
    /// <summary>1 = full lungs. Drains while the head is under water (Leviathan dives, floods).</summary>
    public float Breath { get; internal set; } = 1;
    private bool breathWarned;
    /// <summary>Set by the simulation when a stab is released this tick (charge 0..1); consumed by the encounter.</summary>
    internal float PendingStab = -1;
    /// <summary>True on the tick a stab is released (for the thrust animation).</summary>
    public bool PendingStabRelease => PendingStab >= 0 || stabReleasedAt > 0;
    private float stabReleasedAt;
    private bool secondWindUsed;
    private Vector3 lastBodyWorld;
    private bool hasLastBody;
    private float footstepDistance;
    private bool staminaWarned;
    private bool wasAttackHeld;

    public Player(int slot, string name)
    {
        Slot = slot;
        Name = name;
        Health = Stats.MaxHealth;
        Stamina = Stats.MaxStamina;
    }

    public bool IsAttachedTo(int owner, ClimbWorld climb) => Mode == PlayerMode.Attached && climb[Contact.Box].Owner == owner;
    public bool IsOnGround => Mode is PlayerMode.Ground or PlayerMode.Swim;
    public Vector3 BodyCenter => Mode == PlayerMode.Attached && !IsStanding ? Position + Up * (BodyHeight * .5f) : Position + Vector3.UnitY * (BodyHeight * .5f);

    public void Reset(Vector3 spawn, float yaw)
    {
        Mode = PlayerMode.Ground;
        Position = spawn;
        Velocity = Vector3.Zero;
        Yaw = yaw;
        Health = Stats.MaxHealth;
        Stamina = Stats.MaxStamina;
        StabCharge = 0;
        Charging = false;
        secondWindUsed = false;
        hasLastBody = false;
        Up = Vector3.UnitY;
        Facing = Mathx.Forward(yaw);
        Riding = -1;
        Gliding = false;
        Breath = 1;
        breathWarned = false;
    }

    /// <summary>Applies damage unless dodging. Returns true if it landed.</summary>
    public bool Hurt(float amount, Vector3 knockback, IPlayerEnvironment env)
    {
        if (Mode == PlayerMode.Dead || Invulnerable > 0 || amount <= 0) return false;
        Health = MathF.Max(0, Health - amount);
        env.Emit(new(GameEventKind.PlayerHurt, Position, amount, Slot));
        if (IsRiding && (knockback.LengthSquared() > 1 || Health <= 0)) Dismount(env, Vector3.Zero);
        if (Gliding && knockback.LengthSquared() > 1) CloseGlider(env);
        if (knockback.LengthSquared() > 1 && Mode != PlayerMode.Attached)
        {
            Velocity = knockback;
            Mode = PlayerMode.Air;
        }
        if (Health <= 0)
        {
            Mode = PlayerMode.Dead;
            Velocity = Vector3.Zero;
            Charging = false;
            env.Emit(new(GameEventKind.PlayerDied, Position, 0, Slot));
        }
        else Invulnerable = .6f;
        return true;
    }

    public void Heal(float amount) => Health = MathF.Min(Stats.MaxHealth, Health + amount);

    public void SpendStamina(float amount) => Stamina = MathF.Max(0, Stamina - amount);

    public void Update(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        if (Mode == PlayerMode.Dead) return;
        Invulnerable = MathF.Max(0, Invulnerable - dt);
        DodgeTimer = MathF.Max(0, DodgeTimer - dt);
        stabReleasedAt = MathF.Max(0, stabReleasedAt - dt);
        UpdateStab(input, env, dt);

        if (IsRiding)
        {
            UpdateRide(input, env, dt);
            UpdateBreath(env, dt);
            return;
        }
        switch (Mode)
        {
            case PlayerMode.Ground:
            case PlayerMode.Swim:
                UpdateGround(input, env, dt);
                break;
            case PlayerMode.Air:
                UpdateAir(input, env, dt);
                break;
            case PlayerMode.Attached:
                UpdateAttached(input, env, dt);
                break;
        }

        UpdateBreath(env, dt);
        if (env.InGas(BodyCenter))
        {
            Health = MathF.Max(0, Health - 5.5f * dt);
            if (Health <= 0) Hurt(1, Vector3.Zero, env);
        }

        if (Stamina <= 0 && Stats.SecondWind && !secondWindUsed)
        {
            secondWindUsed = true;
            Stamina = Stats.MaxStamina * .4f;
            env.Emit(new(GameEventKind.SecondWind, Position, 0, Slot));
        }
        var low = Stamina < Stats.MaxStamina * .25f && Mode == PlayerMode.Attached && !IsStanding;
        if (low && !staminaWarned) env.Emit(new(GameEventKind.StaminaLow, Position, Stamina, Slot));
        staminaWarned = low;
    }

    private void UpdateStab(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        var canStab = Mode == PlayerMode.Attached;
        if (input.Attack && canStab)
        {
            if (!Charging) { Charging = true; StabCharge = 0; }
            StabCharge = MathF.Min(1, StabCharge + dt / StabChargeSeconds);
        }
        else if (Charging)
        {
            Charging = false;
            if (canStab && wasAttackHeld)
            {
                PendingStab = StabCharge;
                stabReleasedAt = .05f;
                SpendStamina(7);
            }
            StabCharge = 0;
        }
        wasAttackHeld = input.Attack;
    }

    private (Vector3 Forward, Vector3 Right) CameraBasis(in PlayerInput input)
    {
        var forward = Mathx.SafeNormalize(Mathx.Flat(input.CameraForward), Mathx.Forward(Yaw));
        return (forward, Vector3.Cross(forward, Vector3.UnitY));
    }

    private void UpdateGround(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        var region = env.Region;
        var (forward, right) = CameraBasis(input);
        var wish = forward * input.Move.Y + right * input.Move.X;
        var amount = MathF.Min(1, wish.Length());
        if (amount > .01f) wish /= wish.Length();
        var level = env.WaterLevel;
        var water = float.IsNaN(level) ? 0 : level - region.Height(Position.X, Position.Z);
        var swimming = water > 1.25f;
        Mode = swimming ? PlayerMode.Swim : PlayerMode.Ground;

        var sprinting = input.Sprint && Stamina > 1 && amount > .1f && !swimming;
        var speed = swimming ? 2.6f : sprinting ? Stats.SprintSpeed : Stats.RunSpeed;
        if (water > .5f && !swimming) speed *= .7f;
        if (input.DodgePressed && DodgeTimer <= 0 && Stamina >= Stats.DodgeCost * .5f && !swimming)
        {
            DodgeTimer = .32f;
            Invulnerable = MathF.Max(Invulnerable, .38f);
            SpendStamina(Stats.DodgeCost);
            var dir = amount > .1f ? wish : Mathx.Forward(Yaw);
            Velocity = dir * 13f;
            env.Emit(new(GameEventKind.Dodge, Position, 0, Slot));
        }
        var target = wish * speed * amount;
        var horizontal = Mathx.Flat(Velocity);
        horizontal = DodgeTimer > 0 ? horizontal : Mathx.Damp(horizontal, target, swimming ? 4 : 12, dt);
        if (amount > .1f) Yaw = Mathx.TurnToward(Yaw, Mathx.YawOf(wish), 10 * dt);
        IsMoving = horizontal.LengthSquared() > .25f;

        var next = Position + horizontal * dt;
        var r = MathF.Sqrt(next.X * next.X + next.Z * next.Z);
        var limit = region.ArenaRadius * 1.08f;
        if (r > limit) next = new Vector3(next.X / r * limit, next.Y, next.Z / r * limit);
        var ground = region.Height(next.X, next.Z);
        if (swimming)
        {
            next.Y = level - 1.35f;
            Stamina = MathF.Max(0, Stamina - 4 * dt);
            if (Stamina <= 0) Hurt(8 * dt, Vector3.Zero, env);
        }
        else
        {
            // Step down small slopes; leave the ground on cliffs.
            if (Position.Y - ground > .6f && Velocity.Y <= 0)
            {
                Mode = PlayerMode.Air;
                Position = next;
                Velocity = horizontal;
                return;
            }
            next.Y = ground;
            var regen = sprinting ? -11f : 30f * env.StaminaRegenScale;
            if (env.InGas(BodyCenter)) regen = MathF.Min(regen, 0);
            Stamina = Math.Clamp(Stamina + regen * dt, 0, Stats.MaxStamina);
        }
        Velocity = horizontal;
        Position = next;
        PushOutOfBoxes(env, out _);
        if (env.GustStrength > 0 && !swimming) Position += env.GustDirection * env.GustStrength * 2.5f * dt;

        footstepDistance += horizontal.Length() * dt;
        if (footstepDistance > (sprinting ? 2.2f : 1.6f))
        {
            footstepDistance = 0;
            env.Emit(new(swimming || water > .2f ? GameEventKind.Splash : GameEventKind.Footstep, Position, sprinting ? 1 : .6f, Slot));
        }

        if (input.InteractPressed && TryMount(env)) return;
        if (input.JumpPressed && !swimming)
        {
            Velocity = horizontal + Vector3.UnitY * 6.4f;
            Mode = PlayerMode.Air;
            AirTime = 0;
            env.Emit(new(GameEventKind.Jump, Position, 0, Slot));
            return;
        }
        // Grab a climbable face we are pressed against.
        if (input.Grip && env.Climb.FindGrip(BodyCenter, Radius + .45f, true, out var grip) && !env.Climb.IsStanding(grip))
            Attach(grip, env, GameEventKind.Grab);
        Up = Vector3.UnitY;
        Facing = Mathx.Forward(Yaw);
        IsStanding = true;
    }

    private void UpdateAir(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        AirTime += dt;
        var (forward, right) = CameraBasis(input);
        var wish = forward * input.Move.Y + right * input.Move.X;
        var toggle = input.InteractPressed || (input.JumpPressed && !Gliding);
        if (toggle && AirTime > Glider.MinAirTime)
        {
            if (Gliding) CloseGlider(env);
            else if (Stamina > 5) { Gliding = true; env.Emit(new(GameEventKind.GliderOpen, Position, 0, Slot)); }
        }
        var v = Velocity;
        if (Gliding)
        {
            if (wish.LengthSquared() > .01f) Yaw = Mathx.TurnToward(Yaw, Mathx.YawOf(wish), Glider.TurnRate * dt);
            var cruise = Glider.CruiseSpeed + Glider.DiveBoost * MathF.Max(0, input.Move.Y);
            var horizontal = Mathx.Damp(Mathx.Flat(v), Mathx.Forward(Yaw) * cruise, 1.6f, dt);
            var sink = Mathx.Damp(v.Y, -Glider.SinkSpeed, v.Y < -Glider.SinkSpeed ? 5 : 1.5f, dt);
            v = horizontal + Vector3.UnitY * sink;
            if (env.GustStrength > 0) v += env.GustDirection * env.GustStrength * 14 * dt;
            Stamina = MathF.Max(0, Stamina - Glider.StaminaDrain * dt);
            if (Stamina <= 0) CloseGlider(env);
        }
        else
        {
            v += new Vector3(wish.X, 0, wish.Z) * 4f * dt;
            v.Y -= Gravity * dt;
            if (env.GustStrength > 0) v += env.GustDirection * env.GustStrength * 6 * dt;
        }
        Velocity = v;
        Position += v * dt;
        IsMoving = true;
        IsStanding = false;

        // Collide with boxes: land on tops, grab climbable faces, otherwise slide off.
        var center = BodyCenter;
        var hit = env.Climb.FindContaining(center, Radius + .35f);
        if (hit >= 0 && env.Climb.Project(hit, center, false, out var contact, out _))
        {
            var normal = env.Climb.WorldNormal(contact);
            if (normal.Y > .7f && Velocity.Y <= 1)
            {
                LandImpact(env, -Velocity.Y);
                Attach(contact, env, GameEventKind.Land);
                return;
            }
            ref readonly var box = ref env.Climb[hit];
            if (input.Grip && box.IsClimbable(contact.Axis, contact.Sign))
            {
                Attach(contact, env, GameEventKind.Grab);
                return;
            }
            var surface = env.Climb.WorldPosition(contact) + normal * (Radius + .36f);
            Position += surface - center;
            Velocity -= normal * MathF.Min(0, Vector3.Dot(Velocity, normal));
        }
        else if (input.Grip && env.Climb.FindGrip(center, Radius + .55f, true, out var grip))
        {
            Attach(grip, env, GameEventKind.Grab);
            return;
        }

        var region = env.Region;
        var ground = region.Height(Position.X, Position.Z);
        var level = env.WaterLevel;
        if (!float.IsNaN(level) && level - ground > 1.25f && Position.Y < level - 1.35f)
        {
            if (Gliding) CloseGlider(env);
            env.Emit(new(GameEventKind.Splash, Position, MathF.Abs(Velocity.Y), Slot));
            Position = Position with { Y = level - 1.35f };
            Velocity = Mathx.Flat(Velocity) * .3f;
            Mode = PlayerMode.Swim;
            return;
        }
        if (Position.Y <= ground)
        {
            if (Gliding) CloseGlider(env);
            LandImpact(env, -Velocity.Y);
            if (Mode == PlayerMode.Dead) return;
            Position = Position with { Y = ground };
            Velocity = Mathx.Flat(Velocity) * .4f;
            Mode = PlayerMode.Ground;
            env.Emit(new(GameEventKind.Land, Position, AirTime, Slot));
        }
    }

    private void LandImpact(IPlayerEnvironment env, float speed)
    {
        const float safe = 15.5f;
        if (speed > safe) Hurt((speed - safe) * 4.5f * Stats.FallDamageScale, Vector3.Zero, env);
    }

    private void Attach(ClimbContact contact, IPlayerEnvironment env, GameEventKind kind)
    {
        if (Gliding) CloseGlider(env);
        Contact = contact;
        Mode = PlayerMode.Attached;
        hasLastBody = false;
        AirTime = 0;
        IsStanding = env.Climb.IsStanding(contact);
        env.Emit(new(kind, env.Climb.WorldPosition(contact), env.Climb[contact.Box].Owner, Slot));
    }

    private void Detach(Vector3 velocity, IPlayerEnvironment env, GameEventKind kind)
    {
        Mode = PlayerMode.Air;
        Velocity = velocity;
        AirTime = 0;
        Charging = false;
        Up = Vector3.UnitY;
        env.Emit(new(kind, Position, 0, Slot));
    }

    private void UpdateAttached(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        var climb = env.Climb;
        ref readonly var box = ref climb[Contact.Box];
        if (!box.Active)
        {
            Detach(Velocity, env, GameEventKind.Release);
            return;
        }
        var normal = climb.WorldNormal(Contact);
        var surfacePoint = climb.WorldPosition(Contact);
        var body = surfacePoint + normal * ClimbWorld.SurfaceOffset;
        var surfaceVelocity = hasLastBody ? (body - lastBodyWorld) / MathF.Max(dt, 1e-4f) : Vector3.Zero;
        if (surfaceVelocity.LengthSquared() > 900) surfaceVelocity = Vector3.Zero; // teleport guard
        var standing = normal.Y > .7f;
        IsStanding = standing;
        var shake = env.ShakeOf(box.Owner);
        var (camForward, camRight) = CameraBasis(input);

        // ---- leaving the surface ----
        if (!standing && !input.Grip)
        {
            Detach(surfaceVelocity + normal * 2.2f, env, GameEventKind.Release);
            return;
        }
        if (standing && !input.Grip && (shake > .55f || env.GustStrength > .6f))
        {
            var fling = normal * 5 + Mathx.Flat(normal + new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw))) * 6 + env.GustDirection * env.GustStrength * 8;
            Detach(surfaceVelocity + fling, env, GameEventKind.ThrownOff);
            return;
        }
        if (!standing && env.GustStrength > .6f && Stamina < 5)
        {
            Detach(surfaceVelocity + env.GustDirection * 8, env, GameEventKind.ThrownOff);
            return;
        }
        if (input.JumpPressed && Stamina > 4)
        {
            Vector3 leap;
            if (standing) leap = Vector3.UnitY * 6.4f + (camForward * input.Move.Y + camRight * input.Move.X) * 4.5f;
            else
            {
                var upT = SurfaceUp(normal, camForward);
                var rightT = Vector3.Cross(upT, normal);
                leap = normal * 3.6f + upT * 5.2f * (input.Move.Y >= -.2f ? 1 : -.4f) + rightT * input.Move.X * 3.5f;
                SpendStamina(10);
            }
            Position = standing ? surfacePoint : body;
            Detach(surfaceVelocity + leap, env, GameEventKind.Jump);
            return;
        }

        // ---- moving across the surface ----
        Vector3 delta;
        float speed;
        if (standing)
        {
            var wish = camForward * input.Move.Y + camRight * input.Move.X;
            speed = Stats.RunSpeed * .72f * (input.Grip ? .55f : 1);
            delta = wish * speed * dt;
            delta -= normal * Vector3.Dot(delta, normal);
            if (wish.LengthSquared() > .01f) Yaw = Mathx.TurnToward(Yaw, Mathx.YawOf(wish), 9 * dt);
            Up = normal;
            Facing = Mathx.Forward(Yaw);
        }
        else
        {
            var upT = SurfaceUp(normal, camForward);
            var rightT = Vector3.Cross(upT, normal);
            speed = Stamina > 0 ? Stats.ClimbSpeed : 0;
            if (shake > .4f) speed *= .25f;
            if (Charging) speed *= .3f;
            delta = (rightT * input.Move.X + upT * input.Move.Y) * speed * dt;
            Up = upT;
            Facing = -normal;
        }
        IsMoving = delta.LengthSquared() > 1e-6f;
        var result = climb.Move(ref Contact, delta, input.Grip);
        if (result == ClimbWorld.MoveResult.SteppedOff)
        {
            Position = surfacePoint;
            Detach(surfaceVelocity + delta / MathF.Max(dt, 1e-4f), env, GameEventKind.Release);
            return;
        }

        // ---- grip stamina ----
        normal = climb.WorldNormal(Contact);
        standing = normal.Y > .7f;
        IsStanding = standing;
        if (standing)
        {
            var drain = input.Grip && (shake > .3f || env.GustStrength > .3f) ? 6 + 14 * MathF.Max(shake, env.GustStrength) : -12 * env.StaminaRegenScale;
            Stamina = Math.Clamp(Stamina - drain * Stats.GripDrainScale * dt, 0, Stats.MaxStamina);
        }
        else
        {
            var hanging = normal.Y < -.5f ? 1.8f : 1f;
            var rate = (IsMoving ? 7.5f : 4.8f) * hanging * env.GripDrainHazard;
            rate += shake * 20f + env.GustStrength * 12f;
            Stamina = MathF.Max(0, Stamina - rate * Stats.GripDrainScale * dt);
            if (Stamina <= 0 && !(Stats.SecondWind && !secondWindUsed))
            {
                Position = climb.BodyPosition(Contact) - Up * (BodyHeight * .5f);
                Detach(surfaceVelocity + normal * 2, env, GameEventKind.GripLost);
                return;
            }
        }

        surfacePoint = climb.WorldPosition(Contact);
        body = surfacePoint + normal * ClimbWorld.SurfaceOffset;
        lastBodyWorld = body;
        hasLastBody = true;
        Velocity = surfaceVelocity;
        Position = standing ? surfacePoint : body - Up * (BodyHeight * .5f);
        if (IsMoving)
        {
            footstepDistance += delta.Length();
            if (footstepDistance > (standing ? 1.6f : .9f))
            {
                footstepDistance = 0;
                env.Emit(new(standing ? GameEventKind.Footstep : GameEventKind.ClimbStep, Position, .5f, Slot));
            }
        }
    }

    // ---------------------------------------------------------------- glider, vehicles, breath

    private void CloseGlider(IPlayerEnvironment env)
    {
        if (!Gliding) return;
        Gliding = false;
        env.Emit(new(GameEventKind.GliderClose, Position, 0, Slot));
    }

    private bool TryMount(IPlayerEnvironment env)
    {
        var best = -1;
        var bestDistance = Vehicle.MountRange;
        for (var i = 0; i < env.VehicleCount; i++)
        {
            ref readonly var v = ref env.Vehicles[i];
            if (v.Rider >= 0 || MathF.Abs(v.Position.Y - Position.Y) > 2.5f) continue;
            var d = Vector3.Distance(Mathx.Flat(v.Position), Mathx.Flat(Position));
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        if (best < 0) return false;
        ref var vehicle = ref env.Vehicles[best];
        vehicle.Rider = Slot;
        vehicle.Speed = 0;
        Riding = best;
        Mode = PlayerMode.Ground;
        Position = vehicle.Position;
        Yaw = vehicle.Yaw;
        Velocity = Vector3.Zero;
        Charging = false;
        env.Emit(new(GameEventKind.Mount, Position, (float)vehicle.Kind, Slot));
        return true;
    }

    /// <summary>Step off (or get thrown off). <paramref name="leap"/> is added to the vehicle's momentum.</summary>
    private void Dismount(IPlayerEnvironment env, Vector3 leap)
    {
        if (!IsRiding) return;
        ref var vehicle = ref env.Vehicles[Riding];
        var momentum = Mathx.Forward(vehicle.Yaw) * vehicle.Speed;
        vehicle.Rider = -1;
        vehicle.Speed = 0;
        Riding = -1;
        Velocity = momentum * .8f + leap;
        if (leap.Y > 0)
        {
            Mode = PlayerMode.Air;
            AirTime = 0;
        }
        else Position += Vector3.Cross(Vector3.UnitY, Mathx.Forward(Yaw)) * 1.2f; // step off to the side
        env.Emit(new(GameEventKind.Dismount, Position, (float)vehicle.Kind, Slot));
    }

    private void UpdateRide(in PlayerInput input, IPlayerEnvironment env, float dt)
    {
        ref var vehicle = ref env.Vehicles[Riding];
        if (input.InteractPressed) { Dismount(env, Vector3.Zero); return; }
        if (input.JumpPressed) { Dismount(env, Vector3.UnitY * 7.5f); return; }

        var region = env.Region;
        var here = env.WaterLevel - region.Height(vehicle.Position.X, vehicle.Position.Z);
        if (vehicle.Kind == VehicleKind.Trail && here > 1.25f) { Dismount(env, Vector3.Zero); return; } // swept off by a flood
        var (forward, right) = CameraBasis(input);
        var wish = forward * input.Move.Y + right * input.Move.X;
        var amount = MathF.Min(1, wish.Length());
        var max = Vehicle.MaxSpeed(vehicle.Kind) * (input.Sprint ? 1.2f : 1);
        if (amount > .1f)
        {
            // Steer like a bike: turning needs speed, and tightens as you slow down.
            var turn = (1.4f + 1.2f * MathF.Min(1, MathF.Abs(vehicle.Speed) / 6)) * dt;
            vehicle.Yaw = Mathx.TurnToward(vehicle.Yaw, Mathx.YawOf(wish), turn);
            var alignment = MathF.Max(0, Vector3.Dot(Mathx.Forward(vehicle.Yaw), wish / MathF.Max(.001f, wish.Length())));
            vehicle.Speed = Mathx.Damp(vehicle.Speed, max * amount * (.35f + .65f * alignment), 1.8f, dt);
        }
        else vehicle.Speed = Mathx.Damp(vehicle.Speed, 0, 1.4f, dt);

        var heading = Mathx.Forward(vehicle.Yaw);
        var next = vehicle.Position + heading * vehicle.Speed * dt;
        var r = MathF.Sqrt(next.X * next.X + next.Z * next.Z);
        var limit = region.ArenaRadius * 1.05f;
        if (r > limit) { next = new Vector3(next.X / r * limit, next.Y, next.Z / r * limit); vehicle.Speed *= .5f; }

        var level = env.WaterLevel;
        var ground = region.Height(next.X, next.Z);
        var depth = float.IsNaN(level) ? -1 : level - ground;
        var blocked = vehicle.Kind == VehicleKind.Trail ? depth > .9f : depth < .45f;
        if (blocked)
        {
            // The trail bike stops at the shoreline; the jet ski runs aground.
            vehicle.Speed = 0;
            next = vehicle.Position;
        }
        else next.Y = vehicle.Kind == VehicleKind.JetSki ? level : ground;

        // Bounce off ruins, trees and guardian legs instead of driving through them.
        var center = next + Vector3.UnitY * .9f;
        if (env.Climb.FindContaining(center, Radius + .6f) >= 0)
        {
            next = vehicle.Position;
            if (MathF.Abs(vehicle.Speed) > 4) env.Emit(new(GameEventKind.Land, next, MathF.Abs(vehicle.Speed), Slot));
            vehicle.Speed = -vehicle.Speed * .3f;
        }
        if (env.GustStrength > 0) next += env.GustDirection * env.GustStrength * 1.5f * dt;

        vehicle.Position = next;
        VehicleState = vehicle;
        Position = next;
        Velocity = heading * vehicle.Speed;
        Yaw = vehicle.Yaw;
        Up = Vector3.UnitY;
        Facing = heading;
        Mode = PlayerMode.Ground;
        IsStanding = true;
        IsMoving = MathF.Abs(vehicle.Speed) > .5f;
        Stamina = MathF.Min(Stats.MaxStamina, Stamina + 30 * env.StaminaRegenScale * dt);
    }

    private void UpdateBreath(IPlayerEnvironment env, float dt)
    {
        var level = env.WaterLevel;
        var head = Mode == PlayerMode.Attached && !IsStanding ? BodyCenter + Up * .75f : Position + Vector3.UnitY * 1.6f;
        var under = !float.IsNaN(level) && Mode != PlayerMode.Swim && !IsRiding && head.Y < level - .05f;
        if (under)
        {
            Breath = MathF.Max(0, Breath - dt / 14f);
            if (Breath < .35f && !breathWarned)
            {
                breathWarned = true;
                env.Emit(new(GameEventKind.BreathLow, Position, Breath, Slot));
            }
            if (Breath <= 0)
            {
                Health = MathF.Max(0, Health - 10 * dt);
                if (Health <= 0) Hurt(1, Vector3.Zero, env);
            }
        }
        else
        {
            Breath = MathF.Min(1, Breath + dt * .5f);
            if (Breath > .6f) breathWarned = false;
        }
    }

    /// <summary>"Up" along a wall; on near-horizontal surfaces (belly, ceilings) it follows the camera.</summary>
    private static Vector3 SurfaceUp(Vector3 normal, Vector3 cameraForward)
    {
        var up = Vector3.UnitY - normal * normal.Y;
        if (up.LengthSquared() > .04f) return Vector3.Normalize(up);
        var f = cameraForward - normal * Vector3.Dot(cameraForward, normal);
        return Mathx.SafeNormalize(f, Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitX)));
    }

    private void PushOutOfBoxes(IPlayerEnvironment env, out bool pushed)
    {
        pushed = false;
        var climb = env.Climb;
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var center = BodyCenter;
            var hit = climb.FindContaining(center, Radius + .3f);
            if (hit < 0) return;
            if (!climb.Project(hit, center, false, out var contact, out _)) return;
            var normal = climb.WorldNormal(contact);
            if (normal.Y > .7f)
            {
                // Walked onto a low ledge or a lowered colossus limb: step up.
                Attach(contact, env, GameEventKind.Land);
                return;
            }
            var surface = climb.WorldPosition(contact) + normal * (Radius + .32f);
            var push = Mathx.Flat(surface - center);
            if (push.LengthSquared() < 1e-6f) push = Mathx.Flat(normal) * .05f;
            Position += push;
            pushed = true;
        }
    }
}
