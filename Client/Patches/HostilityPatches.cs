using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace RoguesVRaiders.Patches
{
    internal class IsPlayerEnemyPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsGroup).GetMethod(nameof(BotsGroup.IsPlayerEnemy), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        static bool PatchPrefix(BotsGroup __instance, IPlayer player, ref bool __result)
        {
            var mode = RvRPlugin.FriendlinessMode.Value;
            if (mode == Friendliness.FactionAuthentic) return true;
            if (player == null || player.IsAI) return true;
            if (!SquadRegistry.IsWarbandGroup(__instance)) return true;

            __result = mode == Friendliness.HostileToAll;
            return false;
        }
    }

    internal class AddEnemyPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsGroup).GetMethod(nameof(BotsGroup.AddEnemy), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        static bool PatchPrefix(BotsGroup __instance, IPlayer person, EBotEnemyCause cause, ref bool __result)
        {
            if (RvRPlugin.FriendlinessMode.Value != Friendliness.FriendlyToPlayers) return true;
            if (person == null || person.IsAI) return true;
            if (cause == EBotEnemyCause.byKill) return true;
            if (!SquadRegistry.IsWarbandGroup(__instance)) return true;

            __result = false;
            return false;
        }
    }

    internal class CheckAndAddEnemyPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsGroup).GetMethod(nameof(BotsGroup.CheckAndAddEnemy), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        static bool PatchPrefix(BotsGroup __instance, IPlayer player, ref bool __result)
        {
            if (RvRPlugin.FriendlinessMode.Value != Friendliness.FriendlyToPlayers) return true;
            if (player == null || player.IsAI) return true;
            if (!SquadRegistry.IsWarbandGroup(__instance)) return true;

            __result = false;
            return false;
        }
    }
}
