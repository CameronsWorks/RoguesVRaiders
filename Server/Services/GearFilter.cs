using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace RoguesVRaidersServer.Services;

public static class GearFilter
{
    public static int ResolveClass(MongoId id, Func<MongoId, TemplateItem?> lookup)
    {
        var item = lookup(id);
        if (item?.Properties == null) { return 0; }

        var best = item.Properties.ArmorClass ?? 0;

        if (item.Properties.Slots == null) { return best; }

        foreach (var slot in item.Properties.Slots)
        {
            var options = slot.Properties?.Filters?.FirstOrDefault()?.Filter;
            if (options == null || options.Count == 0) { continue; }

            var isBuiltInInsert = slot.Required == true || options.Count == 1;
            if (!isBuiltInInsert) { continue; }

            foreach (var optionId in options)
            {
                var childClass = lookup(optionId)?.Properties?.ArmorClass ?? 0;
                if (childClass > best) { best = childClass; }
            }
        }

        return best;
    }

    public static bool HasPlateSlots(MongoId id, Func<MongoId, TemplateItem?> lookup)
    {
        var slots = lookup(id)?.Properties?.Slots;
        return slots != null &&
            slots.Any(s => s.Name?.Contains("plate", StringComparison.OrdinalIgnoreCase) == true);
    }

    public static Dictionary<MongoId, double> FilterPool(
        Dictionary<MongoId, double> pool, int minClass, bool dropUnarmored, bool keepPlateCarriers,
        Func<MongoId, TemplateItem?> lookup)
    {
        var filtered = pool
            .Where(kv =>
            {
                var cls = ResolveClass(kv.Key, lookup);
                var keep = cls == 0 ? !dropUnarmored : cls >= minClass;
                return keep || (keepPlateCarriers && HasPlateSlots(kv.Key, lookup));
            })
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return filtered.Count == 0 ? pool : filtered;
    }
}
