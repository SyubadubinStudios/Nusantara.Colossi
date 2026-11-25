using System.Numerics;
using NusantaraColossi.Core;

namespace NusantaraColossi.Core.Tests;

public class BlueprintTests
{
    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var c in Colossi.All) data.Add(c.Id);
        return data;
    }

    [Fact]
    public void EveryRegionHasItsColossus()
    {
        Assert.Equal(9, Colossi.All.Length);
        Assert.Equal(8, Regions.Main.Count());
        Assert.True(Regions.Get(RegionId.Nusantara).Secret);
        Assert.Equal(Colossi.All.Select(c => c.Id).Order(), Regions.All.Select(r => r.ColossusId).Order());
        for (var i = 0; i < Regions.All.Length; i++) Assert.Equal((RegionId)i, Regions.All[i].Id);
    }

    [Theory, MemberData(nameof(Ids))]
    public void PartsAreTopologicallyOrdered(string id)
    {
        var bp = Colossi.Get(id);
        Assert.Equal(-1, bp.Parts[0].Parent);
        for (var i = 1; i < bp.Parts.Length; i++)
            Assert.InRange(bp.Parts[i].Parent, 0, i - 1);
        Assert.True(bp.HeadPart >= 0, "every colossus needs a head for the codex model and look-at");
    }

    [Theory, MemberData(nameof(Ids))]
    public void CoresSitOnReachableFaces(string id)
    {
        var bp = Colossi.Get(id);
        var colossus = new Colossus(bp, Regions.ForColossus(id), 1);
        Assert.InRange(bp.Cores.Length, 2, 4);
        foreach (var core in bp.Cores)
        {
            var part = bp.Parts[core.Part];
            var (_, normal) = ColossusRig.CoreWorld(bp, core, colossus.PartWorld);
            var standable = normal.Y > .7f;
            Assert.True(part.IsClimbable(core.FaceAxis, core.FaceSign) || standable, $"{id}/{core.Id} is on a face nobody can reach");
        }
    }

    [Theory, MemberData(nameof(Ids))]
    public void RestPoseStandsOnTheGround(string id)
    {
        var bp = Colossi.Get(id);
        var region = Regions.ForColossus(id);
        var world = new Matrix4x4[bp.Parts.Length];
        var pose = new ColossusPose { Position = new Vector3(0, 0, 0) };
        ColossusRig.Evaluate(bp, pose, world);
        var lowest = float.MaxValue;
        for (var i = 0; i < bp.Parts.Length; i++)
        {
            var part = bp.Parts[i];
            var box = ColossusRig.BoxWorld(part, world[i]);
            for (var corner = 0; corner < 8; corner++)
            {
                var c = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1) * part.Half;
                lowest = MathF.Min(lowest, Vector3.Transform(c, box).Y);
            }
        }
        if (bp.Floats) Assert.True(lowest < 0, $"{id} should sit in the water (lowest {lowest:0.0})");
        else Assert.InRange(lowest, -2.5f, 1.5f);
        Assert.True(region.ArenaRadius > bp.Height, "arena must be larger than its guardian");
    }

    [Theory, MemberData(nameof(Ids))]
    public void RigIsFiniteAcrossEveryAttack(string id)
    {
        var bp = Colossi.Get(id);
        var world = new Matrix4x4[bp.Parts.Length];
        foreach (var attack in Enum.GetValues<AttackKind>())
            for (var p = 0f; p <= 1; p += .1f)
            {
                var pose = new ColossusPose { Attack = attack, AttackProgress = p, GaitPhase = p * 3, GaitWeight = 1.4f, Time = p * 10, Shake = p, Kneel = p, Collapse = p };
                ColossusRig.Evaluate(bp, pose, world);
                foreach (var m in world) Assert.False(float.IsNaN(m.M11 + m.M41 + m.M42 + m.M43));
            }
    }

    [Fact]
    public void StompLiftsThenSlams()
    {
        Assert.Equal(0, ColossusRig.Lift(0));
        Assert.Equal(1, ColossusRig.Lift(.55f), 3);
        Assert.Equal(0, ColossusRig.Lift(ColossusRig.StompImpact));
        Assert.Equal(0, ColossusRig.Lift(.9f));
    }

    [Fact]
    public void TerrainIsDeterministicAndWalledAtTheEdge()
    {
        foreach (var r in Regions.All)
        {
            Assert.Equal(r.Height(12.3f, -40.1f), r.Height(12.3f, -40.1f));
            Assert.True(r.Height(0, r.ArenaRadius * 1.6f) > r.Height(0, 0) + 10, $"{r.Id} needs a horizon wall");
            var spawn = r.SpawnPoint;
            Assert.DoesNotContain(r.Obstacles, o => MathF.Abs(o.Center.X - spawn.X) < o.Half.X + 1 && MathF.Abs(o.Center.Z - spawn.Z) < o.Half.Z + 1);
        }
    }
}
