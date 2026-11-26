using NUnit.Framework;
using UnityEngine;
using WhackySlappy.NPC;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the pure wander steering helper
    /// (NpcWander.SteerHorizontal): the horizontal velocity must ramp to
    /// the desired speed, never overshoot it, and never touch the
    /// vertical axis.
    /// </summary>
    public class NpcWanderTests
    {
        [Test]
        public void SteerHorizontal_ReachesDesiredVelocityAfterEnoughTime()
        {
            Vector3 v = Vector3.zero;
            Vector3 desired = new Vector3(1.6f, 0f, 0f);
            // 8 m/s^2 over a 1/60 s tick needs 0.2 s to close 1.6 m/s;
            // run a full second of fixed ticks.
            for (int i = 0; i < 60; i++)
                v = NpcWander.SteerHorizontal(v, desired, 8f, 1f / 60f);

            Assert.That(v.x, Is.EqualTo(desired.x).Within(1e-4f));
            Assert.That(v.z, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void SteerHorizontal_NeverExceedsDesiredMagnitude()
        {
            Vector3 v = Vector3.zero;
            Vector3 desired = new Vector3(0f, 0f, -1.6f);
            for (int i = 0; i < 600; i++)
            {
                v = NpcWander.SteerHorizontal(v, desired, 8f, 1f / 50f);
                Assert.That(v.magnitude,
                    Is.LessThanOrEqualTo(desired.magnitude + 1e-4f));
            }
        }

        [Test]
        public void SteerHorizontal_IgnoresVerticalComponents()
        {
            // A vertical part in either input must never reach the output:
            // linearVelocity.y is steered by gravity, not by wander.
            Vector3 v = NpcWander.SteerHorizontal(
                new Vector3(0f, 9f, 0f), new Vector3(0f, 0f, 1.6f), 8f, 1f / 60f);
            Assert.That(v.y, Is.EqualTo(0f));
            Assert.That(v.magnitude, Is.LessThanOrEqualTo(1.6f + 1e-4f));
        }
    }
}
