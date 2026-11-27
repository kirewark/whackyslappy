using NUnit.Framework;
using UnityEngine;
using WhackySlappy.NPC;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the pure aggro hysteresis helper
    /// (NpcAggro.Evaluate / NpcAggro.FlatDirection): entry at the enter
    /// radius, persistence between enter and exit radius, exit only beyond
    /// the exit radius, and flat XZ direction handling that ignores y.
    /// </summary>
    public class NpcAggroTests
    {
        [Test]
        public void Evaluate_EntersExactlyAtEnterRadius()
        {
            Assert.That(NpcAggro.Evaluate(false, 8f, 8f, 13f), Is.True);
            Assert.That(NpcAggro.Evaluate(false, 7.9f, 8f, 13f), Is.True);
        }

        [Test]
        public void Evaluate_DoesNotEnterAboveEnterRadius()
        {
            Assert.That(NpcAggro.Evaluate(false, 8.1f, 8f, 13f), Is.False);
            // A lost lock only re-arms at the enter radius, not mid-band.
            Assert.That(NpcAggro.Evaluate(false, 10f, 8f, 13f), Is.False);
        }

        [Test]
        public void Evaluate_StaysAggroBetweenEnterAndExit()
        {
            Assert.That(NpcAggro.Evaluate(true, 12.9f, 8f, 13f), Is.True);
            Assert.That(NpcAggro.Evaluate(true, 8.5f, 8f, 13f), Is.True);
            Assert.That(NpcAggro.Evaluate(true, 13f, 8f, 13f), Is.True);
        }

        [Test]
        public void Evaluate_LeavesOnlyBeyondExitRadius()
        {
            Assert.That(NpcAggro.Evaluate(true, 13.1f, 8f, 13f), Is.False);
            Assert.That(NpcAggro.Evaluate(true, 50f, 8f, 13f), Is.False);
        }

        [Test]
        public void FlatDirection_IgnoresHeight()
        {
            Vector3 dir = NpcAggro.FlatDirection(
                new Vector3(1f, 5f, 2f), new Vector3(4f, -3f, 6f));
            // (3, 0, 4) normalized: y stripped, length 1.
            Assert.That(dir.x, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(dir.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(dir.z, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(dir.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void FlatDirection_CoincidentHorizontallyReturnsZero()
        {
            // Same XZ, different height: coincident horizontally -> zero.
            Vector3 dir = NpcAggro.FlatDirection(
                new Vector3(2f, 0f, 3f), new Vector3(2f, 7f, 3f));
            Assert.That(dir, Is.EqualTo(Vector3.zero));
        }
    }
}
