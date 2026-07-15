using RoguesVRaiders.Core;
using Xunit;

namespace RoguesVRaiders.Tests
{
    public class HostilityTests
    {
        // WildSpawnType role ints: exUsec=24 (rogue), pmcBot=9 (raider),
        // Goons bossKnight=26/followerBigPipe=27/followerBirdEye=28, scav assault=1/marksman=0,
        // cultist sectantWarrior=20, AI PMC pmcUSEC=52/pmcBEAR=51,
        // customs RUAF 848400-848406 / Black Division 848420-848424 / UNTAR 1170-1173.

        [Theory]
        [InlineData(24)]   // exUsec — own faction ally (incl. vanilla Lighthouse rogues + other rogue RvR squads)
        [InlineData(26)]   // bossKnight — Goon
        [InlineData(27)]   // followerBigPipe — Goon
        [InlineData(28)]   // followerBirdEye — Goon
        public void RoguesSpareOwnFactionAndGoons(int role)
        {
            Assert.False(Hostility.IsHostileRole(role, Warband.Rogue));
        }

        [Theory]
        [InlineData(9)]      // pmcBot — the opposite faction (rivalry)
        [InlineData(1)]      // assault scav
        [InlineData(0)]      // marksman scav
        [InlineData(20)]     // cultist
        [InlineData(52)]     // AI PMC USEC
        [InlineData(51)]     // AI PMC BEAR
        [InlineData(848400)] // RUAF
        [InlineData(848406)] // RUAF Remnant
        [InlineData(848420)] // Black Division
        [InlineData(1170)]   // UNTAR
        public void RoguesFightEveryoneElse(int role)
        {
            Assert.True(Hostility.IsHostileRole(role, Warband.Rogue));
        }

        [Fact]
        public void RaidersSpareOwnFaction()
        {
            Assert.False(Hostility.IsHostileRole(9, Warband.Raider)); // pmcBot ally (incl. vanilla Reserve raiders)
        }

        [Theory]
        [InlineData(24)]     // exUsec — the opposite faction (rivalry)
        [InlineData(26)]     // bossKnight — RAIDERS FIGHT GOONS (the asymmetry)
        [InlineData(27)]     // followerBigPipe
        [InlineData(28)]     // followerBirdEye
        [InlineData(1)]      // scav
        [InlineData(20)]     // cultist
        [InlineData(52)]     // AI PMC
        [InlineData(848420)] // Black Division
        [InlineData(1173)]   // UNTAR
        public void RaidersFightEveryoneElseIncludingGoons(int role)
        {
            Assert.True(Hostility.IsHostileRole(role, Warband.Raider));
        }
    }
}
