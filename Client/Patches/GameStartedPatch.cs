using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace RoguesVRaiders.Patches
{
    internal class GameStartedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        static void PatchPostfix(GameWorld __instance)
        {
            if (__instance is HideoutGameWorld) return;
            if (!FikaBridge.IsHost()) return;
            if (!RvRPlugin.MasterEnable.Value) return;
            if (__instance.GetComponent<TriggerScheduler>() == null)
            {
                __instance.gameObject.AddComponent<TriggerScheduler>();
            }
        }
    }
}
