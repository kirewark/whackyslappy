using NUnit.Framework;
using WhackySlappy.NPC;
using WhackySlappy.Slap;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// Pure-logic EditMode tests for NpcDamage: slap damage scaling,
    /// zero-damage sweeps, safe damage application and the death check
    /// (no scene, no GameObjects).
    /// </summary>
    public class NpcDamageTests
    {
        private static readonly float[] Strengths = { -0.5f, 0f, 0.25f, 0.5f, 1f, 1.5f };

        [Test]
        public void SweepDealsNoDamageAtAnyStrength()
        {
            foreach (float strength in Strengths)
            {
                Assert.That(NpcDamage.Compute(SlapKind.Sweep, strength), Is.EqualTo(0f),
                    "Sweep must deal 0 damage at strength " + strength);
            }
        }

        [Test]
        public void SlapDamageUsesStrengthEndpoints()
        {
            Assert.That(NpcDamage.Compute(SlapKind.Slap, 0f), Is.EqualTo(NpcDamage.MinSlapDamage));
            Assert.That(NpcDamage.Compute(SlapKind.Slap, 1f), Is.EqualTo(NpcDamage.MaxSlapDamage));
        }

        [Test]
        public void SlapDamageClampsOutOfRangeStrength()
        {
            Assert.That(NpcDamage.Compute(SlapKind.Slap, -0.5f), Is.EqualTo(NpcDamage.MinSlapDamage));
            Assert.That(NpcDamage.Compute(SlapKind.Slap, 1.5f), Is.EqualTo(NpcDamage.MaxSlapDamage));
        }

        [Test]
        public void SlapDamageLerpIsBetweenEnds()
        {
            float mid = NpcDamage.Compute(SlapKind.Slap, 0.5f);
            Assert.That(mid, Is.EqualTo(27f).Within(0.001f)); // Lerp(20, 34, 0.5)
            Assert.That(mid, Is.GreaterThan(NpcDamage.MinSlapDamage));
            Assert.That(mid, Is.LessThan(NpcDamage.MaxSlapDamage));
        }

        [Test]
        public void ThreeFullSlapsKillFromMaxHealth()
        {
            float full = NpcDamage.Compute(SlapKind.Slap, 1f);
            float health = NpcDamage.MaxHealth;

            health = NpcDamage.Apply(health, full);
            Assert.That(NpcDamage.IsDead(health), Is.False, "Dead after only 1 full slap");
            health = NpcDamage.Apply(health, full);
            Assert.That(NpcDamage.IsDead(health), Is.False, "Dead after only 2 full slaps");
            health = NpcDamage.Apply(health, full);
            Assert.That(NpcDamage.IsDead(health), Is.True, "Two full slaps (34+34) must not kill");
        }

        [Test]
        public void FiveWeakestSlapsKillFromMaxHealth()
        {
            float weak = NpcDamage.Compute(SlapKind.Slap, 0f);
            float health = NpcDamage.MaxHealth;

            for (int i = 0; i < 4; i++)
            {
                health = NpcDamage.Apply(health, weak);
                Assert.That(NpcDamage.IsDead(health), Is.False, "Dead after only " + (i + 1) + " weak slaps");
            }

            health = NpcDamage.Apply(health, weak);
            Assert.That(NpcDamage.IsDead(health), Is.True, "Four weak slaps (20*4) must not kill");
        }

        [Test]
        public void ApplyNeverGoesBelowZeroAndIgnoresNegativeDamage()
        {
            Assert.That(NpcDamage.Apply(10f, 50f), Is.EqualTo(0f));
            Assert.That(NpcDamage.Apply(10f, -5f), Is.EqualTo(10f), "Negative damage must not heal");
            Assert.That(NpcDamage.Apply(0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void IsDeadMatchesZeroHealth()
        {
            Assert.That(NpcDamage.IsDead(0f), Is.True);
            Assert.That(NpcDamage.IsDead(-1f), Is.True);
            Assert.That(NpcDamage.IsDead(0.01f), Is.False);
            Assert.That(NpcDamage.IsDead(NpcDamage.MaxHealth), Is.False);
        }
    }
}
