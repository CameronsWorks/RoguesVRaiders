using System;
using System.Collections.Generic;

namespace RoguesVRaiders.Core
{
    public static class SpawnPick
    {
        // Chooses `count` spawn-point indices for a squad, given each point's distance to the nearest
        // player. Points past the floor are drawn at random; if none clear it, the farthest points
        // stand in, so a camped zone still fields its squad as far out as it has. A squad bigger than
        // the pool reuses points, the way the spawner has always padded.
        public static List<int> Pick(IReadOnlyList<float> nearestPlayer, float floor, int count, Func<int, int> rng)
        {
            var picked = new List<int>();
            if (nearestPlayer == null || nearestPlayer.Count == 0 || count < 1) return picked;

            var pool = new List<int>();
            if (floor > 0f)
            {
                for (var i = 0; i < nearestPlayer.Count; i++)
                    if (nearestPlayer[i] >= floor) pool.Add(i);
            }
            if (pool.Count == 0)
            {
                for (var i = 0; i < nearestPlayer.Count; i++) pool.Add(i);
                if (floor > 0f)
                {
                    // Nothing qualifies: keep only the `count` farthest, never a random near one.
                    pool.Sort((a, b) => nearestPlayer[b].CompareTo(nearestPlayer[a]));
                    if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
                }
            }

            // Draw without replacement first, then cycle the pool for the remainder.
            for (var i = pool.Count - 1; i > 0; i--)
            {
                var j = rng(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (var i = 0; picked.Count < count; i++) picked.Add(pool[i % pool.Count]);
            return picked;
        }
    }
}
