using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace RoguesVRaiders
{
    public enum Friendliness { FactionAuthentic, FriendlyToPlayers, HostileToAll }

    [BepInPlugin(PluginId, "Rogues V Raiders", "1.4.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("xyz.drakia.bigbrain")]
    [BepInDependency("xyz.drakia.waypoints", BepInDependency.DependencyFlags.SoftDependency)]
    public class RvRPlugin : BaseUnityPlugin
    {
        public const string PluginId = "com.sipto.roguesvraiders";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> MasterEnable;
        internal static ConfigEntry<bool> RoguesEnable;
        internal static ConfigEntry<bool> RaidersEnable;
        internal static ConfigEntry<bool> LevelScaling;
        internal static ConfigEntry<Friendliness> FriendlinessMode;
        internal static ConfigEntry<int> SpawnDistance;
        internal static ConfigEntry<int> AliveBotCeiling;
        internal static ConfigEntry<bool> IgnoreBotCap;
        internal static ConfigEntry<bool> DebugLogs;
        internal static ConfigEntry<bool> ObjectiveEnable;
        internal static ConfigEntry<int> ObjectivePriorityOverride;
        internal static ConfigEntry<bool> LockdownEnable;
        internal static ConfigEntry<int> LockdownDelay;
        internal static ConfigEntry<bool> HostilityEnable;
        internal static ConfigEntry<bool> HuntEnable;

        void Awake()
        {
            Log = Logger;

            MasterEnable = Config.Bind("1. General", "Enable", true,
                "Master switch. Off = no warband squads spawn.");
            RoguesEnable = Config.Bind("1. General", "Rogue Squads", true,
                "Allow roaming Rogue squads.");
            RaidersEnable = Config.Bind("1. General", "Raider Squads", true,
                "Allow roaming Raider squads.");
            FriendlinessMode = Config.Bind("2. Behavior", "Friendliness", Friendliness.FactionAuthentic,
                "How warband squads treat players. Faction authentic: rogues warn USEC first, raiders shoot.");
            SpawnDistance = Config.Bind("3. Spawning", "Min Spawn Distance", 75,
                new ConfigDescription("When the squad has a named spawn zone, wait until that zone is at least this far from every player (meters). Squads without a zone are gated only by the bot ceiling.",
                    new AcceptableValueRange<int>(0, 200)));
            AliveBotCeiling = Config.Bind("3. Spawning", "Alive Bot Ceiling", 22,
                new ConfigDescription("Squads wait while more bots than this are alive. Ignored when Force Spawn Over Bot Cap is on.",
                    new AcceptableValueRange<int>(10, 40)));
            IgnoreBotCap = Config.Bind("3. Spawning", "Force Spawn Over Bot Cap", false,
                "Spawn warband squads even when the map is already at the bot ceiling. The squads never take vanilla spawn slots, so this pushes the live bot count higher for a heavier fight at some performance cost.");
            LevelScaling = Config.Bind("3. Spawning", "Scale Chance By Level", true,
                "Spawn chance scales with the raid's average level: 10% at level 15, +5% every 5 levels, capped at 25%. Off = use the per-faction chances from the server config.");
            DebugLogs = Config.Bind("4. Debug", "Verbose Logs", false,
                "Log scheduling and squad registration detail.");
            ObjectiveEnable = Config.Bind("2. Behavior", "Roaming Objectives", true,
                "Warband squads roam and patrol as a group. Off = Phase-1 behavior (SAIN + vanilla patrol only). A mid-raid enable engages from the next raid.");
            ObjectivePriorityOverride = Config.Bind("2. Behavior", "Objective Layer Priority", 0,
                new ConfigDescription("BigBrain priority for the roam layer. 0 = auto (just below SAIN combat). Advanced.",
                    new AcceptableValueRange<int>(0, 19)));
            LockdownEnable = Config.Bind("2. Behavior", "Lockdown POIs", true,
                "Roaming squads claim and hold a point of interest after patrolling it. Off = patrol only (2a behavior).");
            LockdownDelay = Config.Bind("2. Behavior", "Lockdown Delay (s)", 90,
                new ConfigDescription("Seconds a squad patrols its ring before claiming it and posting up.",
                    new AcceptableValueRange<int>(0, 600)));
            HostilityEnable = Config.Bind("2. Behavior", "Broad AI Hostility", true,
                "Warband squads fight the AI ecosystem - the opposite faction, scavs, cultists, bosses, custom factions, and AI PMCs - but never their own faction, and rogues stay friendly with the Goons (raiders fight them). Off = squads stay neutral to other AI. The human-player relationship is set separately by Friendliness.");
            HuntEnable = Config.Bind("2. Behavior", "Hunt and Takeover", true,
                "After a squad locks down a POI, an available opposite-faction squad is sent to hunt it; if the hunters wipe the holders themselves they inherit the POI and hold it. Off = squads lock down and hold, no hunts.");

            FikaBridge.Init();
            new Patches.GameStartedPatch().Enable();
            new Patches.IsPlayerEnemyPatch().Enable();
            new Patches.AddEnemyPatch().Enable();
            new Patches.CheckAndAddEnemyPatch().Enable();
            new Patches.BotsControllerInitPatch().Enable();

            FriendlinessMode.SettingChanged += (_, _) => SquadRegistry.ReapplyFriendliness();

            Log.LogInfo("Rogues V Raiders loaded");
        }
    }
}
