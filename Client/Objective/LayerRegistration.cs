using System.Collections.Generic;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;

namespace RoguesVRaiders.Objective
{
    internal static class LayerRegistration
    {
        static bool _done;

        public static void EnsureRegistered()
        {
            if (_done) return;
            _done = true;

            var priority = SainInterop.LayerPriority();

            BrainManager.AddCustomLayer(typeof(RvRObjectiveLayer),
                new List<string> { "ExUsec" }, priority, new List<WildSpawnType> { WildSpawnType.exUsec });
            BrainManager.AddCustomLayer(typeof(RvRObjectiveLayer),
                new List<string> { "PMC" }, priority, new List<WildSpawnType> { WildSpawnType.pmcBot });

            SuppressionPatches.Install(new Harmony("com.sipto.roguesvraiders.suppression"));

            RvRPlugin.Log.LogInfo($"RvR objective layer registered at priority {priority}");
        }
    }
}
