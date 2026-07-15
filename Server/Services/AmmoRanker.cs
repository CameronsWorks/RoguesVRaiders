using SPTarkov.Server.Core.Models.Common;

namespace RoguesVRaidersServer.Services;

public static class AmmoRanker
{
    public record AmmoProps(int Pen, double Damage, string AmmoType);

    public static void Reweight(
        Dictionary<MongoId, double> pool,
        Func<MongoId, AmmoProps?> props,
        IReadOnlyList<int> rankWeights,
        int tailWeight)
    {
        var ranked = pool.Keys
            .Select(id => (id, p: props(id)))
            .Where(x => x.p != null && x.p.AmmoType != "grenade")
            .OrderByDescending(x => x.p!.Pen)
            .ThenByDescending(x => x.p!.Damage)
            .Select(x => x.id)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            pool[ranked[i]] = i < rankWeights.Count ? rankWeights[i] : tailWeight;
        }
    }
}
