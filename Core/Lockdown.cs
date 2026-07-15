using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguesVRaiders.Core
{
    public static class Lockdown
    {
        // Candidate hold posts: `headings` evenly-spaced compass directions, each at every radius.
        public static List<Vector3> RadialPosts(Vector3 anchor, IReadOnlyList<float> radii, int headings)
        {
            var posts = new List<Vector3>();
            if (headings < 1 || radii == null) return posts;

            for (var h = 0; h < headings; h++)
            {
                var ang = 2.0 * Math.PI * h / headings;
                var dir = new Vector3((float)Math.Sin(ang), 0f, (float)Math.Cos(ang));
                foreach (var r in radii) posts.Add(anchor + dir * r);
            }
            return posts;
        }
    }
}
