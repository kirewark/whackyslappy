using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Central tuning hub for the silly-physics pass (Pass 2, Phase 4).
    /// One master Silliness slider (0..1) drives every feel multiplier,
    /// so Silliness = 0 reproduces the plain pre-pass behaviour.
    /// All lerp endpoints live here and nowhere else.
    /// </summary>
    public static class SillyPhysics
    {
        /// <summary>Silliness restored on every domain reload / play-mode entry.</summary>
        public const float DefaultSilliness = 1f;

        /// <summary>Master silliness slider: 0 = plain physics, 1 = full silly.
        /// Statics are reset to DefaultSilliness on domain reload so stale
        /// Editor-tuned values never leak between play sessions.</summary>
        public static float Silliness = DefaultSilliness;

        // ---- Lerp endpoints (single source of truth for all tuning) ----

        public const float ImpulseMultiplierAt0 = 1.0f;
        public const float ImpulseMultiplierAt1 = 1.3f;

        public const float SpinTorqueAt0 = 0.0f;
        public const float SpinTorqueAt1 = 3.0f;

        public const float HangTimeAt0 = 0.0f;
        public const float HangTimeAt1 = 0.3f;

        public const float HangGravityScaleAt0 = 1.0f;
        public const float HangGravityScaleAt1 = 0.6f;

        public const float MaxSpeedAt0 = 12.0f;
        public const float MaxSpeedAt1 = 16.0f;

        public const float BounceFactorAt0 = 0.0f;
        public const float BounceFactorAt1 = 0.45f;

        public const float SquashAmountAt0 = 0.0f;
        public const float SquashAmountAt1 = 0.25f;

        // ---- Derived feel values ----

        /// <summary>Slap impulse magnitude multiplier (1 .. 1.3).</summary>
        public static float ImpulseMultiplier =>
            Mathf.Lerp(ImpulseMultiplierAt0, ImpulseMultiplierAt1, Mathf.Clamp01(Silliness));

        /// <summary>Random-axis spin torque added to a launched NPC (0 .. 3).</summary>
        public static float SpinTorque =>
            Mathf.Lerp(SpinTorqueAt0, SpinTorqueAt1, Mathf.Clamp01(Silliness));

        /// <summary>Duration (s) of the low-gravity hang window after a launch (0 .. 0.3).</summary>
        public static float HangTimeSeconds =>
            Mathf.Lerp(HangTimeAt0, HangTimeAt1, Mathf.Clamp01(Silliness));

        /// <summary>Fraction of gravity felt during the hang window (1 .. 0.6).</summary>
        public static float HangGravityScale =>
            Mathf.Lerp(HangGravityScaleAt0, HangGravityScaleAt1, Mathf.Clamp01(Silliness));

        /// <summary>Hard cap on NPC linear speed (m/s, 12 .. 16).</summary>
        public static float MaxSpeed =>
            Mathf.Lerp(MaxSpeedAt0, MaxSpeedAt1, Mathf.Clamp01(Silliness));

        /// <summary>Bounciness of the NPC body PhysicsMaterial (0 .. 0.45);
        /// 0 at Silliness 0 keeps the NPC at default (non-bouncy) physics.</summary>
        public static float BounceFactor =>
            Mathf.Lerp(BounceFactorAt0, BounceFactorAt1, Mathf.Clamp01(Silliness));

        /// <summary>Peak squash-stretch scale delta on hit (0 .. 0.25).</summary>
        public static float SquashAmount =>
            Mathf.Lerp(SquashAmountAt0, SquashAmountAt1, Mathf.Clamp01(Silliness));

        /// <summary>
        /// Unity 6 domain-reload safety: static mutable state must be reset
        /// explicitly, otherwise a value tuned in one play session persists
        /// into the next when "Enter Play Mode Options" is enabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Silliness = DefaultSilliness;
        }
    }
}
