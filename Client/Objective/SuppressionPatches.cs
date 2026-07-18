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

        // Which of the names above actually turned up this raid, and how many layer checks we saw at all.
        // A rename in the game's layer names is otherwise completely silent - the set simply stops
        // matching and RvR bots quietly resume the vanilla behaviour this exists to suppress.
        static readonly HashSet<string> _matched = new HashSet<string>();
        static int _observed;

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

        // Raid end. Plenty of layer checks but not one of our names matched means the names have moved.
        public static void ReportUnmatched()
        {
            if (_observed > 0 && _matched.Count == 0)
                RvRPlugin.Log.LogWarning(
                    $"RvR suppression matched none of [{string.Join(", ", Suppressed)}] in {_observed} checks - " +
                    "the game's layer names have probably changed, and RvR bots ran on vanilla layers");

            _observed = 0;
            _matched.Clear();
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

            _observed++;
            var name = __instance.Name();
            if (!Suppressed.Contains(name)) return true;
            _matched.Add(name);

            if (RvRPlugin.DebugLogs.Value && _loggedOnce.Add(name))
                RvRPlugin.Log.LogInfo($"RvR suppression: forcing {name} off for RvR bots");

            __result = false;
            return false;
        }
    }
}
