using System;
using System.Collections.Generic;
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

                var all = zone.SpawnPoints.Where(p => p != null).ToList();
                if (all.Count == 0)
                {
                    RvRPlugin.Log.LogWarning($"RvR: zone {zone.NameZone} has no spawn points for {plan.TriggerId} - not spawned");
                    return;
                }

                // The scheduler's gate paces the attempt off the zone's centre; the promise is kept
                // here, where the actual points are chosen. Distance is measured now — "at least this
                // far" means when the squad spawns, not where people stood when the raid started.
                var humans = new List<UnityEngine.Vector3>();
                var world = Singleton<GameWorld>.Instance;
                if (world?.AllAlivePlayersList != null)
                {
                    foreach (var player in world.AllAlivePlayersList)
                        if (player != null && !player.IsAI) humans.Add(player.Position);
                }

                var nearest = new List<float>(all.Count);
                foreach (var point in all)
                {
                    var d = float.MaxValue;
                    foreach (var pos in humans)
                    {
                        var dist = UnityEngine.Vector3.Distance(point.Position, pos);
                        if (dist < d) d = dist;
                    }
                    nearest.Add(d);
                }

                var floor = humans.Count > 0 ? (float)RvRPlugin.SpawnDistance.Value : 0f;
                var picked = Core.SpawnPick.Pick(nearest, floor, data.Count, max => UnityEngine.Random.Range(0, max));
                var points = picked.Select(i => all[i]).ToList();

                spawner.TryToSpawnInZoneAndDelay(zone, data, withCheckMinMax: false, newWave: true,
                    pointsToSpawn: points, forcedSpawn: true);

                if (floor > 0f)
                {
                    var closest = float.MaxValue;
                    foreach (var i in picked) if (nearest[i] < closest) closest = nearest[i];
                    var cleared = nearest.Count(d => d >= floor);
                    RvRPlugin.Log.LogInfo($"RvR: spawning {plan.Faction} squad of {data.Count} at {zone.NameZone} "
                        + $"({closest:F0}m from the nearest player; {cleared}/{all.Count} points cleared the {floor:F0}m floor)");
                }
                else
                {
                    RvRPlugin.Log.LogInfo($"RvR: spawning {plan.Faction} squad of {data.Count} at {zone.NameZone}");
                }
            }
            catch (Exception ex)
            {
                RvRPlugin.Log.LogWarning($"RvR: squad spawn failed for {plan.TriggerId}: {ex}");
            }
        }
    }
}
