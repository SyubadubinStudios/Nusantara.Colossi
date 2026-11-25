using System.Numerics;
using NusantaraColossi.Core;

namespace NusantaraColossi.Core.Tests;

public class GameplayTests
{
    private const float Dt = 1 / 60f;

    private static (Encounter Encounter, Player Player) Hunt(string colossusId, bool authority = true)
    {
        var e = new Encounter(Regions.ForColossus(colossusId), Colossi.Get(colossusId), new EncounterOptions { IsAuthority = authority });
        return (e, e.AddPlayer("Arka"));
    }

    private static int Slot(Encounter e, string partId) => e.Climb.StaticCount + Array.FindIndex(e.Blueprint.Parts, p => p.Id == partId);

    private static void AttachTo(Encounter e, Player p, string partId, Vector3 local)
    {
        var slot = Slot(e, partId);
        Assert.True(e.Climb.Project(slot, e.Climb[slot].ToWorld(local), true, out var contact, out _));
        p.Contact = contact;
        p.Mode = PlayerMode.Attached;
    }

    [Fact]
    public void ClimbsFromLegOntoTheBuffalosBack()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        AttachTo(e, p, "leg-fl", new Vector3(-2.4f, -5, 0));
        Assert.False(e.Climb.IsStanding(p.Contact));
        var input = new PlayerInput { Grip = true, Move = new Vector2(0, 1), CameraForward = Vector3.UnitZ };
        var reachedBack = false;
        for (var i = 0; i < 60 * 25 && !reachedBack; i++)
        {
            e.Step([input], Dt);
            Assert.Equal(PlayerMode.Attached, p.Mode);
            reachedBack = p.IsStanding && e.Climb[p.Contact.Box].Owner == 0;
        }
        Assert.True(reachedBack, $"stuck on part {e.Climb[p.Contact.Box].Index} face {p.Contact.Axis}/{p.Contact.Sign} at {p.Position}");
        Assert.True(p.Position.Y > 20, "the back of Kerbau Langit is above 20 m");
    }

    [Fact]
    public void ClimbingWrapsAroundEdges()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        AttachTo(e, p, "body", new Vector3(-6.5f, 0, 0)); // left flank
        var startAxis = p.Contact.Axis;
        var input = new PlayerInput { Grip = true, Move = new Vector2(1, 0), CameraForward = Vector3.UnitZ };
        for (var i = 0; i < 60 * 12; i++) e.Step([input], Dt);
        Assert.Equal(PlayerMode.Attached, p.Mode);
        Assert.True(p.Contact.Axis != startAxis || e.Climb[p.Contact.Box].Index != 0, "moving sideways along a flank must wrap onto the next face");
    }

    [Fact]
    public void ReleasingGripOnAWallFalls()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        AttachTo(e, p, "body", new Vector3(-6.5f, 0, 0));
        e.Step([new PlayerInput { Grip = true }], Dt);
        e.Step([new PlayerInput { Grip = false }], Dt);
        Assert.Equal(PlayerMode.Air, p.Mode);
        for (var i = 0; i < 300 && p.Mode == PlayerMode.Air; i++) e.Step([new PlayerInput()], Dt);
        Assert.Equal(PlayerMode.Ground, p.Mode);
    }

    [Fact]
    public void GripStaminaRunsOutAndDropsTheClimber()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        AttachTo(e, p, "body", new Vector3(0, -6, 0)); // belly: hanging drains fastest
        var input = new PlayerInput { Grip = true, Move = new Vector2(0, .2f), CameraForward = Vector3.UnitZ };
        var fell = false;
        for (var i = 0; i < 60 * 40 && !fell; i++)
        {
            e.Step([input], Dt);
            fell = e.Events.Any(x => x.Kind == GameEventKind.GripLost);
        }
        Assert.True(fell);
    }

    [Fact]
    public void FallingFromHeightHurts()
    {
        var (e, p) = Hunt("kerbau-langit", authority: false);
        p.Mode = PlayerMode.Air;
        p.Position = p.Position + new Vector3(0, 15, 0);
        for (var i = 0; i < 300 && p.Mode == PlayerMode.Air; i++) e.Step([new PlayerInput()], Dt);
        Assert.True(p.Health < p.Stats.MaxHealth);
        Assert.Contains(e.Events, x => x.Kind == GameEventKind.Land);
    }

    [Fact]
    public void SealedCoresIgnoreStabsUntilGarudaScans()
    {
        var (e, _) = Hunt("kerbau-langit");
        var core = e.Blueprint.Cores[0];
        var (position, normal) = ColossusRig.CoreWorld(e.Blueprint, core, e.Colossus.PartWorld);
        var body = position + normal * .9f;
        e.ResolveStab(body, true, 1, 0, 0);
        Assert.Equal(core.Health, e.Colossus.Cores[0].Health);
        Assert.Contains(e.Events, x => x.Kind == GameEventKind.StabSealed);

        e.Colossus.Scan(20);
        e.ResolveStab(body, true, 1, 0, 0);
        Assert.True(e.Colossus.Cores[0].Health < core.Health);
    }

    [Fact]
    public void StabbingOnTheHeartbeatResonates()
    {
        var (e, _) = Hunt("kerbau-langit");
        e.Colossus.Scan(30);
        var core = e.Blueprint.Cores[1];
        var (position, normal) = ColossusRig.CoreWorld(e.Blueprint, core, e.Colossus.PartWorld);
        var period = e.Colossus.CorePulsePeriod;
        e.ResolveStab(position + normal, true, .5f, 0, period * .5f); // off-beat
        var offBeat = core.Health - e.Colossus.Cores[1].Health;
        e.ResolveStab(position + normal, true, .5f, 0, period); // on the beat
        var onBeat = core.Health - offBeat - e.Colossus.Cores[1].Health;
        Assert.Equal(offBeat * 1.5f, onBeat, 2);
        Assert.Equal(1, e.Resonances);
    }

    [Fact]
    public void PlatedCoresNeedAHack()
    {
        var (e, _) = Hunt("gedung-titan");
        e.Colossus.Scan(30);
        var plated = Array.FindIndex(e.Blueprint.Cores, c => c.Plated);
        var core = e.Blueprint.Cores[plated];
        var (position, normal) = ColossusRig.CoreWorld(e.Blueprint, core, e.Colossus.PartWorld);
        e.ResolveStab(position + normal, true, 1, 0, 0);
        Assert.Equal(core.Health, e.Colossus.Cores[plated].Health);
        Assert.Contains(e.Events, x => x.Kind == GameEventKind.StabDeflected);
        e.Colossus.Stun(5);
        e.ResolveStab(position + normal, true, 1, 0, 0);
        Assert.True(e.Colossus.Cores[plated].Health < core.Health);
    }

    [Fact]
    public void DestroyingEveryCoreWinsTheHunt()
    {
        var (e, p) = Hunt("kerbau-langit");
        var rageEvents = 0;
        for (var round = 0; round < 40 && !e.Colossus.IsDefeated; round++)
        {
            e.Colossus.Scan(30);
            for (var i = 0; i < e.Blueprint.Cores.Length; i++)
            {
                if (e.Colossus.Cores[i].Destroyed) continue;
                var (position, normal) = ColossusRig.CoreWorld(e.Blueprint, e.Blueprint.Cores[i], e.Colossus.PartWorld);
                e.ResolveStab(position + normal, true, 1, 0, 0);
                rageEvents += e.Events.Count(x => x.Kind == GameEventKind.Rage);
                break;
            }
            e.Step([new PlayerInput()], Dt);
        }
        Assert.True(e.Colossus.IsDefeated);
        Assert.Equal(1, rageEvents);
        var victory = false;
        for (var i = 0; i < 60 * 10 && !victory; i++)
        {
            e.Step([new PlayerInput()], Dt);
            victory = e.Events.Any(x => x.Kind == GameEventKind.Victory);
        }
        Assert.True(victory);
        Assert.Equal(EncounterPhase.Victory, e.Phase);
        Assert.NotEqual(PlayerMode.Dead, p.Mode);
    }

    [Fact]
    public void ClimbersProvokeAShakeOff()
    {
        var (e, p) = Hunt("orangutan-purba");
        AttachTo(e, p, "torso", new Vector3(0, 0, -6.5f));
        var shook = false;
        var input = new PlayerInput { Grip = true, CameraForward = Vector3.UnitZ };
        for (var i = 0; i < 60 * 12 && !shook; i++)
        {
            e.Step([input], Dt);
            shook = e.Events.Any(x => x.Kind == GameEventKind.ShakeStart);
        }
        Assert.True(shook);
    }

    [Fact]
    public void ColossusHuntsAndStompsAGroundedPlayer()
    {
        var (e, p) = Hunt("kerbau-langit");
        var attacked = false;
        for (var i = 0; i < 60 * 90 && !attacked; i++)
        {
            var toward = Mathx.Flat(e.Colossus.Pose.Position - p.Position);
            var input = toward.Length() > 20 ? new PlayerInput { Move = new Vector2(0, 1), CameraForward = toward } : new PlayerInput { CameraForward = toward };
            e.Step([input], Dt);
            attacked = e.Events.Any(x => x.Kind is GameEventKind.StompImpact or GameEventKind.ChargeStart);
        }
        Assert.True(attacked, $"colossus state {e.Colossus.State} at {e.Colossus.Pose.Position}, player at {p.Position}");
    }

    [Fact]
    public void GarudaAbilitiesRespectCooldownsAndUnlocks()
    {
        var region = Regions.Get(RegionId.Bromo);
        var e = new Encounter(region, Colossi.Get(region.ColossusId), new EncounterOptions { Abilities = DroneAbilities.Scan });
        var p = e.AddPlayer("Arka");
        Assert.True(e.UseAbility(p, DroneAbilities.Scan, Vector3.UnitZ));
        Assert.False(e.UseAbility(p, DroneAbilities.Scan, Vector3.UnitZ));
        Assert.False(e.UseAbility(p, DroneAbilities.Hack, Vector3.UnitZ));
        Assert.Contains(e.Events, x => x.Kind == GameEventKind.HackDenied);
        Assert.All(e.Colossus.Cores, c => Assert.True(c.SealOpen > 0));
    }

    [Fact]
    public void ToxicGasHurtsInIjen()
    {
        var (e, p) = Hunt("penjaga-api-biru", authority: false);
        e.Step([new PlayerInput()], Dt);
        var cloud = e.GasClouds.ToArray().First(c => c.Radius > 0);
        p.Position = cloud.Center;
        var before = p.Health;
        for (var i = 0; i < 60; i++) e.Step([new PlayerInput()], Dt);
        Assert.True(p.Health < before);
    }
}
