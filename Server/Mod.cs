using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using RoguesVRaidersServer.Services;

namespace RoguesVRaidersServer;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.sipto.roguesvraiders";
    public override string Name { get; init; } = "RoguesVRaiders";
    public override string Author { get; init; } = "Sipto";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new(1, 4, 0);
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

// Boot injection has to land after the spawn mods that rebuild or add waves at load:
// ABPS replaces every map's BossLocationSpawn at 469420, BlackDiv injects at 480085.
[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader + 90000)]
public class RvRLoader(
    QualityUpgradeService qualityUpgrades,
    SpawnInjector spawnInjector,
    ISptLogger<RvRLoader> logger
) : IOnLoad
{
    public Task OnLoad()
    {
        // Nothing upstream catches a throw out of IOnLoad - it reaches Program.StartServer, which logs a
        // critical exception and stops the server. A bad value in our own config would cost the player the
        // whole game rather than just this mod, so it fails inert instead.
        try
        {
            qualityUpgrades.Apply();
            spawnInjector.Refresh();
            logger.Info("[RvR] server mod ready");
        }
        catch (Exception ex)
        {
            logger.Error("[RvR] startup failed, no squads will spawn this session. Gear upgrades may be " +
                         $"partly applied - restart the server once this is fixed. {ex}");
        }
        return Task.CompletedTask;
    }
}
