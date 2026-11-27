namespace WhackySlappy.Core
{
    /// <summary>
    /// Pure score and combo bookkeeping for the score HUD. A kill lands
    /// inside the combo window when it happens within comboWindow seconds
    /// of the previous kill; that continues the combo, otherwise the combo
    /// restarts at 1. Each kill scores 100 times the combo, capped at x5.
    /// Deliberately plain C# (no UnityEngine types) so EditMode tests can
    /// exercise the scoring directly.
    /// </summary>
    public class ScoreKeeper
    {
        private const int BasePoints = 100;    // score of a x1 kill
        private const int MaxCombo = 5;        // points multiplier cap

        private readonly float m_comboWindow;

        // Starts at negative infinity so the very first kill can never
        // look like a continuation of a previous one (same trap as the
        // -cooldown cooldown timers in rule 11, taken to the safe extreme).
        private float m_lastKillTime = float.NegativeInfinity;

        private int m_score;
        private int m_kills;
        private int m_combo;       // 0 = no kill has landed yet
        private int m_bestCombo;

        /// <summary>Creates a keeper with the given combo window in seconds.</summary>
        public ScoreKeeper(float comboWindow = 6f)
        {
            m_comboWindow = comboWindow;
        }

        /// <summary>Total points earned since the last Reset.</summary>
        public int Score => m_score;

        /// <summary>Number of kills registered since the last Reset.</summary>
        public int Kills => m_kills;

        /// <summary>Current combo (0 before the first kill, else 1..n).</summary>
        public int Combo => m_combo;

        /// <summary>Highest combo reached since the last Reset.</summary>
        public int BestCombo => m_bestCombo;

        /// <summary>Combo window length in seconds (read-only).</summary>
        public float ComboWindow => m_comboWindow;

        /// <summary>
        /// Registers one kill at the given game time (Time.time seconds).
        /// The combo continues when the kill is inside the window of the
        /// previous kill, otherwise it restarts at 1.
        /// </summary>
        public void RegisterKill(float time)
        {
            m_combo = (time - m_lastKillTime <= m_comboWindow)
                ? m_combo + 1
                : 1;
            m_lastKillTime = time;
            m_kills++;

            int multiplier = m_combo < MaxCombo ? m_combo : MaxCombo;
            m_score += BasePoints * multiplier;

            if (m_combo > m_bestCombo)
                m_bestCombo = m_combo;
        }

        /// <summary>
        /// True while the combo from the most recent kill is still inside
        /// its window at the given time. Used by the HUD to decide whether
        /// to show the combo line.
        /// </summary>
        public bool ComboActive(float time)
        {
            return m_kills > 0 && time - m_lastKillTime <= m_comboWindow;
        }

        /// <summary>Clears score, kills and combo for a fresh run.</summary>
        public void Reset()
        {
            m_score = 0;
            m_kills = 0;
            m_combo = 0;
            m_bestCombo = 0;
            m_lastKillTime = float.NegativeInfinity;
        }
    }
}
