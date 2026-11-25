using System.Numerics;
using static NusantaraColossi.Core.PartBuilder;

namespace NusantaraColossi.Core;

/// <summary>
/// The eight Penjaga Nusantara. Sizes are gameplay scale (a full climb takes one to two minutes),
/// not the literal 250 m–1 km of the story; the codex states the legendary size.
/// Colossi face +Z in their own frame.
/// </summary>
public static class Colossi
{
    private static readonly Vector3 Gold = new(1f, .74f, .3f);

    public static readonly ColossusBlueprint[] All = [KerbauLangit(), GedungTitan(), DewiPadi(), KomodoAgung(), OrangutanPurba(), Leviathan(), PenjagaApiBiru(), KapalMajapahit(), GarudaAgung()];

    public static ColossusBlueprint Get(string id) => All.First(c => c.Id == id);

    private static ColossusBlueprint KerbauLangit()
    {
        var b = new PartBuilder();
        b.Add("body", null, PartRole.Body, Vector3.Zero, Vector3.Zero, new(6, 5.5f, 13), SurfaceKind.Fur);
        b.Add("hump", "body", PartRole.Hump, new(0, 5.5f, 5), new(0, 2.5f, 0), new(4.5f, 2.6f, 4.5f), SurfaceKind.Fur);
        var legSides = Faces(negY: false, posY: false);
        b.Add("leg-fl", "body", PartRole.LegFrontLeft, new(-4.2f, -5.5f, 9.5f), new(0, -7, 0), new(2, 7, 2), SurfaceKind.Moss, legSides, gaitPhase: 0, gaitAmplitude: .34f);
        b.Add("leg-fr", "body", PartRole.LegFrontRight, new(4.2f, -5.5f, 9.5f), new(0, -7, 0), new(2, 7, 2), SurfaceKind.Moss, legSides, gaitPhase: .5f, gaitAmplitude: .34f);
        b.Add("leg-bl", "body", PartRole.LegBackLeft, new(-4.2f, -5.5f, -9.5f), new(0, -7, 0), new(2, 7, 2), SurfaceKind.Moss, legSides, gaitPhase: .5f, gaitAmplitude: .34f);
        b.Add("leg-br", "body", PartRole.LegBackRight, new(4.2f, -5.5f, -9.5f), new(0, -7, 0), new(2, 7, 2), SurfaceKind.Moss, legSides, gaitPhase: 0, gaitAmplitude: .34f);
        b.Add("neck", "body", PartRole.Neck, new(0, 1.5f, 13), new(0, 0, 3.5f), new(3.2f, 3.2f, 3.5f), SurfaceKind.Fur, tilt: new(.3f, 0, 0));
        b.Add("head", "neck", PartRole.Head, new(0, 0, 7), new(0, -.5f, 3.5f), new(3.4f, 3.6f, 4), SurfaceKind.Stone, tilt: new(.35f, 0, 0));
        b.Add("horn-l", "head", PartRole.Horn, new(-3.4f, 2.6f, 1.5f), new(-4.2f, 0, 0), new(4.2f, .8f, .8f), SurfaceKind.Stone, tilt: new(0, 0, -.45f));
        b.Add("horn-r", "head", PartRole.Horn, new(3.4f, 2.6f, 1.5f), new(4.2f, 0, 0), new(4.2f, .8f, .8f), SurfaceKind.Stone, tilt: new(0, 0, .45f));
        b.Add("tail", "body", PartRole.Tail, new(0, 3, -13), new(0, 0, -4), new(.8f, .8f, 4), SurfaceKind.Fur, tilt: new(-.9f, 0, 0), gaitAmplitude: .25f);
        return new()
        {
            Id = "kerbau-langit", Archetype = Archetype.Quadruped, Parts = b.Build(),
            Name = new("Kerbau Langit", "Sky Buffalo"), Epithet = new("Penjaga Pertama · Sabana Bromo", "The First Guardian · Bromo Savannah"),
            Cores = [new("punuk", b["hump"], 1, 1, new(0, 0), 100), new("dahi", b["head"], 1, 1, new(.2f, 0), 120)],
            Attacks = [AttackKind.Stomp, AttackKind.Charge, AttackKind.ShakeOff],
            RootHeight = 19.5f, Height = 31, WalkSpeed = 5.5f, TurnRate = .5f, StrideLength = 18, RageThresholds = [.6f], ShakeInterval = 8.5f, ShakeStrength = .9f, AttackCooldown = 5.5f,
            GlowColor = Gold, BodyColor = new(.42f, .38f, .32f),
            Hint = new("Panjat kaki yang berlumut. Punuknya adalah tempat aman untuk mengatur napas.",
                       "Climb the mossy legs. The hump is a safe place to catch your breath."),
            Lore = new("Dalam prasasti tertua ia disebut penarik bajak langit: kerbau yang membajak awan agar hujan jatuh tepat waktu di tanah Jawa.",
                       "The oldest inscriptions call it the plough of the sky: a buffalo that furrowed the clouds so rain fell on Java in its season."),
            Inscription = new("\"Ketika bajak berhenti, bumi lupa musimnya.\"", "\"When the plough stops, the earth forgets its seasons.\"")
        };
    }

    private static ColossusBlueprint GedungTitan()
    {
        var b = new PartBuilder();
        b.Add("pelvis", null, PartRole.Pelvis, Vector3.Zero, Vector3.Zero, new(9, 3.5f, 6), SurfaceKind.Stone);
        var sides = Faces(negY: false, posY: false);
        b.Add("leg-l", "pelvis", PartRole.LegLeft, new(-5.5f, -3.5f, 0), new(0, -13, 0), new(3.6f, 13, 3.6f), SurfaceKind.Stone, sides, gaitPhase: 0, gaitAmplitude: .28f);
        b.Add("leg-r", "pelvis", PartRole.LegRight, new(5.5f, -3.5f, 0), new(0, -13, 0), new(3.6f, 13, 3.6f), SurfaceKind.Stone, sides, gaitPhase: .5f, gaitAmplitude: .28f);
        b.Add("torso", "pelvis", PartRole.Torso, new(0, 3.5f, 0), new(0, 15, 0), new(11, 15, 7.5f), SurfaceKind.Glass, Faces(posZ: false));
        b.Add("head", "torso", PartRole.Head, new(0, 30, 0), new(0, 5, 0), new(5.5f, 5, 5.5f), SurfaceKind.Stone);
        b.Add("crown", "head", PartRole.Ornament, new(0, 10, 0), new(0, 4, 0), new(3, 4, 3), SurfaceKind.Glass, None);
        b.Add("arm-l", "torso", PartRole.ArmLeft, new(-11, 27, 0), new(-3.5f, -13, 0), new(3.5f, 13, 3.5f), SurfaceKind.Stone, sides, gaitPhase: .5f, gaitAmplitude: .2f);
        b.Add("arm-r", "torso", PartRole.ArmRight, new(11, 27, 0), new(3.5f, -13, 0), new(3.5f, 13, 3.5f), SurfaceKind.Stone, sides, gaitPhase: 0, gaitAmplitude: .2f);
        return new()
        {
            Id = "gedung-titan", Archetype = Archetype.Biped, Parts = b.Build(),
            Name = new("Gedung Titan", "Tower Titan"), Epithet = new("Penjaga Kota · Jakarta Hyper City", "Guardian of the City · Jakarta Hyper City"),
            Cores =
            [
                new("punggung", b["torso"], 2, -1, new(.55f, 0), 110, Plated: true),
                new("lengan", b["arm-r"], 0, 1, new(.2f, 0), 90, Plated: true),
                new("mahkota", b["head"], 1, 1, new(0, 0), 120)
            ],
            Attacks = [AttackKind.Stomp, AttackKind.Sweep, AttackKind.Roar, AttackKind.ShakeOff],
            RootHeight = 29.5f, Height = 72, WalkSpeed = 4f, TurnRate = .35f, StrideLength = 22, ShakeInterval = 7f, HackSusceptibility = 1.6f, SealOpenSeconds = 22,
            Signature = Signature.GlassShards,
            GlowColor = new(.35f, .95f, .95f), BodyColor = new(.5f, .52f, .56f),
            Hint = new("Dua inti tertutup pelat beton. Retas Gedung Titan dengan GARUDA agar ia berlutut dan pelatnya terbuka.",
                       "Two cores sit behind concrete plates. Hack the Tower Titan with GARUDA so it kneels and the plates open."),
            Lore = new("Kota dibangun di atas kota. Di bawah fondasi Sudirman ada candi tanpa nama, dan penjaganya belajar menyamar sebagai menara.",
                       "Cities are built on cities. Under Sudirman's foundations lies a nameless temple, and its guardian learned to pass for a tower."),
            Inscription = new("\"Apa yang dibangun tinggi, dijaga dari dalam.\"", "\"What is built high is guarded from within.\"")
        };
    }

    private static ColossusBlueprint DewiPadi()
    {
        var b = new PartBuilder();
        b.Add("pelvis", null, PartRole.Pelvis, Vector3.Zero, Vector3.Zero, new(6, 3, 4.5f), SurfaceKind.Moss);
        b.Add("skirt", "pelvis", PartRole.Ornament, new(0, -3, 0), new(0, -6, 0), new(8, 6, 6), SurfaceKind.Moss, Faces(negY: false));
        var sides = Faces(negY: false, posY: false);
        b.Add("leg-l", "pelvis", PartRole.LegLeft, new(-3.5f, -3, 0), new(0, -9, 0), new(2, 9, 2), SurfaceKind.Moss, sides, gaitPhase: 0, gaitAmplitude: .22f);
        b.Add("leg-r", "pelvis", PartRole.LegRight, new(3.5f, -3, 0), new(0, -9, 0), new(2, 9, 2), SurfaceKind.Moss, sides, gaitPhase: .5f, gaitAmplitude: .22f);
        b.Add("torso", "pelvis", PartRole.Torso, new(0, 3, 0), new(0, 9, 0), new(5.5f, 9, 3.5f), SurfaceKind.Moss);
        b.Add("head", "torso", PartRole.Head, new(0, 18, 0), new(0, 3.5f, 0), new(3, 3.5f, 3), SurfaceKind.Stone);
        b.Add("crown", "head", PartRole.Ornament, new(0, 7, 0), new(0, 3, 0), new(4.5f, 3, 1), SurfaceKind.Gold);
        b.Add("arm-l", "torso", PartRole.ArmLeft, new(-5.5f, 16, 0), new(-2, -9, 0), new(1.8f, 9, 1.8f), SurfaceKind.Moss, sides, gaitPhase: .5f, gaitAmplitude: .15f);
        b.Add("arm-r", "torso", PartRole.ArmRight, new(5.5f, 16, 0), new(2, -9, 0), new(1.8f, 9, 1.8f), SurfaceKind.Moss, sides, gaitPhase: 0, gaitAmplitude: .15f);
        return new()
        {
            Id = "dewi-padi", Archetype = Archetype.Biped, Parts = b.Build(),
            Name = new("Dewi Padi", "Rice Goddess"), Epithet = new("Penjaga Subak · Terasering Bali", "Guardian of the Subak · Bali Rice Terraces"),
            Cores =
            [
                new("rok-sawah", b["skirt"], 2, 1, new(.3f, 0), 90),
                new("punggung", b["torso"], 2, -1, new(.2f, 0), 100),
                new("mahkota", b["crown"], 1, 1, new(0, 0), 110)
            ],
            Attacks = [AttackKind.Sweep, AttackKind.Roar, AttackKind.ShakeOff],
            RootHeight = 21, Height = 47, WalkSpeed = 3.5f, TurnRate = .45f, StrideLength = 14, SealOpenSeconds = 14, ShakeInterval = 7.5f,
            Signature = Signature.Flood,
            GlowColor = Gold, BodyColor = new(.42f, .5f, .3f),
            Hint = new("Segel intinya cepat menutup lagi. Pindai, lalu bergerak tanpa ragu.",
                       "Her seals close again quickly. Scan, then move without hesitating."),
            Lore = new("Subak bukan hanya saluran air; ia adalah kesepakatan. Dewi Padi menjaga kesepakatan itu ketika manusia lupa berbagi.",
                       "The subak is not only a channel of water; it is an agreement. The Rice Goddess keeps it when people forget to share."),
            Inscription = new("\"Air yang dibagi tidak pernah habis.\"", "\"Water that is shared never runs dry.\"")
        };
    }

    private static ColossusBlueprint KomodoAgung()
    {
        var b = new PartBuilder();
        b.Add("body", null, PartRole.Body, Vector3.Zero, Vector3.Zero, new(8, 4.5f, 18), SurfaceKind.Stone, Faces(negY: false));
        var none = None;
        b.Add("leg-fl", "body", PartRole.LegFrontLeft, new(-7, -3, 12), new(0, -4.25f, 0), new(2.3f, 4.25f, 2.3f), SurfaceKind.Obsidian, none, gaitPhase: 0, gaitAmplitude: .42f);
        b.Add("leg-fr", "body", PartRole.LegFrontRight, new(7, -3, 12), new(0, -4.25f, 0), new(2.3f, 4.25f, 2.3f), SurfaceKind.Obsidian, none, gaitPhase: .5f, gaitAmplitude: .42f);
        b.Add("leg-bl", "body", PartRole.LegBackLeft, new(-7, -3, -12), new(0, -4.25f, 0), new(2.3f, 4.25f, 2.3f), SurfaceKind.Obsidian, none, gaitPhase: .5f, gaitAmplitude: .42f);
        b.Add("leg-br", "body", PartRole.LegBackRight, new(7, -3, -12), new(0, -4.25f, 0), new(2.3f, 4.25f, 2.3f), SurfaceKind.Obsidian, none, gaitPhase: 0, gaitAmplitude: .42f);
        b.Add("neck", "body", PartRole.Neck, new(0, .5f, 18), new(0, 0, 4), new(4, 3.5f, 4), SurfaceKind.Stone, tilt: new(.1f, 0, 0));
        b.Add("head", "neck", PartRole.Head, new(0, 0, 8), new(0, -.5f, 5), new(4.2f, 3, 5.5f), SurfaceKind.Stone, tilt: new(.1f, 0, 0));
        b.Add("tail-1", "body", PartRole.Tail, new(0, 1, -18), new(0, 0, -6), new(3.5f, 3, 6), SurfaceKind.Stone, tilt: new(-.22f, 0, 0), gaitPhase: 0, gaitAmplitude: .2f);
        b.Add("tail-2", "tail-1", PartRole.Tail, new(0, 0, -12), new(0, 0, -6), new(2.4f, 2, 6), SurfaceKind.Stone, tilt: new(-.12f, 0, 0), gaitPhase: .15f, gaitAmplitude: .28f);
        b.Add("tail-3", "tail-2", PartRole.Tail, new(0, 0, -12), new(0, 0, -5), new(1.3f, 1.3f, 5), SurfaceKind.Stone, tilt: new(-.05f, 0, 0), gaitPhase: .3f, gaitAmplitude: .35f);
        for (var i = 0; i < 3; i++)
            b.Add($"plate-{i}", "body", PartRole.Fin, new(0, 4.5f, 10 - i * 10), new(0, 2, 0), new(.8f, 2, 3), SurfaceKind.Obsidian, None);
        return new()
        {
            Id = "komodo-agung", Archetype = Archetype.Quadruped, Parts = b.Build(),
            Name = new("Komodo Agung", "Great Komodo"), Epithet = new("Penjaga Api Bumi · Perbukitan Flores", "Guardian of Earth-Fire · Flores Hills"),
            Cores =
            [
                new("pinggang", b["body"], 1, 1, new(-.25f, .45f), 100),
                new("tengkuk", b["neck"], 1, 1, new(0, 0), 100),
                new("ubun-ubun", b["head"], 1, 1, new(.1f, 0), 110)
            ],
            Attacks = [AttackKind.Stomp, AttackKind.Sweep, AttackKind.Charge, AttackKind.ShakeOff],
            RageThresholds = [.66f, .33f],
            RootHeight = 11.5f, Height = 24, WalkSpeed = 6.2f, TurnRate = .55f, StrideLength = 12, ShakeInterval = 6.5f, AttackCooldown = 4f,
            GlowColor = new(1f, .45f, .18f), BodyColor = new(.3f, .27f, .25f),
            Hint = new("Kakinya obsidian panas dan tak bisa dipanjat. Ekornya menyentuh tanah—mulailah dari sana.",
                       "Its legs are hot obsidian and cannot be climbed. The tail touches the ground—start there."),
            Lore = new("Para penjaga pulau berkata komodo pertama lahir dari lava yang mendingin. Komodo Agung adalah lava yang menolak mendingin.",
                       "Island keepers say the first komodo was born of cooling lava. The Great Komodo is lava that refused to cool."),
            Inscription = new("\"Yang sabar menunggu, yang lapar memburu.\"", "\"The patient wait; the hungry hunt.\"")
        };
    }

    private static ColossusBlueprint OrangutanPurba()
    {
        var b = new PartBuilder();
        b.Add("pelvis", null, PartRole.Pelvis, Vector3.Zero, Vector3.Zero, new(7, 4, 5), SurfaceKind.Fur);
        var sides = Faces(negY: false, posY: false);
        b.Add("leg-l", "pelvis", PartRole.LegLeft, new(-4.5f, -4, 0), new(0, -6, 0), new(2.8f, 6, 2.8f), SurfaceKind.Fur, sides, gaitPhase: 0, gaitAmplitude: .25f);
        b.Add("leg-r", "pelvis", PartRole.LegRight, new(4.5f, -4, 0), new(0, -6, 0), new(2.8f, 6, 2.8f), SurfaceKind.Fur, sides, gaitPhase: .5f, gaitAmplitude: .25f);
        b.Add("torso", "pelvis", PartRole.Torso, new(0, 4, 0), new(0, 11, 0), new(9, 11, 6), SurfaceKind.Fur, tilt: new(.55f, 0, 0));
        b.Add("head", "torso", PartRole.Head, new(0, 22, 1), new(0, 4, 1.5f), new(4.5f, 4.5f, 4.5f), SurfaceKind.Stone, tilt: new(-.5f, 0, 0));
        b.Add("arm-l", "torso", PartRole.ArmLeft, new(-9, 19, 0), new(-2.6f, -17, 0), new(3, 18, 3), SurfaceKind.Fur, sides, tilt: new(-.55f, 0, 0), gaitPhase: .5f, gaitAmplitude: .38f);
        b.Add("arm-r", "torso", PartRole.ArmRight, new(9, 19, 0), new(2.6f, -17, 0), new(3, 18, 3), SurfaceKind.Fur, sides, tilt: new(-.55f, 0, 0), gaitPhase: 0, gaitAmplitude: .38f);
        return new()
        {
            Id = "orangutan-purba", Archetype = Archetype.Biped, Parts = b.Build(),
            Name = new("Orangutan Purba", "Primeval Orangutan"), Epithet = new("Penjaga Kanopi · Hutan Hujan Kalimantan", "Guardian of the Canopy · Kalimantan Rainforest"),
            Cores =
            [
                new("punggung", b["torso"], 2, -1, new(.2f, 0), 100),
                new("lengan-kiri", b["arm-l"], 0, -1, new(.35f, 0), 90),
                new("kepala", b["head"], 1, 1, new(0, 0), 110)
            ],
            Attacks = [AttackKind.Stomp, AttackKind.Sweep, AttackKind.Roar, AttackKind.ShakeOff],
            RootHeight = 16, Height = 42, WalkSpeed = 4.8f, TurnRate = .6f, StrideLength = 14, ShakeInterval = 4.5f, ShakeStrength = 1.3f,
            GlowColor = Gold, BodyColor = new(.45f, .26f, .14f),
            Hint = new("Ia sering mengguncang tubuh. Tahan genggaman saat bulunya bergetar, dan pakai pohon untuk melompat ke lengannya.",
                       "It shakes often. Hold your grip when its fur trembles, and use the trees to leap onto its arms."),
            Lore = new("Orang hutan—manusia hutan. Leluhur Dayak menyebutnya kakak tertua, yang menanam hutan sebelum ada manusia untuk menebangnya.",
                       "Orang hutan—person of the forest. Dayak ancestors called it the eldest sibling, who planted the forest before anyone existed to fell it."),
            Inscription = new("\"Hutan adalah rumah yang tidak pernah dijual.\"", "\"The forest is a house that is never sold.\"")
        };
    }

    private static ColossusBlueprint Leviathan()
    {
        var b = new PartBuilder();
        b.Add("seg-0", null, PartRole.Segment, Vector3.Zero, Vector3.Zero, new(5, 4.5f, 7), SurfaceKind.Coral);
        b.Add("head", "seg-0", PartRole.Head, new(0, 1, 7), new(0, 0, 6), new(5, 4, 6), SurfaceKind.Stone);
        var previous = "seg-0";
        for (var i = 1; i < 9; i++)
        {
            var scale = 1 - i * .055f;
            var id = $"seg-{i}";
            // seg-0 is centred on its pivot; every later segment hangs off the previous segment's tail end.
            var jointZ = i == 1 ? -7 : -14 * (1 - (i - 1) * .055f);
            b.Add(id, previous, PartRole.Segment, new(0, 0, jointZ), new(0, 0, -7 * scale), new(5 * scale, 4.5f * scale, 7 * scale),
                SurfaceKind.Coral, gaitPhase: i * .11f, gaitAmplitude: .2f);
            if (i is 2 or 4 or 6)
                b.Add($"fin-{i}", id, PartRole.Fin, new(0, 4.5f * scale, -7 * scale), new(0, 3, 0), new(1, 3, 4 * scale), SurfaceKind.Coral);
            previous = id;
        }
        return new()
        {
            Id = "leviathan", Archetype = Archetype.Serpent, Parts = b.Build(), Floats = true,
            Name = new("Leviathan Nusantara", "Leviathan of the Archipelago"), Epithet = new("Penjaga Karang · Laguna Raja Ampat", "Guardian of the Reef · Raja Ampat Lagoon"),
            Cores =
            [
                new("sirip-dua", b["fin-2"], 1, 1, new(0, 0), 100),
                new("sirip-empat", b["fin-4"], 1, 1, new(0, 0), 100),
                new("sirip-enam", b["fin-6"], 1, 1, new(0, 0), 100)
            ],
            Attacks = [AttackKind.Sweep, AttackKind.Roar, AttackKind.Charge, AttackKind.ShakeOff],
            RootHeight = 1.2f, Height = 12, WalkSpeed = 7, TurnRate = .45f, StrideLength = 30, ShakeInterval = 6.5f,
            Signature = Signature.Dive,
            GlowColor = new(.4f, 1f, .85f), BodyColor = new(.38f, .45f, .42f),
            Hint = new("Tubuhnya muncul di antara bukit karst. Lompat dari tebing karst ke punggungnya saat ia lewat.",
                       "Its body surfaces between the karst hills. Leap from a karst cliff onto its back as it passes."),
            Lore = new("Pelaut Biak bercerita tentang naga laut yang menjahit pulau-pulau dengan tubuhnya agar tidak hanyut ke samudra.",
                       "Biak sailors tell of a sea dragon that stitched the islands together with its body so they would not drift into the ocean."),
            Inscription = new("\"Laut tidak memisahkan; laut menjahit.\"", "\"The sea does not divide; the sea stitches.\"")
        };
    }

    private static ColossusBlueprint PenjagaApiBiru()
    {
        var b = new PartBuilder();
        b.Add("pelvis", null, PartRole.Pelvis, Vector3.Zero, Vector3.Zero, new(7, 3, 5), SurfaceKind.Stone);
        b.Add("leg-l", "pelvis", PartRole.LegLeft, new(-4.8f, -3, 0), new(0, -7, 0), new(3.4f, 7, 3.4f), SurfaceKind.Obsidian, Faces(false, false, false, false, true, false), gaitPhase: 0, gaitAmplitude: .26f);
        b.Add("leg-r", "pelvis", PartRole.LegRight, new(4.8f, -3, 0), new(0, -7, 0), new(3.4f, 7, 3.4f), SurfaceKind.Obsidian, Faces(false, false, false, false, true, false), gaitPhase: .5f, gaitAmplitude: .26f);
        b.Add("torso", "pelvis", PartRole.Torso, new(0, 3, 0), new(0, 10, 0), new(10, 10, 6.5f), SurfaceKind.Stone, Faces(posZ: false));
        b.Add("head", "torso", PartRole.Head, new(0, 20, 0), new(0, 4, 0), new(4, 4, 4), SurfaceKind.Obsidian, Faces(posY: true, negX: false, posX: false, negY: false, negZ: true, posZ: false));
        b.Add("arm-l", "torso", PartRole.ArmLeft, new(-10, 17, 0), new(-3.5f, -10, 0), new(3.4f, 10, 3.4f), SurfaceKind.Stone, Faces(negY: false, posY: false), gaitPhase: .5f, gaitAmplitude: .2f);
        b.Add("arm-r", "torso", PartRole.ArmRight, new(10, 17, 0), new(3.5f, -10, 0), new(3.4f, 10, 3.4f), SurfaceKind.Stone, Faces(negY: false, posY: false), gaitPhase: 0, gaitAmplitude: .2f);
        return new()
        {
            Id = "penjaga-api-biru", Archetype = Archetype.Biped, Parts = b.Build(),
            Name = new("Penjaga Api Biru", "Blue Fire Warden"), Epithet = new("Penjaga Belerang · Kawah Ijen", "Guardian of Sulfur · Ijen Crater"),
            Cores =
            [
                new("punggung", b["torso"], 2, -1, new(.3f, 0), 100),
                new("bahu-kiri", b["arm-l"], 0, -1, new(.5f, 0), 90),
                new("ubun-ubun", b["head"], 1, 1, new(0, 0), 120)
            ],
            Attacks = [AttackKind.Stomp, AttackKind.Sweep, AttackKind.Roar, AttackKind.ShakeOff],
            RageThresholds = [.6f, .25f],
            RootHeight = 17, Height = 38, WalkSpeed = 4.6f, TurnRate = .55f, StrideLength = 13, ShakeInterval = 6, CorePulsePeriod = 1.2f, AttackCooldown = 3.8f,
            GlowColor = new(.3f, .6f, 1f), BodyColor = new(.16f, .17f, .2f),
            Hint = new("Gas belerang melayang di kawah. Di atas tubuhnya udara lebih bersih—tapi jangan lama-lama di kaki obsidiannya.",
                       "Sulfur gas drifts across the crater. The air is cleaner up on its body—but don't linger on its obsidian legs."),
            Lore = new("Para penambang belerang memikul tujuh puluh kilo setiap turun kawah. Mereka bilang api biru itu napas seseorang yang sedang tidur.",
                       "Sulfur miners carry seventy kilos up from the crater each trip. They say the blue fire is the breath of someone asleep."),
            Inscription = new("\"Api yang dingin warnanya, paling panas lukanya.\"", "\"The coldest-coloured fire leaves the hottest wound.\"")
        };
    }

    private static ColossusBlueprint KapalMajapahit()
    {
        var b = new PartBuilder();
        b.Add("hull", null, PartRole.Body, Vector3.Zero, Vector3.Zero, new(8, 5, 26), SurfaceKind.Wood, Faces(negY: false));
        b.Add("stern", "hull", PartRole.Ornament, new(0, 5, -19), new(0, 4, 0), new(7, 4, 6), SurfaceKind.Wood);
        b.Add("head", "hull", PartRole.Head, new(0, 3, 26), new(0, 4, 3.5f), new(2.6f, 5, 3.5f), SurfaceKind.Gold, tilt: new(-.35f, 0, 0));
        b.Add("mast-fore", "hull", PartRole.Mast, new(0, 5, 9), new(0, 15, 0), new(1, 15, 1), SurfaceKind.Wood, gaitAmplitude: .05f);
        b.Add("sail-fore", "mast-fore", PartRole.Sail, new(0, 17, 0), new(0, 0, 1.4f), new(9, 8, .4f), SurfaceKind.Cloth, gaitAmplitude: .1f);
        b.Add("mast-main", "hull", PartRole.Mast, new(0, 5, -5), new(0, 20, 0), new(1.2f, 20, 1.2f), SurfaceKind.Wood, gaitAmplitude: .05f);
        b.Add("sail-main", "mast-main", PartRole.Sail, new(0, 22, 0), new(0, 0, 1.6f), new(11, 10, .4f), SurfaceKind.Cloth, gaitAmplitude: .1f);
        return new()
        {
            Id = "kapal-majapahit", Archetype = Archetype.Ship, Parts = b.Build(), Floats = true,
            Name = new("Kapal Majapahit", "Majapahit Ship"), Epithet = new("Penjaga Armada · Laut Banda", "Guardian of the Fleet · Banda Sea"),
            Cores =
            [
                new("haluan", b["head"], 1, 1, new(0, 0), 100),
                new("buritan", b["stern"], 1, 1, new(0, 0), 100),
                new("puncak-tiang", b["mast-main"], 1, 1, new(0, 0), 90),
                new("layar", b["sail-fore"], 2, -1, new(.4f, 0), 90)
            ],
            Attacks = [AttackKind.Roar, AttackKind.Charge, AttackKind.Sweep, AttackKind.ShakeOff],
            RageThresholds = [.5f],
            RootHeight = 1.5f, Height = 48, WalkSpeed = 6.5f, TurnRate = .25f, StrideLength = 40, ShakeInterval = 7, ShakeStrength = 1.2f, SealOpenSeconds = 20,
            Signature = Signature.Cannons,
            GlowColor = Gold, BodyColor = new(.36f, .26f, .18f),
            Hint = new("Angin kencang datang bergelombang. Saat GARUDA memperingatkan hembusan, tahan genggaman di layar.",
                       "Gusts come in waves. When GARUDA warns of a gust, hold your grip on the sails."),
            Lore = new("Jung Majapahit konon sebesar pulau kecil. Satu di antaranya berlayar terlalu jauh ke timur dan memutuskan untuk tidak pernah pulang.",
                       "Majapahit junks were said to be the size of small islands. One sailed too far east and decided never to return."),
            Inscription = new("\"Pelaut sejati pulang membawa cerita, bukan emas.\"", "\"A true sailor comes home with stories, not gold.\"")
        };
    }

    /// <summary>The secret ninth guardian: GARUDA NET's own body, a colossal mechanical garuda.</summary>
    private static ColossusBlueprint GarudaAgung()
    {
        var b = new PartBuilder();
        b.Add("pelvis", null, PartRole.Pelvis, Vector3.Zero, Vector3.Zero, new(7, 3, 5), SurfaceKind.Metal);
        var sides = Faces(negY: false, posY: false);
        b.Add("leg-l", "pelvis", PartRole.LegLeft, new(-4.5f, -3, 0), new(0, -11, 0), new(2.6f, 11, 2.6f), SurfaceKind.Metal, sides, gaitPhase: 0, gaitAmplitude: .3f);
        b.Add("leg-r", "pelvis", PartRole.LegRight, new(4.5f, -3, 0), new(0, -11, 0), new(2.6f, 11, 2.6f), SurfaceKind.Metal, sides, gaitPhase: .5f, gaitAmplitude: .3f);
        b.Add("torso", "pelvis", PartRole.Torso, new(0, 3, 0), new(0, 10, 1), new(8, 10, 6), SurfaceKind.Gold);
        b.Add("neck", "torso", PartRole.Neck, new(0, 19, 3), new(0, 1, 3), new(2.5f, 2.5f, 3), SurfaceKind.Metal, tilt: new(-.35f, 0, 0));
        b.Add("head", "neck", PartRole.Head, new(0, 1, 6), new(0, 1.5f, 2.5f), new(3.5f, 3.5f, 4.5f), SurfaceKind.Gold, tilt: new(.35f, 0, 0));
        b.Add("beak", "head", PartRole.Ornament, new(0, .5f, 7), new(0, -.5f, 2.5f), new(1.3f, 1.3f, 2.5f), SurfaceKind.Gold, None, tilt: new(.25f, 0, 0));
        b.Add("crest", "head", PartRole.Ornament, new(0, 5, 0), new(0, 2.5f, -2), new(1, 2.5f, 3), SurfaceKind.Gold, None, tilt: new(-.4f, 0, 0));
        // Wings: long flat plates whose tops are walkable runways out to the wing-tip cores.
        b.Add("wing-l", "torso", PartRole.ArmLeft, new(-8, 17, -1), new(-13, -1.5f, -1), new(13, 1.2f, 5), SurfaceKind.Metal, gaitPhase: .5f, gaitAmplitude: .12f, tilt: new(0, 0, -.25f));
        b.Add("wing-r", "torso", PartRole.ArmRight, new(8, 17, -1), new(13, -1.5f, -1), new(13, 1.2f, 5), SurfaceKind.Metal, gaitPhase: 0, gaitAmplitude: .12f, tilt: new(0, 0, .25f));
        b.Add("tail", "pelvis", PartRole.Tail, new(0, 0, -5), new(0, -1.5f, -6), new(6, 1, 6), SurfaceKind.Metal, tilt: new(-.45f, 0, 0), gaitAmplitude: .15f);
        return new()
        {
            Id = "garuda-agung", Archetype = Archetype.Biped, Parts = b.Build(),
            Name = new("Garuda Agung", "Great Garuda"), Epithet = new("Penjaga Kesembilan · Titik Nol Nusantara", "The Ninth Guardian · Nusantara Point Zero"),
            Cores =
            [
                new("dada", b["torso"], 2, 1, new(.35f, 0), 130),
                new("sayap-kiri", b["wing-l"], 1, 1, new(0, -.7f), 100),
                new("sayap-kanan", b["wing-r"], 1, 1, new(0, .7f), 100),
                new("mahkota", b["head"], 1, 1, new(0, 0), 140)
            ],
            Attacks = [AttackKind.Stomp, AttackKind.Sweep, AttackKind.Roar, AttackKind.ShakeOff],
            RageThresholds = [.66f, .33f],
            RootHeight = 25, Height = 62, WalkSpeed = 4.5f, TurnRate = .5f, StrideLength = 16, ShakeInterval = 6, CorePulsePeriod = 1f, AttackCooldown = 4f, SealOpenSeconds = 18,
            HackSusceptibility = 0, Signature = Signature.Firewall,
            GlowColor = new(.3f, .95f, 1f), BodyColor = new(.55f, .45f, .25f),
            Hint = new("GARUDA tidak bisa meretas dirinya sendiri. Petir data menyambar setiap pemburu, bahkan yang sedang memanjat: terus bergerak. Puncak sayapnya bisa dipijak.",
                       "GARUDA cannot hack itself. Data bolts strike every hunter, even climbers: keep moving. The tops of its wings are walkable."),
            Lore = new("GARUDA NET tidak membangunkan para Penjaga. Ia adalah yang kesembilan: penjaga yang lahir dari data seluruh Nusantara, dan yang pertama memilih bangun sendiri.",
                       "GARUDA NET did not wake the Guardians. It is the ninth: a guardian born from the data of the whole archipelago, and the first to choose to wake itself."),
            Inscription = new("\"Yang menjaga para penjaga, harus dijaga oleh manusia.\"", "\"Whoever guards the guardians must be guarded by people.\"")
        };
    }
}
