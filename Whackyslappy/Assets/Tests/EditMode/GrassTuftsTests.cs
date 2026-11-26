using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// Pure-logic EditMode tests for the GrassTufts blade budget. Only
    /// the static BladeCountFor lookup is touched: no scene, no GameObjects.
    /// </summary>
    public class GrassTuftsTests
    {
        [Test]
        public void BladeCountForOffTierIsZero()
        {
            Assert.That(GrassTufts.BladeCountFor(GraphicsQuality.Tier.Off), Is.EqualTo(0));
        }

        [Test]
        public void LowTierBudgetIsSmallerThanHighTier()
        {
            Assert.That(
                GrassTufts.BladeCountFor(GraphicsQuality.Tier.Low),
                Is.LessThan(GrassTufts.BladeCountFor(GraphicsQuality.Tier.High)));
        }

        [Test]
        public void HighTierBudgetStaysInsideWebGLBudget()
        {
            Assert.That(GrassTufts.BladeCountFor(GraphicsQuality.Tier.High), Is.LessThanOrEqualTo(30000));
        }
    }
}
