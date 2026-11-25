using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraColossi.Game.Rendering;

public sealed partial class WorldRenderer
{
    private sealed record Emitter(ParticleEffect Effect, Geometry Geometry, Node Node);

    private readonly List<CharacterVisual> hunters = [];
    private readonly List<DroneVisual> drones = [];
    private readonly List<Emitter> emitters = [];
    private Emitter sparks = null!, dust = null!, ichor = null!, splash = null!, embers = null!;
    private readonly List<(Emitter Emitter, int Cloud)> gasPlumes = [];
    private Node scanSphere = null!;
    private Material scanMaterial = null!;
    private float scanTime = -1;
    private Vector3 scanOrigin;
    private Node hackBeam = null!;
    private float hackTime;
    private CharacterVisual? decoy;
    private float decoyTime;
    private Node telegraph = null!;
    private Material telegraphMaterial = null!;
    private Node roarRing = null!;
    private float roarTime = -1;
    private float roarRadius;
    private Vector3 roarOrigin;
    private Node? rain;
    private readonly Random fxRandom = new(7);

    private void BuildCharacters()
    {
        Color4[] jackets = [new(.14f, .3f, .32f), new(.42f, .18f, .14f), new(.22f, .24f, .4f), new(.35f, .32f, .12f)];
        for (var i = 0; i < Coop.MaxPlayers; i++)
        {
            hunters.Add(new CharacterVisual(this, Scene, $"hunter-{i}", jackets[i].Rgb));
            hunters[i].Root.Visible = false;
            drones.Add(new DroneVisual(Scene, $"garuda-{i}"));
            drones[i].Root.Visible = false;
        }
    }

    private readonly record struct Color4(float R, float G, float B)
    {
        public Vector3 Rgb => new(R, G, B);
    }

    private Emitter AddEmitter(int capacity, MaterialOptions material, Action<ParticleEffect> configure)
    {
        var effect = new ParticleEffect(capacity, fxRandom.Next()) { Rate = 0 };
        configure(effect);
        var geometry = effect.CreateGeometry(Scene);
        var node = Scene.AddMesh(geometry, Scene.CreateMaterial(material));
        node.CastShadow = false;
        var emitter = new Emitter(effect, geometry, node);
        emitters.Add(emitter);
        return emitter;
    }

    private void AddPlume(Vector3 origin, Vector3 color, float radius, float rate)
    {
        AddEmitter(Quality == 0 ? 200 : 500, ParticleEffect.SmokeMaterial(smokeShader, color, .3f), e =>
        {
            e.Origin = origin;
            e.Shape = EmitterShape.Disc;
            e.EmitterRadius = radius;
            e.Rate = rate * (Quality == 0 ? .5f : 1);
            e.Lifetime = 18;
            e.Size = 40;
            e.Growth = 3;
            e.Direction = Vector3.UnitY;
            e.Spread = .3f;
            e.Gravity = new Vector3(2, 3, 0);
            e.Turbulence = 2;
            e.TurbulenceScale = .02f;
        });
    }

    private void BuildEffects()
    {
        var glow = Encounter.Blueprint.GlowColor;
        sparks = AddEmitter(900, ParticleEffect.GlowMaterial(particleShader, Vector3.Lerp(glow, Vector3.One, .4f), glow * .4f, 3.2f), e =>
        {
            e.Shape = EmitterShape.Sphere;
            e.EmitterRadius = .3f;
            e.Lifetime = .9f;
            e.Variation = .5f;
            e.Size = .16f;
            e.Growth = -.8f;
            e.Radial = true;
            e.Direction = Vector3.UnitY;
            e.Spread = 1;
            e.Gravity = new Vector3(0, -9, 0);
            e.Drag = 1.2f;
            e.StretchBySpeed = .06f;
        });
        dust = AddEmitter(Quality == 0 ? 500 : 1400, ParticleEffect.SmokeMaterial(smokeShader, Vector3.Lerp(Encounter.Region.GroundLow, Vector3.One, .25f), .45f), e =>
        {
            e.Shape = EmitterShape.Ring;
            e.EmitterRadius = 4;
            e.Lifetime = 3.2f;
            e.Variation = .4f;
            e.Size = 4.5f;
            e.Growth = 2.6f;
            e.Radial = true;
            e.Direction = Vector3.UnitY;
            e.Spread = .4f;
            e.Gravity = new Vector3(0, .6f, 0);
            e.Drag = 1.6f;
            e.Turbulence = 1.2f;
            e.TurbulenceScale = .15f;
        });
        ichor = AddEmitter(700, ParticleEffect.SmokeMaterial(smokeShader, new Vector3(.04f, .03f, .05f), .7f), e =>
        {
            e.Shape = EmitterShape.Sphere;
            e.EmitterRadius = .6f;
            e.Lifetime = 3.5f;
            e.Size = 1.4f;
            e.Growth = 1.4f;
            e.Direction = Vector3.UnitY;
            e.Spread = .5f;
            e.Gravity = new Vector3(0, 2.4f, 0);
            e.Turbulence = 2.5f;
            e.TurbulenceScale = .4f;
        });
        splash = AddEmitter(500, ParticleEffect.GlowMaterial(particleShader, new Vector3(.8f, .9f, .95f), new Vector3(.3f, .45f, .5f), .8f), e =>
        {
            e.Shape = EmitterShape.Disc;
            e.EmitterRadius = .4f;
            e.Lifetime = .8f;
            e.Size = .12f;
            e.Direction = Vector3.UnitY;
            e.Spread = .6f;
            e.Rise = 4;
            e.Gravity = new Vector3(0, -12, 0);
        });
        if (Encounter.Region.Hazard == Hazard.ToxicGas || Encounter.Region.Weather == Weather.Dawn)
        {
            embers = AddEmitter(600, ParticleEffect.GlowMaterial(particleShader, new Vector3(.5f, .8f, 1), new Vector3(.1f, .2f, .6f), 1.6f), e =>
            {
                e.Shape = EmitterShape.Disc;
                e.EmitterRadius = Encounter.Region.ArenaRadius * .8f;
                e.Rate = 60;
                e.Lifetime = 7;
                e.Size = .08f;
                e.Direction = Vector3.UnitY;
                e.Gravity = new Vector3(.3f, .5f, 0);
                e.Turbulence = 1;
            });
        }
        for (var i = 0; i < Encounter.GasClouds.Length; i++)
        {
            var plume = AddEmitter(Quality == 0 ? 160 : 380, ParticleEffect.SmokeMaterial(smokeShader, new Vector3(.75f, .72f, .38f), .3f), e =>
            {
                e.Shape = EmitterShape.Disc;
                e.Rate = Quality == 0 ? 7 : 16;
                e.Lifetime = 9;
                e.Size = 9;
                e.Growth = 1.5f;
                e.Direction = Vector3.UnitY;
                e.Spread = .6f;
                e.Gravity = new Vector3(0, .6f, 0);
                e.Turbulence = 1.4f;
                e.TurbulenceScale = .05f;
            });
            gasPlumes.Add((plume, i));
        }

        scanMaterial = Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.25f, .85f, .82f, .35f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, CullMode = CullMode.None, Wireframe = false, EmissiveIntensity = 2 });
        scanSphere = Mesh(sphere, scanMaterial, Vector3.Zero, Vector3.One);
        scanSphere.Visible = false;
        scanSphere.CastShadow = false;
        hackBeam = Mesh(cylinder, Mat("hack-beam", () => MaterialOptions.Basic(new Vector4(.3f, 1, .95f, .8f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 5 }), Vector3.Zero, Vector3.One);
        hackBeam.Visible = false;
        hackBeam.CastShadow = false;
        telegraphMaterial = Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.88f, .38f, .23f, .8f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 3 });
        telegraph = Mesh(torus, telegraphMaterial, Vector3.Zero, Vector3.One);
        telegraph.Visible = false;
        telegraph.CastShadow = false;
        roarRing = Mesh(torus, Mat("roar", () => MaterialOptions.Basic(new Vector4(Encounter.Blueprint.GlowColor, .5f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 2 }), Vector3.Zero, Vector3.One);
        roarRing.Visible = false;
        roarRing.CastShadow = false;

        if (Encounter.Region.Weather is Weather.Rain or Weather.Storm || Encounter.Region.Hazard == Hazard.Rain)
        {
            var count = Quality switch { 0 => 1200, 1 => 3000, _ => 6000 };
            var transforms = new Matrix4x4[count];
            for (var i = 0; i < count; i++)
            {
                var x = (fxRandom.NextSingle() * 2 - 1) * 45;
                var z = (fxRandom.NextSingle() * 2 - 1) * 45;
                var y = fxRandom.NextSingle() * 40;
                transforms[i] = Matrix4x4.CreateScale(.02f, .9f, .02f) * Matrix4x4.CreateRotationX(.08f) * Matrix4x4.CreateTranslation(x, y, z);
            }
            rain = Scene.AddMesh(box, Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.7f, .78f, .9f, .35f)) with { AlphaMode = AlphaMode.Blend, DepthWrite = false }), name: "rain");
            rain.SetInstances(transforms);
            rain.CastShadow = false;
        }
    }

    private void UpdateCharacters(float dt, int localSlot)
    {
        var players = Encounter.Players;
        for (var i = 0; i < hunters.Count; i++)
        {
            if (i < players.Count)
            {
                hunters[i].Update(players[i], dt);
                drones[i].Update(players[i], dt, Time);
            }
            else
            {
                hunters[i].Root.Visible = false;
                drones[i].Hide();
            }
        }
    }

    /// <summary>React to simulation events with particles, rings and lights.</summary>
    public void OnEvent(in GameEvent e)
    {
        OnSignatureEvent(e);
        switch (e.Kind)
        {
            case GameEventKind.CoreHit:
            case GameEventKind.CoreResonance:
                Burst(sparks, e.Position, e.Kind == GameEventKind.CoreResonance ? 160 : 70);
                Burst(ichor, e.Position, 18);
                for (var i = 0; i < Encounter.Blueprint.Cores.Length; i++)
                    if (Vector3.Distance(ColossusRig.CoreWorld(Encounter.Blueprint, Encounter.Blueprint.Cores[i], Encounter.Colossus.PartWorld).Position, e.Position) < .5f)
                        FlashCore(i);
                break;
            case GameEventKind.CoreDestroyed:
                Burst(sparks, e.Position, 400);
                Burst(ichor, e.Position, 160);
                break;
            case GameEventKind.StabFlesh:
            case GameEventKind.StabDeflected:
            case GameEventKind.StabSealed:
                Burst(sparks, e.Position, 20);
                break;
            case GameEventKind.StompImpact:
                dust.Effect.EmitterRadius = MathF.Max(2, e.Value * .6f);
                Burst(dust, e.Position + Vector3.UnitY * .5f, Quality == 0 ? 80 : 220);
                break;
            case GameEventKind.ChargeHit:
            case GameEventKind.Land when e.Value > .8f:
                dust.Effect.EmitterRadius = 1;
                Burst(dust, e.Position, 25);
                break;
            case GameEventKind.Splash:
                Burst(splash, e.Position with { Y = float.IsNaN(Encounter.WaterLevel) ? e.Position.Y : Encounter.WaterLevel }, e.Value > 4 ? 140 : 20);
                water?.Splash(e.Position, e.Value > 4 ? 1.2f : .4f, e.Value > 4 ? .3f : .08f);
                break;
            case GameEventKind.RoarWave:
                roarTime = 0;
                roarRadius = e.Value;
                roarOrigin = e.Position;
                break;
            case GameEventKind.Scan:
                scanTime = 0;
                scanOrigin = e.Slot < drones.Count ? drones[e.Slot].Position : e.Position;
                if (e.Slot < drones.Count) drones[e.Slot].Scan();
                break;
            case GameEventKind.Hack:
                hackTime = .9f;
                break;
            case GameEventKind.Decoy:
                decoy ??= new CharacterVisual(this, Scene, "decoy", Vector3.One, hologram: true);
                decoy.Root.Position = e.Position;
                decoyTime = 7;
                break;
            case GameEventKind.Collapse:
                dust.Effect.EmitterRadius = Encounter.Blueprint.Height * .4f;
                Burst(dust, Encounter.Colossus.Pose.Position, 400);
                break;
        }
    }

    private static void Burst(Emitter emitter, Vector3 position, int count)
    {
        emitter.Effect.Origin = position;
        emitter.Effect.Burst(count);
    }

    private void UpdateEffects(float dt, int localSlot, bool hunterInstinct)
    {
        UpdateEnvironment(dt);
        var cameraWorld = Camera.WorldMatrix;
        var gas = Encounter.GasClouds;
        foreach (var (plume, cloud) in gasPlumes)
        {
            var c = gas[cloud];
            plume.Effect.Origin = c.Center + Vector3.UnitY;
            plume.Effect.EmitterRadius = MathF.Max(1, c.Radius * .8f);
            plume.Effect.Rate = c.Radius > 0 ? (Quality == 0 ? 7 : 16) : 0;
        }
        foreach (var e in emitters)
        {
            e.Effect.Update(dt, Time);
            e.Effect.Upload(e.Geometry, cameraWorld);
        }

        // Scan: a cyan shell sweeps out from GARUDA across the whole arena.
        if (scanTime >= 0)
        {
            scanTime += dt;
            var t = scanTime / 1.6f;
            var radius = 2 + t * Encounter.ScanRange;
            scanSphere.Visible = t < 1;
            scanSphere.Position = scanOrigin;
            scanSphere.Scale = new Vector3(radius);
            scanMaterial.Update(o => o with { BaseColor = new Vector4(.25f, .85f, .82f, .35f * (1 - t)) });
            if (t >= 1) scanTime = -1;
        }

        hackTime = MathF.Max(0, hackTime - dt);
        var beaming = hackTime > 0 && localSlot < drones.Count;
        hackBeam.Visible = beaming;
        if (beaming)
        {
            var from = drones[localSlot].Position;
            var head = Encounter.Blueprint.HeadPart;
            var to = Encounter.Colossus.PartWorld[Math.Max(0, head)].Translation;
            var mid = (from + to) * .5f;
            var length = Vector3.Distance(from, to);
            hackBeam.Position = mid;
            hackBeam.Rotation = RotationFromTo(Vector3.UnitY, Vector3.Normalize(to - from));
            hackBeam.Scale = new Vector3(.08f + MathF.Sin(Time * 60) * .03f, length, .08f);
        }

        if (decoy is not null)
        {
            decoyTime = MathF.Max(0, decoyTime - dt);
            decoy.Root.Visible = decoyTime > 0 && MathF.Sin(Time * 30) > -.6f;
        }

        var tele = hunterInstinct ? Encounter.Colossus.Telegraph() : null;
        telegraph.Visible = tele is not null;
        if (tele is { } t2)
        {
            var y = Encounter.Region.Height(t2.Center.X, t2.Center.Z) + .3f;
            telegraph.Position = t2.Center with { Y = y };
            telegraph.Scale = new Vector3(t2.Radius, t2.Radius, t2.Radius * 6);
            telegraph.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            telegraphMaterial.Update(o => o with { BaseColor = new Vector4(.88f, .38f, .23f, .5f + .4f * MathF.Sin(Time * 14)) });
        }

        if (roarTime >= 0)
        {
            roarTime += dt;
            var t = roarTime / 1.1f;
            roarRing.Visible = t < 1;
            roarRing.Position = roarOrigin + Vector3.UnitY * 2;
            roarRing.Scale = new Vector3(roarRadius * t, roarRadius * t, 30);
            roarRing.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            if (t >= 1) roarTime = -1;
        }

        if (rain is not null)
        {
            var c = Camera.WorldPosition;
            rain.Position = new Vector3(c.X, c.Y - 18 - (Time * 26 % 20), c.Z);
        }
    }

    private void DisposeEffects()
    {
        emitters.Clear();
        fires.Clear();
    }
}
