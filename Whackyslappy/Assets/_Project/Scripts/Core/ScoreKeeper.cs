using System;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Pure hit/combo score bookkeeping for the score HUD. An accepted
    /// hit continues the combo when it lands within comboWindow seconds
    /// of the previous hit; otherwise the combo restarts at 1. A hit
    /// scores (10 + 40 * strength) points — leg sweeps count half that,
    /// with a 5-point floor — times the combo, capped at x5. A kill adds
    /// a flat 100-point bonus and never touches the combo. Deliberately
    /// plain C# (no UnityEngine types) so EditMode tests can exercise
    /// the scoring directly.
    /// </summary>
    public class ScoreKeeper
    {
        private const int KillBonus = 100;   // flat KO bonus, no multiplier
        private const int MaxCombo = 5;      // points multiplier cap
        private const int SweepFloor = 5;    // lowest a sweep hit can award

        private readonly float m_comboWindow;

        // Starts at negative infinity so the very first hit can never
        // look like a continuation of a previous one (same trap as the
        // -cooldown cooldown timers in rule 11, taken to the safe extreme).
        private float m_lastHitTime = float.NegativeInfinity;

        private int m_score;
        private int m_kills;
        private int m_hits;
        private int m_combo;       // 0 = no hit has landed yet
        private int m_bestCombo;

        /// <summary>Creates a keeper with the given combo window in seconds.</summary>
        public ScoreKeeper(float comboWindow = 4f)
        {
            m_comboWindow = comboWindow;
        }

        /// <summary>Total points earned since the last Reset.</summary>
        public int Score => m_score;

        /// <summary>Number of kills registered since the last Reset.</summary>
        public int Kills => m_kills;

        /// <summary>Number of accepted hits since the last Reset.</summary>
        public int Hits => m_hits;

        /// <summary>Current combo (0 before the first hit, else 1..n).</summary>
        public int Combo => m_combo;

        /// <summary>Highest combo reached since the last Reset.</summary>
        public int BestCombo => m_bestCombo;

        /// <summary>Combo window length in seconds (read-only).</summary>
        public float ComboWindow => m_comboWindow;

        /// <summary>
        /// Registers one accepted hit at the given game time (Time.time
        /// seconds). The combo continues when the hit is inside the window
        /// of the previous hit, otherwise it restarts at 1. Strength is
        /// clamped to 0..1; a leg sweep (isSweep) scores half before the
        /// combo multiplier applies.
        /// </summary>
        public void RegisterHit(float strength, bool isSweep, float time)
        {
            m_combo = (time - m_lastHitTime <= m_comboWindow)
                ? m_combo + 1
                : 1;
            m_lastHitTime = time;
            m_hits++;

            float clamped = strength < 0f ? 0f : (strength > 1f ? 1f : strength);
            float points = 10f + 40f * clamped;
            if (isSweep)
                points *= 0.5f;

            int multiplier = m_combo < MaxCombo ? m_combo : MaxCombo;
            int awarded = (int)Math.Round(points * multiplier,
                MidpointRounding.AwayFromZero);
            if (isSweep && awarded < SweepFloor)
                awarded = SweepFloor;
            m_score += awarded;

            if (m_combo > m_bestCombo)
                m_bestCombo = m_combo;
        }

        /// <summary>
        /// Registers one kill at the given game time: a flat KillBonus,
        /// no combo multiplier. The combo and its window are untouched —
        /// only hits move the combo.
        /// </summary>
        public void RegisterKill(float time)
        {
            m_kills++;
            m_score += KillBonus;
        }

        /// <summary>
        /// True while the combo from the most recent hit is still inside
        /// its window at the given time. Used by the HUD to decide whether
        /// to show the combo line.
        /// </summary>
        public bool ComboActive(float time)
        {
            return m_hits > 0 && time - m_lastHitTime <= m_comboWindow;
        }

        /// <summary>Clears score, hits, kills and combo for a fresh run.</summary>
        public void Reset()
        {
            m_score = 0;
            m_kills = 0;
            m_hits = 0;
            m_combo = 0;
            m_bestCombo = 0;
            m_lastHitTime = float.NegativeInfinity;
        }
    }
}
