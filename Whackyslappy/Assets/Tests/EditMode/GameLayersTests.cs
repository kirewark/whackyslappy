using NUnit.Framework;
using WhackySlappy.Core;

namespace WhackySlappy.Tests.EditMode
{
    /// <summary>
    /// Pure-logic EditMode tests for the GameLayers layer constants
    /// and the layer-mask helpers (no scene, no GameObjects).
    /// </summary>
    public class GameLayersTests
    {
        private static readonly int[] AllMasks =
        {
            GameLayers.MaskEnvironment,
            GameLayers.MaskPlayer,
            GameLayers.MaskPlayerHand,
            GameLayers.MaskNPC,
            GameLayers.MaskPlayerBlocker,
        };

        [Test]
        public void MasksEqualOneShiftedByLayerIndex()
        {
            Assert.That(GameLayers.Environment, Is.EqualTo(6));
            Assert.That(GameLayers.Player, Is.EqualTo(7));
            Assert.That(GameLayers.PlayerHand, Is.EqualTo(8));
            Assert.That(GameLayers.NPC, Is.EqualTo(9));
            Assert.That(GameLayers.PlayerBlocker, Is.EqualTo(10));

            Assert.That(GameLayers.MaskEnvironment, Is.EqualTo(1 << GameLayers.Environment));
            Assert.That(GameLayers.MaskPlayer, Is.EqualTo(1 << GameLayers.Player));
            Assert.That(GameLayers.MaskPlayerHand, Is.EqualTo(1 << GameLayers.PlayerHand));
            Assert.That(GameLayers.MaskNPC, Is.EqualTo(1 << GameLayers.NPC));
            Assert.That(GameLayers.MaskPlayerBlocker, Is.EqualTo(1 << GameLayers.PlayerBlocker));

            // Pin the concrete layer numbers against TagManager drift.
            Assert.That(GameLayers.MaskEnvironment, Is.EqualTo(64));
            Assert.That(GameLayers.MaskPlayer, Is.EqualTo(128));
            Assert.That(GameLayers.MaskPlayerHand, Is.EqualTo(256));
            Assert.That(GameLayers.MaskNPC, Is.EqualTo(512));
            Assert.That(GameLayers.MaskPlayerBlocker, Is.EqualTo(1024));
        }

        [Test]
        public void MasksAreDistinctSingleBits()
        {
            foreach (int mask in AllMasks)
            {
                Assert.That(mask, Is.GreaterThan(0));
                Assert.That(mask & (mask - 1), Is.EqualTo(0), "Mask " + mask + " is not a single bit");
            }

            for (int i = 0; i < AllMasks.Length; i++)
            {
                for (int j = i + 1; j < AllMasks.Length; j++)
                {
                    Assert.That(AllMasks[i] & AllMasks[j], Is.EqualTo(0),
                        "Masks " + AllMasks[i] + " and " + AllMasks[j] + " share a bit");
                }
            }
        }

        [Test]
        public void AllGameLayersContainsEveryMaskAndNothingElse()
        {
            int union = 0;
            foreach (int mask in AllMasks)
            {
                union |= mask;
                Assert.That(GameLayers.AllGameLayers & mask, Is.EqualTo(mask),
                    "AllGameLayers is missing mask " + mask);
            }

            Assert.That(GameLayers.AllGameLayers, Is.EqualTo(union));
            Assert.That(GameLayers.AllGameLayers & ~union, Is.EqualTo(0),
                "AllGameLayers contains a bit outside the five game layers");
            Assert.That(GameLayers.AllGameLayers, Is.EqualTo(1984)); // 64+128+256+512+1024
        }
    }
}
