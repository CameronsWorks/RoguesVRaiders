using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;

namespace RoguesVRaiders
{
    internal static class SquadSpawner
    {
        public static async Task SpawnSquad(SquadPlan plan)
        {
            if (!FikaBridge.IsHost()) return;

            try
            {
                var spawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
                var zone = spawner.GetZoneByName(plan.Zone) ?? spawner.GetRandomBotZone(false);
                if (zone == null)
                {
                    RvRPlugin.Log.LogWarning($"RvR: no zone for {plan.TriggerId} (wanted {plan.Zone}) - not spawned");
                    return;
                }

                var role = plan.Faction == "rogue" ? WildSpawnType.exUsec : WildSpawnType.pmcBot;
                if (!Enum.TryParse(plan.Difficulty, true, out BotDifficulty dif)) dif = BotDifficulty.normal;

                // TriggerId already carries SquadRegistry.Marker ("sipto.rvr.") from the server -
                // set it directly, do not prefix it again.
                var spawnParams = new BotSpawnParams { Id_spawn = plan.TriggerId };
                IGetProfileData profileData = new BotProfileDataClass(EPlayerSide.Savage, role, dif, 0f, spawnParams);

                var data = await BotCreationDataClass.Create(profileData, spawner.BotCreator, plan.Size, spawner);
                if (data == null || data.Count == 0)
                {
                    RvRPlugin.Log.LogWarning($"RvR: profile generation returned nothing for {plan.TriggerId} (size {plan.Size}) - not spawned");
                    return;
                }

                spawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, data.Count);

                var all = zone.SpawnPoints;
                var points = all.OrderBy(_ => UnityEngine.Random.value).Take(data.Count).ToList();
                for (var i = 0; points.Count < data.Count && all.Length > 0; i++) points.Add(all[i % all.Length]);

                spawner.TryToSpawnInZoneAndDelay(zone, data, withCheckMinMax: false, newWave: true,
                    pointsToSpawn: points, forcedSpawn: true);

                RvRPlugin.Log.LogInfo($"RvR: spawning {plan.Faction} squad of {data.Count} at {zone.NameZone}");
            }
            catch (Exception ex)
            {
                RvRPlugin.Log.LogWarning($"RvR: squad spawn failed for {plan.TriggerId}: {ex}");
            }
        }
    }
}
