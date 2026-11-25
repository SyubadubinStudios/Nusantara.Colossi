using System.Numerics;
using NusantaraColossi.Core;

namespace NusantaraColossi.Core.Tests;

/// <summary>Guardian signature mechanics, vehicles, the glider, breath and the local leaderboard.</summary>
public class SignatureTests
{
    private const float Dt = 1 / 60f;

    private static (Encounter Encounter, Player Player) Hunt(string colossusId, bool authority = true)
    {
        var e = new Encounter(Regions.ForColossus(colossusId), Colossi.Get(colossusId), new EncounterOptions { IsAuthority = authority });
        return (e, e.AddPlayer("Arka"));
    }

    private static void Place(Encounter e, Player p, float x, float z)
    {
        var y = e.Region.Height(x, z);
        p.Reset(new Vector3(x, y, z), 0);
    }

    [Fact]
    public void EachGuardianHasItsSignature()
    {
        Assert.Equal(Signature.Flood, Colossi.Get("dewi-padi").Signature);
        Assert.Equal(Signature.GlassShards, Colossi.Get("gedung-titan").Signature);
        Assert.Equal(Signature.Dive, Colossi.Get("leviathan").Signature);
        Assert.Equal(Signature.Cannons, Colossi.Get("kapal-majapahit").Signature);
        Assert.Equal(Signature.Firewall, Colossi.Get("garuda-agung").Signature);
    }

    [Fact]
    public void SchedulesArePureFunctionsOfTime()
    {
        Assert.Equal(0, Signatures.FloodAmount(5));
        Assert.Equal(1, Signatures.FloodAmount(28));
        Assert.Equal(Signatures.FloodAmount(28), Signatures.FloodAmount(28 + Signatures.FloodCycle));
        Assert.True(Signatures.FloodWarning(17));
        Assert.Equal(0, Signatures.DiveAmount(3));
        Assert.Equal(1, Signatures.DiveAmount(24));
        Assert.Equal(Signatures.Hash01(3, 4, 5), Signatures.Hash01(3, 4, 5));
        Assert.NotEqual(Signatures.Hash01(3, 4, 5), Signatures.Hash01(3, 4, 6));
    }

    [Fact]
    public void DewiPadiFloodsTheTerracesAndForcesHuntersToSwim()
    {
        var (e, p) = Hunt("dewi-padi");
        Place(e, p, 0, -40);
        var ground = e.Region.Height(0, -40);
        Assert.True(e.WaterLevel < ground, "dry before the flood");
        e.Colossus.Pose.Time = 28; // full flood
        e.Step([new PlayerInput { CameraForward = Vector3.UnitZ }], Dt);
        Assert.True(e.WaterLevel > ground + 1.25f, $"flood {e.WaterLevel} over ground {ground}");
        Assert.Equal(PlayerMode.Swim, p.Mode);
    }

    [Fact]
    public void LeviathanDivesBelowTheSurface()
    {
        var (e, _) = Hunt("leviathan");
        e.Step([new PlayerInput()], Dt);
        var surfaced = e.Colossus.Pose.Position.Y;
        e.Colossus.Pose.Time = 24;
        e.Step([new PlayerInput()], Dt);
        Assert.Equal(surfaced - Signatures.DiveDepth, e.Colossus.Pose.Position.Y, 1);
    }

    [Fact]
    public void ClimbersRunOutOfBreathUnderWater()
    {
        var (e, p) = Hunt("leviathan", authority: false);
        // A client follows the host's pose: put the serpent fully under (as a snapshot would).
        e.Colossus.Pose.Time = 24;
        e.Colossus.Pose.Position = e.Colossus.Pose.Position with { Y = e.Region.WaterLevel - Signatures.DiveDepth };
        e.Colossus.Evaluate();
        e.SyncColossusBoxes();
        var slot = e.Climb.StaticCount + Array.FindIndex(e.Blueprint.Parts, part => part.Role == PartRole.Head);
        Assert.True(e.Climb.Project(slot, e.Climb[slot].ToWorld(new Vector3(-5.4f, 0, 0)), true, out var contact, out _));
        p.Contact = contact;
        p.Mode = PlayerMode.Attached;
        var input = new PlayerInput { Grip = true, CameraForward = Vector3.UnitZ };
        var events = new List<GameEventKind>();
        for (var i = 0; i < 60 * 12; i++)
        {
            e.Step([input], Dt);
            foreach (var ev in e.Events) events.Add(ev.Kind);
        }
        Assert.True(p.Breath < .3f, $"breath {p.Breath}");
        Assert.Contains(GameEventKind.BreathLow, events);
    }

    [Fact]
    public void TitanShedsShardsThatHurtHuntersInTheOpen()
    {
        var (e, p) = Hunt("gedung-titan");
        Place(e, p, 0, 0);
        e.Colossus.Lure(Vector3.Zero, 60); // awake
        var period = Signatures.VolleyPeriod(Signature.GlassShards, 0);
        var health = p.Health;
        var volley = false;
        var impact = false;
        for (var i = 0; i < (int)(60 * (period * 2 + 3)); i++)
        {
            e.Step([new PlayerInput { CameraForward = Vector3.UnitZ }], Dt);
            foreach (var ev in e.Events)
            {
                volley |= ev.Kind == GameEventKind.Volley;
                impact |= ev.Kind == GameEventKind.StrikeImpact;
            }
            if (p.Mode == PlayerMode.Dead) break;
        }
        Assert.True(volley && impact);
        Assert.True(p.Health < health, "an aimed shard lands on a hunter who never moves");
    }

    [Fact]
    public void GarudaCannotBeHacked()
    {
        var (e, p) = Hunt("garuda-agung");
        Assert.False(e.UseAbility(p, DroneAbilities.Hack, Vector3.UnitZ));
        Assert.Contains(e.Events, ev => ev.Kind == GameEventKind.HackDenied && ev.Value < 0);
        Assert.NotEqual(ColossusState.Stunned, e.Colossus.State);
    }

    [Fact]
    public void HuntersMountRideAndDismountTheTrailBike()
    {
        var (e, p) = Hunt("kerbau-langit");
        Assert.Equal(1, e.VehicleCount);
        Assert.Equal(VehicleKind.Trail, e.Vehicles[0].Kind);
        p.Reset(e.Vehicles[0].Position + new Vector3(1, 0, 0), 0);
        e.Step([new PlayerInput { InteractPressed = true, CameraForward = Vector3.UnitZ }], Dt);
        Assert.True(p.IsRiding);
        var start = p.Position;
        for (var i = 0; i < 60 * 3; i++) e.Step([new PlayerInput { Move = new Vector2(0, 1), CameraForward = Vector3.UnitZ }], Dt);
        Assert.True(Vector3.Distance(start, p.Position) > 25, "the bike is much faster than running");
        Assert.Equal(p.Position, e.Vehicles[0].Position);
        e.Step([new PlayerInput { JumpPressed = true, CameraForward = Vector3.UnitZ }], Dt);
        Assert.False(p.IsRiding);
        Assert.Equal(PlayerMode.Air, p.Mode);
        Assert.Equal(-1, e.Vehicles[0].Rider);
    }

    [Fact]
    public void SeaArenasGetAJetSki()
    {
        var (e, _) = Hunt("kapal-majapahit");
        Assert.Equal(VehicleKind.JetSki, e.Vehicles[0].Kind);
    }

    [Fact]
    public void TheGliderTurnsAFallIntoAGlide()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        Place(e, p, 0, -60);
        p.Position += Vector3.UnitY * 40;
        p.Mode = PlayerMode.Air;
        for (var i = 0; i < 20; i++) e.Step([new PlayerInput { CameraForward = Vector3.UnitZ }], Dt);
        e.Step([new PlayerInput { InteractPressed = true, CameraForward = Vector3.UnitZ }], Dt);
        Assert.True(p.Gliding);
        for (var i = 0; i < 60 * 3; i++) e.Step([new PlayerInput { Move = new Vector2(0, 1), CameraForward = Vector3.UnitZ }], Dt);
        Assert.True(p.Velocity.Y > -Glider.SinkSpeed - .5f, $"sink {p.Velocity.Y}");
        Assert.True(new Vector2(p.Velocity.X, p.Velocity.Z).Length() > Glider.CruiseSpeed * .8f);
        var health = p.Health;
        for (var i = 0; i < 60 * 30 && p.Mode == PlayerMode.Air; i++) e.Step([new PlayerInput { CameraForward = Vector3.UnitZ }], Dt);
        Assert.Equal(health, p.Health); // a glided landing never hurts
        Assert.False(p.Gliding);
    }

    [Fact]
    public void LeaderboardKeepsTheFiveFastestRuns()
    {
        var profile = new Profile();
        var when = new DateTime(2045, 8, 17);
        Assert.Equal(1, profile.RecordTime("kerbau-langit", 300, Rank.B, false, 1, when));
        Assert.Equal(1, profile.RecordTime("kerbau-langit", 200, Rank.A, false, 1, when));
        Assert.Equal(3, profile.RecordTime("kerbau-langit", 400, Rank.C, false, 2, when));
        for (var i = 0; i < 3; i++) profile.RecordTime("kerbau-langit", 250 + i, Rank.B, false, 1, when);
        var board = profile.Leaderboard("kerbau-langit");
        Assert.Equal(Profile.LeaderboardSize, board.Count);
        Assert.Equal(200, board[0].Seconds);
        Assert.True(board.Zip(board.Skip(1)).All(x => x.First.Seconds <= x.Second.Seconds));
        Assert.Equal(0, profile.RecordTime("kerbau-langit", 999, Rank.C, false, 1, when));
        Assert.Equal("2045-08-17", board[0].Date);
    }

    [Fact]
    public void TheNinthGuardianStaysHiddenUntilAllEightFall()
    {
        var profile = new Profile();
        Assert.False(profile.IsUnlocked(RegionId.Nusantara));
        Assert.DoesNotContain(profile.VisibleRegions(), r => r.Secret);
        foreach (var r in Regions.Main) profile.RecordVictory(r.ColossusId, 300, Rank.B, false);
        Assert.True(profile.IsUnlocked(RegionId.Nusantara));
        Assert.Contains(profile.VisibleRegions(), r => r.Id == RegionId.Nusantara);
    }
}
