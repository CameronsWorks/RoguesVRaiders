using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;

namespace RoguesVRaidersServer.Services;

public static class QualityUpgrades
{
    public static readonly string[] TargetBossNames = ["exUsec", "pmcBot"];

    public static void ApplyDurability(DefaultDurability d,
        int weaponLowestMax, int weaponMaxDelta, int armorLowestMaxPercent, int armorMaxDelta)
    {
        d.Weapon.LowestMax = weaponLowestMax;
        d.Weapon.HighestMax = 100;
        d.Weapon.MaxDelta = weaponMaxDelta;
        d.Weapon.MinDelta = 0;

        d.Armor.LowestMaxPercent = armorLowestMaxPercent;
        d.Armor.HighestMaxPercent = 100;
        d.Armor.MaxDelta = armorMaxDelta;
        d.Armor.MinDelta = 0;
    }

    public static int ForceDifficulty(IEnumerable<BossLocationSpawn> waves, string difficulty)
    {
        var touched = 0;
        foreach (var wave in waves)
        {
            if (wave.BossName == null ||
                !TargetBossNames.Contains(wave.BossName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            wave.BossDifficulty = difficulty;
            wave.BossEscortDifficulty = difficulty;
            touched++;
        }
        return touched;
    }
}

#pragma warning disable CS0618 // ConfigServer is slated for removal in SPT 4.1; fine for ~4.0.0
[SPTarkov.DI.Annotations.Injectable(SPTarkov.DI.Annotations.InjectionType.Singleton)]
public class QualityUpgradeService(
    SPTarkov.Server.Core.Services.DatabaseService databaseService,
    SPTarkov.Server.Core.Servers.ConfigServer configServer,
    ConfigService configService,
    ISptLogger<QualityUpgradeService> logger)
{
    static readonly string[] BotTypeKeys = ["exusec", "pmcbot"];

    public void Apply()
    {
        var cfg = configService.Config;

        if (cfg.upgradeDurability)
        {
            var durabilities = configServer.GetConfig<SPTarkov.Server.Core.Models.Spt.Config.BotConfig>()
                .Durability.BotDurabilities;
            var hits = 0;
            foreach (var key in BotTypeKeys)
            {
                if (durabilities.TryGetValue(key, out var d))
                {
                    QualityUpgrades.ApplyDurability(d, cfg.weaponLowestMax, cfg.weaponMaxDelta,
                        cfg.armorLowestMaxPercent, cfg.armorMaxDelta);
                    hits++;
                }
            }
            var msg = $"[RvR] durability upgraded for {hits}/{BotTypeKeys.Length} bot types";
            if (hits < BotTypeKeys.Length) { logger.Warning(msg); } else { logger.Info(msg); }
        }

        if (cfg.upgradeAmmo)
        {
            var items = databaseService.GetItems();
            var pools = 0;
            var hits = 0;
            foreach (var key in BotTypeKeys)
            {
                if (!databaseService.GetBots().Types.TryGetValue(key, out var bot) || bot == null) { continue; }
                hits++;
                foreach (var pool in bot.BotInventory.Ammo.Values)
                {
                    AmmoRanker.Reweight(pool,
                        id => items.TryGetValue(id, out var item) && item.Properties != null
                            ? new AmmoRanker.AmmoProps(
                                item.Properties.PenetrationPower ?? 0,
                                item.Properties.Damage ?? 0,
                                item.Properties.AmmoType ?? "bullet")
                            : null,
                        cfg.ammoRankWeights, cfg.ammoTailWeight);
                    pools++;
                }
            }
            var ammoMsg = $"[RvR] {pools} ammo pools re-weighted across {hits}/{BotTypeKeys.Length} bot types";
            if (hits < BotTypeKeys.Length) { logger.Warning(ammoMsg); } else { logger.Info(ammoMsg); }
        }

        if (cfg.upgradeGearTier)
        {
            var items = databaseService.GetItems();
            SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateItem? Lookup(MongoId id) =>
                items.GetValueOrDefault(id);

            (EquipmentSlots slot, bool dropUnarmored, bool keepPlateCarriers)[] slotRules =
            [
                (EquipmentSlots.Headwear, true, false),
                (EquipmentSlots.ArmorVest, true, true),
                (EquipmentSlots.TacticalVest, false, true),
            ];

            var hits = 0;
            foreach (var key in BotTypeKeys)
            {
                if (!databaseService.GetBots().Types.TryGetValue(key, out var bot) || bot == null) { continue; }
                hits++;
                foreach (var (slot, dropUnarmored, keepPlateCarriers) in slotRules)
                {
                    if (!bot.BotInventory.Equipment.TryGetValue(slot, out var pool) || pool == null || pool.Count == 0)
                    {
                        continue;
                    }
                    var filtered = GearFilter.FilterPool(pool, cfg.minArmorClass, dropUnarmored, keepPlateCarriers, Lookup);
                    if (ReferenceEquals(filtered, pool))
                    {
                        logger.Warning($"[RvR] {key}/{slot} gear pool would be emptied by class {cfg.minArmorClass}+ filter, left untouched");
                    }
                    else
                    {
                        bot.BotInventory.Equipment[slot] = filtered;
                    }
                }
            }
            var gearMsg = $"[RvR] gear pools filtered to class {cfg.minArmorClass}+ for {hits}/{BotTypeKeys.Length} bot types";
            if (hits < BotTypeKeys.Length) { logger.Warning(gearMsg); } else { logger.Info(gearMsg); }

            var plateSlotIds = new[] { "front_plate", "back_plate", "left_side_plate", "right_side_plate" };
            var weighting = new ArmorPlateWeights
            {
                LevelRange = new MinMax<int>(0, int.MaxValue),
                Values = plateSlotIds.ToDictionary(id => id, _ => new Dictionary<string, double>
                {
                    ["4"] = 60,
                    ["5"] = 30,
                    ["6"] = 10,
                }),
            };
            var botConfig = configServer.GetConfig<SPTarkov.Server.Core.Models.Spt.Config.BotConfig>();
            var plateHits = 0;
            foreach (var key in BotTypeKeys)
            {
                if (botConfig.Equipment.TryGetValue(key, out var filters) && filters != null)
                {
                    filters.ArmorPlateWeighting = [weighting];
                    filters.FilterPlatesByLevel = true;
                    plateHits++;
                }
            }
            var plateMsg = $"[RvR] plate weighting (class 4/5/6 = 60/30/10) set for {plateHits}/{BotTypeKeys.Length} bot types";
            if (plateHits < BotTypeKeys.Length) { logger.Warning(plateMsg); } else { logger.Info(plateMsg); }
        }

        if (cfg.forceHardestDifficulty)
        {
            var touched = 0;
            foreach (var (_, location) in databaseService.GetLocations().GetDictionary())
            {
                if (location?.Base?.BossLocationSpawn != null)
                {
                    touched += QualityUpgrades.ForceDifficulty(location.Base.BossLocationSpawn, cfg.difficulty);
                }
            }
            logger.Info($"[RvR] {touched} rogue/raider waves forced to {cfg.difficulty}");
        }
    }
}
#pragma warning restore CS0618
