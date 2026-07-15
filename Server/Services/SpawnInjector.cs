using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace RoguesVRaidersServer.Services;

[Injectable(InjectionType.Singleton)]
public class SpawnInjector(
    DatabaseService databaseService,
    ConfigService configService,
    ISptLogger<SpawnInjector> logger)
{
    public const string Marker = SpawnPlanner.Marker;

    static readonly object _sync = new();

    readonly Random _rng = new();

    public Dictionary<string, List<SpawnPlanner.SquadPlan>> CurrentPlan { get; private set; } = new();

    // Full re-roll: new chances, zones and times everywhere. Boot and raid end only -
    // never between a raid's location snapshot and the client fetching the plan.
    public void Refresh()
    {
        lock (_sync)
        {
            var cfg = configService.Config;
            var next = new Dictionary<string, List<SpawnPlanner.SquadPlan>>();

            // GetDictionary() keys by C# property name (Bigmap, RezervBase, TarkovStreets...), not
            // the lowercase map id our config and routes use, so read the real id off LocationBase.
            foreach (var location in databaseService.GetLocations().GetDictionary().Values)
            {
                var mapBase = location?.Base;
                if (mapBase?.BossLocationSpawn == null) { continue; }

                // LocationBase.Id casing is inconsistent across vanilla maps (bigmap/laboratory
                // are lowercase, Woods/RezervBase/TarkovStreets/Sandbox_high are not), so normalize.
                // Null-guard too: custom-map mods can ship locations without an Id.
                var mapId = mapBase.Id?.ToLowerInvariant();
                if (mapId == null) { continue; }

                if (mapId == "labyrinth")
                {
                    ApplyToMap(mapBase, null, cfg);
                    continue;
                }

                var input = new SpawnPlanner.MapInput(mapId, mapBase.EscapeTimeLimit ?? 40, mapBase.OpenZones ?? "");
                var plans = SpawnPlanner.PlanMap(input, cfg, _rng);
                ApplyToMap(mapBase, plans, cfg);
                if (plans.Count > 0) { next[mapId] = plans; }

                if (cfg.debugLogs && plans.Count > 0)
                {
                    logger.Info($"[RvR] {mapId}: {string.Join(", ", plans.Select(p => $"{p.Faction}@{p.Chance}% t={p.FireAtSeconds}s zone={(p.Zone == "" ? "(engine)" : p.Zone)}"))}");
                }
            }

            CurrentPlan = next;
        }
    }

    // Reassert the current roll without re-rolling. Raid start runs this: other spawn mods
    // rebuild the wave lists between raids, and the raid may snapshot the location before or
    // after this hook depending on the host - either way the waves must carry the exact
    // TriggerIds already in CurrentPlan, or the client arms triggers nothing listens for.
    public void EnsurePresent()
    {
        lock (_sync)
        {
            var cfg = configService.Config;
            var plan = CurrentPlan;
            var reasserted = 0;

            foreach (var location in databaseService.GetLocations().GetDictionary().Values)
            {
                var mapBase = location?.Base;
                if (mapBase?.BossLocationSpawn == null) { continue; }

                var mapId = mapBase.Id?.ToLowerInvariant();
                if (mapId == null) { continue; }

                plan.TryGetValue(mapId, out var plans);
                ApplyToMap(mapBase, plans, cfg);
                if (plans is { Count: > 0 }) { reasserted++; }
            }

            if (cfg.debugLogs)
            {
                logger.Info($"[RvR] reasserted squad waves on {reasserted} maps");
            }
        }
    }

    void ApplyToMap(LocationBase mapBase, List<SpawnPlanner.SquadPlan>? plans, ModConfig cfg)
    {
        mapBase.BossLocationSpawn!.RemoveAll(w => w.TriggerId?.StartsWith(Marker) == true);

        if (plans != null)
        {
            foreach (var plan in plans)
            {
                mapBase.BossLocationSpawn.Add(ToWave(plan, cfg));
            }
        }

        // ABPS (and anything else) rebuilds BossLocationSpawn wholesale at boot and on raid
        // end, wiping the difficulty rewrite of vanilla exUsec/pmcBot waves. Reapplying here
        // covers our own entries redundantly and the vanilla/ABPS-recreated ones meaningfully.
        if (cfg.forceHardestDifficulty)
        {
            QualityUpgrades.ForceDifficulty(mapBase.BossLocationSpawn, cfg.difficulty);
        }
    }

    static BossLocationSpawn ToWave(SpawnPlanner.SquadPlan plan, ModConfig cfg) => new()
    {
        BossName = plan.BossType,
        BossEscortType = plan.BossType,
        BossEscortAmount = cfg.escortAmount,
        BossChance = plan.Chance,
        BossDifficulty = cfg.difficulty,
        BossEscortDifficulty = cfg.difficulty,
        BossZone = plan.Zone,
        IsBossPlayer = false,
        Time = -1,
        Delay = 0,
        TriggerId = plan.TriggerId,
        TriggerName = "botEvent",
        IsRandomTimeSpawn = false,
        IgnoreMaxBots = true,
        ForceSpawn = true,
        DependKarma = false,
        DependKarmaPVE = false,
        ShowOnTarkovMap = false,
        ShowOnTarkovMapPvE = false,
        Supports = null!,
        SpawnMode = new[] { "regular", "pve" },
    };
}
