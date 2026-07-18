using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace RoguesVRaidersServer.Services;

[Injectable(InjectionType.Singleton)]
public class ConfigService
{
    // A squad is one boss plus its escorts, and Size is handed to the client as a bot-creation count,
    // so this is what stops a stray digit in the config from asking for thousands of bots.
    public const int MaxEscorts = 20;

    public ModConfig Config { get; }

    public ConfigService(ModHelper modHelper, ISptLogger<ConfigService> logger)
    {
        ModConfig? config = null;
        try
        {
            var modDir = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            config = modHelper.GetJsonDataFromFile<ModConfig>(modDir, "config.jsonc");
        }
        catch (Exception ex)
        {
            logger.Error($"[RvR] failed to load config.jsonc, falling back to defaults: {ex}");
        }

        if (config == null)
        {
            logger.Error("[RvR] config.jsonc missing or empty, falling back to defaults");
        }

        Config = Sanitize(config ?? new ModConfig(), msg => logger.Warning($"[RvR] {msg}"));
    }

    // The load above only rescues malformed JSON. A well-formed file holding an unusable value - an empty
    // escortAmount, an explicit null over a list, a chance of 5000 - used to surface as an exception during
    // boot, and nothing upstream catches that, so the whole server refuses to start. Everything unusable
    // reverts to its default here and says so in the log.
    public static ModConfig Sanitize(ModConfig cfg, Action<string> warn)
    {
        var def = new ModConfig();

        cfg.rogueMaps = OrDefault(cfg.rogueMaps, def.rogueMaps, nameof(cfg.rogueMaps), warn);
        cfg.raiderMaps = OrDefault(cfg.raiderMaps, def.raiderMaps, nameof(cfg.raiderMaps), warn);
        cfg.overlapMaps = OrDefault(cfg.overlapMaps, def.overlapMaps, nameof(cfg.overlapMaps), warn);

        if (cfg.chanceOverrides == null)
        {
            warn("chanceOverrides was null, treating it as empty");
            cfg.chanceOverrides = new Dictionary<string, double>();
        }

        cfg.rogueChance = Percent(cfg.rogueChance, def.rogueChance, nameof(cfg.rogueChance), warn);
        cfg.raiderChance = Percent(cfg.raiderChance, def.raiderChance, nameof(cfg.raiderChance), warn);
        cfg.overlapChance = Percent(cfg.overlapChance, def.overlapChance, nameof(cfg.overlapChance), warn);
        foreach (var map in cfg.chanceOverrides.Keys.ToList())
        {
            cfg.chanceOverrides[map] = Percent(cfg.chanceOverrides[map], 0, $"chanceOverrides[{map}]", warn);
        }

        if (ParseEscorts(cfg.escortAmount) == null)
        {
            warn($"escortAmount \"{cfg.escortAmount}\" holds no usable counts, using \"{def.escortAmount}\"");
            cfg.escortAmount = def.escortAmount;
        }

        cfg.startSpawnShare = Fraction(cfg.startSpawnShare, def.startSpawnShare, nameof(cfg.startSpawnShare), warn);
        cfg.midRaidEarliest = Fraction(cfg.midRaidEarliest, def.midRaidEarliest, nameof(cfg.midRaidEarliest), warn);
        cfg.midRaidLatest = Fraction(cfg.midRaidLatest, def.midRaidLatest, nameof(cfg.midRaidLatest), warn);
        if (cfg.midRaidLatest < cfg.midRaidEarliest)
        {
            warn($"midRaidLatest {cfg.midRaidLatest} lands before midRaidEarliest {cfg.midRaidEarliest}, swapping them");
            (cfg.midRaidEarliest, cfg.midRaidLatest) = (cfg.midRaidLatest, cfg.midRaidEarliest);
        }

        if (string.IsNullOrWhiteSpace(cfg.difficulty))
        {
            warn($"difficulty was empty, using \"{def.difficulty}\"");
            cfg.difficulty = def.difficulty;
        }

        cfg.weaponLowestMax = Ranged(cfg.weaponLowestMax, 1, 100, nameof(cfg.weaponLowestMax), warn);
        cfg.weaponMaxDelta = Ranged(cfg.weaponMaxDelta, 0, 100, nameof(cfg.weaponMaxDelta), warn);
        cfg.armorMaxDelta = Ranged(cfg.armorMaxDelta, 0, 100, nameof(cfg.armorMaxDelta), warn);
        cfg.minArmorClass = Ranged(cfg.minArmorClass, 1, 6, nameof(cfg.minArmorClass), warn);
        cfg.ammoTailWeight = Ranged(cfg.ammoTailWeight, 0, 1000, nameof(cfg.ammoTailWeight), warn);

        if (cfg.ammoRankWeights == null || cfg.ammoRankWeights.Count == 0 || cfg.ammoRankWeights.Any(w => w < 0))
        {
            warn($"ammoRankWeights was unusable, using [{string.Join(", ", def.ammoRankWeights)}]");
            cfg.ammoRankWeights = def.ammoRankWeights;
        }

        return cfg;
    }

    // "2,2,3,3,4" -> the escort counts a squad rolls between. Null when nothing usable parses, so callers
    // fall back instead of indexing an empty array. Unparseable entries are dropped individually, which
    // keeps "2,x,4" working as "2,4" rather than throwing the whole line away.
    public static int[]? ParseEscorts(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var counts = new List<int>();
        foreach (var part in raw.Split(','))
        {
            if (int.TryParse(part.Trim(), out var n) && n >= 0 && n <= MaxEscorts) counts.Add(n);
        }

        return counts.Count > 0 ? counts.ToArray() : null;
    }

    static List<string> OrDefault(List<string> value, List<string> fallback, string name, Action<string> warn)
    {
        if (value != null) return value;
        warn($"{name} was null, using the default list");
        return fallback;
    }

    static double Percent(double value, double fallback, string name, Action<string> warn) =>
        Ranged(value, 0, 100, fallback, name, warn);

    static double Fraction(double value, double fallback, string name, Action<string> warn) =>
        Ranged(value, 0, 1, fallback, name, warn);

    static double Ranged(double value, double min, double max, double fallback, string name, Action<string> warn)
    {
        if (double.IsNaN(value))
        {
            warn($"{name} was not a number, using {fallback}");
            return fallback;
        }
        if (value < min || value > max)
        {
            var clamped = Math.Clamp(value, min, max);
            warn($"{name} was {value}, clamped to {clamped}");
            return clamped;
        }
        return value;
    }

    static int Ranged(int value, int min, int max, string name, Action<string> warn)
    {
        if (value >= min && value <= max) return value;

        var clamped = Math.Clamp(value, min, max);
        warn($"{name} was {value}, clamped to {clamped}");
        return clamped;
    }
}
