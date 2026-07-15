using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using RoguesVRaidersServer.Services;

namespace RoguesVRaidersServer.Routers;

// Routers on the same path all run, in ascending TypePriority. 150000 puts this one
// after ABPS's router (100000) and before the core match router (int.MaxValue),
// mandatory for the raid-start hook below, see its comment.
[Injectable(TypePriority = 150000)]
public class RaidEndRouter : StaticRouter
{
    static SpawnInjector _injector = null!;
    static ISptLogger<RaidEndRouter> _logger = null!;

    public RaidEndRouter(SpawnInjector injector, ISptLogger<RaidEndRouter> logger, JsonUtil jsonUtil)
        : base(jsonUtil, GetRoutes())
    {
        _injector = injector;
        _logger = logger;
    }

    static List<RouteAction> GetRoutes() =>
    [
        new RouteAction(
            "/client/match/local/end",
            async (url, info, sessionId, output) =>
            {
                // No per-router try/catch upstream, so an unguarded throw here would
                // fail the whole raid-end response, and the re-roll must never break it.
                try
                {
                    _injector.Refresh();
                }
                catch (Exception ex)
                {
                    _logger.Error("[RvR] raid-end refresh failed: " + ex);
                }
                return await new ValueTask<object>(output ?? string.Empty);
            }
        ),
        new RouteAction(
            "/client/match/local/start",
            async (url, info, sessionId, output) =>
            {
                // ABPS (if installed) rebuilds BossLocationSpawn wholesale, at boot and on
                // raid end via its router at TypePriority 100000. The raid consumes a clone
                // of Location.Base whose exact timing depends on the host, so this hook
                // REASSERTS the current roll rather than re-rolling: whatever moment the
                // snapshot is taken, the waves in it carry the TriggerIds the client will
                // fetch from CurrentPlan and fire. Re-rolls happen at boot and raid end
                // only. See docs/COMPAT.md.
                try
                {
                    _injector.EnsurePresent();
                }
                catch (Exception ex)
                {
                    _logger.Error("[RvR] raid-start reassert failed: " + ex);
                }
                return await new ValueTask<object>(output ?? string.Empty);
            }
        ),
    ];
}

[Injectable]
public class PlanRouter : DynamicRouter
{
    static SpawnInjector _injector = null!;
    static HttpResponseUtil _http = null!;

    public PlanRouter(SpawnInjector injector, HttpResponseUtil http, JsonUtil jsonUtil)
        : base(jsonUtil, GetRoutes())
    {
        _injector = injector;
        _http = http;
    }

    static List<RouteAction> GetRoutes() =>
    [
        new RouteAction(
            "/roguesvraiders/plan",
            async (url, info, sessionId, output) =>
            {
                var mapId = url.Split('/').Last().ToLowerInvariant();
                var plan = _injector.CurrentPlan;
                var plans = plan.TryGetValue(mapId, out var p)
                    ? p
                    : new List<SpawnPlanner.SquadPlan>();
                return await new ValueTask<object>(_http.NoBody(plans));
            }
        ),
    ];
}
