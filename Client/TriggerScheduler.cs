using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using RoguesVRaiders.Objective;
using UnityEngine;

namespace RoguesVRaiders
{
    public class TriggerScheduler : MonoBehaviour
    {
        const float RetrySeconds = 20f;
        const float GiveUpAfterSeconds = 300f;

        class Pending
        {
            public SquadPlan Plan;
            public bool Fired;
            public float FirstAttempt = -1f;
        }

        readonly List<Pending> _pending = new List<Pending>();
        float _raidStart;
        float _nextTick;

        void Start()
        {
            _raidStart = Time.time;
            var world = Singleton<GameWorld>.Instance;
            var mapId = world.LocationId;

            List<SquadPlan> plans;
            try
            {
                plans = RaidPlan.Fetch(mapId);
            }
            catch (Exception)
            {
                RvRPlugin.Log.LogWarning("RvR: plan fetch failed - server mod missing or unreachable");
                plans = new List<SquadPlan>();
            }

            // Level scaling (on by default) rolls every squad against the raid's average-level chance;
            // off falls back to the per-faction chance the server planned.
            var levelChance = RvRPlugin.LevelScaling.Value ? (float)LevelScale.Chance() : -1f;

            foreach (var plan in plans)
            {
                if (!RvRPlugin.MasterEnable.Value) break;
                if (plan.Faction == "rogue" && !RvRPlugin.RoguesEnable.Value) continue;
                if (plan.Faction == "raider" && !RvRPlugin.RaidersEnable.Value) continue;

                // The engine wave that used to carry BossChance is gone, so roll it here instead.
                var chance = levelChance >= 0f ? levelChance : (float)plan.Chance;
                if (UnityEngine.Random.Range(0f, 100f) >= chance)
                {
                    Debug($"{plan.TriggerId}: chance roll missed ({chance:0}%)");
                    continue;
                }

                _pending.Add(new Pending { Plan = plan });
            }

            RvRPlugin.Log.LogInfo($"RvR scheduler: {_pending.Count} squad(s) planned on {mapId}");
            SquadRegistry.BeginRaid();
            if (RvRPlugin.ObjectiveEnable.Value) PoiRegistry.Build();
        }

        void OnDestroy()
        {
            SquadRegistry.EndRaid();
            RvRObjectiveController.Reset();
            PoiRegistry.Clear();
        }

        void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 5f;
            SquadRegistry.Tick();
            HostilitySeeder.Seed();
            RvRObjectiveController.Tick();

            var elapsed = Time.time - _raidStart;
            foreach (var entry in _pending)
            {
                if (entry.Fired || elapsed < entry.Plan.FireAtSeconds) continue;

                if (!RvRPlugin.MasterEnable.Value ||
                    (entry.Plan.Faction == "rogue" && !RvRPlugin.RoguesEnable.Value) ||
                    (entry.Plan.Faction == "raider" && !RvRPlugin.RaidersEnable.Value))
                {
                    entry.Fired = true;
                    Debug($"{entry.Plan.TriggerId}: toggled off, skipped");
                    continue;
                }

                if (entry.FirstAttempt < 0) entry.FirstAttempt = Time.time;
                if (Time.time - entry.FirstAttempt > GiveUpAfterSeconds)
                {
                    entry.Fired = true;
                    RvRPlugin.Log.LogInfo($"RvR: gave up on {entry.Plan.TriggerId} (gates never cleared)");
                    continue;
                }

                if (!GatesClear(entry.Plan))
                {
                    entry.Plan.FireAtSeconds = (int)(elapsed + RetrySeconds);
                    continue;
                }

                SquadSpawner.SpawnSquad(entry.Plan).HandleExceptions();
                entry.Fired = true;
                RvRPlugin.Log.LogInfo($"RvR: dispatching {entry.Plan.TriggerId} ({entry.Plan.Chance:0}% roll)");
            }
        }

        static bool GatesClear(SquadPlan plan)
        {
            var botsController = Singleton<IBotGame>.Instance?.BotsController;
            if (botsController?.BotSpawner == null) return false;

            if (!RvRPlugin.IgnoreBotCap.Value &&
                botsController.AliveAndLoadingBotsCount > RvRPlugin.AliveBotCeiling.Value)
            {
                Debug($"{plan.TriggerId}: ceiling ({botsController.AliveAndLoadingBotsCount} alive)");
                return false;
            }

            if (!string.IsNullOrEmpty(plan.Zone))
            {
                var zone = botsController.BotSpawner.GetZoneByName(plan.Zone);
                if (zone != null)
                {
                    var world = Singleton<GameWorld>.Instance;
                    if (world == null) return false;

                    var minDist = float.MaxValue;
                    foreach (var player in world.AllAlivePlayersList)
                    {
                        if (player.IsAI) continue;
                        var dist = Vector3.Distance(player.Position, zone.CenterOfSpawnPoints);
                        if (dist < minDist) minDist = dist;
                    }
                    if (minDist < RvRPlugin.SpawnDistance.Value)
                    {
                        Debug($"{plan.TriggerId}: player {minDist:F0}m from {plan.Zone}");
                        return false;
                    }
                }
                else
                {
                    Debug($"{plan.TriggerId}: zone {plan.Zone} not found, distance gate skipped");
                }
            }
            return true;
        }

        static void Debug(string message)
        {
            if (RvRPlugin.DebugLogs.Value) RvRPlugin.Log.LogInfo($"RvR gate: {message}");
        }
    }
}
