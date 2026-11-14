using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Static class with layer index constants and mask helpers for the game.
    /// Layer assignments:
    ///   6  = Environment
    ///   7  = Player
    ///   8  = PlayerHand
    ///   9  = NPC
    ///   10 = PlayerBlocker
    /// </summary>
    public static class GameLayers
    {
        public const int Environment = 6;
        public const int Player = 7;
        public const int PlayerHand = 8;
        public const int NPC = 9;
        public const int PlayerBlocker = 10;

        /// <summary>Mask for the Environment layer.</summary>
        public static int MaskEnvironment { get; } = 1 << Environment;

        /// <summary>Mask for the Player layer.</summary>
        public static int MaskPlayer { get; } = 1 << Player;

        /// <summary>Mask for the PlayerHand layer.</summary>
        public static int MaskPlayerHand { get; } = 1 << PlayerHand;

        /// <summary>Mask for the NPC layer.</summary>
        public static int MaskNPC { get; } = 1 << NPC;

        /// <summary>Mask for the PlayerBlocker layer.</summary>
        public static int MaskPlayerBlocker { get; } = 1 << PlayerBlocker;

        /// <summary>
        /// Returns a bitmask for all named game layers.
        /// </summary>
        public static int AllGameLayers { get; }
            = MaskEnvironment | MaskPlayer | MaskPlayerHand | MaskNPC | MaskPlayerBlocker;
    }
}
