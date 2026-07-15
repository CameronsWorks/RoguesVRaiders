using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RoguesVRaiders
{
    internal class WarbandSquad
    {
        public BotsGroup Group;
        public readonly List<BotOwner> Members = new List<BotOwner>();
        public float FormedAt;
        public bool Locked;
        public bool Released;
    }

    internal static class SquadRegistry
    {
        public const string Marker = "sipto.rvr.";
        // Escorts on a triggered (botEvent) wave spawn deferred, arriving seconds to tens of
        // seconds after the boss, so lock as soon as the group reaches its target size and
        // only fall back to this grace window for squads whose escorts never made it in.
        const float LockGraceSeconds = 120f;

        static readonly List<BotOwner> _unassigned = new List<BotOwner>();
        static readonly List<WarbandSquad> _squads = new List<WarbandSquad>();
        static BotSpawner _spawner;

        public static void BeginRaid()
        {
            _unassigned.Clear();
            _squads.Clear();
            _spawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            _spawner.OnBotCreated += OnBotCreated;
            _spawner.OnBotRemoved += OnBotRemoved;
        }

        public static void EndRaid()
        {
            _unassigned.Clear();
            _squads.Clear();
            if (_spawner != null)
            {
                _spawner.OnBotCreated -= OnBotCreated;
                _spawner.OnBotRemoved -= OnBotRemoved;
                _spawner = null;
            }
        }

        static void OnBotCreated(BotOwner bot)
        {
            var spawnId = bot?.SpawnProfileData?.SpawnParams?.Id_spawn;
            if (spawnId == null || !spawnId.StartsWith(Marker)) return;
            _unassigned.Add(bot);
            if (RvRPlugin.DebugLogs.Value)
                RvRPlugin.Log.LogInfo($"RvR registry: created {bot.Profile.Info.Settings.Role} ({spawnId})");
        }

        static void OnBotRemoved(BotOwner bot)
        {
            _unassigned.Remove(bot);
            foreach (var squad in _squads) squad.Members.Remove(bot);
        }

        // Called from TriggerScheduler.Update every ~5s.
        public static void Tick()
        {
            for (var i = _unassigned.Count - 1; i >= 0; i--)
            {
                var bot = _unassigned[i];
                if (bot?.BotsGroup == null) continue;
                _unassigned.RemoveAt(i);

                var squad = _squads.FirstOrDefault(s => s.Group == bot.BotsGroup);
                if (squad == null)
                {
                    squad = new WarbandSquad { Group = bot.BotsGroup, FormedAt = Time.time };
                    _squads.Add(squad);

                    if (FindForeignMember(squad) == null)
                    {
                        ApplyFriendliness(squad);
                    }
                    else
                    {
                        squad.Released = true;
                        RvRPlugin.Log.LogWarning("RvR registry: released squad at creation - vanilla group adopted our bot");
                    }
                }
                squad.Members.Add(bot);
            }

            foreach (var squad in _squads)
            {
                if (squad.Locked || squad.Released) continue;

                var foreign = FindForeignMember(squad);
                if (foreign != null)
                {
                    squad.Released = true;
                    squad.Group.SetAggressiveToAllNewPlayers(false);
                    RvRPlugin.Log.LogWarning($"RvR registry: released squad - foreign member {foreign.Profile.Info.Settings.Role} in group");
                    continue;
                }

                var target = squad.Group.TargetMembersCount;
                var full = target > 0 && squad.Group.Members.Count >= target;
                if (full || Time.time - squad.FormedAt > LockGraceSeconds)
                {
                    squad.Group.Lock();
                    squad.Locked = true;
                    var count = squad.Group.Members.Count;
                    if (full)
                        RvRPlugin.Log.LogInfo($"RvR registry: locked squad of {count}");
                    else
                        RvRPlugin.Log.LogInfo($"RvR registry: locked squad of {count} (target {target}, escorts never arrived)");
                }
            }
        }

        static BotOwner FindForeignMember(WarbandSquad squad)
        {
            for (var i = 0; i < squad.Group.Members.Count; i++)
            {
                var member = squad.Group.Members[i];
                var id = member?.SpawnProfileData?.SpawnParams?.Id_spawn;
                if (id == null || !id.StartsWith(Marker)) return member;
            }
            return null;
        }

        public static bool IsWarbandGroup(BotsGroup group)
        {
            foreach (var squad in _squads)
            {
                if (!squad.Released && squad.Group == group) return true;
            }
            return false;
        }

        public static IEnumerable<WarbandSquad> ActiveSquads
        {
            get
            {
                foreach (var squad in _squads)
                    if (!squad.Released && squad.Locked) yield return squad;
            }
        }

        public static bool IsRvRSquadMember(BotOwner bot)
        {
            if (bot == null) return false;
            var id = bot.SpawnProfileData?.SpawnParams?.Id_spawn;
            if (id == null || !id.StartsWith(Marker)) return false;
            return IsWarbandGroup(bot.BotsGroup);
        }

        public static BotOwner LeaderOf(WarbandSquad squad)
        {
            foreach (var m in squad.Group.Members)
                if (m != null && !m.IsDead) return m;
            return null;
        }

        public static void ReapplyFriendliness()
        {
            if (Singleton<GameWorld>.Instance == null) return;
            foreach (var squad in _squads)
            {
                if (!squad.Released) ApplyFriendliness(squad, true);
            }
        }

        static void ApplyFriendliness(WarbandSquad squad, bool reapply = false)
        {
            var mode = RvRPlugin.FriendlinessMode.Value;
            var world = Singleton<GameWorld>.Instance;

            if (mode == Friendliness.FriendlyToPlayers)
            {
                foreach (var enemy in squad.Group.Enemies.Keys.Where(p => !p.IsAI).ToList())
                {
                    squad.Group.RemoveEnemy(enemy, EBotEnemyCause.initial);
                }
                squad.Group.SetAggressiveToAllNewPlayers(false);
            }
            else if (mode == Friendliness.HostileToAll)
            {
                squad.Group.SetAggressiveToAllNewPlayers(true);
                foreach (var player in world.AllAlivePlayersList)
                {
                    if (!player.IsAI) squad.Group.CheckAndAddEnemy(player, true);
                }
            }
            else
            {
                squad.Group.SetAggressiveToAllNewPlayers(false);
                if (reapply)
                {
                    foreach (var enemy in squad.Group.Enemies.Keys.Where(p => !p.IsAI).ToList())
                    {
                        squad.Group.RemoveEnemy(enemy, EBotEnemyCause.initial);
                    }
                }
            }
        }
    }
}
