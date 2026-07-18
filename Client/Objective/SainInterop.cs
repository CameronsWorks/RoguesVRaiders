using System;
using System.Reflection;
using EFT;
using HarmonyLib;

namespace RoguesVRaiders.Objective
{
    internal static class SainInterop
    {
        const int PriorityGap = 8;
        const int PriorityFloor = 5;
        const int FallbackPriority = 12;

        static bool _init, _sainPresent, _bindOk, _warned;
        static FieldInfo _instanceField, _generalField, _layersField, _soloField, _squadField, _extractField;
        static PropertyInfo _spawnControllerInstance;
        static MethodInfo _getSain;
        static PropertyInfo _activeLayer;

        static void EnsureInit()
        {
            if (_init) return;
            _init = true;
            try
            {
                var gsc = AccessTools.TypeByName("SAIN.Preset.GlobalSettings.GlobalSettingsClass");
                var layers = AccessTools.TypeByName("SAIN.Preset.GlobalSettings.LayerSettings");
                if (gsc != null && layers != null)
                {
                    _instanceField = gsc.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    _generalField = gsc.GetField("General", BindingFlags.Public | BindingFlags.Instance);
                    var general = _generalField?.FieldType;
                    _layersField = general?.GetField("Layers", BindingFlags.Public | BindingFlags.Instance);
                    _soloField = layers.GetField("SAINCombatSoloLayerPriority", BindingFlags.Public | BindingFlags.Instance);
                    _squadField = layers.GetField("SAINCombatSquadLayerPriority", BindingFlags.Public | BindingFlags.Instance);
                    _extractField = layers.GetField("SAINExtractLayerPriority", BindingFlags.Public | BindingFlags.Instance);
                }

                var spawn = AccessTools.TypeByName("SAIN.Components.BotController.BotSpawnController");
                if (spawn != null)
                {
                    _spawnControllerInstance = spawn.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _getSain = AccessTools.Method(spawn, "GetSAIN", new[] { typeof(BotOwner) });
                }
                var component = AccessTools.TypeByName("SAIN.Components.BotComponent");
                _activeLayer = component?.GetProperty("ActiveLayer", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                RvRPlugin.Log.LogWarning($"RvR SAIN interop init failed, using fallbacks: {ex.Message}");
            }

            _sainPresent = SainLoaded();
            _bindOk = _spawnControllerInstance != null && _getSain != null && _activeLayer != null;
            if (_sainPresent && !_bindOk) StandDown("SAIN is loaded but its ownership members did not bind");
        }

        // Assembly name rather than type names: a SAIN refactor renames types far more readily than it
        // renames the assembly, and "is it installed" has to stay answerable through exactly that change.
        static bool SainLoaded()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == "SAIN") return true;
            return false;
        }

        static void StandDown(string reason)
        {
            if (_warned) return;
            _warned = true;
            RvRPlugin.Log.LogWarning($"RvR: {reason} - yielding movement to SAIN for the rest of the session");
        }

        public static int LayerPriority()
        {
            if (RvRPlugin.ObjectivePriorityOverride.Value > 0) return RvRPlugin.ObjectivePriorityOverride.Value;
            EnsureInit();
            try
            {
                var instance = _instanceField?.GetValue(null);
                var general = _generalField?.GetValue(instance);
                var layers = _layersField?.GetValue(general);
                if (layers == null) return FallbackPriority;
                var solo = (int)_soloField.GetValue(layers);
                var squad = (int)_squadField.GetValue(layers);
                var extract = (int)_extractField.GetValue(layers);
                return Core.ObjectivePriority.Compute(solo, squad, extract, PriorityGap, PriorityFloor);
            }
            catch { return FallbackPriority; }
        }

        // True when SAIN is driving this bot (any ESAINLayer but None) and we must NOT drive it.
        // SAIN absent, or the bot not registered with it => false (we drive).
        //
        // Installed but unreadable answers TRUE. Answering false would put both systems on the mover at
        // once, which reads in-raid as a bot stuttering between two destinations; yielding just leaves it
        // on stock SAIN behaviour, which is much the cheaper failure.
        public static bool SainOwns(BotOwner bot)
        {
            EnsureInit();
            if (!_sainPresent) return false;
            if (!_bindOk) return true;
            try
            {
                var controller = _spawnControllerInstance.GetValue(null);
                if (controller == null) return false;
                var comp = _getSain.Invoke(controller, new object[] { bot });
                if (comp == null) return false;
                var layer = (int)_activeLayer.GetValue(comp);
                return layer != 0; // ESAINLayer.None == 0; anything else = SAIN is driving
            }
            catch (Exception ex)
            {
                StandDown($"SAIN ownership check failed ({ex.Message})");
                return true;
            }
        }

        // True when SAIN has this bot in a fighting layer (Combat or Squad) - our evidence that the squad
        // actually engaged, not that a third party did the killing. Unreadable answers FALSE, opposite to
        // SainOwns: this one gates a reward, so failing it closed under-grants rather than handing out
        // POIs for fights we never saw.
        public static bool SainInCombat(BotOwner bot)
        {
            EnsureInit();
            if (!_sainPresent || !_bindOk) return false;
            try
            {
                var controller = _spawnControllerInstance.GetValue(null);
                if (controller == null) return false;
                var comp = _getSain.Invoke(controller, new object[] { bot });
                if (comp == null) return false;
                var name = _activeLayer.GetValue(comp)?.ToString();
                return name == "Combat" || name == "Squad";
            }
            catch (Exception ex)
            {
                StandDown($"SAIN combat check failed ({ex.Message})");
                return false;
            }
        }
    }
}
