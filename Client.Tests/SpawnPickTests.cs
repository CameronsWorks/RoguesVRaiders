using System.Collections.Generic;
using System.Linq;
using RoguesVRaiders.Core;
using Xunit;

namespace RoguesVRaiders.Tests
{
    public class SpawnPickTests
    {
        static int FirstIndex(int _) => 0;

        [Fact]
        public void OnlyQualifyingPointsArePickedWhenSomeClearTheFloor()
        {
            var distances = new List<float> { 50f, 250f, 120f, 300f, 210f };
            var picked = SpawnPick.Pick(distances, 200f, 3, FirstIndex);

            Assert.Equal(3, picked.Count);
            Assert.All(picked, i => Assert.True(distances[i] >= 200f));
        }

        [Fact]
        public void NoDuplicatesWhileThePoolLasts()
        {
            var distances = new List<float> { 250f, 260f, 270f, 280f };
            var picked = SpawnPick.Pick(distances, 200f, 4, FirstIndex);

            Assert.Equal(4, picked.Distinct().Count());
        }

        [Fact]
        public void SquadBiggerThanPoolReusesQualifyingPoints()
        {
            var distances = new List<float> { 50f, 250f, 60f };
            var picked = SpawnPick.Pick(distances, 200f, 5, FirstIndex);

            Assert.Equal(5, picked.Count);
            Assert.All(picked, i => Assert.Equal(1, i));
        }

        [Fact]
        public void NothingClearsTheFloorFallsBackToTheFarthest()
        {
            var distances = new List<float> { 10f, 50f, 30f, 40f };
            var picked = SpawnPick.Pick(distances, 200f, 2, FirstIndex);

            Assert.Equal(2, picked.Count);
            Assert.Equal(new[] { 1, 3 }, picked.OrderBy(i => i).ToArray());
        }

        [Fact]
        public void FloorOffPicksFreely()
        {
            var distances = new List<float> { 10f, 20f, 30f };
            var picked = SpawnPick.Pick(distances, 0f, 3, FirstIndex);

            Assert.Equal(3, picked.Distinct().Count());
        }

        [Fact]
        public void EmptyInputsGiveEmptyResults()
        {
            Assert.Empty(SpawnPick.Pick(new List<float>(), 200f, 3, FirstIndex));
            Assert.Empty(SpawnPick.Pick(null, 200f, 3, FirstIndex));
            Assert.Empty(SpawnPick.Pick(new List<float> { 100f }, 200f, 0, FirstIndex));
        }
    }
}
