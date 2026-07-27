using System.Collections.Generic;
using Comfort.Common;
using EFT;
using RoguesVRaiders.Core;

namespace RoguesVRaiders
{
    internal static class HostilitySeeder
    {
        static readonly HashSet<string> _reached = new HashSet<string>();

        public static void Reset()
        {
            _reached.Clear();
        }

        // Host-only (called from TriggerScheduler.Update, which is host-gated). Reconcile pass: for each
        // active warband squad, make it hostile to every valid foreign AI - reciprocally, per member.
        public static void Seed()
        {
            if (!RvRPlugin.MasterEnable.Value || !RvRPlugin.HostilityEnable.Value) return;

            var world = Singleton<GameWorld>.Instance;
            if (world == null) return;
            var players = world.AllAlivePlayersList;

            foreach (var squad in SquadRegistry.ActiveSquads)
            {
                var group = squad.Group;
                if (group == null) continue;
                var leader = SquadRegistry.LeaderOf(squad);
                if (leader == null) continue;

                var self = leader.Profile.Info.Settings.Role == WildSpawnType.exUsec ? Warband.Rogue : Warband.Raider;

                foreach (var foreign in players)
                {
                    var ai = foreign.AIData;
                    if (ai == null || !ai.IsAI) continue;          // skip the human player
                    var fbo = ai.BotOwner;
                    if (fbo == null) continue;
                    var fg = fbo.BotsGroup;
                    if (fg == null || fg == group) continue;       // skip our own group

                    var role = fbo.Profile.Info.Settings.Role;
                    if (!Hostility.IsHostileRole((int)role, self)) continue;

                    group.CheckAndAddEnemy(foreign, true);          // our whole squad -> this foreign bot

                    var mutual = true;
                    foreach (var m in group.Members)                // reciprocal: foreign group -> each of us
                    {
                        if (m == null || m.IsDead) continue;
                        fg.CheckAndAddEnemy(m.GetPlayer, true);
                        mutual &= fg.Enemies.ContainsKey(m.GetPlayer);
                    }

                    // Read both directions back out of the groups instead of trusting the return value -
                    // CheckAndAddEnemy also answers false for a bot that was already an enemy, so it can't
                    // tell a refused seed from a redundant one. Once per role per raid.
                    if (RvRPlugin.DebugLogs.Value && _reached.Add(self + ":" + (int)role))
                    {
                        RvRPlugin.Log.LogInfo(
                            $"RvR hostility: {self} squad vs {role} - ours {group.Enemies.ContainsKey(foreign)}, theirs {mutual}");
                    }
                }
            }
        }
    }
}
