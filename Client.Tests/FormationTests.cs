using RoguesVRaiders.Core;
using UnityEngine;
using Xunit;

namespace RoguesVRaiders.Tests
{
    public class FormationTests
    {
        static void Approx(Vector3 expected, Vector3 actual)
        {
            Assert.True((expected - actual).magnitude < 0.001f, $"expected {expected}, got {actual}");
        }

        [Fact]
        public void Slot0IsOneRowBehindAndLeftOfLeader()
        {
            // Leader at origin facing +Z. Behind = -Z; left = -X (Cross(up, forward) = Cross((0,1,0),(0,0,1)) = (1,0,0) is Right, so slot0 side -1 => -X).
            var p = Formation.SlotOffset(Vector3.zero, Vector3.forward, slot: 0, spacing: 3f, lateralStep: 2f);
            Approx(new Vector3(-2f, 0f, -3f), p);
        }

        [Fact]
        public void Slot1IsSameRowRightSide()
        {
            var p = Formation.SlotOffset(Vector3.zero, Vector3.forward, slot: 1, spacing: 3f, lateralStep: 2f);
            Approx(new Vector3(2f, 0f, -3f), p);
        }

        [Fact]
        public void Slot2StartsTheSecondRowDeeper()
        {
            var p = Formation.SlotOffset(Vector3.zero, Vector3.forward, slot: 2, spacing: 3f, lateralStep: 2f);
            Approx(new Vector3(-2f, 0f, -6f), p);
        }

        [Fact]
        public void RespectsLeaderHeading()
        {
            // Facing +X: behind = -X, right = Cross(up, +X) = (0,0,-1) so slot0 side -1 => +Z.
            var p = Formation.SlotOffset(Vector3.zero, Vector3.right, slot: 0, spacing: 3f, lateralStep: 2f);
            Approx(new Vector3(-3f, 0f, 2f), p);
        }

        [Fact]
        public void FlatHeadingIgnoresVerticalComponent()
        {
            var p = Formation.SlotOffset(new Vector3(10f, 5f, 10f), new Vector3(0f, 9f, 1f), slot: 1, spacing: 3f, lateralStep: 2f);
            // forward flattens to +Z; result stays on leader's y.
            Approx(new Vector3(12f, 5f, 7f), p);
        }

        [Fact]
        public void DegenerateHeadingFallsBackToForward()
        {
            // Straight-up heading flattens to zero, so fwd falls back to +Z; right = Cross(up, +Z) = +X.
            var p = Formation.SlotOffset(Vector3.zero, Vector3.up, slot: 0, spacing: 3f, lateralStep: 2f);
            Approx(new Vector3(-2f, 0f, -3f), p);
        }
    }
}
