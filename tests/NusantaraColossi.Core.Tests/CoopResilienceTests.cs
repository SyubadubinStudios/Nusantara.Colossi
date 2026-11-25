using System.Numerics;
using NusantaraColossi.Core;

namespace NusantaraColossi.Core.Tests;

/// <summary>Milestone 4 co-op: LAN beacon, slot reclaim, host migration.</summary>
public class CoopResilienceTests
{
    private static readonly Coop.Beacon SampleBeacon = new(123456, "Sekar", "leviathan", true, 2, 4, 27045, 95);

    [Fact]
    public void BeaconRoundTrips()
    {
        var bytes = Coop.EncodeBeacon(SampleBeacon);
        Assert.True(Coop.TryDecodeBeacon(bytes, out var decoded));
        Assert.Equal(SampleBeacon, decoded);
    }

    [Fact]
    public void BeaconClipsLongNamesAndStillDecodes()
    {
        var b = SampleBeacon with { HostName = new string('x', 80) };
        Assert.True(Coop.TryDecodeBeacon(Coop.EncodeBeacon(b), out var decoded));
        Assert.Equal(24, decoded.HostName.Length);
    }

    [Fact]
    public void BeaconRejectsMalformedInput()
    {
        var good = Coop.EncodeBeacon(SampleBeacon);
        Assert.False(Coop.TryDecodeBeacon([], out _));
        Assert.False(Coop.TryDecodeBeacon(good.AsSpan(0, 10), out _));          // truncated header
        Assert.False(Coop.TryDecodeBeacon(good.AsSpan(0, good.Length - 1), out _)); // truncated string
        Assert.False(Coop.TryDecodeBeacon([.. good, 0], out _));                  // trailing garbage

        byte[] Mutate(int index, byte value) { var copy = (byte[])good.Clone(); copy[index] = value; return copy; }
        Assert.False(Coop.TryDecodeBeacon(Mutate(0, (byte)'X'), out _));                   // magic
        Assert.False(Coop.TryDecodeBeacon(Mutate(4, Coop.ProtocolVersion + 1), out _));   // version
        Assert.False(Coop.TryDecodeBeacon(Mutate(9, 5), out _));                          // more players than max
        Assert.False(Coop.TryDecodeBeacon(Mutate(10, 0), out _));                         // max 0
        Assert.False(Coop.TryDecodeBeacon(Mutate(10, 9), out _));                         // max above 4
        Assert.False(Coop.TryDecodeBeacon(Mutate(13, 7), out _));                         // legend flag not 0/1
        Assert.False(Coop.TryDecodeBeacon(Mutate(16, 200), out _));                       // host name length past the end

        var badPort = (byte[])good.Clone();
        badPort[11] = 0; badPort[12] = 0;
        Assert.False(Coop.TryDecodeBeacon(badPort, out _));

        Assert.False(Coop.TryDecodeBeacon(Coop.EncodeBeacon(SampleBeacon with { ColossusId = "not-a-guardian" }), out _));
        Assert.False(Coop.TryDecodeBeacon(Coop.EncodeBeacon(SampleBeacon with { ColossusId = "" }), out _));

        // Invalid UTF-8 in the host name.
        var badText = (byte[])good.Clone();
        badText[17] = 0xFF;
        Assert.False(Coop.TryDecodeBeacon(badText, out _));

        // Random noise never throws.
        var random = new Random(7);
        for (var i = 0; i < 2000; i++)
        {
            var noise = new byte[random.Next(0, 64)];
            random.NextBytes(noise);
            Coop.TryDecodeBeacon(noise, out _);
            var flipped = (byte[])good.Clone();
            flipped[random.Next(flipped.Length)] ^= (byte)(1 << random.Next(8));
            Coop.TryDecodeBeacon(flipped, out _);
        }
    }

    [Fact]
    public void HelloCarriesTokenAndPreferredSlot()
    {
        var hello = Coop.ReadHello(Coop.Hello("Sekar", 0x1234_5678_9ABC, 2));
        Assert.Equal(new Coop.HelloInfo(Coop.ProtocolVersion, "Sekar", 0x1234_5678_9ABC, 2), hello);
        Assert.Equal(Coop.AnySlot, Coop.ReadHello(Coop.Hello("New")).PreferredSlot);
    }

    [Fact]
    public void RosterRoundTripsIncludingLongAddresses()
    {
        Coop.RosterEntry[] roster = [new(0, "Host", ""), new(1, "Sekar", "192.168.1.42"), new(3, "Bayu", "fe80::1c2b:3d4e:5f60:7182%12")];
        Assert.Equal(roster, Coop.ReadRoster(Coop.RosterPacket(roster)));
    }

    [Fact]
    public void DroppedHunterReclaimsTheirSlotByToken()
    {
        var table = new SlotTable(localSlot: 0);
        var a = table.Claim(111, Coop.AnySlot, out var reclaimed);
        Assert.Equal(1, a);
        Assert.False(reclaimed);
        var b = table.Claim(222, Coop.AnySlot, out _);
        Assert.Equal(2, b);
        Assert.Equal(3, table.Occupied);

        table.Release(a);
        Assert.Equal(SlotStatus.Reserved, table[a]);
        Assert.Equal(SlotTable.ReserveSeconds, table.ReservedFor(a));

        // A newcomer can't take the held slot, even by asking for it.
        Assert.Equal(3, table.Claim(333, a, out _));
        Assert.Equal(-1, table.Claim(444, Coop.AnySlot, out _)); // full: slot 1 is still held

        Assert.Equal(0, table.Tick(SlotTable.ReserveSeconds - 1));
        Assert.Equal(a, table.Claim(111, Coop.AnySlot, out reclaimed));
        Assert.True(reclaimed);
        Assert.Equal(SlotStatus.Connected, table[a]);

        // Same token while the host still thinks the old connection is alive: same slot.
        Assert.Equal(a, table.Claim(111, Coop.AnySlot, out reclaimed));
        Assert.True(reclaimed);
    }

    [Fact]
    public void HeldSlotFreesAfterThirtySeconds()
    {
        var table = new SlotTable(0);
        var slot = table.Claim(5, Coop.AnySlot, out _);
        table.Release(slot);
        Assert.Equal(0, table.Tick(29.5f));
        Assert.Equal(1 << slot, table.Tick(1f));
        Assert.Equal(SlotStatus.Free, table[slot]);
        Assert.Equal(slot, table.Claim(9, Coop.AnySlot, out var reclaimed)); // token 5 is forgotten; slot reused
        Assert.False(reclaimed);
    }

    [Fact]
    public void MigratedHostHoldsRosterSlotsForTheirOwners()
    {
        // Slot 1 took over; slots 2 and 3 were on the old host.
        var table = new SlotTable(localSlot: 1);
        table.Reserve(2);
        table.Reserve(3);
        Assert.Equal(0, table.Claim(999, Coop.AnySlot, out _));   // newcomer gets the old host's slot, not a held one
        Assert.Equal(3, table.Claim(77, preferred: 3, out var reclaimed));
        Assert.True(reclaimed);
        Assert.Equal(77, table.TokenOf(3));
        Assert.Equal(-1, table.Claim(1000, Coop.AnySlot, out _)); // 2 still held for its owner
        Assert.Equal(2, table.Claim(55, preferred: 2, out reclaimed));
        Assert.True(reclaimed);
    }

    [Fact]
    public void MigrationPicksLowestRemainingSlot()
    {
        Coop.RosterEntry[] roster = [new(0, "Host", ""), new(2, "B", "10.0.0.2"), new(1, "A", "10.0.0.1"), new(3, "C", "10.0.0.3")];
        Assert.Equal(1, Coop.MigrationCandidate(roster, lostHostSlot: 0));
        Assert.Equal(2, Coop.MigrationCandidate(roster, lostHostSlot: 0, skipBelow: 2));
        Assert.Equal(-1, Coop.MigrationCandidate(roster, lostHostSlot: 0, skipBelow: 4));
        Assert.Equal(2, Coop.MigrationCandidate(roster, lostHostSlot: 1, skipBelow: 1));
    }

    [Fact]
    public void PromotedClientCarriesOnFromTheHostsGuardianState()
    {
        var region = Regions.Get(RegionId.Flores);
        var bp = Colossi.Get(region.ColossusId);
        var options = new EncounterOptions { Seed = 42 };
        var host = new Encounter(region, bp, options);
        host.AddPlayer("Host");
        var client = new Encounter(region, bp, options with { IsAuthority = false });
        client.AddPlayer("Host");
        client.Players.Add(new Player(1, "Client"));

        // Make the guardian interesting: awake, scanned, wounded, enraged.
        for (var i = 0; i < 600; i++) host.Step([new PlayerInput { Move = new Vector2(0, 1), CameraForward = Vector3.UnitZ }], 1 / 60f);
        host.Colossus.Scan(10);
        host.Colossus.Cores[0].Health *= .4f;
        host.Colossus.Rage = 1;
        host.Colossus.Lure(new Vector3(5, 0, 5), 3);

        Coop.ApplySnapshot(client, Coop.Snapshot(host), localSlot: 1);
        Assert.False(client.Options.IsAuthority);
        Coop.PromoteToAuthority(client, formerHostSlot: 0);

        Assert.True(client.Options.IsAuthority);
        var h = host.Colossus;
        var c = client.Colossus;
        Assert.Equal(h.Pose.Position, c.Pose.Position);
        Assert.Equal(h.Pose.Yaw, c.Pose.Yaw);
        Assert.Equal(h.Pose.Time, c.Pose.Time);
        Assert.Equal(h.State, c.State);
        Assert.Equal(h.StateTime, c.StateTime);
        Assert.Equal(h.Rage, c.Rage);
        Assert.Equal(h.Annoyance, c.Annoyance);
        Assert.Equal(h.DecoyTime, c.DecoyTime);
        Assert.Equal(h.DecoyPoint, c.DecoyPoint);
        Assert.Equal(h.Target, c.Target);
        Assert.Equal(host.Elapsed, client.Elapsed);
        Assert.Equal(host.Phase, client.Phase);
        for (var i = 0; i < h.Cores.Length; i++)
        {
            Assert.Equal(h.Cores[i].Health, c.Cores[i].Health);
            Assert.Equal(h.Cores[i].SealOpen, c.Cores[i].SealOpen);
            Assert.Equal(h.Cores[i].Revealed, c.Cores[i].Revealed);
        }
        Assert.Equal(h.Vitality, c.Vitality);
        for (var i = 0; i < h.PartWorld.Length; i++) Assert.Equal(h.PartWorld[i], c.PartWorld[i]);

        // The old host is gone; the new host's hunter is untouched.
        Assert.Equal(PlayerMode.Dead, client.Players[0].Mode);
        Assert.True(client.Players[0].IsRemote);
        Assert.NotEqual(PlayerMode.Dead, client.Players[1].Mode);

        // And it now runs the guardian itself: the AI clock and seals advance, and stabs resolve locally.
        var time = c.Pose.Time;
        var seal = c.Cores[0].SealOpen;
        client.Step([new PlayerInput { CameraForward = Vector3.UnitZ }], 1 / 60f, firstLocalSlot: 1);
        Assert.True(c.Pose.Time > time);
        Assert.True(c.Cores[0].SealOpen < seal);
        Assert.Empty(client.PendingRemoteStabs);
        client.UseAbility(client.Players[1], DroneAbilities.Hack, Vector3.UnitZ);
        Assert.Empty(client.PendingRemoteAbilities);
    }

    [Fact]
    public void RemotePlayerSlotsBeyondFourAreIgnored()
    {
        var region = Regions.Get(RegionId.Bromo);
        var e = new Encounter(region, Colossi.Get(region.ColossusId));
        e.AddPlayer("Host");
        Coop.ApplyRemotePlayer(e, new Coop.PlayerSnapshot(200, PlayerMode.Ground, Vector3.Zero, 0, Vector3.UnitY, Vector3.UnitZ, 100, false, false, true, "Spoof"));
        Assert.Single(e.Players);
    }
}
