using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraColossi.Game.Rendering;

public sealed partial class WorldRenderer
{
    private readonly List<FireEffect> fires = [];
    private readonly List<Node> fireLights = [];

    private MaterialOptions StoneOptions(Vector3 color, float relief, Vector3 moss, float mossAmount, float seed, bool wood = false, bool fur = false, bool glass = false, Vector3 glow = default, float glowIntensity = 0) =>
        MaterialOptions.Pbr(new Vector4(color, 1), 0, .85f) with
        {
            Shader = colossusShader,
            Custom0 = new Vector4(color, relief),
            Custom1 = new Vector4(moss, mossAmount),
            Custom2 = new Vector4(fur ? 1 : 0, wood ? 1 : 0, glass ? 1 : 0, seed),
            Custom3 = new Vector4(glow, glowIntensity)
        };

    private void BuildEnvironment()
    {
        var region = Encounter.Region;
        var size = region.ArenaRadius * 3.1f;
        var resolution = Quality switch { 0 => 128, 1 => 192, _ => 256 };
        var wet = region.Weather is Weather.Rain or Weather.Storm || region.Hazard == Hazard.Rain ? 1f : 0f;
        var terrain = Scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0, .9f) with
        {
            Shader = terrainShader,
            Custom0 = new Vector4(region.GroundLow, 0),
            Custom1 = new Vector4(region.GroundHigh, 0),
            Custom2 = new Vector4(region.RockColor, 0),
            Custom3 = new Vector4(region.HasWater ? region.WaterLevel : -999, (float)region.Biome, wet, 0)
        });
        var ground = Scene.AddMesh(MeshFactory.Heightfield(Scene, region, size, resolution), terrain, name: "terrain");
        ground.CastShadow = false;
        ground.ReceiveShadow = true;

        if (region.HasWater)
        {
            var style = region.Biome == Biome.Sea
                ? WaterStyle.Ocean with { DeepColor = new(.02f, .06f, .09f), ShallowColor = new(.08f, .2f, .24f), Detail = .09f, Foam = .55f, Displacement = .9f }
                : WaterStyle.Ocean with { DeepColor = new(.0f, .18f, .28f), ShallowColor = new(.12f, .62f, .6f), Absorption = .12f, Detail = .03f, Caustics = .7f };
            water = new WaterSurface(Scene, new Vector3(0, region.WaterLevel, 0), size * .9f, region.WaterLevel - 8, Quality == 0 ? 64 : 128, Quality == 0 ? 96 : 160)
            {
                Style = style,
                SunDirection = region.SunDirection
            };
            water.Node.CastShadow = false;
        }

        if (Quality > 0 && region.Weather is Weather.Clear or Weather.Dawn or Weather.Storm or Weather.Fog)
        {
            clouds = new CloudLayer(Scene, region.Weather == Weather.Storm ? 420 : 900, 2200, shader: null)
            {
                Style = region.Weather switch { Weather.Storm => CloudStyle.Storm, Weather.Fog => CloudStyle.Overcast, _ => CloudStyle.FairWeather },
                SunDirection = region.SunDirection,
                Wind = region.Weather == Weather.Storm ? new Vector2(24, 9) : new Vector2(6, 2)
            };
        }

        BuildObstacles();
        switch (region.Biome)
        {
            case Biome.Savanna: Savanna(); break;
            case Biome.City: City(); break;
            case Biome.Terraces: Terraces(); break;
            case Biome.Hills: Hills(); break;
            case Biome.Rainforest: Rainforest(); break;
            case Biome.Lagoon: Lagoon(); break;
            case Biome.Crater: Crater(); break;
            case Biome.Sea: Sea(); break;
        }
        RimCliffs();
    }

    /// <summary>
    /// Dresses the terrain's rising rim as broken cliffs: a ring of instanced rock outcrops (six sculpted
    /// shapes, randomly scaled and turned) seated into the slope, so the arena edge reads as rock, not a
    /// smooth pale wall. Visual only: the rim is outside the playable ring.
    /// </summary>
    private void RimCliffs()
    {
        var region = Encounter.Region;
        if (region.Biome == Biome.City) return; // the skyline already closes the horizon
        var random = new Random(region.Seed + 77);
        var moss = region.Biome switch { Biome.Rainforest or Biome.Terraces or Biome.Lagoon => .9f, Biome.Crater => 0f, _ => .35f };
        var rock = Mat("rim-rock", () => StoneOptions(region.RockColor, .25f, new Vector3(.16f, .28f, .1f), moss, 51));
        const int Shapes = 6;
        var sets = new List<Matrix4x4>[Shapes];
        for (var i = 0; i < Shapes; i++) sets[i] = [];
        var count = Quality == 0 ? 56 : 88;
        var tall = region.Biome is Biome.Lagoon or Biome.Hills or Biome.Sea ? 1.6f : region.Biome == Biome.Crater ? .8f : 1f;
        for (var i = 0; i < count; i++)
        {
            var a = (i + random.NextSingle() * .8f) / count * Mathx.Tau;
            var r = region.ArenaRadius * (1.08f + random.NextSingle() * .42f);
            var x = MathF.Sin(a) * r;
            var z = MathF.Cos(a) * r;
            var scale = new Vector3(7 + random.NextSingle() * 11, (9 + random.NextSingle() * 15) * tall, 7 + random.NextSingle() * 11);
            var ground = region.Height(x, z);
            // Unit shape (half extent 1): sink a third of it into the slope.
            var y = ground + scale.Y * .65f;
            var tilt = Matrix4x4.CreateRotationX((random.NextSingle() - .5f) * .3f) * Matrix4x4.CreateRotationZ((random.NextSingle() - .5f) * .3f);
            sets[i % Shapes].Add(Matrix4x4.CreateScale(scale) * tilt * Matrix4x4.CreateRotationY(a + random.NextSingle() * 1.5f) * Matrix4x4.CreateTranslation(x, y, z));
        }
        for (var i = 0; i < Shapes; i++)
        {
            if (sets[i].Count == 0) continue;
            var node = Scene.AddMesh(MeshFactory.SculptedBox(Scene, Vector3.One, .6f, .3f, 300 + i, 9, 16), rock, name: $"rim-cliffs-{i}");
            node.SetInstances(sets[i].ToArray());
            node.CastShadow = false;
            node.ReceiveShadow = true;
        }
    }

    private void BuildObstacles()
    {
        var region = Encounter.Region;
        var obstacles = region.Obstacles;
        var mossy = Mat("ruin", () => StoneOptions(new Vector3(.44f, .41f, .36f), 1, new Vector3(.2f, .3f, .12f), .7f, 3));
        var rock = Mat("rock", () => StoneOptions(region.RockColor, .15f, region.GroundHigh * .8f, .35f, 9));
        var bark = Mat("bark", () => StoneOptions(new Vector3(.3f, .22f, .16f), 0, new Vector3(.18f, .28f, .1f), .5f, 21, wood: true));
        var gold = Mat("shrine-gold", () => MaterialOptions.Pbr(Rgba(new Vector3(1f, .72f, .3f)), 1, .25f) with { Emissive = new Vector3(1f, .7f, .3f), EmissiveIntensity = 1.5f });
        var night = region.Weather is Weather.Night;
        for (var i = 0; i < obstacles.Count; i++)
        {
            var o = obstacles[i];
            var root = Scene.CreateNode(name: $"obstacle-{i}");
            root.Position = o.Center;
            root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, o.Yaw);
            switch (o.Kind)
            {
                case ObstacleKind.Ruin:
                case ObstacleKind.Shrine:
                {
                    // Candi: three receding tiers and a stupa bell, all inside the collision box.
                    var h = o.Half;
                    var tier = h.Y * 2 / 3.4f;
                    var y = -h.Y;
                    for (var t = 0; t < 3; t++)
                    {
                        var shrink = 1 - t * .16f;
                        var half = new Vector3(h.X * shrink, tier * .5f, h.Z * shrink);
                        var node = Scene.AddMesh(MeshFactory.SculptedBox(Scene, half, .25f, .12f, i * 7 + t, .9f, 12), mossy, root);
                        node.Position = new Vector3(0, y + half.Y, 0);
                        y += tier;
                    }
                    var bell = Scene.AddMesh(sphere, mossy, root);
                    bell.Position = new Vector3(0, y + tier * .35f, 0);
                    bell.Scale = new Vector3(h.X * .45f, tier * .5f, h.Z * .45f);
                    if (o.Kind == ObstacleKind.Shrine)
                    {
                        var lamp = Mesh(sphere, gold, new Vector3(0, h.Y + .4f, 0), new Vector3(.35f), root);
                        if (Quality >= 2 && (night || region.Weather == Weather.Dawn)) Scene.AddLight(Light.Point(new Vector3(1f, .7f, .35f), 30, 18), root).Position = new Vector3(0, h.Y + 1, 0);
                        lamp.CastShadow = false;
                    }
                    break;
                }
                case ObstacleKind.Pillar:
                    Scene.AddMesh(MeshFactory.SculptedBox(Scene, o.Half, .35f, .15f, i * 13, .9f, 10), mossy, root);
                    break;
                case ObstacleKind.Rock:
                    Scene.AddMesh(MeshFactory.SculptedBox(Scene, o.Half, MathF.Min(o.Half.X, o.Half.Z) * .8f, .45f, i * 3 + 1, 1.2f, 10), rock, root);
                    break;
                case ObstacleKind.Karst:
                {
                    var karst = Mat("karst", () => StoneOptions(new Vector3(.52f, .52f, .46f), .2f, new Vector3(.14f, .3f, .1f), 1.1f, 33));
                    Scene.AddMesh(MeshFactory.SculptedBox(Scene, o.Half, 3, 1.2f, i * 5 + 2, .6f, 20), karst, root);
                    var canopy = Mat("karst-canopy", () => MaterialOptions.Pbr(Hex("#2D5A2A"), 0, .9f));
                    for (var k = 0; k < 4; k++)
                    {
                        var a = k * 1.7f + i;
                        Mesh(sphere, canopy, new Vector3(MathF.Sin(a) * o.Half.X * .5f, o.Half.Y + .8f, MathF.Cos(a) * o.Half.Z * .5f), new Vector3(2.6f, 1.6f, 2.6f), root);
                    }
                    break;
                }
                case ObstacleKind.Tree:
                {
                    Scene.AddMesh(MeshFactory.SculptedBox(Scene, o.Half, .5f, .08f, i, 1.2f, 16), bark, root);
                    var leaves = Mat("leaves", () => MaterialOptions.Pbr(Hex("#2E4A1F"), 0, .9f) with { Shader = foliageShader, Custom0 = new Vector4(.1f, .2f, .07f, 0), Custom1 = new Vector4(.3f, .45f, .15f, 0), Custom2 = new Vector4(.3f, 0, 0, 0) });
                    for (var k = 0; k < 3; k++)
                    {
                        var a = k * 2.1f + i * .7f;
                        var leaf = Mesh(sphere, leaves, new Vector3(MathF.Sin(a) * 3, o.Half.Y + 1 + k * 1.2f, MathF.Cos(a) * 3), new Vector3(5.5f, 2.6f, 5.5f), root);
                        leaf.CastShadow = Quality > 1;
                    }
                    break;
                }
                case ObstacleKind.Building:
                {
                    var tint = Vector3.Lerp(new Vector3(.22f, .23f, .26f), new Vector3(.42f, .4f, .38f), (i * 37 % 11) / 10f);
                    var facade = Scene.CreateMaterial(MaterialOptions.Pbr(Rgba(tint), 0, .7f) with
                    {
                        Shader = buildingShader,
                        Custom0 = new Vector4(tint, 0),
                        Custom1 = new Vector4(.35f + (i % 5) * .08f, night ? 1 : .15f, i * 1.37f, 0)
                    });
                    Scene.AddMesh(MeshFactory.SculptedBox(Scene, o.Half, .3f, 0, i, .35f, 24), facade, root);
                    if (i % 3 == 0)
                    {
                        var neon = Mat($"neon-{i % 4}", () => MaterialOptions.Basic(new Vector4((i % 4) switch { 0 => new Vector3(1, .2f, .6f), 1 => new Vector3(.2f, .9f, 1), 2 => new Vector3(1, .7f, .2f), _ => new Vector3(.6f, .3f, 1) }, 1)) with { EmissiveIntensity = 3 });
                        var sign = Mesh(box, neon, new Vector3(0, o.Half.Y * (.3f + (i % 4) * .12f), o.Half.Z + .2f), new Vector3(o.Half.X * 1.2f, 2.2f, .25f), root);
                        sign.CastShadow = false;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Instanced foliage scattered over gentle ground inside the given radius band.</summary>
    private void Scatter(string key, Geometry geometry, Vector3 baseColor, Vector3 tipColor, int count, float minR, float maxR, Vector2 scaleRange, float maxSlope = .35f, Func<float, float, bool>? accept = null, float sway = 1)
    {
        var region = Encounter.Region;
        var random = new Random(region.Seed * 31 + key.Length);
        var transforms = new List<Matrix4x4>(count);
        var attempts = 0;
        while (transforms.Count < count && attempts++ < count * 4)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = minR + MathF.Sqrt(random.NextSingle()) * (maxR - minR);
            var x = MathF.Sin(a) * r;
            var z = MathF.Cos(a) * r;
            if (1 - region.Normal(x, z).Y > maxSlope) continue;
            if (region.HasWater && region.Height(x, z) < region.WaterLevel + .1f) continue;
            if (accept is not null && !accept(x, z)) continue;
            var s = scaleRange.X + random.NextSingle() * (scaleRange.Y - scaleRange.X);
            transforms.Add(Matrix4x4.CreateScale(s) * Matrix4x4.CreateRotationY(random.NextSingle() * Mathx.Tau) * Matrix4x4.CreateTranslation(x, region.Height(x, z) - .05f, z));
        }
        var material = Scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(baseColor, 1), 0, .9f) with
        {
            Shader = foliageShader,
            CullMode = CullMode.None,
            Custom0 = new Vector4(baseColor, 0),
            Custom1 = new Vector4(tipColor, 0),
            Custom2 = new Vector4(sway, 0, 0, 0)
        });
        var node = Scene.AddMesh(geometry, material, name: key);
        node.SetInstances(transforms.ToArray());
        node.CastShadow = false;
    }

    private int FoliageCount(int balanced) => Quality switch { 0 => balanced / 3, 1 => balanced, _ => balanced * 2 };

    private void Savanna()
    {
        var region = Encounter.Region;
        Scatter("savanna-grass", MeshFactory.Blade(Scene, .18f, .9f), new Vector3(.45f, .38f, .18f), new Vector3(.85f, .7f, .38f), FoliageCount(9000), 8, region.ArenaRadius * 1.15f, new(.6f, 1.5f));
        // Bromo's caldera rim and the volcano itself on the horizon, with its plume.
        var volcanoMat = Mat("volcano", () => StoneOptions(new Vector3(.32f, .29f, .27f), 0, new Vector3(.35f, .32f, .2f), .3f, 41));
        // Bromo is a truncated caldera, not a peak: a frustum with a dark crater floor set into its top.
        var cone = Scene.AddMesh(Scene.CreateCylinderGeometry(.2f, 1, 1, 48), volcanoMat, name: "bromo");
        cone.Position = new Vector3(-420, 40, 520);
        cone.Scale = new Vector3(360, 260, 360);
        var crater = Mesh(Scene.CreateCylinderGeometry(1, 1, 1, 32), Mat("crater-dark", () => MaterialOptions.Pbr(Hex("#2A2522"), 0, 1)), new Vector3(-420, 168.5f, 520), new Vector3(62, 4, 62));
        crater.CastShadow = false;
        var semeru = Scene.AddMesh(Scene.CreateConeGeometry(1, 1, 48), volcanoMat, name: "semeru");
        semeru.Position = new Vector3(380, 70, 900);
        semeru.Scale = new Vector3(520, 520, 520);
        AddPlume(new Vector3(-420, 175, 520), new Vector3(.75f, .72f, .7f), 26, 3.5f);
    }

    private void City()
    {
        var region = Encounter.Region;
        // Distant skyline beyond the playable towers.
        var random = new Random(region.Seed);
        var transforms = new List<Matrix4x4>();
        for (var i = 0; i < 160; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (1.7f + random.NextSingle() * 1.5f);
            var h = 40 + random.NextSingle() * 220;
            transforms.Add(Matrix4x4.CreateScale(18 + random.NextSingle() * 20, h, 18 + random.NextSingle() * 20) * Matrix4x4.CreateTranslation(MathF.Sin(a) * r, h / 2, MathF.Cos(a) * r));
        }
        var skyline = Scene.CreateMaterial(MaterialOptions.Pbr(Hex("#1B1F2A"), .2f, .5f) with { Shader = buildingShader, Custom0 = new Vector4(.1f, .11f, .14f, 0), Custom1 = new Vector4(.45f, 1, 3.3f, 0) });
        var node = Scene.AddMesh(box, skyline, name: "skyline");
        node.SetInstances(transforms.ToArray());
        node.CastShadow = false;
        // Street lamps around the plaza.
        var lamp = Mat("lamp", () => MaterialOptions.Basic(new Vector4(1, .82f, .55f, 1)) with { EmissiveIntensity = 4 });
        var pole = Mat("pole", () => MaterialOptions.Pbr(Hex("#3A3D42"), .8f, .4f));
        for (var i = 0; i < 12; i++)
        {
            var a = i / 12f * Mathx.Tau;
            var p = new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * region.ArenaRadius * .55f;
            p.Y = region.Height(p.X, p.Z);
            Mesh(cylinder, pole, p + new Vector3(0, 4, 0), new Vector3(.25f, 8, .25f));
            Mesh(sphere, lamp, p + new Vector3(0, 8.2f, 0), new Vector3(.5f)).CastShadow = false;
            if (Quality >= 2 && i % 3 == 0) Scene.AddLight(Light.Point(new Vector3(1, .78f, .5f), 60, 30)).Position = p + new Vector3(0, 7.5f, 0);
        }
    }

    private void Terraces()
    {
        var region = Encounter.Region;
        Scatter("rice", MeshFactory.Blade(Scene, .12f, .7f), new Vector3(.2f, .42f, .1f), new Vector3(.62f, .78f, .25f), FoliageCount(14000), 20, region.ArenaRadius * 1.2f, new(.7f, 1.3f), .08f);
        var palm = Mat("palm-trunk", () => StoneOptions(new Vector3(.42f, .34f, .25f), 0, Vector3.Zero, 0, 7, wood: true));
        var frond = Mat("palm-frond", () => MaterialOptions.Pbr(Hex("#3E6B2A"), 0, .8f));
        var random = new Random(region.Seed + 3);
        for (var i = 0; i < 26; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (.85f + random.NextSingle() * .5f);
            var p = new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r);
            p.Y = region.Height(p.X, p.Z);
            var h = 9 + random.NextSingle() * 6;
            Mesh(cylinder, palm, p + new Vector3(0, h / 2, 0), new Vector3(.5f, h, .5f));
            for (var k = 0; k < 5; k++)
            {
                var leaf = Mesh(box, frond, p + new Vector3(0, h, 0), new Vector3(.6f, .08f, 4.2f));
                leaf.Rotation = Quaternion.CreateFromYawPitchRoll(k * 1.25f, .45f, 0);
            }
        }
    }

    private void Hills()
    {
        var region = Encounter.Region;
        Scatter("dry-grass", MeshFactory.Blade(Scene, .16f, .7f), new Vector3(.5f, .42f, .22f), new Vector3(.82f, .72f, .45f), FoliageCount(7000), 10, region.ArenaRadius * 1.15f, new(.6f, 1.3f), .4f);
        // Lontar palms dot the savanna.
        var trunk = Mat("lontar", () => StoneOptions(new Vector3(.35f, .3f, .24f), 0, Vector3.Zero, 0, 5, wood: true));
        var crown = Mat("lontar-crown", () => MaterialOptions.Pbr(Hex("#56703A"), 0, .8f));
        var random = new Random(region.Seed);
        for (var i = 0; i < 40; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (.3f + random.NextSingle() * 1.1f);
            var p = new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r);
            p.Y = region.Height(p.X, p.Z);
            var h = 7 + random.NextSingle() * 5;
            Mesh(cylinder, trunk, p + new Vector3(0, h / 2, 0), new Vector3(.45f, h, .45f));
            Mesh(sphere, crown, p + new Vector3(0, h + .5f, 0), new Vector3(2.4f, 1.4f, 2.4f));
        }
    }

    private void Rainforest()
    {
        var region = Encounter.Region;
        Scatter("ferns", MeshFactory.Blade(Scene, .5f, 1.1f), new Vector3(.08f, .2f, .05f), new Vector3(.3f, .5f, .15f), FoliageCount(6000), 6, region.ArenaRadius * 1.15f, new(.8f, 1.8f), .5f);
        // A wall of giant trees beyond the arena.
        var random = new Random(region.Seed + 9);
        var trunks = new List<Matrix4x4>();
        var crowns = new List<Matrix4x4>();
        for (var i = 0; i < 220; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (1.05f + random.NextSingle() * .9f);
            var x = MathF.Sin(a) * r;
            var z = MathF.Cos(a) * r;
            var y = region.Height(x, z);
            var h = 30 + random.NextSingle() * 25;
            trunks.Add(Matrix4x4.CreateScale(1.4f, h, 1.4f) * Matrix4x4.CreateTranslation(x, y + h / 2, z));
            crowns.Add(Matrix4x4.CreateScale(9, 5, 9) * Matrix4x4.CreateTranslation(x, y + h, z));
        }
        var bark = Mat("bark", () => StoneOptions(new Vector3(.3f, .22f, .16f), 0, new Vector3(.18f, .28f, .1f), .5f, 21, wood: true));
        var trunkNode = Scene.AddMesh(cylinder, bark, name: "forest-trunks");
        trunkNode.SetInstances(trunks.ToArray());
        var leaves = Mat("leaves-far", () => MaterialOptions.Pbr(Hex("#203A18"), 0, .95f));
        var crownNode = Scene.AddMesh(sphere, leaves, name: "forest-crowns");
        crownNode.SetInstances(crowns.ToArray());
        crownNode.CastShadow = false;
    }

    private void Lagoon()
    {
        var region = Encounter.Region;
        var random = new Random(region.Seed + 1);
        var karst = Mat("karst", () => StoneOptions(new Vector3(.52f, .52f, .46f), .2f, new Vector3(.14f, .3f, .1f), 1.1f, 33));
        for (var i = 0; i < 24; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (1.2f + random.NextSingle() * .9f);
            var half = new Vector3(10 + random.NextSingle() * 18, 12 + random.NextSingle() * 22, 10 + random.NextSingle() * 18);
            var node = Scene.AddMesh(MeshFactory.SculptedBox(Scene, half, 6, 2.5f, i + 100, .25f, 12), karst);
            // Raja Ampat's karst towers stand on the rim slope, not buried in it.
            var (kx, kz) = (MathF.Sin(a) * r, MathF.Cos(a) * r);
            node.Position = new Vector3(kx, MathF.Max(region.WaterLevel, region.Height(kx, kz)) + half.Y * .7f, kz);
            node.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, a);
        }
        // Coral glows under the clear water.
        var coral = Mat("coral", () => MaterialOptions.Pbr(Hex("#E0705A"), 0, .7f) with { Emissive = new Vector3(.9f, .35f, .3f), EmissiveIntensity = .25f });
        var corals = new List<Matrix4x4>();
        for (var i = 0; i < 400; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = random.NextSingle() * region.ArenaRadius * 1.1f;
            var x = MathF.Sin(a) * r;
            var z = MathF.Cos(a) * r;
            var s = .4f + random.NextSingle() * 1.2f;
            corals.Add(Matrix4x4.CreateScale(s, s * .6f, s) * Matrix4x4.CreateTranslation(x, region.Height(x, z), z));
        }
        var coralNode = Scene.AddMesh(sphere, coral, name: "coral");
        coralNode.SetInstances(corals.ToArray());
        coralNode.CastShadow = false;
    }

    private void Crater()
    {
        var region = Encounter.Region;
        var random = new Random(region.Seed + 4);
        for (var i = 0; i < (Quality == 0 ? 4 : 8); i++)
        {
            var a = i / 8f * Mathx.Tau + random.NextSingle() * .4f;
            var r = region.ArenaRadius * (.35f + random.NextSingle() * .45f);
            var p = new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r);
            p.Y = region.Height(p.X, p.Z);
            var fire = new FireEffect(Scene, p + new Vector3(0, 1.5f, 0), new Vector3(2.4f, 4.5f, 2.4f), seed: 97 + i)
            {
                Style = FireStyle.Magic with { Tint = new Vector3(.25f, .55f, 1f), Brightness = 2.2f }
            };
            fires.Add(fire);
            // Only the nearest few fires cast light; the rest glow through their particles.
            if (i < (Quality >= 2 ? 4 : Quality == 1 ? 2 : 1))
            {
                var light = Scene.AddLight(Light.Point(new Vector3(.3f, .55f, 1f), 70, 30));
                light.Position = p + new Vector3(0, 3, 0);
                fireLights.Add(light);
            }
        }
        // Sulfur miners' baskets and pipes give scale.
        var pipe = Mat("pipe", () => MaterialOptions.Pbr(Hex("#6B5A3A"), .6f, .5f));
        for (var i = 0; i < 6; i++)
        {
            var a = i * 1.1f;
            var p = new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * region.ArenaRadius * .6f;
            p.Y = region.Height(p.X, p.Z);
            var node = Mesh(cylinder, pipe, p + new Vector3(0, .6f, 0), new Vector3(.6f, 9, .6f));
            node.Rotation = Quaternion.CreateFromYawPitchRoll(a, MathF.PI / 2.3f, 0);
        }
    }

    private void Sea()
    {
        var region = Encounter.Region;
        var random = new Random(region.Seed + 8);
        // Wrecks of the old fleet.
        var wood = Mat("wreck", () => StoneOptions(new Vector3(.26f, .19f, .13f), 0, new Vector3(.12f, .2f, .14f), .6f, 55, wood: true));
        for (var i = 0; i < 9; i++)
        {
            var a = random.NextSingle() * Mathx.Tau;
            var r = region.ArenaRadius * (.5f + random.NextSingle() * .8f);
            var p = new Vector3(MathF.Sin(a) * r, region.WaterLevel - 1.5f, MathF.Cos(a) * r);
            var hull = Scene.AddMesh(MeshFactory.SculptedBox(Scene, new Vector3(2.5f, 2, 9), 1.2f, .3f, i + 40, .8f, 10), wood);
            hull.Position = p;
            hull.Rotation = Quaternion.CreateFromYawPitchRoll(a, .2f, .5f);
            var mast = Mesh(cylinder, wood, p + new Vector3(0, 5, 0), new Vector3(.4f, 12, .4f));
            mast.Rotation = Quaternion.CreateFromYawPitchRoll(a, .5f, .3f);
        }
    }

    private void UpdateEnvironment(float dt)
    {
        for (var i = 0; i < fires.Count; i++)
        {
            fires[i].Update(dt);
            if (i < fireLights.Count) fireLights[i].Light = Light.Point(new Vector3(.3f, .55f, 1f), 70 * fires[i].Flicker, 30);
        }
    }
}
