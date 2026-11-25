using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NusantaraColossi.Core;

namespace NusantaraColossi.Game.Net;

/// <summary>
/// Host side of LAN discovery: about once a second, broadcasts a small <see cref="Coop.Beacon"/> on
/// <see cref="Coop.DiscoveryPort"/>. ThreeNet's <c>NetworkSession</c> has no discovery, so this is a plain
/// <see cref="UdpClient"/> with broadcast enabled. It sends to the limited broadcast address, to every
/// interface's directed broadcast address (Windows only routes 255.255.255.255 out of one interface), and
/// to loopback so a second copy of the game on the same machine sees the host too.
/// </summary>
public sealed class LanBeaconBroadcaster : IDisposable
{
    private const float Interval = 1f;
    private const float TargetRefresh = 15f;

    private readonly UdpClient udp;
    private IPEndPoint[] targets = [];
    private float timer, targetAge = float.MaxValue;

    public LanBeaconBroadcaster()
    {
        udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
    }

    /// <summary>Call every frame; sends a beacon when due.</summary>
    public void Tick(float dt, Func<Coop.Beacon> beacon)
    {
        timer -= dt;
        targetAge += dt;
        if (timer > 0) return;
        timer = Interval;
        if (targetAge > TargetRefresh)
        {
            targets = BroadcastTargets();
            targetAge = 0;
        }
        var packet = Coop.EncodeBeacon(beacon());
        foreach (var target in targets)
        {
            try { udp.Send(packet, packet.Length, target); }
            catch (SocketException) { } // interface down, no route: try the others next second
            catch (ObjectDisposedException) { return; }
        }
    }

    private static IPEndPoint[] BroadcastTargets()
    {
        var addresses = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask is null) continue;
                    var ip = unicast.Address.GetAddressBytes();
                    var mask = unicast.IPv4Mask.GetAddressBytes();
                    if (mask.All(b => b == 0)) continue;
                    var broadcast = new byte[4];
                    for (var i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    addresses.Add(new IPAddress(broadcast));
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or NotImplementedException)
        {
            // Some Android builds restrict interface enumeration; the limited broadcast still works.
        }
        return addresses.Select(a => new IPEndPoint(a, Coop.DiscoveryPort)).ToArray();
    }

    public void Dispose() => udp.Dispose();
}

/// <summary>
/// Client side of LAN discovery: listens on <see cref="Coop.DiscoveryPort"/> (shared, so several copies of the
/// game can browse at once) and keeps a list of hosts heard from in the last <see cref="Expiry"/> seconds.
/// Polled from the UI thread; never blocks.
/// </summary>
public sealed class LanBrowser : IDisposable
{
    public const float Expiry = 3f;

    public sealed class Found(IPAddress address, Coop.Beacon beacon)
    {
        public IPAddress Address { get; internal set; } = address;
        public Coop.Beacon Beacon { get; internal set; } = beacon;
        public float Age { get; internal set; }
    }

    private readonly UdpClient? udp;
    private readonly List<Found> sessions = [];
    private readonly byte[] buffer = new byte[1024];

    public IReadOnlyList<Found> Sessions => sessions;

    /// <summary>False when the discovery port could not be opened (another program holds it exclusively).</summary>
    public bool IsListening => udp is not null;

    public LanBrowser()
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { ExclusiveAddressUse = false };
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.EnableBroadcast = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, Coop.DiscoveryPort));
            udp = new UdpClient { Client = socket };
        }
        catch (SocketException)
        {
            udp = null;
        }
    }

    /// <summary>Drains pending beacons and ages out silent hosts. Returns true when the list changed.</summary>
    public bool Poll(float dt)
    {
        var changed = false;
        for (var i = sessions.Count - 1; i >= 0; i--)
        {
            sessions[i].Age += dt;
            if (sessions[i].Age <= Expiry) continue;
            sessions.RemoveAt(i);
            changed = true;
        }
        if (udp is null) return changed;

        try
        {
            var socket = udp.Client;
            for (var guard = 0; guard < 64 && socket.Available > 0; guard++)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                var length = socket.ReceiveFrom(buffer, ref from);
                if (!Coop.TryDecodeBeacon(buffer.AsSpan(0, length), out var beacon)) continue;
                changed |= Heard(((IPEndPoint)from).Address, beacon);
            }
        }
        catch (SocketException) { } // ICMP resets and the like: try again next frame
        catch (ObjectDisposedException) { }
        return changed;
    }

    private bool Heard(IPAddress address, Coop.Beacon beacon)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        foreach (var s in sessions)
        {
            if (s.Beacon.SessionId != beacon.SessionId) continue;
            // The same host arrives via loopback and via the LAN; show the LAN address, which other machines can use too.
            // Elapsed time ticks every beacon; only rebuild the list when something shown changes.
            var changed = s.Beacon with { ElapsedSeconds = 0 } != beacon with { ElapsedSeconds = 0 } || (IPAddress.IsLoopback(s.Address) && !IPAddress.IsLoopback(address));
            if (!IPAddress.IsLoopback(address) || IPAddress.IsLoopback(s.Address)) s.Address = address;
            s.Beacon = beacon;
            s.Age = 0;
            return changed;
        }
        sessions.Add(new Found(address, beacon));
        return true;
    }

    public void Dispose() => udp?.Dispose();
}
