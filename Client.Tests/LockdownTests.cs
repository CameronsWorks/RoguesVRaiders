using RoguesVRaiders.Core;
using UnityEngine;
using Xunit;

namespace RoguesVRaiders.Tests
{
    public class LockdownTests
    {
        static void Approx(Vector3 expected, Vector3 actual)
        {
            Assert.True((expected - actual).magnitude < 0.01f, $"expected {expected}, got {actual}");
        }

        [Fact]
        public void FourHeadingsOneRadiusGivesCardinalPosts()
        {
            var posts = Lockdown.RadialPosts(Vector3.zero, new[] { 10f }, 4);
            Assert.Equal(4, posts.Count);
            Approx(new Vector3(0f, 0f, 10f), posts[0]);   // heading 0: +Z
            Approx(new Vector3(10f, 0f, 0f), posts[1]);   // heading 1: +X
            Approx(new Vector3(0f, 0f, -10f), posts[2]);  // heading 2: -Z
            Approx(new Vector3(-10f, 0f, 0f), posts[3]);  // heading 3: -X
        }

        [Fact]
        public void CountIsHeadingsTimesRadii()
        {
            var posts = Lockdown.RadialPosts(Vector3.zero, new[] { 4f, 8f, 12f }, 8);
            Assert.Equal(24, posts.Count);
        }

        [Fact]
        public void PostsAreOffsetFromTheAnchor()
        {
            var anchor = new Vector3(100f, 5f, -50f);
            var posts = Lockdown.RadialPosts(anchor, new[] { 6f }, 4);
            Approx(new Vector3(100f, 5f, -44f), posts[0]); // +Z*6 from anchor, y preserved
        }

        [Fact]
        public void ZeroHeadingsGivesNoPosts()
        {
            Assert.Empty(Lockdown.RadialPosts(Vector3.zero, new[] { 10f }, 0));
        }
    }
}
