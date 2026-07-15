using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace RoguesVRaidersServer.Services;

[Injectable(InjectionType.Singleton)]
public class ConfigService
{
    public ModConfig Config { get; }

    public ConfigService(ModHelper modHelper, ISptLogger<ConfigService> logger)
    {
        ModConfig? config = null;
        try
        {
            var modDir = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            config = modHelper.GetJsonDataFromFile<ModConfig>(modDir, "config.jsonc");
        }
        catch (Exception ex)
        {
            logger.Error($"[RvR] failed to load config.jsonc, falling back to defaults: {ex}");
        }

        if (config == null)
        {
            logger.Error("[RvR] config.jsonc missing or empty, falling back to defaults");
        }

        Config = config ?? new ModConfig();
    }
}
