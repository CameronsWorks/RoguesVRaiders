using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Bot.GlobalSettings;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace RoguesVRaidersServer.Services;

public static class FactionHostility
{
    public static readonly WildSpawnType[] Warbands = [WildSpawnType.exUsec, WildSpawnType.pmcBot];
    public static readonly string[] WarbandTypeKeys = ["exusec", "pmcbot"];

    // bear/usec are stock PMC templates whose db keys aren't WildSpawnType names - the enum spells
    // those pmcBEAR/pmcUSEC - so the name test on its own would read them as third-party.
    static readonly string[] StockOnlyInDb = ["bear", "usec"];

    public static HashSet<string> StockTypeKeys()
    {
        var keys = new HashSet<string>(Enum.GetNames<WildSpawnType>(), StringComparer.OrdinalIgnoreCase);
        foreach (var key in StockOnlyInDb) keys.Add(key);
        return keys;
    }

    // A faction mod injects its roles as WildSpawnType values above the stock range, so anything the
    // enum doesn't define arrived with one.
    public static bool IsThirdParty(WildSpawnType role) => !Enum.IsDefined(role);

    public static List<string> ThirdPartyTypeKeys(IEnumerable<string> dbKeys, IEnumerable<string> exclusions)
    {
        var stock = StockTypeKeys();
        var skip = new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase);
        return dbKeys.Where(key => !stock.Contains(key) && !skip.Contains(key)).ToList();
    }

    public static IEnumerable<BotGlobalsMindSettings> MindsOf(BotType? type)
    {
        if (type?.BotDifficulty == null) yield break;
        foreach (var difficulty in type.BotDifficulty.Values)
        {
            if (difficulty?.Mind != null) yield return difficulty.Mind;
        }
    }

    // IsPlayerEnemy reads FRIENDLY, then WARN, then ENEMY and returns at the first list that names the
    // role, so a leftover entry on either of the first two quietly outranks the enemy listing.
    public static void MakeEnemies(BotGlobalsMindSettings mind, IEnumerable<WildSpawnType> roles)
    {
        var enemies = mind.EnemyBotTypes ??= [];
        foreach (var role in roles)
        {
            if (!enemies.Contains(role)) enemies.Add(role);
            mind.FriendlyBotTypes?.RemoveAll(listed => listed == role);
            mind.WarnBotTypes?.RemoveAll(listed => listed == role);
        }
    }

    public static bool ListsAll(BotGlobalsMindSettings mind, IEnumerable<WildSpawnType> roles) =>
        mind.EnemyBotTypes != null && roles.All(mind.EnemyBotTypes.Contains);

    public static void CollectThirdPartyRoles(BotGlobalsMindSettings mind, ISet<WildSpawnType> into)
    {
        foreach (var list in new[] { mind.EnemyBotTypes, mind.WarnBotTypes, mind.FriendlyBotTypes, mind.RevengeBotTypes })
        {
            if (list == null) continue;
            foreach (var role in list)
            {
                if (IsThirdParty(role)) into.Add(role);
            }
        }
    }
}

// Faction mods (RUAF, UNTAR, Black Division, Blackout, Wedge, ...) each decide for themselves who their
// bots fight, and they don't agree: RUAF names both rogues and raiders on its enemy list and puts itself
// on theirs, while UNTAR names only cultists and the infected and never lists a warband at all - so UNTAR
// and a rogue squad share a raid as neutrals and walk past each other. This levels that out in the bot db,
// where BotsGroup reads it at group construction, rather than leaving it to the client's reconcile tick:
// every third-party bot type becomes an enemy of both warbands and both warbands of it. Installed mods
// only - a faction that isn't there has no db entry to touch.
[Injectable(InjectionType.Singleton)]
public class FactionHostilityService(
    DatabaseService databaseService,
    ConfigService configService,
    ISptLogger<FactionHostilityService> logger)
{
    public void Apply()
    {
        var cfg = configService.Config;
        if (!cfg.customFactionHostility) return;

        var botDb = databaseService.GetBots().Types;

        // Their role ints, read back out of wherever a mod has already cross-listed them - the db keeps
        // names, not ints, so this is the only place they surface without taking a MoreBotsAPI reference.
        // A faction nobody has named on any list stays invisible here; the client seeder still turns our
        // own squads onto it.
        var theirRoles = new HashSet<WildSpawnType>();
        foreach (var type in botDb.Values)
        {
            foreach (var mind in FactionHostility.MindsOf(type))
            {
                FactionHostility.CollectThirdPartyRoles(mind, theirRoles);
            }
        }

        var wired = new List<string>();
        var failed = new List<string>();
        foreach (var key in FactionHostility.ThirdPartyTypeKeys(botDb.Keys, cfg.customFactionExclusions))
        {
            var minds = FactionHostility.MindsOf(botDb[key]).ToList();
            if (minds.Count == 0) continue;

            var ok = true;
            foreach (var mind in minds)
            {
                FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);
                ok &= FactionHostility.ListsAll(mind, FactionHostility.Warbands);
            }
            (ok ? wired : failed).Add(key);
        }

        if (wired.Count == 0 && failed.Count == 0)
        {
            logger.Info("[RvR] no third-party faction bots installed - nothing to make hostile");
            return;
        }

        var reach = new List<string>();
        foreach (var key in FactionHostility.WarbandTypeKeys)
        {
            if (!botDb.TryGetValue(key, out var warband)) continue;

            var minds = FactionHostility.MindsOf(warband).ToList();
            foreach (var mind in minds)
            {
                FactionHostility.MakeEnemies(mind, theirRoles);
            }
            var listed = minds.Count == 0 ? 0 : minds.Min(m => m.EnemyBotTypes!.Count(FactionHostility.IsThirdParty));
            reach.Add($"{key} {listed}");
        }

        var names = string.Join(", ", wired.Take(8)) + (wired.Count > 8 ? ", ..." : "");
        var summary = $"{wired.Count} third-party bot type(s) fight both warbands ({names}); "
                    + $"third-party roles on each warband's enemy list: {string.Join(", ", reach)}";

        if (failed.Count == 0 && theirRoles.Count > 0)
        {
            logger.Info($"[RvR] custom-faction hostility: {summary}");
        }
        else
        {
            var stalled = failed.Count > 0 ? $"; write failed on {string.Join(", ", failed)}" : "";
            var blind = theirRoles.Count == 0 ? "; none of them are cross-listed anywhere, so the warbands only aggro them through the in-raid seeder" : "";
            logger.Warning($"[RvR] custom-faction hostility incomplete: {summary}{stalled}{blind}");
        }
    }
}
