using RoguesVRaidersServer;
using RoguesVRaidersServer.Services;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class ConfigServiceTests
{
    static (ModConfig Cfg, List<string> Warnings) Sanitize(Action<ModConfig> edit)
    {
        var cfg = new ModConfig();
        edit(cfg);
        var warnings = new List<string>();
        return (ConfigService.Sanitize(cfg, warnings.Add), warnings);
    }

    static SpawnPlanner.MapInput Customs() => new("bigmap", 40, "ZoneDormitory,ZoneGasStation");

    [Fact]
    public void DefaultConfigIsLeftAlone()
    {
        var (cfg, warnings) = Sanitize(_ => { });

        Assert.Empty(warnings);
        Assert.Equal("2,2,3,3,4", cfg.escortAmount);
        Assert.Equal(25, cfg.rogueChance);
        Assert.Equal(4, cfg.minArmorClass);
    }

    // The crash this guards: "" split with RemoveEmptyEntries gave an empty array, rng.Next(0) returned 0,
    // and indexing [0] threw inside OnLoad - which stops the entire SPT server, not just this mod.
    [Fact]
    public void PlanMapSurvivesEveryUnusableEscortAmount()
    {
        foreach (var raw in new[] { "", "   ", ",,,", "two", "99999999999", "-1", null })
        {
            var (cfg, _) = Sanitize(c => c.escortAmount = raw!);
            var plans = SpawnPlanner.PlanMap(Customs(), cfg, new Random(1));
            Assert.All(plans, p => Assert.InRange(p.Size, 3, 5));
        }
    }

    [Fact]
    public void UnusableEscortAmountWarnsAndReverts()
    {
        var (cfg, warnings) = Sanitize(c => c.escortAmount = "");

        Assert.Equal("2,2,3,3,4", cfg.escortAmount);
        Assert.Contains(warnings, w => w.Contains("escortAmount"));
    }

    [Fact]
    public void PartlyGarbledEscortAmountKeepsTheUsableCounts()
    {
        var counts = ConfigService.ParseEscorts("2,x,4");

        Assert.NotNull(counts);
        Assert.Equal([2, 4], counts!);
    }

    [Fact]
    public void EscortCountIsCappedSoSquadSizeCannotRunAway()
    {
        // Size is handed to the client as a bot-creation count, so an unbounded value here would ask the
        // host's game to build that many bot profiles.
        Assert.Null(ConfigService.ParseEscorts("999999"));

        var (cfg, _) = Sanitize(c => c.escortAmount = "999999");
        var plans = SpawnPlanner.PlanMap(Customs(), cfg, new Random(1));
        Assert.All(plans, p => Assert.InRange(p.Size, 3, 5));
    }

    [Fact]
    public void NullCollectionsBecomeUsableDefaults()
    {
        var (cfg, warnings) = Sanitize(c =>
        {
            c.rogueMaps = null!;
            c.raiderMaps = null!;
            c.overlapMaps = null!;
            c.chanceOverrides = null!;
            c.ammoRankWeights = null!;
        });

        Assert.NotNull(cfg.rogueMaps);
        Assert.NotNull(cfg.raiderMaps);
        Assert.NotNull(cfg.overlapMaps);
        Assert.Empty(cfg.chanceOverrides);
        Assert.NotEmpty(cfg.ammoRankWeights);
        Assert.Contains(warnings, w => w.Contains("chanceOverrides"));

        // PlanMap dereferences all three lists and the override dictionary.
        Assert.NotEmpty(SpawnPlanner.PlanMap(Customs(), cfg, new Random(1)));
    }

    [Fact]
    public void OutOfRangeChancesAreClamped()
    {
        var (cfg, warnings) = Sanitize(c =>
        {
            c.rogueChance = 5000;
            c.raiderChance = -20;
        });

        Assert.Equal(100, cfg.rogueChance);
        Assert.Equal(0, cfg.raiderChance);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void NotANumberRevertsRatherThanClamping()
    {
        var (cfg, _) = Sanitize(c => c.rogueChance = double.NaN);

        Assert.Equal(25, cfg.rogueChance);
    }

    [Fact]
    public void InvertedMidRaidWindowIsSwapped()
    {
        var (cfg, _) = Sanitize(c =>
        {
            c.midRaidEarliest = 0.8;
            c.midRaidLatest = 0.2;
        });

        Assert.Equal(0.2, cfg.midRaidEarliest);
        Assert.Equal(0.8, cfg.midRaidLatest);
    }

    [Fact]
    public void HugeMidRaidValuesClampIntoTheRaid()
    {
        var (cfg, _) = Sanitize(c => c.midRaidLatest = 1e308);

        Assert.Equal(1, cfg.midRaidLatest);
    }

    [Fact]
    public void ArmorClassAndDurabilityAreHeldInRange()
    {
        var (cfg, _) = Sanitize(c =>
        {
            c.minArmorClass = 99;
            c.weaponLowestMax = -5;
        });

        Assert.Equal(6, cfg.minArmorClass);
        Assert.Equal(1, cfg.weaponLowestMax);
    }

    [Fact]
    public void NegativeAmmoWeightsRevertToDefaults()
    {
        var (cfg, _) = Sanitize(c => c.ammoRankWeights = [60, -25, 10]);

        Assert.Equal([60, 25, 10], cfg.ammoRankWeights);
    }
}
