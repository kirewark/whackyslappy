using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the pure score/combo bookkeeping
    /// (ScoreKeeper): the fresh first kill, chained kills inside the
    /// combo window, the x5 points cap, window expiry restarting the
    /// combo, the exact window boundary, a custom window, BestCombo
    /// tracking across a gap, and Reset.
    /// </summary>
    public class ScoreKeeperTests
    {
        [Test]
        public void RegisterKill_FirstKill_Scores100WithComboOne()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            Assert.That(keeper.Score, Is.EqualTo(0));
            Assert.That(keeper.Combo, Is.EqualTo(0));

            keeper.RegisterKill(0f);
            Assert.That(keeper.Score, Is.EqualTo(100));
            Assert.That(keeper.Kills, Is.EqualTo(1));
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.BestCombo, Is.EqualTo(1));
        }

        [Test]
        public void RegisterKill_ChainedKills_Score100Then200Then300()
        {
            // Default 6 s window; 1 s apart, all inside the window.
            ScoreKeeper keeper = new ScoreKeeper();
            int[] scoresAfterKill = { 100, 300, 600, 1000, 1500 };
            for (int i = 0; i < 5; i++)
            {
                keeper.RegisterKill(i * 1.0f);
                Assert.That(keeper.Score, Is.EqualTo(scoresAfterKill[i]));
                Assert.That(keeper.Combo, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void RegisterKill_PointsPerKill_CapAtFiveHundred()
        {
            // Seven chained kills: 100+200+300+400+500+500+500 = 2500.
            ScoreKeeper keeper = new ScoreKeeper();
            for (int i = 0; i < 7; i++)
            {
                keeper.RegisterKill(i * 1.0f);
            }
            Assert.That(keeper.Combo, Is.EqualTo(7));
            Assert.That(keeper.Score, Is.EqualTo(2500));
        }

        [Test]
        public void RegisterKill_WindowExpiry_RestartsComboAtOne()
        {
            ScoreKeeper keeper = new ScoreKeeper(); // window 6 s
            keeper.RegisterKill(0f);   // 100
            keeper.RegisterKill(2f);   // inside: 200
            keeper.RegisterKill(10f);  // 8 s after the last kill: expired

            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Kills, Is.EqualTo(3));
            Assert.That(keeper.Score, Is.EqualTo(100 + 200 + 100));
        }

        [Test]
        public void RegisterKill_ExactlyAtWindowBoundary_StillChains()
        {
            // time - last == window (6 s) counts as inside (<=).
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterKill(0f);
            keeper.RegisterKill(6f);
            Assert.That(keeper.Combo, Is.EqualTo(2));
            Assert.That(keeper.Score, Is.EqualTo(300));
        }

        [Test]
        public void Ctor_CustomWindow_ExpiresSooner()
        {
            ScoreKeeper keeper = new ScoreKeeper(1f); // 1 s window
            Assert.That(keeper.ComboWindow, Is.EqualTo(1f));
            keeper.RegisterKill(0f);
            keeper.RegisterKill(1.5f); // past the 1 s window

            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Score, Is.EqualTo(200));
        }

        [Test]
        public void ComboActive_TracksWindowFromLastKill()
        {
            ScoreKeeper keeper = new ScoreKeeper(); // window 6 s
            Assert.That(keeper.ComboActive(0f), Is.False); // no kill yet

            keeper.RegisterKill(0f);
            Assert.That(keeper.ComboActive(5.9f), Is.True);
            Assert.That(keeper.ComboActive(6.5f), Is.False);
        }

        [Test]
        public void BestCombo_KeepsHighestAcrossExpiredWindow()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            for (int i = 0; i < 4; i++)
            {
                keeper.RegisterKill(i * 1.0f); // combo climbs to 4
            }
            Assert.That(keeper.BestCombo, Is.EqualTo(4));

            keeper.RegisterKill(20f); // window expired: combo back to 1
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.BestCombo, Is.EqualTo(4));
        }

        [Test]
        public void Reset_ClearsEverything_AndNextKillIsFresh()
        {
            ScoreKeeper keeper = new ScoreKeeper();
            keeper.RegisterKill(0f);
            keeper.RegisterKill(1f); // combo 2, inside window
            Assert.That(keeper.Score, Is.EqualTo(300));

            keeper.Reset();
            Assert.That(keeper.Score, Is.EqualTo(0));
            Assert.That(keeper.Kills, Is.EqualTo(0));
            Assert.That(keeper.Combo, Is.EqualTo(0));
            Assert.That(keeper.BestCombo, Is.EqualTo(0));
            Assert.That(keeper.ComboActive(1f), Is.False);

            // Same instant as the pre-reset kill: still a fresh combo 1,
            // never a continuation.
            keeper.RegisterKill(1f);
            Assert.That(keeper.Combo, Is.EqualTo(1));
            Assert.That(keeper.Score, Is.EqualTo(100));
        }
    }
}
