using UnityEngine;

namespace RoguesVRaiders.Core
{
    public static class Formation
    {
        // Column-of-pairs behind the leader: slots 0,1 fill row 1 (left,right), 2,3 fill row 2, etc.
        public static Vector3 SlotOffset(Vector3 leaderPos, Vector3 leaderForward, int slot, float spacing, float lateralStep)
        {
            var fwd = leaderForward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;

            var right = Vector3.Cross(Vector3.up, fwd); // unit: up perpendicular to flattened fwd
            var row = slot / 2 + 1;
            var side = slot % 2 == 0 ? -1f : 1f;

            return leaderPos - fwd * (spacing * row) + right * (lateralStep * side);
        }
    }
}
