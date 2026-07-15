using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace RoguesVRaiders.Objective
{
    internal static class SuppressionPatches
    {
        public static bool Active;

        static readonly HashSet<string> Suppressed = new HashSet<string>
        {
            "StationaryWS", "ExURequest", "GroupForce",
        };

        static readonly HashSet<string> _loggedOnce = new HashSet<string>();

        public static void Install(Harmony harmony)
        {
            var baseType = typeof(BaseLogicLayerAbstractClass);
            var prefix = new HarmonyMethod(typeof(SuppressionPatches).GetMethod(
                nameof(ShallUseNowPrefix), BindingFlags.NonPublic | BindingFlags.Static));

            var patched = 0;
            foreach (var type in SafeGetTypes(baseType.Assembly))
            {
                if (type == null || type.IsAbstract || !baseType.IsAssignableFrom(type)) continue;
                var method = type.GetMethod("ShallUseNow",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (method == null) continue;
                try { harmony.Patch(method, prefix: prefix); patched++; }
                catch (Exception ex) { RvRPlugin.Log.LogWarning($"RvR suppression: could not patch {type.Name}.ShallUseNow: {ex.Message}"); }
            }
            RvRPlugin.Log.LogInfo($"RvR suppression: patched {patched} layer ShallUseNow override(s)");
        }

        // Assembly-CSharp is large and some types fail to load; GetTypes() throws ReflectionTypeLoadException.
        static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types; }
        }

        static bool ShallUseNowPrefix(BaseLogicLayerAbstractClass __instance, ref bool __result)
        {
            if (!Active) return true;
            var bot = __instance.BotOwner_0;
            if (!SquadRegistry.IsRvRSquadMember(bot)) return true;

            var name = __instance.Name();
            if (!Suppressed.Contains(name)) return true;

            if (RvRPlugin.DebugLogs.Value && _loggedOnce.Add(name))
                RvRPlugin.Log.LogInfo($"RvR suppression: forcing {name} off for RvR bots");

            __result = false;
            return false;
        }
    }
}
