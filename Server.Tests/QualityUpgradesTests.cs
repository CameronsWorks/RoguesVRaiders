using RoguesVRaidersServer.Services;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class QualityUpgradesTests
{
    static DefaultDurability Stock() => new()
    {
        Armor = new ArmorDurability { MaxDelta = 10, MinDelta = 0, MinLimitPercent = 15 },
        Weapon = new WeaponDurability { LowestMax = 80, HighestMax = 100, MaxDelta = 40, MinDelta = 20, MinLimitPercent = 15 },
    };

    [Fact]
    public void DurabilityUpgradeSetsNinetyToHundredBand()
    {
        var d = Stock();
        QualityUpgrades.ApplyDurability(d, weaponLowestMax: 95, weaponMaxDelta: 5, armorLowestMaxPercent: 95, armorMaxDelta: 5);

        Assert.Equal(95, d.Weapon.LowestMax);
        Assert.Equal(100, d.Weapon.HighestMax);
        Assert.Equal(5, d.Weapon.MaxDelta);
        Assert.Equal(0, d.Weapon.MinDelta);

        Assert.Equal(95, d.Armor.LowestMaxPercent);
        Assert.Equal(100, d.Armor.HighestMaxPercent);
        Assert.Equal(5, d.Armor.MaxDelta);
        Assert.Equal(0, d.Armor.MinDelta);
    }

    [Fact]
    public void DifficultyRewriteOnlyTouchesRogueAndRaiderWaves()
    {
        var waves = new List<BossLocationSpawn>
        {
            new() { BossName = "exUsec", BossDifficulty = "normal", BossEscortDifficulty = "normal" },
            new() { BossName = "pmcBot", BossDifficulty = "normal", BossEscortDifficulty = "normal" },
            new() { BossName = "bossKnight", BossDifficulty = "normal", BossEscortDifficulty = "normal" },
        };

        var touched = QualityUpgrades.ForceDifficulty(waves, "impossible");

        Assert.Equal(2, touched);
        Assert.Equal("impossible", waves[0].BossDifficulty);
        Assert.Equal("impossible", waves[1].BossEscortDifficulty);
        Assert.Equal("normal", waves[2].BossDifficulty);
    }
}
