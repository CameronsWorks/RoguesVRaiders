using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace RoguesVRaiders
{
    public class SquadPlan
    {
        [JsonProperty("TriggerId")] public string TriggerId;
        [JsonProperty("Faction")] public string Faction;
        [JsonProperty("BossType")] public string BossType;
        [JsonProperty("Chance")] public double Chance;
        [JsonProperty("Zone")] public string Zone;
        [JsonProperty("FireAtSeconds")] public int FireAtSeconds;
        [JsonProperty("Size")] public int Size;
        [JsonProperty("Difficulty")] public string Difficulty;
    }

    internal static class RaidPlan
    {
        public static List<SquadPlan> Fetch(string mapId)
        {
            var json = RequestHandler.GetJson($"/roguesvraiders/plan/{mapId.ToLowerInvariant()}");
            if (string.IsNullOrEmpty(json)) return new List<SquadPlan>();
            return JsonConvert.DeserializeObject<List<SquadPlan>>(json) ?? new List<SquadPlan>();
        }
    }
}
