using System.Numerics;
using NusantaraColossi.Core;

namespace NusantaraColossi.Core.Tests;

public class ProgressionAndCoopTests
{
    [Fact]
    public void RegionsUnlockInOrderAndGarudaLearns()
    {
        var profile = new Profile();
        Assert.True(profile.IsUnlocked(RegionId.Bromo));
        Assert.False(profile.IsUnlocked(RegionId.Jakarta));
        Assert.Equal(DroneAbilities.Scan, profile.Abilities);
        Assert.Equal(2, profile.RecordVictory("kerbau-langit", 300, Rank.A, false));
        Assert.True(profile.IsUnlocked(RegionId.Jakarta));
        Assert.True(profile.Abilities.HasFlag(DroneAbilities.Hack));
        Assert.Equal(0, profile.RecordVictory("kerbau-langit", 200, Rank.A, false));
        Assert.Equal(1, profile.RecordVictory("kerbau-langit", 150, Rank.S, false));
        Assert.Equal(150, profile.Hunts["kerbau-langit"].BestSeconds);
    }

    [Fact]
    public void SkillsRequireTheirTreeAndPoints()
    {
        var profile = new Profile { SkillPoints = 3 };
        Assert.False(profile.Buy("napas-kedua"));
        Assert.True(profile.Buy("genggaman-baja"));
        Assert.True(profile.Buy("panjat-kilat"));
        Assert.Equal(1, profile.SkillPoints);
        Assert.False(profile.Buy("napas-kedua")); // costs 2
        var stats = profile.Stats;
        Assert.Equal(.8f, stats.GripDrainScale);
        Assert.Equal(120, stats.MaxStamina);
    }

    [Fact]
    public void SaveRoundTripsAndSurvivesCorruption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SaveStore(dir);
            var profile = new Profile { SkillPoints = 4, PlayerName = "Sekar" };
            profile.Skills.Add("kulit-badak");
            profile.RecordVictory("kerbau-langit", 222, Rank.B, true);
            store.Save(profile);
            var loaded = store.Load(out var warning);
            Assert.Null(warning);
            Assert.Equal("Sekar", loaded.PlayerName);
            Assert.Contains("kulit-badak", loaded.Skills);
            Assert.True(loaded.Hunts["kerbau-langit"].LegendCleared);
            Assert.Equal(Rank.B, loaded.Hunts["kerbau-langit"].BestRank);

            File.WriteAllText(store.FilePath, "{ not json");
            var fresh = store.Load(out warning);
            Assert.NotNull(warning);
            Assert.Equal(0, fresh.SkillPoints);
            Assert.True(File.Exists(store.FilePath + ".corrupt"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SnapshotReproducesTheHostPoseExactly()
    {
        var region = Regions.Get(RegionId.Flores);
        var host = new Encounter(region, Colossi.Get(region.ColossusId));
        var hostPlayer = host.AddPlayer("Host");
        var client = new Encounter(region, Colossi.Get(region.ColossusId), new EncounterOptions { IsAuthority = false });
        client.AddPlayer("Host");
        var clientPlayer = new Player(1, "Client");
        client.Players.Add(clientPlayer);
        for (var i = 0; i < 400; i++) host.Step([new PlayerInput { Move = new Vector2(0, 1), CameraForward = Vector3.UnitZ }], 1 / 60f);
        host.Colossus.Scan(10);

        var packet = Coop.Snapshot(host);
        Assert.Equal(Coop.Message.Snapshot, Coop.Kind(packet));
        Coop.ApplySnapshot(client, packet, localSlot: 1);

        Assert.Equal(host.Colossus.Pose.Position, client.Colossus.Pose.Position);
        Assert.Equal(host.Colossus.State, client.Colossus.State);
        for (var i = 0; i < host.Colossus.PartWorld.Length; i++)
            Assert.Equal(host.Colossus.PartWorld[i], client.Colossus.PartWorld[i]);
        Assert.Equal(host.Colossus.Cores[0].SealOpen, client.Colossus.Cores[0].SealOpen);
        Assert.Equal(hostPlayer.Position, client.Players[0].Position);
        Assert.True(client.Players[0].IsRemote);
    }

    [Fact]
    public void ClientStabsAreForwardedNotApplied()
    {
        var region = Regions.Get(RegionId.Bromo);
        var client = new Encounter(region, Colossi.Get(region.ColossusId), new EncounterOptions { IsAuthority = false });
        var p = client.AddPlayer("Client");
        client.UseAbility(p, DroneAbilities.Scan, Vector3.UnitZ);
        Assert.Single(client.PendingRemoteAbilities);
        Assert.All(client.Colossus.Cores, c => Assert.Equal(0, c.SealOpen));

        var request = client.PendingRemoteAbilities[0];
        var round = Coop.ReadAbility(Coop.AbilityPacket(request));
        Assert.Equal(request, round);
        var stab = new StabRequest(2, new Vector3(1, 2, 3), true, .75f, 4.5f);
        Assert.Equal(stab, Coop.ReadStab(Coop.StabPacket(stab)));
        var hurt = Coop.ReadHurt(Coop.HurtPacket(3, 12.5f, new Vector3(0, 4, 1)));
        Assert.Equal((3, 12.5f, new Vector3(0, 4, 1)), hurt);
        var welcome = new Coop.Welcome(2, "leviathan", true, DroneAbilities.All, 77);
        Assert.Equal(welcome, Coop.ReadWelcome(Coop.WelcomePacket(welcome)));
    }

    [Fact]
    public void HostHitsOnRemotePlayersAreQueuedForTheirOwner()
    {
        var region = Regions.Get(RegionId.Bromo);
        var host = new Encounter(region, Colossi.Get(region.ColossusId));
        host.AddPlayer("Host");
        Coop.ApplyRemotePlayer(host, new Coop.PlayerSnapshot(1, PlayerMode.Ground, host.Colossus.Pose.Position + new Vector3(0, 0, 8), 0, Vector3.UnitY, Vector3.UnitZ, 100, false, false, true, "Remote"));
        var hit = false;
        for (var i = 0; i < 60 * 60 && !hit; i++)
        {
            host.Step([new PlayerInput()], 1 / 60f);
            hit = host.RemoteHurts.Count > 0;
        }
        Assert.True(hit);
        Assert.Equal(1, host.RemoteHurts[0].Slot);
        Assert.Equal(100, host.Players[1].Health); // the owner applies the damage, not the host's ghost
    }
}
