using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the pure hit-based score/combo bookkeeping
    /// (ScoreKeeper): first-hit points by strength, chained-hit combo
    /// multipliers and the x5 cap, sweep halving and its floor, window
    /// expiry, the flat kill bonus that leaves the combo untouched,
    /// ComboActive tracking and Reset.
    /// </summary>
    public class ScoreKeeperTests
    {
        [Test]
        public void RegisterHit_FullStrength_Scores50WithComboOne()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            Assert.That(keeper.Score, Is.EqualTo(0));
            Assert.That(keeper.Combo, Is.EqualTo(0));
            Assert.That(keeper.Hits, Is.EqualTo(0));

            keeper.RegisterHit(1f, false, 0f);
            Assert.That(keeper.Score, Is.EqualTo(50));
            Assert.That(keeper.Hits, Is.EqualTo(1));
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.BestCombo, Is.EqualTo(1));
        }

        [Test]
        public void RegisterHit_WeakHit_ZeroStrengthScores10()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterHit(0f, false, 0f);
            Assert.That(keeper.Score, Is.EqualTo(10));
            Assert.That(keeper.Combo, Is.EqualTo(1));
        }

        [Test]
        public void RegisterHit_ChainedHits_MultipliersClimbOneToFive()
        {
            // Default 4 s window; 1 s apart, all inside the window.
            // Full-strength hits: 50, 100, 150, 200, 250.
            ScoreKeeper keeper = new ScoreKeeper();
            int[] scoresAfterHit = { 50, 150, 300, 500, 750 };
            for (int i = 0; i < 5; i++)
            {
                keeper.RegisterHit(1f, false, i * 1.0f);
                Assert.That(keeper.Score, Is.EqualTo(scoresAfterHit[i]));
                Assert.That(keeper.Combo, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void RegisterHit_Chain_PointsCapAtFiveTimes()
        {
            // Seven chained full-strength hits: 50+100+150+200+250+250+250
            // = 1250, while the combo counter itself keeps climbing.
            ScoreKeeper keeper = new ScoreKeeper();
            for (int i = 0; i < 7; i++)
            {
                keeper.RegisterHit(1f, false, i * 1.0f);
            }
            Assert.That(keeper.Combo, Is.EqualTo(7));
            Assert.That(keeper.BestCombo, Is.EqualTo(7));
            Assert.That(keeper.Hits, Is.EqualTo(7));
            Assert.That(keeper.Score, Is.EqualTo(1250));
        }

        [Test]
        public void RegisterHit_StrengthClampedToZeroAndOne()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterHit(2f, false, 0f);    // clamps to full: 50
            keeper.RegisterHit(-1f, false, 0.5f); // clamps to weak: 10 x2 = 20
            Assert.That(keeper.Score, Is.EqualTo(70));
            Assert.That(keeper.Combo, Is.EqualTo(2));
        }

        [Test]
        public void RegisterHit_Sweep_HalfPointsFloorAndRounding()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterHit(1f, true, 0f);   // sweep: 50 / 2 = 25
            keeper.RegisterHit(1f, false, 1f);  // slap, combo 2: 50 x2 = 100
            Assert.That(keeper.Score, Is.EqualTo(125));
            Assert.That(keeper.Hits, Is.EqualTo(2));

            // Weakest sweep: 10 / 2 = 5 (the sweep floor).
            ScoreKeeper weak = new ScoreKeeper();
            weak.RegisterHit(0f, true, 0f);
            Assert.That(weak.Score, Is.EqualTo(5));

            // 10 + 40*0.125 = 15, halved = 7.5, rounded up = 8.
            ScoreKeeper rounded = new ScoreKeeper();
            rounded.RegisterHit(0.125f, true, 0f);
            Assert.That(rounded.Score, Is.EqualTo(8));
        }

        [Test]
        public void RegisterHit_WindowExpiry_RestartsComboAtOne()
        {
            ScoreKeeper keeper = new ScoreKeeper(); // window 4 s
            keeper.RegisterHit(1f, false, 0f);   // 50
            keeper.RegisterHit(1f, false, 2f);   // inside: 100
            keeper.RegisterHit(1f, false, 7f);   // 5 s after last hit: expired

            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Hits, Is.EqualTo(3));
            Assert.That(keeper.Score, Is.EqualTo(50 + 100 + 50));
        }

        [Test]
        public void RegisterHit_ExactlyAtWindowBoundary_StillChains()
        {
            // time - last == window (4 s) counts as inside (<=).
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterHit(1f, false, 0f);
            keeper.RegisterHit(1f, false, 4f);
            Assert.That(keeper.Combo, Is.EqualTo(2));
            Assert.That(keeper.Score, Is.EqualTo(150));
        }

        [Test]
        public void Ctor_CustomWindow_ExpiresSooner()
        {
            ScoreKeeper keeper = new ScoreKeeper(1f); // 1 s window
            Assert.That(keeper.ComboWindow, Is.EqualTo(1f));
            keeper.RegisterHit(1f, false, 0f);
            keeper.RegisterHit(1f, false, 1.5f); // past the 1 s window

            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Score, Is.EqualTo(100));
        }

        [Test]
        public void RegisterKill_FlatBonus_LeavesComboAndWindowUntouched()
        {
            ScoreKeeper keeper = new ScoreKeeper(); // window 4 s
            keeper.RegisterHit(1f, false, 0f);   // combo 1
            keeper.RegisterHit(1f, false, 1f);   // combo 2, score 150

            keeper.RegisterKill(1.2f);           // flat +100, combo unchanged
            Assert.That(keeper.Score, Is.EqualTo(250));
            Assert.That(keeper.Kills, Is.EqualTo(1));
            Assert.That(keeper.Combo, Is.EqualTo(2));
            Assert.That(keeper.BestCombo, Is.EqualTo(2));
            Assert.That(keeper.Hits, Is.EqualTo(2));

            // The kill does not refresh the hit combo window: last hit was
            // at 1 s, so the window is already gone at 5.5 s.
            Assert.That(keeper.ComboActive(5.5f), Is.False);
            keeper.RegisterKill(6f);
            keeper.RegisterHit(1f, false, 7f);   // expired: restarts at 1
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Kills, Is.EqualTo(2));
            Assert.That(keeper.Score, Is.EqualTo(250 + 100 + 50));
        }

        [Test]
        public void ComboActive_TracksWindowFromLastHit()
        {
            ScoreKeeper keeper = new ScoreKeeper(); // window 4 s
            Assert.That(keeper.ComboActive(0f), Is.False); // no hit yet

            keeper.RegisterHit(1f, false, 0f);
            Assert.That(keeper.ComboActive(3.9f), Is.True);
            Assert.That(keeper.ComboActive(4.5f), Is.False);
        }

        [Test]
        public void Reset_ClearsEverything_AndNextHitIsFresh()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterHit(1f, false, 0f);
            keeper.RegisterHit(1f, false, 1f); // combo 2, inside window
            keeper.RegisterKill(1.1f);
            Assert.That(keeper.Score, Is.EqualTo(250));

            keeper.Reset();
            Assert.That(keeper.Score, Is.EqualTo(0));
            Assert.That(keeper.Kills, Is.EqualTo(0));
            Assert.That(keeper.Hits, Is.EqualTo(0));
            Assert.That(keeper.Combo, Is.EqualTo(0));
            Assert.That(keeper.BestCombo, Is.EqualTo(0));
            Assert.That(keeper.ComboActive(1f), Is.False);

            // Same instant as the pre-reset hits: still a fresh combo 1,
            // never a continuation.
            keeper.RegisterHit(1f, false, 1f);
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Score, Is.EqualTo(50));
        }
    }
}
