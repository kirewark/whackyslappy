using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// Pure-logic EditMode tests for the SillyPhysics silliness-driven feel
    /// values. Only static getters and Mathf are touched: no scene, no GameObjects.
    /// </summary>
    public class SillyPhysicsTests
    {
        [TearDown]
        public void RestoreDefaultSilliness()
        {
            SillyPhysics.Silliness = SillyPhysics.DefaultSilliness;
        }

        [Test]
        public void FeelValuesEqualAtZeroEndpointsWhenSillinessIsZero()
        {
            SillyPhysics.Silliness = 0f;
            Assert.That(SillyPhysics.ImpulseMultiplier, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(SillyPhysics.SpinTorque, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(SillyPhysics.HangTimeSeconds, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(SillyPhysics.HangGravityScale, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(SillyPhysics.MaxSpeed, Is.EqualTo(12.0f).Within(1e-5f));
            Assert.That(SillyPhysics.BounceFactor, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(SillyPhysics.SquashAmount, Is.EqualTo(0.0f).Within(1e-5f));
        }

        [Test]
        public void FeelValuesEqualAtOneEndpointsWhenSillinessIsOne()
        {
            SillyPhysics.Silliness = 1f;
            Assert.That(SillyPhysics.ImpulseMultiplier, Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(SillyPhysics.SpinTorque, Is.EqualTo(2.2f).Within(1e-5f));
            Assert.That(SillyPhysics.HangTimeSeconds, Is.EqualTo(0.06f).Within(1e-5f));
            Assert.That(SillyPhysics.HangGravityScale, Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(SillyPhysics.MaxSpeed, Is.EqualTo(13.0f).Within(1e-5f));
            Assert.That(SillyPhysics.BounceFactor, Is.EqualTo(0.12f).Within(1e-5f));
            Assert.That(SillyPhysics.SquashAmount, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void OutOfRangeSillinessClampsToNearestEndpoint()
        {
            SillyPhysics.Silliness = -1f;
            Assert.That(SillyPhysics.ImpulseMultiplier, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(SillyPhysics.SpinTorque, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(SillyPhysics.MaxSpeed, Is.EqualTo(12.0f).Within(1e-5f));

            SillyPhysics.Silliness = 2f;
            Assert.That(SillyPhysics.ImpulseMultiplier, Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(SillyPhysics.SpinTorque, Is.EqualTo(2.2f).Within(1e-5f));
            Assert.That(SillyPhysics.MaxSpeed, Is.EqualTo(13.0f).Within(1e-5f));
        }
    }
}
