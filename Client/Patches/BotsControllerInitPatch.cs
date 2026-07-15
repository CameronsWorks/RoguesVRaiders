using System.Reflection;
using EFT;
using RoguesVRaiders.Objective;
using SPT.Reflection.Patching;

namespace RoguesVRaiders.Patches
{
    internal class BotsControllerInitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsController).GetMethod(nameof(BotsController.Init), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        static void PatchPostfix()
        {
            if (!FikaBridge.IsHost()) return;
            if (!RvRPlugin.MasterEnable.Value) return;
            LayerRegistration.EnsureRegistered();
        }
    }
}
