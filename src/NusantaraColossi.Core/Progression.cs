using System.Text.Json;
using System.Text.Json.Serialization;

namespace NusantaraColossi.Core;

public enum SkillTree { Explorer, Engineer, Survivalist }

public sealed record SkillDefinition(string Id, SkillTree Tree, int Tier, int Cost, LocText Name, LocText Description);

public static class Skills
{
    public static readonly SkillDefinition[] All =
    [
        new("genggaman-baja", SkillTree.Explorer, 1, 1, new("Genggaman Baja", "Iron Grip"),
            new("Stamina genggaman berkurang 20% lebih lambat.", "Grip stamina drains 20% slower.")),
        new("panjat-kilat", SkillTree.Explorer, 2, 1, new("Panjat Kilat", "Swift Climber"),
            new("Memanjat 25% lebih cepat dan stamina maksimum +20.", "Climb 25% faster and +20 max stamina.")),
        new("napas-kedua", SkillTree.Explorer, 3, 2, new("Napas Kedua", "Second Wind"),
            new("Sekali per pertarungan, stamina yang habis terisi kembali 40%.", "Once per fight, empty stamina refills to 40%.")),
        new("pemindai-cepat", SkillTree.Engineer, 1, 1, new("Pemindai Cepat", "Rapid Scan"),
            new("Jeda pemindaian GARUDA berkurang 30%.", "GARUDA scan cooldown reduced by 30%.")),
        new("retas-dalam", SkillTree.Engineer, 2, 1, new("Retas Dalam", "Deep Hack"),
            new("Retasan melumpuhkan 3 detik lebih lama; jeda retas berkurang 10 detik.", "Hacks stun 3 s longer; hack cooldown reduced by 10 s.")),
        new("segel-abadi", SkillTree.Engineer, 3, 2, new("Segel Terbuka", "Open Seals"),
            new("Segel inti tetap terbuka 50% lebih lama dan tusukan 15% lebih kuat.", "Core seals stay open 50% longer and stabs hit 15% harder.")),
        new("kulit-badak", SkillTree.Survivalist, 1, 1, new("Kulit Badak", "Rhino Hide"),
            new("Nyawa maksimum +30.", "+30 max health.")),
        new("jatuh-lunak", SkillTree.Survivalist, 2, 1, new("Jatuh Lunak", "Soft Landing"),
            new("Kerusakan jatuh berkurang 50%; menghindar memakai stamina 40% lebih sedikit.", "Fall damage halved; dodging costs 40% less stamina.")),
        new("naluri-pemburu", SkillTree.Survivalist, 3, 2, new("Naluri Pemburu", "Hunter's Instinct"),
            new("Lingkaran peringatan muncul di tempat serangan penjaga akan mendarat.", "Warning rings show where a guardian's attack will land."))
    ];

    public static SkillDefinition Get(string id) => All.First(s => s.Id == id);

    public static PlayerStats StatsFor(IReadOnlyCollection<string> owned)
    {
        var s = new PlayerStats();
        bool Has(string id) => owned.Contains(id);
        if (Has("genggaman-baja")) s = s with { GripDrainScale = .8f };
        if (Has("panjat-kilat")) s = s with { ClimbSpeed = s.ClimbSpeed * 1.25f, MaxStamina = s.MaxStamina + 20 };
        if (Has("napas-kedua")) s = s with { SecondWind = true };
        if (Has("pemindai-cepat")) s = s with { ScanCooldown = s.ScanCooldown * .7f };
        if (Has("retas-dalam")) s = s with { HackBonusSeconds = 3, HackCooldown = s.HackCooldown - 10 };
        if (Has("segel-abadi")) s = s with { SealScale = 1.5f, StabPower = 1.15f };
        if (Has("kulit-badak")) s = s with { MaxHealth = s.MaxHealth + 30 };
        if (Has("jatuh-lunak")) s = s with { FallDamageScale = .5f, DodgeCost = s.DodgeCost * .6f };
        if (Has("naluri-pemburu")) s = s with { HunterInstinct = true };
        return s;
    }
}

public sealed class TimeEntry
{
    public float Seconds { get; set; }
    public Rank Rank { get; set; }
    public bool Legend { get; set; }
    /// <summary>1 solo, 2–4 co-op.</summary>
    public int Hunters { get; set; } = 1;
    /// <summary>ISO date (yyyy-MM-dd), kept as text so saves stay culture-independent.</summary>
    public string Date { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class HuntRecord
{
    public float BestSeconds { get; set; }
    public Rank BestRank { get; set; }
    public int Victories { get; set; }
    public bool LegendCleared { get; set; }
}

/// <summary>Everything that persists between sessions.</summary>
public sealed class Profile
{
    public int Version { get; set; } = 1;
    public string PlayerName { get; set; } = "Arka";
    public Dictionary<string, HuntRecord> Hunts { get; set; } = [];
    public HashSet<string> Skills { get; set; } = [];
    public int SkillPoints { get; set; }
    public bool IntroSeen { get; set; }

    public bool IsDefeated(string colossusId) => Hunts.TryGetValue(colossusId, out var r) && r.Victories > 0;

    /// <summary>Regions unlock in order: each hunt opens the next. The secret finale opens once all eight have fallen.</summary>
    public bool IsUnlocked(RegionId region)
    {
        var definition = Regions.Get(region);
        if (definition.Secret) return Regions.Main.All(r => IsDefeated(r.ColossusId));
        var index = (int)region;
        return index == 0 || IsDefeated(Regions.All[index - 1].ColossusId);
    }

    /// <summary>Regions shown on the map: the eight, plus the finale once it is unlocked.</summary>
    public IEnumerable<RegionDefinition> VisibleRegions() => Regions.All.Where(r => !r.Secret || IsUnlocked(r.Id));

    public int DefeatedCount => Regions.Main.Count(r => IsDefeated(r.ColossusId));

    /// <summary>Local time-attack boards: the five fastest victories per guardian.</summary>
    public Dictionary<string, List<TimeEntry>> Leaderboards { get; set; } = [];

    public const int LeaderboardSize = 5;

    public IReadOnlyList<TimeEntry> Leaderboard(string colossusId) =>
        Leaderboards.TryGetValue(colossusId, out var list) ? list : [];

    /// <summary>Adds a run to the guardian's board. Returns its place (1 = fastest), or 0 if it didn't make the board.</summary>
    public int RecordTime(string colossusId, float seconds, Rank rank, bool legend, int hunters, DateTime when)
    {
        if (seconds <= 0) return 0;
        if (!Leaderboards.TryGetValue(colossusId, out var list)) Leaderboards[colossusId] = list = [];
        var entry = new TimeEntry { Seconds = seconds, Rank = rank, Legend = legend, Hunters = Math.Max(1, hunters), Date = when.ToString("yyyy-MM-dd"), Name = PlayerName };
        var index = list.FindIndex(e => seconds < e.Seconds);
        if (index < 0) index = list.Count;
        if (index >= LeaderboardSize) return 0;
        list.Insert(index, entry);
        if (list.Count > LeaderboardSize) list.RemoveRange(LeaderboardSize, list.Count - LeaderboardSize);
        return index + 1;
    }

    /// <summary>GARUDA learns new tricks as the Penjaga fall: hack after the first hunt, decoy after the second.</summary>
    public DroneAbilities Abilities =>
        DroneAbilities.Scan
        | (DefeatedCount >= 1 ? DroneAbilities.Hack : DroneAbilities.None)
        | (DefeatedCount >= 2 ? DroneAbilities.Decoy : DroneAbilities.None);

    public PlayerStats Stats => Core.Skills.StatsFor(Skills);

    public bool CanBuy(SkillDefinition skill) =>
        !Skills.Contains(skill.Id) && SkillPoints >= skill.Cost &&
        Core.Skills.All.Where(s => s.Tree == skill.Tree && s.Tier < skill.Tier).All(s => Skills.Contains(s.Id));

    public bool Buy(string skillId)
    {
        var skill = Core.Skills.Get(skillId);
        if (!CanBuy(skill)) return false;
        SkillPoints -= skill.Cost;
        Skills.Add(skillId);
        return true;
    }

    /// <summary>Record a finished hunt. Returns skill points awarded.</summary>
    public int RecordVictory(string colossusId, float seconds, Rank rank, bool legend)
    {
        var first = !IsDefeated(colossusId);
        if (!Hunts.TryGetValue(colossusId, out var record)) Hunts[colossusId] = record = new HuntRecord();
        var improvedToS = rank == Rank.S && record.BestRank != Rank.S;
        record.Victories++;
        if (record.BestSeconds <= 0 || seconds < record.BestSeconds) record.BestSeconds = seconds;
        if (rank > record.BestRank) record.BestRank = rank;
        var legendFirst = legend && !record.LegendCleared;
        if (legend) record.LegendCleared = true;
        var points = (first ? 2 : 0) + (improvedToS ? 1 : 0) + (legendFirst ? 1 : 0);
        SkillPoints += points;
        return points;
    }
}

[JsonSerializable(typeof(Profile))]
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
internal sealed partial class ProfileJson : JsonSerializerContext;

/// <summary>Atomic JSON save: write to a temp file, then replace. A corrupt file is kept aside, never overwritten silently.</summary>
public sealed class SaveStore(string directory)
{
    public string FilePath => Path.Combine(directory, "profile.json");

    public Profile Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath)) return new Profile();
            var profile = JsonSerializer.Deserialize(File.ReadAllText(FilePath), ProfileJson.Default.Profile) ?? new Profile();
            profile.SkillPoints = Math.Max(0, profile.SkillPoints);
            profile.Skills.RemoveWhere(id => Core.Skills.All.All(s => s.Id != id));
            return profile;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            warning = ex.Message;
            try { File.Copy(FilePath, FilePath + ".corrupt", true); } catch (IOException) { }
            return new Profile();
        }
    }

    public void Save(Profile profile)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, ProfileJson.Default.Profile));
        File.Move(temp, FilePath, true);
    }
}
