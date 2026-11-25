using System.Numerics;
using NusantaraColossi.Core;
using NusantaraColossi.Game.Rendering;

namespace NusantaraColossi.Game.UI;

/// <summary>
/// The one 3D world currently on screen. Menus show a "showcase" world (a guardian at rest, the
/// camera drifting around it); a hunt swaps in a full encounter. Only one world is alive at a time,
/// which keeps mobile GPU memory flat.
/// </summary>
public sealed class Stage : IDisposable
{
    public WorldRenderer? World { get; private set; }
    public Encounter? Encounter => World?.Encounter;
    public CameraRig Camera { get; private set; } = new();
    public bool IsShowcase { get; private set; }
    public string? ShowcaseColossus { get; private set; }

    /// <summary>Raised when the world (and therefore the scene object) changes.</summary>
    public event Action<WorldRenderer?>? WorldChanged;

    public void ShowShowcase(string colossusId, int quality)
    {
        if (IsShowcase && ShowcaseColossus == colossusId && World is not null) return;
        var region = Regions.ForColossus(colossusId);
        var encounter = new Encounter(region, Colossi.Get(colossusId), new EncounterOptions { Seed = 7 });
        Replace(new WorldRenderer(encounter, quality));
        IsShowcase = true;
        ShowcaseColossus = colossusId;
    }

    public WorldRenderer ShowHunt(Encounter encounter, int quality)
    {
        var world = new WorldRenderer(encounter, quality);
        Replace(world);
        IsShowcase = false;
        ShowcaseColossus = null;
        return world;
    }

    private void Replace(WorldRenderer world)
    {
        var old = World;
        World = world;
        Camera = new CameraRig();
        WorldChanged?.Invoke(world);
        old?.Dispose();
    }

    /// <summary>Advances the menu showcase: the guardian breathes and looks around; no AI runs.</summary>
    public void TickShowcase(float dt)
    {
        if (World is not { } world || !IsShowcase) return;
        var c = world.Encounter.Colossus;
        c.Pose.Time += dt;
        c.Pose.GaitWeight = Mathx.Damp(c.Pose.GaitWeight, .15f, 1, dt);
        c.Pose.GaitPhase += dt * .25f;
        c.Pose.HeadYaw = MathF.Sin(c.Pose.Time * .21f) * .35f;
        c.Evaluate();
        world.Encounter.SyncColossusBoxes();
        Camera.Showcase(c, world.Encounter.Region, dt);
        world.Update(dt, Camera, 0, false);
    }

    public void Clear()
    {
        var old = World;
        World = null;
        IsShowcase = false;
        ShowcaseColossus = null;
        WorldChanged?.Invoke(null);
        old?.Dispose();
    }

    public void Dispose() => Clear();
}
