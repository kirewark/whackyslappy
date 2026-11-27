using NUnit.Framework;
using WhackySlappy.NPC;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the pure strike timing state machine
    /// (NpcStrike): immediate first strike when in range, out-of-range
    /// idle, windup cancel without cooldown penalty, the single
    /// strike-landed signal on the Swing entry, swings that complete
    /// regardless of range, the cooldown window, and Reset.
    /// </summary>
    public class NpcStrikeTests
    {
        [Test]
        public void Tick_FirstInRangeTickStartsWindup()
        {
            NpcStrike strike = new NpcStrike();
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
            Assert.That(strike.Tick(0.1f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
        }

        [Test]
        public void Tick_OutOfRangeStaysIdle()
        {
            NpcStrike strike = new NpcStrike();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(strike.Tick(0.1f, false), Is.False);
            }
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
            Assert.That(strike.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Windup_CancelsToIdleWhenTargetLeaves_NoPenalty()
        {
            // Defaults: windup 0.5, swing 0.2, cooldown 2.
            NpcStrike strike = new NpcStrike();
            strike.Tick(0.1f, true); // -> Windup
            strike.Tick(0.1f, true); // still winding up (0.1 of 0.5)
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));

            // Target leaves range mid-windup: cancel back to Idle.
            Assert.That(strike.Tick(0.1f, false), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));

            // No cooldown penalty: back in range, windup starts at once.
            Assert.That(strike.Tick(0.1f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
        }

        [Test]
        public void Tick_ReturnsTrueOnlyOnTheTickEnteringSwing()
        {
            // windup 1.0, swing 0.4, cooldown 2.0, dt 0.5 (exact floats).
            NpcStrike strike = new NpcStrike(1.0f, 0.4f, 2.0f);
            int landedCount = 0;
            int landedTick = -1;
            for (int tick = 1; tick <= 12; tick++)
            {
                if (strike.Tick(0.5f, true))
                {
                    landedCount++;
                    landedTick = tick;
                }
            }
            // Idle->Windup (1), windup completes (3), recover done (8),
            // second windup (9), swing again... so exactly two cycles:
            // the flag is true on exactly the Swing-entry Ticks.
            Assert.That(landedCount, Is.EqualTo(2));
            Assert.That(landedTick, Is.EqualTo(11));

            // A single strike lands exactly once.
            NpcStrike single = new NpcStrike(1.0f, 0.4f, 2.0f);
            int firstLand = 0;
            for (int tick = 1; tick <= 10; tick++)
            {
                if (single.Tick(0.5f, true))
                {
                    if (firstLand == 0)
                        firstLand = tick;
                    Assert.That(single.Phase,
                        Is.EqualTo(NpcStrikePhase.Swing));
                }
            }
            Assert.That(firstLand, Is.EqualTo(3));
        }

        [Test]
        public void Swing_CompletesEvenWhenTargetLeavesRange()
        {
            NpcStrike strike = new NpcStrike();
            strike.Tick(0.25f, true); // -> Windup
            strike.Tick(0.25f, true); // winding up: 0.25 of 0.5
            // On the third Tick the windup completes and the swing
            // starts (this Tick returns true).
            Assert.That(strike.Tick(0.25f, true), Is.True);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Swing));

            // Out of range during the swing: it still completes.
            Assert.That(strike.Tick(0.25f, false), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Recover));
        }

        [Test]
        public void PhaseProgress_RampsThroughWindupAndRecover()
        {
            // windup 1.0, swing 0.2, cooldown 1.0, dt 0.5 (exact).
            NpcStrike strike = new NpcStrike(1.0f, 0.2f, 1.0f);
            Assert.That(strike.PhaseProgress, Is.EqualTo(0f)); // Idle

            strike.Tick(0.5f, true); // -> Windup, timer 0
            Assert.That(strike.PhaseProgress, Is.EqualTo(0f));
            strike.Tick(0.5f, true); // 0.5 of 1.0 windup
            Assert.That(strike.PhaseProgress,
                Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(strike.Tick(0.5f, true), Is.True); // -> Swing
            strike.Tick(0.2f, true); // 0.2 of 0.2 swing -> Recover
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Recover));

            strike.Tick(0.5f, false); // 0.5 of 1.0 cooldown
            Assert.That(strike.PhaseProgress,
                Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(strike.Tick(0.5f, false), Is.False); // -> Idle
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
            Assert.That(strike.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Recover_CooldownBlocksRestartThenAllowsIt()
        {
            // windup 0.2, swing 0.2, cooldown 2.0, dt 0.5 (exact).
            NpcStrike strike = new NpcStrike(0.2f, 0.2f, 2.0f);
            strike.Tick(0.5f, true); // -> Windup
            Assert.That(strike.Tick(0.5f, true), Is.True); // -> Swing
            strike.Tick(0.5f, true); // -> Recover (0.5 >= 0.2 swing)

            // Cooldown still running: in range does not restart.
            for (int i = 0; i < 3; i++) // 0.5, 1.0, 1.5 of 2.0
            {
                Assert.That(strike.Tick(0.5f, true), Is.False);
                Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Recover));
            }

            // Fourth Recover Tick completes the 2.0s cooldown -> Idle.
            Assert.That(strike.Tick(0.5f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));

            // Cooldown elapsed: the next in-range Tick winds up.
            Assert.That(strike.Tick(0.5f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
        }

        [Test]
        public void Reset_FromRecover_AllowsImmediateStrike()
        {
            NpcStrike strike = new NpcStrike(0.2f, 0.2f, 2.0f);
            strike.Tick(0.5f, true); // -> Windup
            strike.Tick(0.5f, true); // -> Swing (true)
            strike.Tick(0.5f, true); // -> Recover
            Assert.That(strike.Tick(0.5f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Recover));

            strike.Reset();
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
            Assert.That(strike.PhaseProgress, Is.EqualTo(0f));

            // Reset leaves the cooldown elapsed: strike starts at once.
            Assert.That(strike.Tick(0.5f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
        }

        [Test]
        public void Reset_FromWindup_ReturnsToIdle()
        {
            NpcStrike strike = new NpcStrike();
            strike.Tick(0.1f, true); // -> Windup
            strike.Reset();
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
            Assert.That(strike.Tick(0.1f, false), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Idle));
        }

        [Test]
        public void Tick_NegativeDtTreatedAsZero_TimersNotNegative()
        {
            NpcStrike strike = new NpcStrike(1.0f, 0.2f, 1.0f);
            Assert.That(strike.Tick(-1f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
            strike.Tick(0.5f, true); // 0.5 of 1.0
            Assert.That(strike.Tick(-2f, true), Is.False);
            Assert.That(strike.Phase, Is.EqualTo(NpcStrikePhase.Windup));
            Assert.That(strike.PhaseProgress,
                Is.EqualTo(0.5f).Within(1e-5f));
        }
    }
}
