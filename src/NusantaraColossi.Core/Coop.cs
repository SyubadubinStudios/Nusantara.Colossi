using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace NusantaraColossi.Core;

/// <summary>
/// Expedition co-op wire format (2–4 hunters). The host owns the colossus and resolves stabs, drone
/// abilities and attack hits; every peer simulates its own climber locally against the replicated pose,
/// which <see cref="ColossusRig"/> turns into identical part matrices on every machine.
/// </summary>
/// <remarks>
/// Protocol 2 (Milestone 4) adds: a client token and preferred slot in Hello (slot reclaim on reconnect),
/// the guardian's AI clocks in Snapshot (so a client can take over as host), the Roster message (peer
/// addresses for host migration) and the LAN discovery beacon.
/// Protocol 3 adds the glider flag and the ridden vehicle (slot-indexed) to every player state.
/// </remarks>
public static class Coop
{
    public const int MaxPlayers = 4;
    public const byte ProtocolVersion = 3;
    /// <summary>UDP port hosts broadcast their <see cref="Beacon"/> on. The game itself uses the host's chosen port.</summary>
    public const int DiscoveryPort = 27046;
    public const int DefaultGamePort = 27045;
    /// <summary>"No preference" for <see cref="Hello"/>'s preferred slot.</summary>
    public const int AnySlot = 255;

    public enum Message : byte { Hello = 1, Welcome = 2, PlayerState = 3, Snapshot = 4, Stab = 5, Ability = 6, Hurt = 7, Event = 8, Full = 9, Roster = 10 }

    /// <summary>Events worth replicating from host to clients (audio/VFX/HUD cues for colossus actions).</summary>
    public static bool IsReplicated(GameEventKind kind) => kind is
        GameEventKind.CoreHit or GameEventKind.CoreResonance or GameEventKind.CoreDestroyed or GameEventKind.StabSealed or GameEventKind.StabDeflected
        or GameEventKind.Awaken or GameEventKind.ShakeWarn or GameEventKind.ShakeStart or GameEventKind.StompWindup or GameEventKind.StompImpact
        or GameEventKind.SweepStart or GameEventKind.RoarStart or GameEventKind.RoarWave or GameEventKind.ChargeStart
        or GameEventKind.Stunned or GameEventKind.Staggered or GameEventKind.Rage or GameEventKind.Collapse or GameEventKind.Defeated
        or GameEventKind.Victory or GameEventKind.GustWarn or GameEventKind.Gust;

    public readonly record struct HelloInfo(byte Version, string Name, long Token, int PreferredSlot);

    public readonly record struct Welcome(int Slot, string ColossusId, bool Legend, DroneAbilities Abilities, int Seed);

    public readonly record struct PlayerSnapshot(int Slot, PlayerMode Mode, Vector3 Position, float Yaw, Vector3 Up, Vector3 Facing, float Health, bool Charging, bool Moving, bool Standing, string Name,
        bool Gliding = false, int Riding = -1, Vector3 VehiclePosition = default, float VehicleYaw = 0, float VehicleSpeed = 0);

    /// <summary>One hunter as the host sees it: clients keep this so they can find each other if the host drops.</summary>
    public readonly record struct RosterEntry(int Slot, string Name, string Address);

    // ---------------------------------------------------------------- writing

    public static byte[] Hello(string name, long token = 0, int preferredSlot = AnySlot)
    {
        var w = new PacketWriter(Message.Hello);
        w.Byte(ProtocolVersion);
        w.String(name);
        w.Long(token);
        w.Byte((byte)Math.Clamp(preferredSlot, 0, AnySlot));
        return w.ToArray();
    }

    public static byte[] WelcomePacket(Welcome welcome)
    {
        var w = new PacketWriter(Message.Welcome);
        w.Byte((byte)welcome.Slot);
        w.String(welcome.ColossusId);
        w.Bool(welcome.Legend);
        w.Byte((byte)welcome.Abilities);
        w.Int(welcome.Seed);
        return w.ToArray();
    }

    public static byte[] Full() => new PacketWriter(Message.Full).ToArray();

    public static byte[] PlayerStatePacket(Player p)
    {
        var w = new PacketWriter(Message.PlayerState);
        WritePlayer(ref w, p);
        return w.ToArray();
    }

    private static void WritePlayer(ref PacketWriter w, Player p)
    {
        w.Byte((byte)p.Slot);
        w.Byte((byte)p.Mode);
        w.Vector(p.Position);
        w.Float(p.Yaw);
        w.Vector(p.Up);
        w.Vector(p.Facing);
        w.Float(p.Health);
        w.Byte((byte)((p.Charging ? 1 : 0) | (p.IsMoving ? 2 : 0) | (p.IsStanding ? 4 : 0) | (p.Gliding ? 8 : 0)));
        w.String(p.Name);
        w.Byte((byte)(p.Riding + 1));
        if (p.Riding >= 0)
        {
            var v = p.VehicleState;
            w.Vector(v.Position);
            w.Float(v.Yaw);
            w.Float(v.Speed);
        }
    }

    public static byte[] Snapshot(Encounter e)
    {
        var w = new PacketWriter(Message.Snapshot);
        var c = e.Colossus;
        w.Float(e.Elapsed);
        w.Byte((byte)e.Phase);
        w.Vector(c.Pose.Position);
        w.Float(c.Pose.Yaw);
        w.Float(c.Pose.GaitPhase);
        w.Float(c.Pose.GaitWeight);
        w.Float(c.Pose.Time);
        w.Byte((byte)c.Pose.Attack);
        w.Float(c.Pose.AttackProgress);
        w.Float(c.Pose.Kneel);
        w.Float(c.Pose.Shake);
        w.Float(c.Pose.Collapse);
        w.Float(c.Pose.HeadYaw);
        w.Byte((byte)c.State);
        w.Byte((byte)c.Rage);
        // v2: the AI clocks a client needs to carry on as host.
        w.Float(c.StateTime);
        w.Float(c.Annoyance);
        w.Float(c.DecoyTime);
        w.Vector(c.DecoyPoint);
        w.Vector(c.Target);
        w.Byte((byte)c.Cores.Length);
        foreach (var core in c.Cores)
        {
            w.Float(core.Health);
            w.Float(core.SealOpen);
            w.Bool(core.Revealed);
        }
        w.Float(e.GustStrength);
        w.Vector(e.GustDirection);
        w.Bool(e.GustWarning);
        w.Byte((byte)e.Players.Count);
        foreach (var p in e.Players) WritePlayer(ref w, p);
        return w.ToArray();
    }

    public static byte[] StabPacket(StabRequest s)
    {
        var w = new PacketWriter(Message.Stab);
        w.Byte((byte)s.Slot);
        w.Vector(s.Body);
        w.Bool(s.OnColossus);
        w.Float(s.Charge);
        w.Float(s.PulseTime);
        return w.ToArray();
    }

    public static byte[] AbilityPacket(AbilityRequest a)
    {
        var w = new PacketWriter(Message.Ability);
        w.Byte((byte)a.Slot);
        w.Byte((byte)a.Ability);
        w.Vector(a.Position);
        w.Vector(a.Aim);
        return w.ToArray();
    }

    public static byte[] HurtPacket(int slot, float amount, Vector3 knockback)
    {
        var w = new PacketWriter(Message.Hurt);
        w.Byte((byte)slot);
        w.Float(amount);
        w.Vector(knockback);
        return w.ToArray();
    }

    public static byte[] EventPacket(GameEvent e)
    {
        var w = new PacketWriter(Message.Event);
        w.Byte((byte)e.Kind);
        w.Vector(e.Position);
        w.Float(e.Value);
        w.Byte((byte)e.Slot);
        return w.ToArray();
    }

    /// <summary>Host → clients: every hunter's slot and address as the host sees it (empty for the host itself).</summary>
    public static byte[] RosterPacket(IReadOnlyList<RosterEntry> entries)
    {
        var w = new PacketWriter(Message.Roster);
        var count = Math.Min(entries.Count, MaxPlayers);
        w.Byte((byte)count);
        for (var i = 0; i < count; i++)
        {
            w.Byte((byte)entries[i].Slot);
            w.String(entries[i].Name);
            w.String(entries[i].Address, 64);
        }
        return w.ToArray();
    }

    // ---------------------------------------------------------------- reading

    public static Message Kind(ReadOnlySpan<byte> packet) => packet.Length == 0 ? 0 : (Message)packet[0];

    public static HelloInfo ReadHello(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        var version = r.Byte();
        var name = r.String();
        // Older clients stop here; the version check rejects them before the rest matters.
        if (version < 2) return new(version, name, 0, AnySlot);
        return new(version, name, r.Long(), r.Byte());
    }

    public static Welcome ReadWelcome(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return new(r.Byte(), r.String(), r.Bool(), (DroneAbilities)r.Byte(), r.Int());
    }

    public static PlayerSnapshot ReadPlayerState(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return ReadPlayer(ref r);
    }

    private static PlayerSnapshot ReadPlayer(ref PacketReader r)
    {
        var slot = r.Byte();
        var mode = (PlayerMode)r.Byte();
        var position = r.Vector();
        var yaw = r.Float();
        var up = r.Vector();
        var facing = r.Vector();
        var health = r.Float();
        var flags = r.Byte();
        var name = r.String();
        var riding = r.Byte() - 1;
        Vector3 vp = default; float vy = 0, vs = 0;
        if (riding >= 0) { vp = r.Vector(); vy = r.Float(); vs = r.Float(); }
        return new(slot, mode, position, yaw, up, facing, health, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, name,
            (flags & 8) != 0, riding, vp, vy, vs);
    }

    /// <summary>Client side: overwrite colossus, hazards and remote players from a host snapshot.</summary>
    public static void ApplySnapshot(Encounter e, ReadOnlySpan<byte> packet, int localSlot)
    {
        var r = new PacketReader(packet);
        e.SetElapsed(r.Float(), (EncounterPhase)r.Byte());
        var c = e.Colossus;
        c.Pose.Position = r.Vector();
        c.Pose.Yaw = r.Float();
        c.Pose.GaitPhase = r.Float();
        c.Pose.GaitWeight = r.Float();
        c.Pose.Time = r.Float();
        c.Pose.Attack = (AttackKind)r.Byte();
        c.Pose.AttackProgress = r.Float();
        c.Pose.Kneel = r.Float();
        c.Pose.Shake = r.Float();
        c.Pose.Collapse = r.Float();
        c.Pose.HeadYaw = r.Float();
        c.State = (ColossusState)r.Byte();
        c.Rage = r.Byte();
        c.StateTime = r.Float();
        c.Annoyance = r.Float();
        c.DecoyTime = r.Float();
        c.DecoyPoint = r.Vector();
        c.Target = r.Vector();
        var cores = r.Byte();
        for (var i = 0; i < cores; i++)
        {
            var health = r.Float();
            var seal = r.Float();
            var revealed = r.Bool();
            if (i >= c.Cores.Length) continue;
            c.Cores[i].Health = health;
            c.Cores[i].SealOpen = seal;
            c.Cores[i].Revealed = revealed;
        }
        e.SetWeather(r.Float(), r.Vector(), r.Bool());
        var count = r.Byte();
        for (var i = 0; i < count; i++)
        {
            var s = ReadPlayer(ref r);
            if (s.Slot == localSlot) continue;
            ApplyRemotePlayer(e, s);
        }
        c.Evaluate();
        e.SyncColossusBoxes();
    }

    public static void ApplyRemotePlayer(Encounter e, PlayerSnapshot s)
    {
        if (s.Slot >= MaxPlayers) return; // drones and HUD are sized for four hunters
        while (e.Players.Count <= s.Slot) e.Players.Add(new Player(e.Players.Count, "?") { IsRemote = true });
        var p = e.Players[s.Slot];
        p.IsRemote = true;
        p.Name = s.Name;
        p.Mode = s.Mode;
        p.Position = s.Position;
        p.Yaw = s.Yaw;
        p.Up = s.Up;
        p.Facing = s.Facing;
        p.Health = s.Health;
        p.Charging = s.Charging;
        p.IsMoving = s.Moving;
        p.IsStanding = s.Standing;
        p.SetRemoteTravel(s.Riding, s.Gliding);
        e.EnsureVehicle(s.Slot);
        e.ApplyRemoteVehicle(s.Slot, s.Riding, s.VehiclePosition, s.VehicleYaw, s.VehicleSpeed);
    }

    public static StabRequest ReadStab(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return new(r.Byte(), r.Vector(), r.Bool(), r.Float(), r.Float());
    }

    public static AbilityRequest ReadAbility(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return new(r.Byte(), (DroneAbilities)r.Byte(), r.Vector(), r.Vector());
    }

    public static (int Slot, float Amount, Vector3 Knockback) ReadHurt(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return (r.Byte(), r.Float(), r.Vector());
    }

    public static GameEvent ReadEvent(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        return new((GameEventKind)r.Byte(), r.Vector(), r.Float(), r.Byte());
    }

    public static RosterEntry[] ReadRoster(ReadOnlySpan<byte> packet)
    {
        var r = new PacketReader(packet);
        var count = Math.Min((int)r.Byte(), MaxPlayers);
        var entries = new RosterEntry[count];
        for (var i = 0; i < count; i++) entries[i] = new(r.Byte(), r.String(), r.String());
        return entries;
    }

    // ---------------------------------------------------------------- host migration

    /// <summary>
    /// Host migration: a client whose host vanished takes over the guardian. The encounter keeps the state
    /// of the last applied snapshot (pose, cores, seals, rage, AI clocks, elapsed time) and from now on runs
    /// <see cref="Colossus.Think"/> itself. The former host's hunter is marked disconnected.
    /// </summary>
    public static void PromoteToAuthority(Encounter e, int formerHostSlot)
    {
        if (!e.Options.IsAuthority) e.Options = e.Options with { IsAuthority = true };
        if (formerHostSlot >= 0 && formerHostSlot < e.Players.Count)
        {
            var host = e.Players[formerHostSlot];
            host.IsRemote = true;
            host.Disconnect();
        }
        e.Colossus.Evaluate();
        e.SyncColossusBoxes();
    }

    /// <summary>Which hunter should take over: the lowest slot that is still around, excluding the lost host.</summary>
    public static int MigrationCandidate(IReadOnlyList<RosterEntry> roster, int lostHostSlot, int skipBelow = 0)
    {
        var best = -1;
        foreach (var entry in roster)
            if (entry.Slot != lostHostSlot && entry.Slot >= skipBelow && (best < 0 || entry.Slot < best)) best = entry.Slot;
        return best;
    }

    // ---------------------------------------------------------------- LAN discovery

    /// <summary>What a host broadcasts on <see cref="DiscoveryPort"/> about once a second.</summary>
    public readonly record struct Beacon(int SessionId, string HostName, string ColossusId, bool Legend, int Players, int MaxPlayers, int GamePort, int ElapsedSeconds);

    private static ReadOnlySpan<byte> BeaconMagic => "NCLB"u8;
    private const int BeaconFixedBytes = 4 + 1 + 4 + 1 + 1 + 2 + 1 + 2; // magic, version, session, players, max, port, legend, elapsed

    public static byte[] EncodeBeacon(Beacon b)
    {
        var host = Clip(b.HostName, 24);
        var id = Clip(b.ColossusId, 32);
        var buffer = new byte[BeaconFixedBytes + 2 + host.Length + id.Length];
        var span = buffer.AsSpan();
        BeaconMagic.CopyTo(span);
        span[4] = ProtocolVersion;
        BinaryPrimitives.WriteInt32LittleEndian(span[5..], b.SessionId);
        span[9] = (byte)Math.Clamp(b.Players, 0, MaxPlayers);
        span[10] = (byte)Math.Clamp(b.MaxPlayers, 1, MaxPlayers);
        BinaryPrimitives.WriteUInt16LittleEndian(span[11..], (ushort)Math.Clamp(b.GamePort, 1, 65535));
        span[13] = b.Legend ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], (ushort)Math.Clamp(b.ElapsedSeconds, 0, 65535));
        var o = BeaconFixedBytes;
        span[o++] = (byte)host.Length;
        host.CopyTo(span[o..]);
        o += host.Length;
        span[o++] = (byte)id.Length;
        id.CopyTo(span[o..]);
        return buffer;
    }

    /// <summary>Parses a beacon; false for anything that is not a well-formed beacon of this protocol version.</summary>
    public static bool TryDecodeBeacon(ReadOnlySpan<byte> data, out Beacon beacon)
    {
        beacon = default;
        if (data.Length < BeaconFixedBytes + 2 || data.Length > 512) return false;
        if (!data[..4].SequenceEqual(BeaconMagic) || data[4] != ProtocolVersion) return false;
        var session = BinaryPrimitives.ReadInt32LittleEndian(data[5..]);
        int players = data[9], max = data[10];
        int port = BinaryPrimitives.ReadUInt16LittleEndian(data[11..]);
        if (data[13] > 1) return false;
        var legend = data[13] == 1;
        int elapsed = BinaryPrimitives.ReadUInt16LittleEndian(data[14..]);
        if (max is < 1 or > MaxPlayers || players > max || port < 1) return false;
        var o = BeaconFixedBytes;
        if (!TryString(data, ref o, 24, out var host) || !TryString(data, ref o, 32, out var id) || o != data.Length) return false;
        if (id.Length == 0 || !Array.Exists(Colossi.All, c => c.Id == id)) return false;
        beacon = new(session, host, id, legend, players, max, port, elapsed);
        return true;
    }

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static bool TryString(ReadOnlySpan<byte> data, ref int offset, int maxBytes, out string value)
    {
        value = "";
        if (offset >= data.Length) return false;
        int length = data[offset++];
        if (length > maxBytes * 4 || offset + length > data.Length) return false;
        try { value = StrictUtf8.GetString(data.Slice(offset, length)); }
        catch (DecoderFallbackException) { return false; }
        offset += length;
        return true;
    }

    private static byte[] Clip(string text, int chars) => Encoding.UTF8.GetBytes(text.Length > chars ? text[..chars] : text);

    // ---------------------------------------------------------------- primitives

    private ref struct PacketWriter
    {
        private byte[] buffer;
        private int length;

        public PacketWriter(Message kind)
        {
            buffer = new byte[64];
            length = 0;
            Byte((byte)kind);
        }

        private Span<byte> Take(int count)
        {
            if (length + count > buffer.Length) Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + count));
            var span = buffer.AsSpan(length, count);
            length += count;
            return span;
        }

        public void Byte(byte v) => Take(1)[0] = v;
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
        public void Int(int v) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), v);
        public void Long(long v) => BinaryPrimitives.WriteInt64LittleEndian(Take(8), v);
        public void Float(float v) => BinaryPrimitives.WriteSingleLittleEndian(Take(4), v);
        public void Vector(Vector3 v) { Float(v.X); Float(v.Y); Float(v.Z); }

        public void String(string v, int maxChars = 24)
        {
            var bytes = Encoding.UTF8.GetBytes(v.Length > maxChars ? v[..maxChars] : v);
            if (bytes.Length > 255) bytes = bytes[..255];
            Byte((byte)bytes.Length);
            bytes.CopyTo(Take(bytes.Length));
        }

        public readonly byte[] ToArray() => buffer.AsSpan(0, length).ToArray();
    }

    private ref struct PacketReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> data = data;
        private int offset = 1; // skip message kind

        private ReadOnlySpan<byte> Take(int count)
        {
            if (offset + count > data.Length) throw new InvalidDataException("Truncated co-op packet.");
            var span = data.Slice(offset, count);
            offset += count;
            return span;
        }

        public byte Byte() => Take(1)[0];
        public bool Bool() => Byte() != 0;
        public int Int() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public long Long() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
        public float Float() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));
        public Vector3 Vector() => new(Float(), Float(), Float());
        public string String() => Encoding.UTF8.GetString(Take(Byte()));
    }
}

public enum SlotStatus : byte { Free, Local, Connected, Reserved }

/// <summary>
/// Host-side slot bookkeeping. A hunter who drops keeps their slot for <see cref="ReserveSeconds"/>, and
/// reclaims it by presenting the same client token in Hello. After host migration the new host reserves the
/// roster's slots without tokens; the first returning hunter who asks for such a slot gets it.
/// </summary>
public sealed class SlotTable
{
    public const float ReserveSeconds = 30;

    private readonly SlotStatus[] status = new SlotStatus[Coop.MaxPlayers];
    private readonly long[] tokens = new long[Coop.MaxPlayers];
    private readonly float[] reservedFor = new float[Coop.MaxPlayers];

    public SlotTable(int localSlot)
    {
        status[localSlot] = SlotStatus.Local;
    }

    public SlotStatus this[int slot] => status[slot];

    public long TokenOf(int slot) => tokens[slot];

    public float ReservedFor(int slot) => status[slot] == SlotStatus.Reserved ? reservedFor[slot] : 0;

    /// <summary>Hunters present: the local one plus connected peers.</summary>
    public int Occupied
    {
        get
        {
            var n = 0;
            foreach (var s in status) if (s is SlotStatus.Local or SlotStatus.Connected) n++;
            return n;
        }
    }

    /// <summary>
    /// Finds a slot for a joining hunter. Returns -1 when the expedition is full.
    /// <paramref name="reclaimed"/> is true when the hunter got back a slot they held before.
    /// </summary>
    public int Claim(long token, int preferred, out bool reclaimed)
    {
        reclaimed = false;
        if (token != 0)
            for (var i = 0; i < status.Length; i++)
                if (status[i] is SlotStatus.Connected or SlotStatus.Reserved && tokens[i] == token)
                {
                    Take(i, token);
                    reclaimed = true;
                    return i;
                }

        if (preferred >= 0 && preferred < status.Length)
        {
            // Host migration: slots reserved without a token wait for whoever held them on the old host.
            if (status[preferred] == SlotStatus.Reserved && tokens[preferred] == 0)
            {
                Take(preferred, token);
                reclaimed = true;
                return preferred;
            }
            if (status[preferred] == SlotStatus.Free)
            {
                Take(preferred, token);
                return preferred;
            }
        }

        for (var i = 0; i < status.Length; i++)
            if (status[i] == SlotStatus.Free)
            {
                Take(i, token);
                return i;
            }
        return -1;
    }

    /// <summary>A connected hunter dropped: keep the slot for <see cref="ReserveSeconds"/>.</summary>
    public void Release(int slot)
    {
        if (status[slot] != SlotStatus.Connected) return;
        status[slot] = SlotStatus.Reserved;
        reservedFor[slot] = ReserveSeconds;
    }

    /// <summary>Reserve a slot up front (new host after migration). Token 0 means "whoever asks for this slot".</summary>
    public void Reserve(int slot, long token = 0)
    {
        if (status[slot] == SlotStatus.Local) return;
        status[slot] = SlotStatus.Reserved;
        tokens[slot] = token;
        reservedFor[slot] = ReserveSeconds;
    }

    /// <summary>Counts reservations down. Returns a bitmask of slots whose reservation just expired.</summary>
    public int Tick(float dt)
    {
        var expired = 0;
        for (var i = 0; i < status.Length; i++)
        {
            if (status[i] != SlotStatus.Reserved) continue;
            reservedFor[i] -= dt;
            if (reservedFor[i] > 0) continue;
            status[i] = SlotStatus.Free;
            tokens[i] = 0;
            expired |= 1 << i;
        }
        return expired;
    }

    private void Take(int slot, long token)
    {
        status[slot] = SlotStatus.Connected;
        tokens[slot] = token;
        reservedFor[slot] = 0;
    }
}
