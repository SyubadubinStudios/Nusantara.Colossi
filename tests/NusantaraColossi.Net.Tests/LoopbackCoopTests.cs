using System.Diagnostics;
using System.Numerics;
using NusantaraColossi.Core;
using NusantaraColossi.Game.Net;

namespace NusantaraColossi.Net.Tests;

/// <summary>
/// Host and client <see cref="CoopSession"/>s talking over 127.0.0.1, stepped the way HuntScreen steps them.
/// These exercise the real sockets, handshake, snapshots, player state and host migration.
/// </summary>
[Collection("udp")] // one port range at a time
public sealed class LoopbackCoopTests
{
    private const float Dt = 1 / 60f;
    private static int nextPort = 27300 + Environment.ProcessId % 200;

    private sealed class Peer : IDisposable
    {
        public required CoopSession Session;
        public Encounter? Encounter;
        public PlayerInput Input = new() { CameraForward = Vector3.UnitZ };

        public void Tick()
        {
            Session.Poll();
            var e = Session.Encounter;
            if (e is null) return;
            Encounter = e;
            e.Step([Input], Dt, Session.LocalSlot);
            Session.AfterStep(Dt);
            Input.InteractPressed = Input.JumpPressed = false;
        }

        public void Dispose() => Session.Dispose();
    }

    private static (Peer Host, Peer Client) Connect(string guardian = "kerbau-langit")
    {
        var port = Interlocked.Increment(ref nextPort);
        var bp = Colossi.Get(guardian);
        var options = new EncounterOptions { Abilities = DroneAbilities.All, Seed = 11, IsAuthority = true };
        var hostSession = CoopSession.Host(port, "host");
        hostSession.WelcomeFor = slot => new Coop.Welcome(slot, bp.Id, options.Legend, options.Abilities, options.Seed);
        var hostEncounter = new Encounter(Regions.ForColossus(bp.Id), bp, options);
        hostEncounter.AddPlayer("host");
        hostSession.Encounter = hostEncounter;
        var host = new Peer { Session = hostSession };

        var clientSession = CoopSession.Join("127.0.0.1", port, "tamu");
        var client = new Peer { Session = clientSession };
        clientSession.Accepted += welcome =>
        {
            // Mirrors HuntScreen.Coop.
            var cbp = Colossi.Get(welcome.ColossusId);
            var e = new Encounter(Regions.ForColossus(cbp.Id), cbp, new EncounterOptions { Legend = welcome.Legend, Abilities = welcome.Abilities, Seed = welcome.Seed, IsAuthority = false });
            while (e.Players.Count < clientSession.LocalSlot) e.Players.Add(new Player(e.Players.Count, "…") { IsRemote = true });
            e.AddPlayer("tamu");
            clientSession.Encounter = e;
        };
        Pump(host, client, () => client.Encounter is not null && host.Encounter!.Players.Count == 2, 8);
        return (host, client);
    }

    /// <summary>Ticks both peers in real time (the sessions use a wall clock) until the condition holds.</summary>
    private static bool Pump(Peer a, Peer? b, Func<bool> until, double seconds)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            a.Tick();
            b?.Tick();
            if (until()) return true;
            Thread.Sleep(8);
        }
        return until();
    }

    [Fact]
    public void ClientJoinsAndBothSeeTheSameHunt()
    {
        var (host, client) = Connect();
        using var _h = host; using var _c = client;
        Assert.NotNull(client.Encounter);
        Assert.Equal(1, client.Session.LocalSlot);
        Assert.Equal(2, host.Encounter!.Players.Count);

        // Snapshots: the client's guardian follows the host's clock.
        Assert.True(Pump(host, client, () => MathF.Abs(client.Encounter!.Colossus.Pose.Time - host.Encounter.Colossus.Pose.Time) < .3f && client.Encounter.Colossus.Pose.Time > .5f, 6),
            $"guardian clock host {host.Encounter.Colossus.Pose.Time:0.00} client {client.Encounter!.Colossus.Pose.Time:0.00}");

        // Player state: the client walks and the host sees it move.
        client.Input.Move = new Vector2(0, 1);
        var start = host.Encounter.Players[1].Position;
        Assert.True(Pump(host, client, () => Vector3.Distance(host.Encounter.Players[1].Position, start) > 2, 6), "host never saw the client move");
        client.Input.Move = Vector2.Zero;
        Pump(host, client, () => false, .3);
        Assert.True(Vector3.Distance(host.Encounter.Players[1].Position, client.Encounter.Players[1].Position) < .8f);
    }

    [Fact]
    public void ARemoteRiderAppearsOnTheirVehicle()
    {
        var (host, client) = Connect();
        using var _h = host; using var _c = client;
        var me = client.Encounter!.Players[1];
        me.Reset(client.Encounter.Vehicles[1].Position + new Vector3(1, 0, 0), 0);
        client.Input.InteractPressed = true;
        Assert.True(Pump(host, client, () => me.IsRiding, 2), "client could not mount its own bike");
        client.Input.Move = new Vector2(0, 1);
        Assert.True(Pump(host, client, () => host.Encounter!.Vehicles[1].Rider == 1 && Vector3.Distance(host.Encounter.Vehicles[1].Position, me.Position) < 1.5f, 5),
            "host did not mirror the ridden bike");
        Assert.True(host.Encounter!.Players[1].IsRiding);
    }

    [Fact]
    public void TheClientTakesOverWhenTheHostVanishes()
    {
        var (host, client) = Connect();
        using var _c = client;
        // Let a roster arrive so the client knows it is next in line.
        Assert.True(Pump(host, client, () => client.Session.Roster.Count >= 2, 8), "no roster from the host");
        host.Dispose();
        var clock = Stopwatch.StartNew();
        var promoted = Pump(client, null, () => client.Session.IsHost && client.Session.Link == CoopLink.Online, CoopSession.ReconnectWindow + 12);
        Assert.True(promoted, $"link {client.Session.Link} after {clock.Elapsed.TotalSeconds:0.0}s");
        Assert.True(client.Encounter!.Options.IsAuthority || client.Session.IsHost);
        // As host it now runs the guardian itself.
        var t = client.Encounter.Colossus.Pose.Time;
        Pump(client, null, () => false, .5);
        Assert.True(client.Encounter.Colossus.Pose.Time > t);
    }
}
