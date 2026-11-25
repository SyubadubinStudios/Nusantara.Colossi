using System.Numerics;

namespace NusantaraColossi.Core;

/// <summary>Bilingual text. Indonesian is the source language; English is the translation.</summary>
public readonly record struct LocText(string Id, string En)
{
    public string Get(string language) => language == "en" ? En : Id;
    public override string ToString() => Id;
}

public enum RegionId { Bromo, Jakarta, Bali, Flores, Kalimantan, RajaAmpat, Ijen, Banda, Nusantara }

public enum Weather { Clear, Dawn, Night, Rain, Fog, Ash, Storm }

/// <summary>Environmental pressure that changes the climbing rules of an arena.</summary>
public enum Hazard
{
    None,
    /// <summary>Wet stone: grip stamina drains faster.</summary>
    Rain,
    /// <summary>Drifting sulfur clouds hurt and stop stamina regeneration.</summary>
    ToxicGas,
    /// <summary>Periodic gusts that pull a climber off unless they hold grip.</summary>
    Wind,
    /// <summary>Hot air: stamina regenerates slowly.</summary>
    Heat
}

public enum Biome { Savanna, City, Terraces, Hills, Rainforest, Lagoon, Crater, Sea }

/// <summary>A static, optionally climbable box in the arena (ruins, pillars, karst towers, trees, buildings).</summary>
public readonly record struct Obstacle(Vector3 Center, Vector3 Half, float Yaw, SurfaceKind Surface, bool Climbable, ObstacleKind Kind);

public enum ObstacleKind { Ruin, Pillar, Building, Karst, Tree, Rock, Shrine }

public sealed class RegionDefinition
{
    public required RegionId Id { get; init; }
    public required LocText Name { get; init; }
    public required LocText Place { get; init; }
    public required LocText Blurb { get; init; }
    public required string ColossusId { get; init; }
    public required Biome Biome { get; init; }
    public required Weather Weather { get; init; }
    public required Hazard Hazard { get; init; }
    public required float ArenaRadius { get; init; }
    public required int Seed { get; init; }
    /// <summary>Water surface height, or <see cref="float.NaN"/> when the arena is dry.</summary>
    public float WaterLevel { get; init; } = float.NaN;
    /// <summary>Normalised direction toward the sun.</summary>
    public required Vector3 SunDirection { get; init; }
    public required Vector3 SunColor { get; init; }
    public required Vector3 SkyColor { get; init; }
    public required Vector3 FogColor { get; init; }
    public required float FogDensity { get; init; }
    public required Vector3 GroundLow { get; init; }
    public required Vector3 GroundHigh { get; init; }
    public required Vector3 RockColor { get; init; }
    /// <summary>Map position on the archipelago overview, normalised 0..1 (west→east, north→south).</summary>
    public required Vector2 MapPosition { get; init; }
    /// <summary>Hidden until every other guardian has fallen (the GARUDA NET finale).</summary>
    public bool Secret { get; init; }

    public bool HasWater => !float.IsNaN(WaterLevel);

    /// <summary>Deterministic ground height; shared by simulation, rendering and every co-op peer.</summary>
    public float Height(float x, float z)
    {
        var s = Seed;
        var r = MathF.Sqrt(x * x + z * z);
        var edge = Mathx.SmoothStep(ArenaRadius * .82f, ArenaRadius * 1.35f, r);
        float h;
        switch (Biome)
        {
            case Biome.Savanna:
                h = (Noise.Fbm(x * .008f, z * .008f, s) - .5f) * 14 + (Noise.Fbm(x * .04f, z * .04f, s + 3) - .5f) * 1.6f;
                h += edge * 38 * Noise.Ridged(x * .006f, z * .006f, s + 9);
                break;
            case Biome.City:
                h = (Noise.Fbm(x * .01f, z * .01f, s) - .5f) * .6f;
                break;
            case Biome.Terraces:
            {
                var raw = (Noise.Fbm(x * .006f, z * .006f, s) - .35f) * 30 + r * .06f;
                var step = 2.2f;
                var stepped = MathF.Floor(raw / step) * step;
                var frac = raw / step - MathF.Floor(raw / step);
                h = stepped + Mathx.SmoothStep(.86f, 1f, frac) * step;
                h *= Mathx.SmoothStep(25, 70, r);
                break;
            }
            case Biome.Hills:
                h = (Noise.Ridged(x * .007f, z * .007f, s) - .45f) * 22 * Mathx.SmoothStep(30, 110, r)
                    + (Noise.Fbm(x * .03f, z * .03f, s + 5) - .5f) * 2.4f;
                break;
            case Biome.Rainforest:
                h = (Noise.Fbm(x * .012f, z * .012f, s) - .5f) * 8 + (Noise.Fbm(x * .06f, z * .06f, s + 2) - .5f) * 1.2f;
                break;
            case Biome.Lagoon:
                h = -1.1f + (Noise.Fbm(x * .015f, z * .015f, s) - .5f) * 1.6f;
                break;
            case Biome.Crater:
            {
                var bowl = MathF.Pow(r / ArenaRadius, 2.2f) * 34;
                h = bowl + (Noise.Ridged(x * .02f, z * .02f, s) - .5f) * 3;
                break;
            }
            default: // Sea shoal
                h = -1.4f + (Noise.Ridged(x * .012f, z * .012f, s) - .4f) * 2.6f;
                break;
        }
        // Outside the playable ring every biome rises into a natural wall so the horizon reads as land.
        return h + edge * edge * 30;
    }

    public Vector3 Normal(float x, float z)
    {
        const float e = .5f;
        var dx = Height(x - e, z) - Height(x + e, z);
        var dz = Height(x, z - e) - Height(x, z + e);
        return Vector3.Normalize(new Vector3(dx, 2 * e, dz));
    }

    /// <summary>Floor the player stands on: terrain, or the water floor wading depth.</summary>
    public float StandHeight(float x, float z) => Height(x, z);

    private Obstacle[]? obstacles;

    /// <summary>Static arena props with collision. Generated once, deterministically.</summary>
    public IReadOnlyList<Obstacle> Obstacles => obstacles ??= BuildObstacles();

    private Obstacle[] BuildObstacles()
    {
        var list = new List<Obstacle>();
        var rng = new Random(Seed * 7919 + 13);
        Vector3 Ground(float x, float z, float halfHeight, float sink = .5f) => new(x, Height(x, z) + halfHeight - sink, z);
        void Ring(int count, float minR, float maxR, Func<float, float, Obstacle?> make)
        {
            for (var i = 0; i < count; i++)
            {
                var a = rng.NextSingle() * Mathx.Tau;
                var rr = minR + rng.NextSingle() * (maxR - minR);
                var o = make(MathF.Sin(a) * rr, MathF.Cos(a) * rr);
                if (o is { } ob) list.Add(ob);
            }
        }
        // Every arena has a ring of temple ruins: climbable perches for leaping onto a colossus.
        var ruins = Biome is Biome.Lagoon or Biome.Sea ? 5 : 9;
        for (var i = 0; i < ruins; i++)
        {
            var a = i / (float)ruins * Mathx.Tau + rng.NextSingle() * .3f;
            var rr = ArenaRadius * (.42f + rng.NextSingle() * .3f);
            var x = MathF.Sin(a) * rr;
            var z = MathF.Cos(a) * rr;
            var tall = 5 + rng.NextSingle() * 9;
            var half = new Vector3(2.2f + rng.NextSingle() * 1.5f, tall / 2, 2.2f + rng.NextSingle() * 1.5f);
            list.Add(new(Ground(x, z, half.Y), half, a, SurfaceKind.Moss, true, i % 3 == 0 ? ObstacleKind.Shrine : ObstacleKind.Ruin));
        }
        switch (Biome)
        {
            case Biome.City:
                // A grid of towers around the plaza; the plaza itself is the arena.
                for (var gx = -6; gx <= 6; gx++)
                    for (var gz = -6; gz <= 6; gz++)
                    {
                        var x = gx * 42f + (rng.NextSingle() - .5f) * 8;
                        var z = gz * 42f + (rng.NextSingle() - .5f) * 8;
                        var r = MathF.Sqrt(x * x + z * z);
                        if (r < ArenaRadius * .62f || r > ArenaRadius * 1.6f) continue;
                        var height = 30 + rng.NextSingle() * (r > ArenaRadius ? 140 : 70);
                        var half = new Vector3(8 + rng.NextSingle() * 6, height / 2, 8 + rng.NextSingle() * 6);
                        list.Add(new(Ground(x, z, half.Y, 0), half, 0, SurfaceKind.Glass, r < ArenaRadius * .95f, ObstacleKind.Building));
                    }
                break;
            case Biome.Rainforest:
                Ring(70, 25, ArenaRadius * 1.1f, (x, z) =>
                {
                    var tall = 18 + rng.NextSingle() * 16;
                    var half = new Vector3(.8f + rng.NextSingle() * .5f, tall / 2, .8f + rng.NextSingle() * .5f);
                    return new Obstacle(Ground(x, z, half.Y), half, rng.NextSingle() * Mathx.Tau, SurfaceKind.Wood, true, ObstacleKind.Tree);
                });
                break;
            case Biome.Lagoon:
                Ring(16, ArenaRadius * .35f, ArenaRadius * 1.05f, (x, z) =>
                {
                    var tall = 14 + rng.NextSingle() * 22;
                    var half = new Vector3(5 + rng.NextSingle() * 7, tall / 2, 5 + rng.NextSingle() * 7);
                    return new Obstacle(Ground(x, z, half.Y, 1.5f), half, rng.NextSingle() * Mathx.Tau, SurfaceKind.Moss, true, ObstacleKind.Karst);
                });
                break;
            case Biome.Savanna:
            case Biome.Hills:
            case Biome.Crater:
            case Biome.Sea:
                Ring(Biome == Biome.Sea ? 10 : 18, ArenaRadius * .3f, ArenaRadius * .95f, (x, z) =>
                {
                    var half = new Vector3(1.5f + rng.NextSingle() * 3, 1 + rng.NextSingle() * 2.5f, 1.5f + rng.NextSingle() * 3);
                    return new Obstacle(Ground(x, z, half.Y, .8f), half, rng.NextSingle() * Mathx.Tau, SurfaceKind.Stone, false, ObstacleKind.Rock);
                });
                break;
            case Biome.Terraces:
                Ring(12, 40, ArenaRadius * .9f, (x, z) =>
                {
                    var half = new Vector3(1.1f, 4 + rng.NextSingle() * 3, 1.1f);
                    return new Obstacle(Ground(x, z, half.Y), half, rng.NextSingle(), SurfaceKind.Moss, true, ObstacleKind.Pillar);
                });
                break;
        }
        // Keep the camp clear: nothing may stand on the spawn point.
        var spawnZ = -ArenaRadius * .72f;
        list.RemoveAll(o => MathF.Abs(o.Center.X) < o.Half.X + 3 && MathF.Abs(o.Center.Z - spawnZ) < o.Half.Z + 3);
        return [.. list];
    }

    /// <summary>Where the expedition begins: south edge, facing the arena centre.</summary>
    public Vector3 SpawnPoint
    {
        get
        {
            var z = -ArenaRadius * .72f;
            return new(0, Height(0, z), z);
        }
    }
}

public static class Regions
{
    public static readonly RegionDefinition[] All =
    [
        new()
        {
            Id = RegionId.Bromo, ColossusId = "kerbau-langit", Biome = Biome.Savanna, Weather = Weather.Dawn, Hazard = Hazard.None,
            Name = new("Sabana Bromo", "Bromo Savannah"), Place = new("Jawa Timur", "East Java"),
            Blurb = new("Lautan pasir dan rumput keemasan di kaki gunung api. Sesuatu yang bertanduk tidur di bawah kabut subuh.",
                        "A sea of sand and golden grass below the volcano. Something horned sleeps under the dawn mist."),
            ArenaRadius = 190, Seed = 11, SunDirection = Vector3.Normalize(new(-.7f, .28f, .5f)), SunColor = new(1f, .72f, .48f),
            SkyColor = new(.58f, .62f, .74f), FogColor = new(.86f, .72f, .62f), FogDensity = .0028f,
            GroundLow = new(.55f, .47f, .27f), GroundHigh = new(.42f, .44f, .22f), RockColor = new(.36f, .33f, .31f), MapPosition = new(.355f, .62f)
        },
        new()
        {
            Id = RegionId.Jakarta, ColossusId = "gedung-titan", Biome = Biome.City, Weather = Weather.Night, Hazard = Hazard.Rain,
            Name = new("Jakarta Hyper City", "Jakarta Hyper City"), Place = new("Sudirman–Thamrin–SCBD", "Sudirman–Thamrin–SCBD"),
            Blurb = new("Menara-menara kaca di bawah hujan neon. Salah satunya bernapas.",
                        "Glass towers under neon rain. One of them is breathing."),
            ArenaRadius = 150, Seed = 23, SunDirection = Vector3.Normalize(new(.3f, .55f, -.4f)), SunColor = new(.55f, .62f, .9f),
            SkyColor = new(.05f, .06f, .12f), FogColor = new(.16f, .14f, .26f), FogDensity = .006f,
            GroundLow = new(.2f, .21f, .23f), GroundHigh = new(.26f, .26f, .28f), RockColor = new(.3f, .3f, .32f), MapPosition = new(.27f, .58f)
        },
        new()
        {
            Id = RegionId.Bali, ColossusId = "dewi-padi", Biome = Biome.Terraces, Weather = Weather.Clear, Hazard = Hazard.None,
            Name = new("Terasering Bali", "Bali Rice Terraces"), Place = new("Jatiluwih, Bali", "Jatiluwih, Bali"),
            Blurb = new("Subak berusia seribu tahun mengalirkan air ke sawah bertingkat. Air itu kini mengalir ke atas.",
                        "A thousand-year-old subak feeds the terraces. Today the water flows uphill."),
            ArenaRadius = 170, Seed = 37, WaterLevel = float.NaN, SunDirection = Vector3.Normalize(new(.4f, .75f, .3f)), SunColor = new(1f, .93f, .78f),
            SkyColor = new(.45f, .68f, .9f), FogColor = new(.72f, .82f, .86f), FogDensity = .0028f,
            GroundLow = new(.36f, .52f, .16f), GroundHigh = new(.5f, .62f, .2f), RockColor = new(.38f, .32f, .25f), MapPosition = new(.43f, .64f)
        },
        new()
        {
            Id = RegionId.Flores, ColossusId = "komodo-agung", Biome = Biome.Hills, Weather = Weather.Clear, Hazard = Hazard.Heat,
            Name = new("Perbukitan Flores", "Flores Hills"), Place = new("Nusa Tenggara Timur", "East Nusa Tenggara"),
            Blurb = new("Bukit kering dan sabana bergelombang. Bebatuan di sini kadang berkedip.",
                        "Dry hills and rolling savanna. The rocks here sometimes blink."),
            ArenaRadius = 180, Seed = 41, SunDirection = Vector3.Normalize(new(.2f, .85f, .2f)), SunColor = new(1f, .9f, .74f),
            SkyColor = new(.52f, .7f, .92f), FogColor = new(.86f, .82f, .7f), FogDensity = .0025f,
            GroundLow = new(.62f, .52f, .3f), GroundHigh = new(.48f, .46f, .26f), RockColor = new(.4f, .32f, .27f), MapPosition = new(.555f, .67f)
        },
        new()
        {
            Id = RegionId.Kalimantan, ColossusId = "orangutan-purba", Biome = Biome.Rainforest, Weather = Weather.Fog, Hazard = Hazard.Rain,
            Name = new("Hutan Hujan Kalimantan", "Kalimantan Rainforest"), Place = new("Kalimantan Tengah", "Central Kalimantan"),
            Blurb = new("Kanopi setinggi gedung, kabut, dan suara yang terlalu dalam untuk seekor kera.",
                        "A canopy as tall as towers, mist, and a voice too deep for any ape."),
            ArenaRadius = 160, Seed = 53, SunDirection = Vector3.Normalize(new(.3f, .7f, .4f)), SunColor = new(.85f, .95f, .8f),
            SkyColor = new(.5f, .6f, .58f), FogColor = new(.5f, .6f, .55f), FogDensity = .011f,
            GroundLow = new(.2f, .26f, .12f), GroundHigh = new(.25f, .32f, .14f), RockColor = new(.3f, .3f, .26f), MapPosition = new(.42f, .38f)
        },
        new()
        {
            Id = RegionId.RajaAmpat, ColossusId = "leviathan", Biome = Biome.Lagoon, Weather = Weather.Clear, Hazard = Hazard.None,
            Name = new("Laguna Raja Ampat", "Raja Ampat Lagoon"), Place = new("Papua Barat Daya", "Southwest Papua"),
            Blurb = new("Air sebening kaca di antara bukit karst. Karangnya bergerak dalam satu barisan panjang.",
                        "Glass-clear water between karst hills. The reef moves in one long line."),
            ArenaRadius = 210, Seed = 67, WaterLevel = 0, SunDirection = Vector3.Normalize(new(-.3f, .8f, .3f)), SunColor = new(1f, .96f, .86f),
            SkyColor = new(.42f, .7f, .95f), FogColor = new(.7f, .86f, .92f), FogDensity = .002f,
            GroundLow = new(.86f, .8f, .62f), GroundHigh = new(.7f, .7f, .5f), RockColor = new(.36f, .4f, .34f), MapPosition = new(.83f, .4f)
        },
        new()
        {
            Id = RegionId.Ijen, ColossusId = "penjaga-api-biru", Biome = Biome.Crater, Weather = Weather.Night, Hazard = Hazard.ToxicGas,
            Name = new("Kawah Ijen", "Ijen Crater"), Place = new("Banyuwangi, Jawa Timur", "Banyuwangi, East Java"),
            Blurb = new("Api biru menyala dari belerang. Gasnya bisa membunuh; penjaganya lebih cepat.",
                        "Blue flames burn from the sulfur. The gas can kill; its guardian, faster."),
            ArenaRadius = 140, Seed = 71, WaterLevel = float.NaN, SunDirection = Vector3.Normalize(new(.2f, .5f, -.6f)), SunColor = new(.45f, .55f, .85f),
            SkyColor = new(.03f, .05f, .1f), FogColor = new(.2f, .24f, .22f), FogDensity = .009f,
            GroundLow = new(.62f, .58f, .28f), GroundHigh = new(.3f, .29f, .27f), RockColor = new(.18f, .18f, .2f), MapPosition = new(.395f, .655f)
        },
        new()
        {
            Id = RegionId.Banda, ColossusId = "kapal-majapahit", Biome = Biome.Sea, Weather = Weather.Storm, Hazard = Hazard.Wind,
            Name = new("Laut Banda", "Banda Sea"), Place = new("Maluku", "Maluku"),
            Blurb = new("Badai di atas palung terdalam Nusantara. Sebuah armada lama tidak pernah benar-benar karam.",
                        "A storm over the archipelago's deepest trench. An old fleet never truly sank."),
            ArenaRadius = 200, Seed = 89, WaterLevel = 0, SunDirection = Vector3.Normalize(new(.5f, .35f, .4f)), SunColor = new(.7f, .74f, .8f),
            SkyColor = new(.2f, .24f, .28f), FogColor = new(.3f, .34f, .38f), FogDensity = .0075f,
            GroundLow = new(.4f, .38f, .32f), GroundHigh = new(.3f, .3f, .28f), RockColor = new(.22f, .23f, .24f), MapPosition = new(.72f, .52f)
        },
        new()
        {
            Id = RegionId.Nusantara, ColossusId = "garuda-agung", Biome = Biome.City, Weather = Weather.Storm, Hazard = Hazard.Wind, Secret = true,
            Name = new("Titik Nol Nusantara", "Nusantara Point Zero"), Place = new("Ibu Kota Nusantara, Kalimantan Timur", "Nusantara Capital, East Kalimantan"),
            Blurb = new("Ibu kota baru, menara data, dan badai yang tak pernah reda. Setiap sinyal GARUDA NET berasal dari sini.",
                        "The new capital, data towers and a storm that never ends. Every GARUDA NET signal comes from here."),
            ArenaRadius = 160, Seed = 97, SunDirection = Vector3.Normalize(new(.3f, .45f, -.5f)), SunColor = new(.5f, .7f, 1f),
            SkyColor = new(.06f, .08f, .14f), FogColor = new(.12f, .16f, .22f), FogDensity = .006f,
            GroundLow = new(.18f, .2f, .22f), GroundHigh = new(.24f, .26f, .28f), RockColor = new(.28f, .3f, .33f), MapPosition = new(.5f, .41f)
        }
    ];

    /// <summary>The eight guardians of the main journey, without the secret finale.</summary>
    public static IEnumerable<RegionDefinition> Main => All.Where(r => !r.Secret);

    public static RegionDefinition Get(RegionId id) => All[(int)id];

    public static RegionDefinition ForColossus(string colossusId) => All.First(r => r.ColossusId == colossusId);
}
