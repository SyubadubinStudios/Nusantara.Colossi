using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraColossi.Game.Rendering;

/// <summary>Signature mechanics (flood, shards, cannons, data bolts), vehicles and the GARUDA glider.</summary>
public sealed partial class WorldRenderer
{
    private const int StrikePool = 32;
    private readonly Node[] strikeBodies = new Node[StrikePool];
    private readonly Node[] strikeRings = new Node[StrikePool];
    private Material strikeRingMaterial = null!;
    private Material strikeRingBoltMaterial = null!;
    private readonly List<(Node Root, Node[] Wheels, VehicleKind Kind)> vehicles = [];
    private readonly Node[] gliders = new Node[Coop.MaxPlayers];
    private Emitter? boltSparks;
    private float floodRestLevel;

    private void BuildSignatures()
    {
        var signature = Encounter.Blueprint.Signature;
        if (signature == Signature.Flood && water is null)
        {
            var size = Encounter.Region.ArenaRadius * 2.4f;
            floodRestLevel = Encounter.WaterLevel;
            water = new WaterSurface(Scene, new Vector3(0, Encounter.WaterLevel, 0), size, Encounter.WaterLevel - 6, Quality == 0 ? 64 : 112, Quality == 0 ? 80 : 128)
            {
                Style = WaterStyle.Ocean with { DeepColor = new(.05f, .16f, .1f), ShallowColor = new(.3f, .5f, .32f), Absorption = .2f, Detail = .04f, Caustics = .4f },
                SunDirection = Encounter.Region.SunDirection
            };
            water.Node.CastShadow = false;
            water.Node.Visible = false;
        }

        if (signature is Signature.GlassShards or Signature.Cannons or Signature.Firewall)
        {
            var bodyMaterial = signature switch
            {
                Signature.GlassShards => Mat("shard", () => MaterialOptions.Pbr(new Vector4(.6f, .85f, .95f, .75f), .1f, .05f) with { AlphaMode = AlphaMode.Blend, Emissive = new Vector3(.3f, .7f, .8f), EmissiveIntensity = 1.2f }),
                Signature.Cannons => Mat("cannonball", () => MaterialOptions.Pbr(new Vector4(.08f, .08f, .09f, 1), .9f, .35f) with { Emissive = new Vector3(1f, .45f, .15f), EmissiveIntensity = .6f }),
                _ => Mat("databolt", () => MaterialOptions.Basic(new Vector4(.35f, 1f, .95f, .9f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 6 })
            };
            var bodyGeometry = signature == Signature.Cannons ? sphere : box;
            strikeRingMaterial = Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.88f, .38f, .23f, .7f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 3 });
            strikeRingBoltMaterial = Scene.CreateMaterial(MaterialOptions.Basic(new Vector4(.3f, .95f, 1f, .7f)) with { AlphaMode = AlphaMode.Additive, DepthWrite = false, EmissiveIntensity = 3 });
            for (var i = 0; i < StrikePool; i++)
            {
                strikeBodies[i] = Mesh(bodyGeometry, bodyMaterial, Vector3.Zero, Vector3.One, name: $"strike-{i}");
                strikeBodies[i].Visible = false;
                strikeBodies[i].CastShadow = signature == Signature.Cannons;
                strikeRings[i] = Mesh(torus, signature == Signature.Firewall ? strikeRingBoltMaterial : strikeRingMaterial, Vector3.Zero, Vector3.One);
                strikeRings[i].Visible = false;
                strikeRings[i].CastShadow = false;
                strikeRings[i].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            }
            if (signature == Signature.Firewall)
            {
                boltSparks = AddEmitter(600, ParticleEffect.GlowMaterial(particleShader, new Vector3(.4f, 1, .95f), new Vector3(.1f, .4f, .5f), 3), e =>
                {
                    e.Shape = EmitterShape.Sphere;
                    e.EmitterRadius = .4f;
                    e.Lifetime = .7f;
                    e.Size = .14f;
                    e.Radial = true;
                    e.Direction = Vector3.UnitY;
                    e.Spread = 1;
                    e.Gravity = new Vector3(0, -6, 0);
                    e.StretchBySpeed = .08f;
                });
            }
        }

        // Vehicles: procedural trail bikes and jet skis, the hunter's colour on the tank.
        var metal = Mat("vehicle-metal", () => MaterialOptions.Pbr(new Vector4(.12f, .13f, .14f, 1), .8f, .35f));
        var rubber = Mat("vehicle-rubber", () => MaterialOptions.Pbr(new Vector4(.03f, .03f, .03f, 1), 0, .9f));
        var paint = Mat("vehicle-paint", () => MaterialOptions.Pbr(new Vector4(.78f, .55f, .2f, 1), .4f, .3f));
        var glow = Mat("vehicle-glow", () => MaterialOptions.Basic(new Vector4(.3f, .95f, .9f, 1)) with { EmissiveIntensity = 3 });
        for (var i = 0; i < Encounter.VehicleCount; i++)
        {
            var kind = Encounter.Vehicles[i].Kind;
            var root = Scene.CreateNode(name: $"vehicle-{i}");
            var wheels = Array.Empty<Node>();
            if (kind == VehicleKind.Trail)
            {
                Mesh(box, metal, new Vector3(0, .62f, 0), new Vector3(.22f, .3f, 1.1f), root).CastShadow = true;   // frame
                Mesh(box, paint, new Vector3(0, .9f, .25f), new Vector3(.3f, .22f, .5f), root).CastShadow = true;  // tank
                Mesh(box, rubber, new Vector3(0, .98f, -.3f), new Vector3(.26f, .08f, .6f), root);                  // seat
                Mesh(box, metal, new Vector3(0, 1.12f, .58f), new Vector3(.7f, .04f, .04f), root);                  // handlebar
                Mesh(box, glow, new Vector3(0, .95f, .75f), new Vector3(.12f, .08f, .04f), root);                   // headlight
                var front = Mesh(torus, rubber, new Vector3(0, .36f, .62f), new Vector3(.36f, .36f, 2.4f), root);
                var back = Mesh(torus, rubber, new Vector3(0, .36f, -.58f), new Vector3(.36f, .36f, 2.4f), root);
                front.Rotation = back.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
                wheels = [front, back];
            }
            else
            {
                Mesh(box, paint, new Vector3(0, .25f, 0), new Vector3(.55f, .32f, 1.4f), root).CastShadow = true;  // hull
                Mesh(box, paint, new Vector3(0, .28f, 1.25f), new Vector3(.38f, .24f, .4f), root).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -.35f); // nose
                Mesh(box, rubber, new Vector3(0, .6f, -.25f), new Vector3(.3f, .12f, .7f), root);                  // seat
                Mesh(box, metal, new Vector3(0, .95f, .5f), new Vector3(.7f, .04f, .04f), root);                   // handlebar
                Mesh(box, glow, new Vector3(0, .5f, 1.38f), new Vector3(.3f, .05f, .03f), root);
            }
            vehicles.Add((root, wheels, kind));
        }

        // The GARUDA glider: a cyan-edged wing above each hunter, hidden until opened.
        var wingMaterial = Mat("glider", () => MaterialOptions.Pbr(new Vector4(.85f, .7f, .35f, .92f), .3f, .4f) with { AlphaMode = AlphaMode.Blend, CullMode = CullMode.None, Emissive = new Vector3(.2f, .6f, .6f), EmissiveIntensity = .4f });
        var edgeMaterial = Mat("glider-edge", () => MaterialOptions.Basic(new Vector4(.3f, .95f, .9f, 1)) with { EmissiveIntensity = 3 });
        for (var i = 0; i < gliders.Length && i < hunters.Count; i++)
        {
            var wing = hunters[i].Root.CreateChild($"glider-{i}");
            wing.Position = new Vector3(0, 2.2f, 0);
            Mesh(box, wingMaterial, Vector3.Zero, new Vector3(4.6f, .05f, 1.3f), wing).CastShadow = true;
            Mesh(box, edgeMaterial, new Vector3(0, 0, .66f), new Vector3(4.7f, .06f, .05f), wing);
            Mesh(box, metal, new Vector3(0, -.45f, 0), new Vector3(.04f, .9f, .04f), wing);
            wing.Visible = false;
            gliders[i] = wing;
        }
    }

    private void UpdateSignatures(float dt)
    {
        var e = Encounter;
        if (e.Blueprint.Signature == Signature.Flood && water is not null)
        {
            var flood = e.Flood;
            water.Node.Visible = flood > .01f;
            // The surface mesh is built at the dry level; raise the whole node with the flood.
            water.Node.Position = new Vector3(0, e.WaterLevel - floodRestLevel, 0);
        }

        var strikes = e.Strikes;
        var t = e.SignatureTime;
        for (var i = 0; i < StrikePool && strikeBodies[i] is not null; i++)
        {
            var active = i < strikes.Length && strikes[i].Active;
            strikeBodies[i].Visible = active;
            strikeRings[i].Visible = active;
            if (!active) continue;
            var s = strikes[i];
            var p = s.Progress(t);
            var body = strikeBodies[i];
            body.Position = s.PositionAt(t);
            switch (s.Kind)
            {
                case StrikeKind.Shard:
                    body.Scale = new Vector3(1.6f, .1f, 2.4f);
                    body.Rotation = Quaternion.CreateFromYawPitchRoll(t * 3 + i, t * 2.3f + i * .7f, 0);
                    break;
                case StrikeKind.Cannonball:
                    body.Scale = new Vector3(.55f);
                    break;
                default:
                    // A falling bolt: a thin streak that grows as it nears the target.
                    body.Scale = new Vector3(.12f, 1.5f + p * 4, .12f);
                    body.Rotation = RotationFromTo(Vector3.UnitY, Mathx.SafeNormalize(s.Origin - s.Target, Vector3.UnitY));
                    break;
            }
            // Telegraph: a ring that tightens and brightens on the landing spot.
            var ring = strikeRings[i];
            ring.Position = s.Target + Vector3.UnitY * .25f;
            var radius = s.Radius * (1.6f - .6f * p);
            ring.Scale = new Vector3(radius, radius, radius * 5);
        }
        var pulse = .45f + .4f * MathF.Sin(Time * 16);
        strikeRingMaterial?.Update(o => o with { BaseColor = new Vector4(.88f, .38f, .23f, pulse) });
        strikeRingBoltMaterial?.Update(o => o with { BaseColor = new Vector4(.3f, .95f, 1f, pulse) });

        for (var i = 0; i < vehicles.Count; i++)
        {
            ref readonly var v = ref e.Vehicles[i];
            var (root, wheels, kind) = vehicles[i];
            root.Position = v.Position;
            var lean = v.Rider >= 0 && v.Rider < e.Players.Count ? Math.Clamp(Mathx.WrapAngle(e.Players[v.Rider].Yaw - v.Yaw) * 2, -.3f, .3f) : 0;
            var bob = kind == VehicleKind.JetSki ? MathF.Sin(Time * 2.1f + i) * .06f : 0;
            root.Rotation = Quaternion.CreateFromYawPitchRoll(v.Yaw, kind == VehicleKind.JetSki ? -MathF.Min(.18f, v.Speed * .012f) : 0, lean);
            root.Position += Vector3.UnitY * bob;
            foreach (var wheel in wheels)
                wheel.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -Time * v.Speed / .36f);
        }

        for (var i = 0; i < gliders.Length; i++)
        {
            if (gliders[i] is null) continue;
            gliders[i].Visible = i < e.Players.Count && e.Players[i].Gliding;
        }
    }

    private void OnSignatureEvent(in GameEvent ev)
    {
        switch (ev.Kind)
        {
            case GameEventKind.StrikeImpact:
                var kind = (StrikeKind)(int)ev.Value;
                var wet = !float.IsNaN(Encounter.WaterLevel) && ev.Position.Y <= Encounter.WaterLevel + .1f;
                if (wet)
                {
                    Burst(splash, ev.Position, kind == StrikeKind.Cannonball ? 160 : 60);
                    water?.Splash(ev.Position, kind == StrikeKind.Cannonball ? 2.4f : 1.2f, .3f);
                }
                else
                {
                    dust.Effect.EmitterRadius = Signatures.StrikeRadius(kind) * .5f;
                    Burst(dust, ev.Position + Vector3.UnitY * .3f, Quality == 0 ? 30 : 80);
                }
                if (kind == StrikeKind.DataBolt && boltSparks is not null) Burst(boltSparks, ev.Position, 120);
                else Burst(sparks, ev.Position, kind == StrikeKind.Shard ? 60 : 30);
                break;
            case GameEventKind.Volley when Encounter.Blueprint.Signature == Signature.Cannons:
                Burst(dust, ev.Position + Vector3.UnitY * 5, 40); // gun smoke
                break;
            case GameEventKind.DiveStart or GameEventKind.Surface:
                Burst(splash, ev.Position with { Y = Encounter.Region.WaterLevel }, 260);
                water?.Splash(ev.Position, 6, .5f);
                break;
        }
    }
}
