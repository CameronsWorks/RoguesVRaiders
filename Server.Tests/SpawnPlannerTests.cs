using RoguesVRaidersServer;
using RoguesVRaidersServer.Services;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class SpawnPlannerTests
{
    static ModConfig Cfg() => new();

    static SpawnPlanner.MapInput Customs() =>
        new("bigmap", 40, "ZoneBrige,ZoneCrossRoad,ZoneDormitory,ZoneGasStation,ZoneFactoryCenter,ZoneScavBase");

    [Fact]
    public void HomeMapGetsOneRogueSquad()
    {
        var plans = SpawnPlanner.PlanMap(Customs(), Cfg(), new Random(1));
        var p = Assert.Single(plans);
        Assert.Equal("rogue", p.Faction);
        Assert.Equal("exUsec", p.BossType);
        Assert.Equal(25, p.Chance);
        Assert.StartsWith("sipto.rvr.rogue.bigmap.", p.TriggerId);
        Assert.InRange(p.Size, 3, 5);
        Assert.Equal("impossible", p.Difficulty);
    }

    [Fact]
    public void OverlapMapGetsBothFactionsAtOverlapChance()
    {
        var map = new SpawnPlanner.MapInput("tarkovstreets", 50, "ZoneCarShowroom,ZoneCinema,ZoneConcordia_1");
        var plans = SpawnPlanner.PlanMap(map, Cfg(), new Random(1));
        Assert.Equal(2, plans.Count);
        Assert.Contains(plans, p => p.BossType == "exUsec");
        Assert.Contains(plans, p => p.BossType == "pmcBot");
        Assert.All(plans, p => Assert.Equal(35, p.Chance));
    }

    [Fact]
    public void ChanceOverrideWins()
    {
        var map = new SpawnPlanner.MapInput("rezervbase", 40, "ZoneBarrack,ZonePTOR1,ZoneSubStorage,ZoneRailStrorage,ZoneSubCommand");
        var plans = SpawnPlanner.PlanMap(map, Cfg(), new Random(1));
        var p = Assert.Single(plans);
        Assert.Equal("pmcBot", p.BossType);
        Assert.Equal(15, p.Chance);
    }

    [Fact]
    public void VanillaZonesAreExcludedFromPicks()
    {
        var map = new SpawnPlanner.MapInput("rezervbase", 40, "ZoneBarrack,ZoneRailStrorage,ZoneSubCommand");
        for (var seed = 0; seed < 25; seed++)
        {
            var p = SpawnPlanner.PlanMap(map, Cfg(), new Random(seed)).Single();
            Assert.Equal("ZoneBarrack", p.Zone);
        }
    }

    [Fact]
    public void SniperZonesAreNeverPicked()
    {
        var map = new SpawnPlanner.MapInput("bigmap", 40, "ZoneSnipeBrige,ZoneSnipeTower,ZoneBlockPostSniper3,ZoneDormitory");
        for (var seed = 0; seed < 25; seed++)
        {
            var p = SpawnPlanner.PlanMap(map, Cfg(), new Random(seed)).Single();
            Assert.Equal("ZoneDormitory", p.Zone);
        }
    }

    [Fact]
    public void DuplicateZonesDoNotBiasThePick()
    {
        var map = new SpawnPlanner.MapInput("woods", 40, "ZoneHouse,ZoneHouse,ZoneBigRocks");
        var houseCount = 0;
        for (var seed = 0; seed < 500; seed++)
        {
            var p = SpawnPlanner.PlanMap(map, Cfg(), new Random(seed)).Single();
            if (p.Zone == "ZoneHouse") houseCount++;
        }
        Assert.InRange(houseCount, 200, 300);
    }

    [Fact]
    public void AllVanillaZonesExcludedMeansEngineChoosesZone()
    {
        var map = new SpawnPlanner.MapInput("rezervbase", 40, "ZoneRailStrorage,ZoneSubCommand");
        var p = SpawnPlanner.PlanMap(map, Cfg(), new Random(3)).Single();
        Assert.Equal("", p.Zone);
    }

    [Fact]
    public void UnknownMapPlansNothing()
    {
        var map = new SpawnPlanner.MapInput("labyrinth", 60, "BotZone");
        Assert.Empty(SpawnPlanner.PlanMap(map, Cfg(), new Random(1)));
    }

    [Fact]
    public void FireTimesAreStartOrMidRaidWindow()
    {
        var cfg = Cfg();
        var sawStart = false;
        var sawMid = false;
        for (var seed = 0; seed < 50; seed++)
        {
            var p = SpawnPlanner.PlanMap(Customs(), cfg, new Random(seed)).Single();
            if (p.FireAtSeconds == 0) { sawStart = true; }
            else
            {
                sawMid = true;
                Assert.InRange(p.FireAtSeconds, (int)(0.10 * 40 * 60), (int)(0.60 * 40 * 60));
            }
        }
        Assert.True(sawStart);
        Assert.True(sawMid);
    }

    [Fact]
    public void DebugAlwaysSpawnForcesChanceAndTime()
    {
        var cfg = Cfg();
        cfg.debugAlwaysSpawn = true;
        var p = SpawnPlanner.PlanMap(Customs(), cfg, new Random(1)).Single();
        Assert.Equal(100, p.Chance);
        Assert.Equal(60, p.FireAtSeconds);
    }

    [Fact]
    public void TriggerIdsCarryTheMarker()
    {
        var p = SpawnPlanner.PlanMap(Customs(), Cfg(), new Random(1)).Single();
        Assert.StartsWith(SpawnPlanner.Marker, p.TriggerId);
    }
}
