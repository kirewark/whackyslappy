using UnityEngine;
using WhackySlappy.Slap;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Pure-logic NPC health math: slap damage scaled by hit strength,
    /// safe damage application, and the death check. Leg sweeps deal no
    /// damage. No scene objects, so EditMode tests cover all of it.
    /// </summary>
    public static class NpcDamage
    {
        /// <summary>Health a fresh NPC starts with.</summary>
        public const float MaxHealth = 100f;

        /// <summary>Damage of the weakest slap (strength 0); five kill an NPC.</summary>
        public const float MinSlapDamage = 20f;

        /// <summary>Damage of a full-strength slap (strength 1); three kill an NPC.</summary>
        public const float MaxSlapDamage = 34f;

        /// <summary>
        /// Damage for one hit: a slap deals MinSlapDamage to MaxSlapDamage
        /// lerped by clamped strength 0-1, a leg sweep deals none, and any
        /// other kind deals none.
        /// </summary>
        public static float Compute(SlapKind kind, float strength)
        {
            if (kind == SlapKind.Slap)
            {
                return Mathf.Lerp(MinSlapDamage, MaxSlapDamage, Mathf.Clamp01(strength));
            }

            return 0f;
        }

        /// <summary>
        /// Subtracts damage from health. Clamps damage at 0 so it cannot heal,
        /// and clamps the result at 0 so health never goes negative.
        /// </summary>
        public static float Apply(float health, float damage)
        {
            return Mathf.Max(0f, health - Mathf.Max(0f, damage));
        }

        /// <summary>True when health has reached 0 or below.</summary>
        public static bool IsDead(float health)
        {
            return health <= 0f;
        }
    }
}
