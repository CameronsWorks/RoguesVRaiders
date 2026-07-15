using System;
using Comfort.Common;
using EFT;

namespace RoguesVRaiders
{
    // Spawn chance scaled to the raid's average human level: 10% at level 15, +5% every 5 levels,
    // capped at 25%. Solo = your level; Fika = the true average of everyone in the raid.
    internal static class LevelScale
    {
        const int BaseLevel = 15;
        const double BaseChance = 10;
        const double PerStep = 5;
        const int StepLevels = 5;
        const double Cap = 25;

        public static double Chance()
        {
            var avg = AverageHumanLevel();
            var steps = Math.Floor((avg - BaseLevel) / (double)StepLevels);
            var chance = BaseChance + (PerStep * steps);
            return Math.Max(BaseChance, Math.Min(Cap, chance));
        }

        static int AverageHumanLevel()
        {
            var world = Singleton<GameWorld>.Instance;
            if (world == null) return BaseLevel;

            int sum = 0, count = 0;
            foreach (var player in world.AllAlivePlayersList)
            {
                if (player == null || player.IsAI) continue;
                sum += player.Profile.Info.Level;
                count++;
            }
            return count > 0 ? sum / count : BaseLevel;
        }
    }
}
