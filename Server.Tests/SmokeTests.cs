using RoguesVRaidersServer;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class SmokeTests
{
    [Fact]
    public void DefaultConfigHasAllMaps()
    {
        var cfg = new ModConfig();
        Assert.Contains("bigmap", cfg.rogueMaps);
        Assert.Contains("laboratory", cfg.raiderMaps);
        Assert.Contains("tarkovstreets", cfg.overlapMaps);
        Assert.DoesNotContain("labyrinth", cfg.rogueMaps.Concat(cfg.raiderMaps).Concat(cfg.overlapMaps));
    }
}
