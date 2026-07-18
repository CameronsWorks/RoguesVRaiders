namespace RoguesVRaidersServer.Services;

public static class SpawnPlanner
{
    public const string Marker = "sipto.rvr.";

    static readonly int[] DefaultEscorts = [2, 2, 3, 3, 4];

    public record MapInput(string MapId, double EscapeMinutes, string OpenZones);

    public record SquadPlan(string TriggerId, string Faction, string BossType,
        double Chance, string Zone, int FireAtSeconds, int Size, string Difficulty);

    // Vanilla same-type spawn zones (and Goons anchors) we keep our squads out of.
    static readonly Dictionary<string, string[]> VanillaZones = new()
    {
        ["lighthouse"] = ["Zone_Blockpost", "Zone_RoofContainers", "Zone_TreatmentRocks",
            "Zone_TreatmentBeach", "Zone_Island", "Zone_RoofRocks", "Zone_RoofBeach",
            "Zone_Hellicopter", "Zone_TreatmentContainers", "Zone_Chalet"],
        ["rezervbase"] = ["ZoneRailStrorage", "ZoneSubCommand"],
        ["bigmap"] = ["ZoneScavBase"],
        ["woods"] = ["ZoneScavBase2"],
        ["shoreline"] = ["ZoneMeteoStation"],
    };

    public static List<SquadPlan> PlanMap(MapInput map, ModConfig cfg, Random rng)
    {
        var plans = new List<SquadPlan>();
        var overlap = cfg.overlapMaps.Contains(map.MapId);

        if (overlap || cfg.rogueMaps.Contains(map.MapId))
        {
            plans.Add(Plan(map, cfg, rng, "rogue", "exUsec", overlap));
        }
        if (overlap || cfg.raiderMaps.Contains(map.MapId))
        {
            plans.Add(Plan(map, cfg, rng, "raider", "pmcBot", overlap));
        }
        return plans;
    }

    static SquadPlan Plan(MapInput map, ModConfig cfg, Random rng, string faction, string bossType, bool overlap)
    {
        var chance = cfg.chanceOverrides.TryGetValue(map.MapId, out var over) ? over
            : overlap ? cfg.overlapChance
            : faction == "rogue" ? cfg.rogueChance : cfg.raiderChance;

        var fireAt = rng.NextDouble() < cfg.startSpawnShare ? 0
            : (int)(Lerp(cfg.midRaidEarliest, cfg.midRaidLatest, rng.NextDouble()) * map.EscapeMinutes * 60);

        if (cfg.debugAlwaysSpawn)
        {
            chance = 100;
            fireAt = 60;
        }

        // Total squad = boss + escorts; escortAmount holds escort-only counts (2-4), so +1 boss = 3-5.
        // ConfigService vets the string on load; the fallback covers PlanMap being called directly.
        var escortCounts = ConfigService.ParseEscorts(cfg.escortAmount) ?? DefaultEscorts;
        var size = 1 + escortCounts[rng.Next(escortCounts.Length)];

        // Stable id, never randomized: the raid consumes a snapshot of the location that can be
        // taken from an older roll than the plan the client fetches. Zones and times may drift
        // between the two, but the trigger has to match or nothing ever spawns.
        return new SquadPlan(
            $"{Marker}{faction}.{map.MapId}.1",
            faction, bossType, chance, PickZone(map, rng), fireAt, size, cfg.difficulty);
    }

    static string PickZone(MapInput map, Random rng)
    {
        var excluded = VanillaZones.TryGetValue(map.MapId, out var v) ? v : [];
        var candidates = map.OpenZones
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Where(z => !excluded.Contains(z) && z.IndexOf("snipe", StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();
        return candidates.Count == 0 ? "" : candidates[rng.Next(candidates.Count)];
    }

    static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
