using RoguesVRaiders.Core;
using Xunit;

namespace RoguesVRaiders.Tests
{
    public class ObjectivePriorityTests
    {
        [Fact]
        public void DefaultSainPrioritiesLandAtTwelve()
        {
            Assert.Equal(12, ObjectivePriority.Compute(20, 22, 24, gap: 8, floor: 5));
        }

        [Fact]
        public void UsesMinimumOfTheThree()
        {
            Assert.Equal(7, ObjectivePriority.Compute(15, 22, 24, gap: 8, floor: 5));
        }

        [Fact]
        public void ClampsToFloorWhenGapWouldGoBelow()
        {
            Assert.Equal(5, ObjectivePriority.Compute(12, 12, 12, gap: 8, floor: 5));
        }

        [Fact]
        public void NeverReachesTheMinimumEvenWhenFloorWould()
        {
            // min is 4, floor 5 would exceed it; must stay strictly below min.
            Assert.Equal(3, ObjectivePriority.Compute(4, 9, 9, gap: 8, floor: 5));
        }
    }
}
