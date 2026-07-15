using System.Collections.Generic;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RoguesVRaiders.Objective
{
    internal static class PoiRegistry
    {
        const int MinRingPoints = 3;
        const float SampleRadius = 5f;

        class Ring
        {
            public Vector3 Center;
            public List<Vector3> Points;
        }

        static readonly List<Ring> _rings = new List<Ring>();
        static bool _built;

        static IEnumerable<BotZone> Zones() => LocationScene.GetAll<BotZone>();

        public static void Build()
        {
            _rings.Clear();
            _built = false;

            foreach (var zone in Zones())
            {
                if (zone?.PatrolWays == null) continue;
                foreach (var way in zone.PatrolWays)
                {
                    if (way?.Points == null) continue;
                    var pts = new List<Vector3>();
                    foreach (var p in way.Points)
                    {
                        if (p == null) continue;
                        if (NavMesh.SamplePosition(p.Position, out var hit, SampleRadius, NavMesh.AllAreas))
                            pts.Add(hit.position);
                    }
                    if (pts.Count >= MinRingPoints)
                        _rings.Add(new Ring { Center = Centroid(pts), Points = pts });
                }
            }

            _built = _rings.Count > 0;
            RvRPlugin.Log.LogInfo($"RvR POI: {_rings.Count} patrol ring(s) built");
        }

        public static void Clear() { _rings.Clear(); _built = false; }

        public static bool HasRings => _built;

        // Nearest ring to a position; null if none. `taken` lets the controller spread squads across rings.
        public static List<Vector3> NearestRing(Vector3 from, HashSet<Vector3> taken, out Vector3 center)
        {
            Ring best = null;
            var bestDist = float.MaxValue;
            foreach (var r in _rings)
            {
                if (taken.Contains(r.Center)) continue;
                var d = (r.Center - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = r; }
            }
            if (best == null) { center = Vector3.zero; return null; }
            center = best.Center;
            taken.Add(best.Center);
            return best.Points;
        }

        static Vector3 Centroid(List<Vector3> pts)
        {
            var sum = Vector3.zero;
            foreach (var p in pts) sum += p;
            return sum / pts.Count;
        }
    }
}
