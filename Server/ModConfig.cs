namespace RoguesVRaidersServer;

public class ModConfig
{
    public bool debugLogs { get; set; }
    public bool debugAlwaysSpawn { get; set; }

    public List<string> rogueMaps { get; set; } = ["bigmap", "woods", "shoreline", "lighthouse"];
    public List<string> raiderMaps { get; set; } = ["rezervbase", "factory4_day", "factory4_night", "interchange", "laboratory"];
    public List<string> overlapMaps { get; set; } = ["tarkovstreets", "sandbox", "sandbox_high"];

    public double rogueChance { get; set; } = 25;
    public double raiderChance { get; set; } = 25;
    public double overlapChance { get; set; } = 35;
    public Dictionary<string, double> chanceOverrides { get; set; } = new()
    {
        ["rezervbase"] = 15,
        ["laboratory"] = 15,
    };

    public string escortAmount { get; set; } = "2,2,3,3,4";
    public double startSpawnShare { get; set; } = 0.4;
    public double midRaidEarliest { get; set; } = 0.10;
    public double midRaidLatest { get; set; } = 0.60;

    public string difficulty { get; set; } = "impossible";
    public bool forceHardestDifficulty { get; set; } = true;

    public bool upgradeDurability { get; set; } = true;
    public int weaponLowestMax { get; set; } = 98;
    public int weaponMaxDelta { get; set; } = 6;
    public int armorLowestMaxPercent { get; set; } = 95;
    public int armorMaxDelta { get; set; } = 5;

    public bool upgradeAmmo { get; set; } = true;
    public List<int> ammoRankWeights { get; set; } = [60, 25, 10];
    public int ammoTailWeight { get; set; } = 0;

    public bool upgradeGearTier { get; set; } = true;
    public int minArmorClass { get; set; } = 4;
}
