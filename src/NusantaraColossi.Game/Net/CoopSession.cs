using System.Diagnostics;
using System.Net;
using NusantaraColossi.Core;
using ThreeNet.Networking;

namespace NusantaraColossi.Game.Net;

/// <summary>Health of a co-op link, as the HUD shows it.</summary>
public enum CoopLink { Online, Reconnecting, Migrating, Lost }

/// <summary>
/// 2–4 player co-op over ThreeNet's UDP <see cref="NetworkSession"/>.
/// The host owns the colossus and broadcasts snapshots at 20 Hz; every client simulates its own
/// climber locally (no input latency on the part of the game that is all about feel) and sends
/// its state, stabs and drone abilities to the host. Damage the colossus deals to a remote climber
/// is sent back to that climber's owner. All packet formats live in <see cref="Coop"/>.
/// </summary>
/// <remarks>
/// Resilience (Milestone 4):
/// <list type="bullet">
/// <item>LAN discovery: the host broadcasts a <see cref="Coop.Beacon"/> (see <see cref="LanBeaconBroadcaster"/>).</item>
/// <item>Reconnect: a client that loses the host retries for <see cref="ReconnectWindow"/> seconds and reclaims its
/// slot with the token it sent in Hello. The host holds a dropped hunter's slot for <see cref="SlotTable.ReserveSeconds"/>.</item>
/// <item>Host migration: if the host stays gone, the surviving hunter with the lowest slot (from the host's last
/// <see cref="Coop.Message.Roster"/>) promotes its encounter to authority and hosts on the same port; the others
/// connect to its address. This needs clients to reach each other directly (same LAN, no NAT in between).</item>
/// </list>
/// </remarks>
public sealed class CoopSession : IDisposable
{
    private const byte StateChannel = 0, ReliableChannel = 1;
    private const int HostPeerId = 1;
    private const float SendInterval = 1 / 20f;
    private const float RosterInterval = 5f;
    public const float ReconnectWindow = 10f;
    private const float MigrationWindow = 8f;
    private const float NoticeSeconds = 5f;

    private NetworkSession? session;
    private readonly Dictionary<int, int> peerSlots = [];
    private readonly string localName;
    private readonly int port;
    private readonly long token;
    private readonly int sessionId;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private SlotTable? slots;
    private LanBeaconBroadcaster? beacon;
    private Coop.RosterEntry[] roster = [];
    private string hostAddress;
    private int hostSlot;
    private int migrationTarget = -1;
    private bool sessionDead;
    private string? closeReason;
    private double lastPoll;
    private float sendTimer, rosterTimer, linkTimer;
    private string? notice;
    private float noticeTimer;

    public bool IsHost { get; private set; }
    public bool IsConnected => IsHost || session is { IsConnected: true };
    public int LocalSlot { get; private set; }
    public int PeerCount => session?.Peers.Count ?? 0;
    public int Port => port;
    public Encounter? Encounter { get; set; }
    public Coop.Welcome? Welcome { get; private set; }
    public CoopLink Link { get; private set; } = CoopLink.Online;
    public IReadOnlyList<Coop.RosterEntry> Roster => roster;

    /// <summary>Simulation events the host replicated to us; drained by the hunt screen each frame.</summary>
    public List<GameEvent> ReceivedEvents { get; } = [];

    /// <summary>Client only: the host accepted us the first time; build the encounter from this.</summary>
    public event Action<Coop.Welcome>? Accepted;
    public event Action<string>? Status;
    /// <summary>The session is over for good (initial connect failed, or reconnect and migration both failed).</summary>
    public event Action<string>? Closed;

    /// <summary>Host only: what to tell joining players.</summary>
    public Func<int, Coop.Welcome>? WelcomeFor { get; set; }

    /// <summary>One line for the hunt HUD (link trouble, or a recent join/leave notice); null when there is nothing to say.</summary>
    public string? HudStatus => Link switch
    {
        CoopLink.Reconnecting => Loc.F("coop.reconnecting", Math.Max(0, (int)MathF.Ceiling(ReconnectWindow - linkTimer))),
        CoopLink.Migrating => Loc.F("coop.migrating", NameOf(migrationTarget)),
        CoopLink.Lost => Loc.F("coop.lost", closeReason ?? ""),
        _ => noticeTimer > 0 ? notice : null,
    };

    /// <summary>True while the link is in trouble (HUD shows it in ember).</summary>
    public bool HudAlert => Link != CoopLink.Online;

    private CoopSession(string name, int port, string hostAddress)
    {
        localName = name;
        this.port = port;
        this.hostAddress = hostAddress;
        token = Random.Shared.NextInt64(1, long.MaxValue);
        sessionId = Random.Shared.Next();
    }

    private static NetworkOptions Options(TimeSpan connectTimeout) => new()
    {
        TickRate = 20,
        ConnectTimeout = connectTimeout,
        Timeout = TimeSpan.FromSeconds(5),
    };

    public static CoopSession Host(int port, string name)
    {
        var s = new CoopSession(name, port, "");
        s.StartHosting();
        return s;
    }

    public static CoopSession Join(string address, int port, string name)
    {
        var s = new CoopSession(name, port, address);
        s.Attach(NetworkSession.Connect(address, port, name, Options(TimeSpan.FromSeconds(6))));
        return s;
    }

    private void StartHosting()
    {
        var hosted = NetworkSession.Host(port, localName, Options(TimeSpan.FromSeconds(6)));
        IsHost = true;
        hostSlot = LocalSlot;
        slots = new SlotTable(LocalSlot);
        peerSlots.Clear();
        Attach(hosted);
        try { beacon ??= new LanBeaconBroadcaster(); }
        catch (System.Net.Sockets.SocketException) { beacon = null; } // discovery is optional; direct IP still works
    }

    private void Attach(NetworkSession s)
    {
        session = s;
        sessionDead = false;
        s.MessageReceived += OnMessage;
        s.Closed += OnSessionClosed;
        s.PeerDisconnected += OnPeerLeft;
        s.Connected += OnConnected;
        s.PeerConnected += OnPeerJoined;
    }

    private void Detach()
    {
        if (session is not { } s) return;
        s.MessageReceived -= OnMessage;
        s.Closed -= OnSessionClosed;
        s.PeerDisconnected -= OnPeerLeft;
        s.Connected -= OnConnected;
        s.PeerConnected -= OnPeerJoined;
        s.Dispose();
        session = null;
    }

    private void OnConnected() =>
        session?.Send(Coop.Hello(localName, token, Welcome is null ? Coop.AnySlot : LocalSlot), ReliableChannel, true, HostPeerId);

    private void OnPeerJoined(NetworkPeer peer)
    {
        // The host announces joins itself once it knows the slot; clients just hear about it
        // (ThreeNet also replays the whole peer list, ourselves included, on every (re)connect: skip that).
        if (!IsHost && Link == CoopLink.Online && Welcome is not null && peer.Id != session?.LocalPeerId) Status?.Invoke(Loc.F("coop.joined", peer.Name));
    }

    private void OnSessionClosed(string reason)
    {
        // Raised from inside NetworkSession.Update; the socket is replaced in Poll, not here.
        sessionDead = true;
        closeReason = reason;
    }

    /// <summary>Pumps the socket and the reconnect / migration state machine. Call once per frame before stepping the encounter.</summary>
    public void Poll()
    {
        var now = clock.Elapsed.TotalSeconds;
        var dt = (float)Math.Min(now - lastPoll, .25);
        lastPoll = now;

        session?.Update();
        noticeTimer -= dt;

        if (sessionDead && Link == CoopLink.Online)
        {
            Detach();
            if (Welcome is null || IsHost)
            {
                // Never got in (or a host socket failed): nothing to recover.
                Link = CoopLink.Lost;
                Closed?.Invoke(closeReason ?? "closed");
                return;
            }
            Link = CoopLink.Reconnecting;
            linkTimer = 0;
            Status?.Invoke(HudStatus ?? "");
        }

        if (Link is CoopLink.Reconnecting or CoopLink.Migrating) Recover(dt);

        if (IsHost && slots is not null)
        {
            var expired = slots.Tick(dt);
            if (expired != 0) SendRoster();
            rosterTimer -= dt;
            if (rosterTimer <= 0) SendRoster();
            if (beacon is not null && Encounter is { Phase: EncounterPhase.Fight } e && WelcomeFor is not null)
                beacon.Tick(dt, () => BeaconFor(e));
        }
    }

    private Coop.Beacon BeaconFor(Encounter e)
    {
        var w = WelcomeFor!(LocalSlot);
        return new Coop.Beacon(sessionId, localName, w.ColossusId, w.Legend, slots?.Occupied ?? 1, Coop.MaxPlayers, port, (int)e.Elapsed);
    }

    // ---------------------------------------------------------------- reconnect and host migration

    private void Recover(float dt)
    {
        linkTimer += dt;
        if (Link == CoopLink.Reconnecting)
        {
            if (linkTimer >= ReconnectWindow)
            {
                Detach();
                BeginMigration(skipBelow: 0);
                return;
            }
            Attempt(hostAddress);
            return;
        }

        // Migrating toward migrationTarget.
        if (linkTimer >= MigrationWindow)
        {
            Detach();
            BeginMigration(skipBelow: migrationTarget + 1);
            return;
        }
        Attempt(AddressOf(migrationTarget));
    }

    /// <summary>Keeps one connection attempt alive toward <paramref name="address"/>; starts a new one when the last failed.</summary>
    private void Attempt(string address)
    {
        if (session is not null && !sessionDead) return;
        Detach();
        try
        {
            Attach(NetworkSession.Connect(address, port, localName, Options(TimeSpan.FromSeconds(2.5))));
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException or ArgumentException)
        {
            sessionDead = true; // retry next frame
            closeReason = ex.Message;
        }
    }

    private void BeginMigration(int skipBelow)
    {
        var candidate = Coop.MigrationCandidate(roster, hostSlot, skipBelow);
        // If the host never sent a roster we only know ourselves.
        if (candidate < 0 && skipBelow <= LocalSlot && roster.Length == 0) candidate = LocalSlot;
        if (candidate < 0)
        {
            Fail(Loc.T("coop.noHost"));
            return;
        }
        migrationTarget = candidate;
        linkTimer = 0;
        if (candidate == LocalSlot)
        {
            Promote();
            return;
        }
        Link = CoopLink.Migrating;
        hostAddress = AddressOf(candidate);
        Status?.Invoke(HudStatus ?? "");
    }

    private void Promote()
    {
        var lostHost = hostSlot;
        try
        {
            StartHosting();
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            // Port still held (e.g. the old host is frozen, not gone) — let the next hunter try.
            IsHost = false;
            slots = null;
            Detach();
            BeginMigration(skipBelow: LocalSlot + 1);
            return;
        }
        if (Welcome is { } w) WelcomeFor = slot => w with { Slot = slot };
        foreach (var entry in roster)
            if (entry.Slot != LocalSlot && entry.Slot != lostHost && entry.Slot < Coop.MaxPlayers) slots!.Reserve(entry.Slot);
        if (Encounter is { } e)
        {
            Coop.PromoteToAuthority(e, lostHost);
            // Everyone else is disconnected until they reach us.
            foreach (var p in e.Players)
                if (p.Slot != LocalSlot && p.IsRemote) p.Disconnect();
        }
        Link = CoopLink.Online;
        roster = [new Coop.RosterEntry(LocalSlot, localName, "")];
        SendRoster();
        Notice(Loc.T("coop.promoted"));
    }

    private void Fail(string reason)
    {
        Detach();
        Link = CoopLink.Lost;
        closeReason = reason;
        Closed?.Invoke(reason);
    }

    private string AddressOf(int slot)
    {
        foreach (var entry in roster) if (entry.Slot == slot && entry.Address.Length > 0) return entry.Address;
        return hostAddress;
    }

    private string NameOf(int slot)
    {
        foreach (var entry in roster) if (entry.Slot == slot) return entry.Name;
        return "?";
    }

    private void Notice(string text)
    {
        notice = text;
        noticeTimer = NoticeSeconds;
        Status?.Invoke(text);
    }

    // ---------------------------------------------------------------- traffic

    /// <summary>Sends this tick's outgoing traffic. Call after <see cref="Encounter.Step"/>.</summary>
    public void AfterStep(float dt)
    {
        var e = Encounter;
        if (e is null || session is null || sessionDead) return;
        if (IsHost)
        {
            foreach (var ev in e.Events)
                if (Coop.IsReplicated(ev.Kind)) Broadcast(Coop.EventPacket(ev), reliable: true);
            foreach (var (slot, amount, knockback) in e.RemoteHurts)
                if (PeerOf(slot) is { } peer) session.Send(Coop.HurtPacket(slot, amount, knockback), ReliableChannel, true, peer);
        }
        else if (Link == CoopLink.Online)
        {
            foreach (var stab in e.PendingRemoteStabs) session.Send(Coop.StabPacket(stab), ReliableChannel, true, HostPeerId);
            foreach (var ability in e.PendingRemoteAbilities) session.Send(Coop.AbilityPacket(ability), ReliableChannel, true, HostPeerId);
        }

        sendTimer -= dt;
        if (sendTimer > 0) return;
        sendTimer = SendInterval;
        if (IsHost) Broadcast(Coop.Snapshot(e), reliable: false);
        else if (Link == CoopLink.Online && LocalSlot < e.Players.Count)
            session.Send(Coop.PlayerStatePacket(e.Players[LocalSlot]), StateChannel, false, HostPeerId);
    }

    private void Broadcast(byte[] packet, bool reliable) =>
        session?.Send(packet, reliable ? ReliableChannel : StateChannel, reliable);

    private int? PeerOf(int slot)
    {
        foreach (var (peer, s) in peerSlots) if (s == slot) return peer;
        return null;
    }

    private void SendRoster()
    {
        rosterTimer = RosterInterval;
        if (!IsHost || session is null) return;
        var entries = new List<Coop.RosterEntry> { new(LocalSlot, localName, "") };
        foreach (var peer in session.Peers)
            if (peerSlots.TryGetValue(peer.Id, out var slot)) entries.Add(new(slot, peer.Name, AddressText(peer.EndPoint)));
        roster = [.. entries];
        if (entries.Count > 1) Broadcast(Coop.RosterPacket(entries), reliable: true);
    }

    private static string AddressText(EndPoint? endPoint)
    {
        if (endPoint is not IPEndPoint ip) return "";
        var address = ip.Address.IsIPv4MappedToIPv6 ? ip.Address.MapToIPv4() : ip.Address;
        return address.ToString();
    }

    private void OnMessage(NetworkMessage message)
    {
        var data = message.Data;
        if (data is null || data.Length == 0) return;
        try
        {
            switch (Coop.Kind(data))
            {
                case Coop.Message.Hello when IsHost: AcceptPeer(message.Sender, data); break;
                case Coop.Message.Full when !IsHost:
                    if (Welcome is null) Status?.Invoke(Loc.T("coop.full"));
                    else Fail(Loc.T("coop.full"));
                    break;
                case Coop.Message.Welcome when !IsHost: OnWelcome(Coop.ReadWelcome(data)); break;
                case Coop.Message.Roster when !IsHost: roster = Coop.ReadRoster(data); break;
                case Coop.Message.Snapshot when !IsHost && Encounter is not null: Coop.ApplySnapshot(Encounter, data, LocalSlot); break;
                case Coop.Message.Event when !IsHost && Encounter is not null: ReceivedEvents.Add(Coop.ReadEvent(data)); break;
                case Coop.Message.Hurt when !IsHost && Encounter is not null:
                    var (slot, amount, knockback) = Coop.ReadHurt(data);
                    if (slot == LocalSlot) Encounter.ApplyHurt(slot, amount, knockback);
                    break;
                case Coop.Message.PlayerState when IsHost && Encounter is not null:
                    var state = Coop.ReadPlayerState(data);
                    if (Owns(message.Sender, state.Slot)) Coop.ApplyRemotePlayer(Encounter, state);
                    break;
                case Coop.Message.Stab when IsHost && Encounter is not null:
                    var stab = Coop.ReadStab(data);
                    if (Owns(message.Sender, stab.Slot)) Encounter.ResolveStab(stab.Body, stab.OnColossus, stab.Charge, stab.Slot, stab.PulseTime);
                    break;
                case Coop.Message.Ability when IsHost && Encounter is not null:
                    var ability = Coop.ReadAbility(data);
                    if (Owns(message.Sender, ability.Slot)) Encounter.ApplyRemoteAbility(ability);
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
        {
            // Malformed packet from a peer: drop it rather than crash the hunt.
        }
    }

    private void OnWelcome(Coop.Welcome welcome)
    {
        if (Welcome is null)
        {
            LocalSlot = welcome.Slot;
            Welcome = welcome;
            Accepted?.Invoke(welcome);
            return;
        }
        // Back in after a reconnect or migration: we must have kept our slot, or the local hunter no longer lines up.
        if (welcome.Slot != LocalSlot)
        {
            Fail(Loc.T("coop.slotLost"));
            return;
        }
        if (Link == CoopLink.Migrating) hostSlot = migrationTarget;
        Link = CoopLink.Online;
        linkTimer = 0;
        Notice(Loc.T("coop.reconnected"));
    }

    private bool Owns(NetworkPeer peer, int slot) => peerSlots.TryGetValue(peer.Id, out var s) && s == slot;

    private void AcceptPeer(NetworkPeer peer, byte[] data)
    {
        var hello = Coop.ReadHello(data);
        if (hello.Version != Coop.ProtocolVersion || WelcomeFor is null || slots is null || session is null) return;
        if (!peerSlots.TryGetValue(peer.Id, out var slot))
        {
            slot = slots.Claim(hello.Token, hello.PreferredSlot, out var reclaimed);
            if (slot < 0)
            {
                session.Send(Coop.Full(), ReliableChannel, true, peer.Id);
                return;
            }
            // A reclaimed slot may still be mapped to the hunter's old, timed-out connection.
            foreach (var (old, s) in peerSlots.ToArray()) if (s == slot) peerSlots.Remove(old);
            peerSlots[peer.Id] = slot;
            Notice(Loc.F(reclaimed ? "coop.rejoined" : "coop.joined", hello.Name));
        }
        session.Send(Coop.WelcomePacket(WelcomeFor(slot)), ReliableChannel, true, peer.Id);
        SendRoster();
    }

    private void OnPeerLeft(NetworkPeer peer)
    {
        if (!IsHost)
        {
            if (Link == CoopLink.Online && peer.Id != HostPeerId && peer.Id != session?.LocalPeerId) Status?.Invoke(Loc.F("coop.left", peer.Name));
            return;
        }
        if (!peerSlots.Remove(peer.Id, out var slot)) return;
        slots?.Release(slot);
        if (Encounter is { } e && slot < e.Players.Count) e.Players[slot].Disconnect();
        Notice(Loc.F("coop.held", peer.Name, (int)SlotTable.ReserveSeconds));
        SendRoster();
    }

    public void Dispose()
    {
        Detach();
        beacon?.Dispose();
        beacon = null;
    }
}
