using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// Pure-logic checks on the ArenaProps density budget. The scatter
    /// itself needs a scene, so the testable contract is the exposed
    /// counts: enough props to read as a park, few enough to keep the
    /// renderer budget (under 250 renderers) and the arena walkable.
    /// </summary>
    public class ArenaPropsTests
    {
        [Test]
        public void TreeCount_IsWithinParkBudget()
        {
            Assert.That(ArenaProps.TreeCount, Is.InRange(8, 20));
        }

        [Test]
        public void RockCount_IsWithinParkBudget()
        {
            Assert.That(ArenaProps.RockCount, Is.InRange(8, 24));
        }
    }
}
