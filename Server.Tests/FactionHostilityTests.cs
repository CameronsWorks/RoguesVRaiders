using RoguesVRaidersServer.Services;
using SPTarkov.Server.Core.Models.Eft.Bot.GlobalSettings;
using SPTarkov.Server.Core.Models.Eft.Common;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class FactionHostilityTests
{
    // UNTAR 1170-1173, RUAF 848400-848406, Black Division 848420-848424 on this install; the exact ints
    // don't matter, only that the enum doesn't define them.
    const WildSpawnType Untar = (WildSpawnType)1170;
    const WildSpawnType Ruaf = (WildSpawnType)848400;

    static BotGlobalsMindSettings Mind(
        List<WildSpawnType>? enemy = null,
        List<WildSpawnType>? friendly = null,
        List<WildSpawnType>? warn = null,
        List<WildSpawnType>? revenge = null) => new()
    {
        EnemyBotTypes = enemy,
        FriendlyBotTypes = friendly,
        WarnBotTypes = warn,
        RevengeBotTypes = revenge,
    };

    [Theory]
    [InlineData(1170)]   // UNTAR
    [InlineData(848406)] // RUAF Remnant
    [InlineData(848420)] // Black Division
    public void ThirdPartyRolesAreTheOnesTheEnumDoesNotDefine(int role)
    {
        Assert.True(FactionHostility.IsThirdParty((WildSpawnType)role));
    }

    [Theory]
    [InlineData(WildSpawnType.exUsec)]
    [InlineData(WildSpawnType.pmcBot)]
    [InlineData(WildSpawnType.bossKnight)]
    [InlineData(WildSpawnType.assault)]
    public void StockRolesAreNotThirdParty(WildSpawnType role)
    {
        Assert.False(FactionHostility.IsThirdParty(role));
    }

    [Fact]
    public void ThirdPartyKeysKeepModdedEntriesAndDropStockOnes()
    {
        var keys = new[] { "exusec", "pmcbot", "assault", "bear", "usec", "followeruntar", "ruafrifleman" };

        var third = FactionHostility.ThirdPartyTypeKeys(keys, []);

        Assert.Equal(["followeruntar", "ruafrifleman"], third);
    }

    // bear/usec sit in the db but the enum spells them pmcBEAR/pmcUSEC, so a plain name test reads them
    // as modded and turns the AI PMCs into a faction they were never meant to be.
    [Fact]
    public void StockPmcTemplatesAreNotTreatedAsThirdParty()
    {
        Assert.Empty(FactionHostility.ThirdPartyTypeKeys(["bear", "usec"], []));
    }

    [Fact]
    public void ExcludedKeysAreLeftAlone()
    {
        var third = FactionHostility.ThirdPartyTypeKeys(["followeruntar", "ruafrifleman"], ["FollowerUntar"]);

        Assert.Equal(["ruafrifleman"], third);
    }

    [Fact]
    public void MakeEnemiesAddsBothWarbands()
    {
        var mind = Mind(enemy: []);

        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);

        Assert.Equal([WildSpawnType.exUsec, WildSpawnType.pmcBot], mind.EnemyBotTypes);
    }

    [Fact]
    public void MakeEnemiesFillsAMissingEnemyList()
    {
        var mind = Mind();

        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);

        Assert.True(FactionHostility.ListsAll(mind, FactionHostility.Warbands));
    }

    [Fact]
    public void MakeEnemiesIsIdempotent()
    {
        var mind = Mind(enemy: [WildSpawnType.exUsec]);

        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);
        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);

        Assert.Equal([WildSpawnType.exUsec, WildSpawnType.pmcBot], mind.EnemyBotTypes);
    }

    // IsPlayerEnemy reads friendly and warn first and returns there, so leaving a warband on either
    // list makes the enemy entry we just wrote unreachable.
    [Fact]
    public void MakeEnemiesClearsTheListsCheckedBeforeTheEnemyOne()
    {
        var mind = Mind(
            enemy: [],
            friendly: [WildSpawnType.exUsec, WildSpawnType.bossKnight],
            warn: [WildSpawnType.pmcBot, WildSpawnType.assault]);

        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);

        Assert.Equal([WildSpawnType.bossKnight], mind.FriendlyBotTypes);
        Assert.Equal([WildSpawnType.assault], mind.WarnBotTypes);
        Assert.True(FactionHostility.ListsAll(mind, FactionHostility.Warbands));
    }

    [Fact]
    public void MakeEnemiesLeavesOtherRolesAlone()
    {
        var mind = Mind(enemy: [Untar], friendly: [WildSpawnType.bossKnight], warn: [WildSpawnType.assault]);

        FactionHostility.MakeEnemies(mind, FactionHostility.Warbands);

        Assert.Contains(Untar, mind.EnemyBotTypes!);
        Assert.Equal([WildSpawnType.bossKnight], mind.FriendlyBotTypes);
        Assert.Equal([WildSpawnType.assault], mind.WarnBotTypes);
    }

    [Fact]
    public void CollectingRolesReadsEveryListAndSkipsStockOnes()
    {
        var into = new HashSet<WildSpawnType>();

        FactionHostility.CollectThirdPartyRoles(
            Mind(enemy: [Untar, WildSpawnType.assault], warn: [Ruaf], friendly: null, revenge: [WildSpawnType.exUsec]),
            into);

        Assert.Equal([Untar, Ruaf], into.OrderBy(role => (int)role));
    }

    [Fact]
    public void CollectingRolesFromAnEmptyMindFindsNothing()
    {
        var into = new HashSet<WildSpawnType>();

        FactionHostility.CollectThirdPartyRoles(Mind(), into);

        Assert.Empty(into);
    }
}
