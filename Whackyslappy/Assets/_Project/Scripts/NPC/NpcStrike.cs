namespace WhackySlappy.NPC
{
    /// <summary>
    /// The four states of the NPC strike cycle (Phase 6, task 1a).
    /// </summary>
    public enum NpcStrikePhase
    {
        Idle,
        Windup,
        Swing,
        Recover,
    }

    /// <summary>
    /// Pure strike timing state machine for an NPC (windup, swing,
    /// recover), split out from the controller so EditMode tests can pin
    /// the timing without any physics or scene code. No damage is applied
    /// here: Tick signals the hit moment (the Tick the state enters
    /// Swing) by returning true; applying any hit is the caller's job.
    /// NpcController owns when Tick runs.
    /// </summary>
    public sealed class NpcStrike
    {
        private readonly float m_windup;
        private readonly float m_swing;
        private readonly float m_cooldown;

        // Seconds spent in the current phase. The Recover phase timer
        // doubles as the cooldown timer: Recover lasts 'cooldown'
        // seconds from the swing end, and reaching Idle from Recover
        // therefore always means the cooldown has elapsed.
        private float m_phaseTimer;

        /// <summary>Current state of the strike cycle.</summary>
        public NpcStrikePhase Phase { get; private set; }

        /// <summary>
        /// Fraction (0..1) through the current phase: ramps during
        /// Windup and Swing, and during Recover the cooldown countdown
        /// (progress 1 means the strike is ready again), and is 0 in
        /// Idle.
        /// </summary>
        public float PhaseProgress
        {
            get
            {
                switch (Phase)
                {
                    case NpcStrikePhase.Windup:
                        return Progress(m_phaseTimer, m_windup);
                    case NpcStrikePhase.Swing:
                        return Progress(m_phaseTimer, m_swing);
                    case NpcStrikePhase.Recover:
                        return Progress(m_phaseTimer, m_cooldown);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Creates a cycle that starts Idle with the cooldown already
        /// elapsed, so the first strike is allowed immediately.
        /// </summary>
        public NpcStrike(
            float windup = 0.5f, float swing = 0.2f, float cooldown = 2f)
        {
            m_windup = windup;
            m_swing = swing;
            m_cooldown = cooldown;
            Reset();
        }

        /// <summary>
        /// Advances the cycle by dt seconds. Returns true on exactly the
        /// one Tick when the state enters Swing (the strike-landed
        /// signal for feedback); damage is never applied here. At most
        /// one transition per Tick (leftover dt is not carried across
        /// phases) and timers never go negative: a negative dt is
        /// treated as 0.
        /// </summary>
        public bool Tick(float dt, bool inRange)
        {
            if (dt < 0f)
                dt = 0f;

            switch (Phase)
            {
                case NpcStrikePhase.Idle:
                    // Idle is only entered from a finished Recover, a
                    // cancelled Windup or Reset, all of which leave the
                    // cooldown elapsed, so in range starts the windup.
                    if (inRange)
                    {
                        Phase = NpcStrikePhase.Windup;
                        m_phaseTimer = 0f;
                    }
                    return false;

                case NpcStrikePhase.Windup:
                    m_phaseTimer += dt;
                    if (!inRange)
                    {
                        // Target left range during the windup: cancel
                        // back to Idle with no cooldown penalty, so the
                        // NPC may start again on the next Tick.
                        Phase = NpcStrikePhase.Idle;
                        m_phaseTimer = 0f;
                        return false;
                    }
                    if (m_phaseTimer >= m_windup)
                    {
                        Phase = NpcStrikePhase.Swing;
                        m_phaseTimer = 0f;
                        return true;
                    }
                    return false;

                case NpcStrikePhase.Swing:
                    // The swing always completes, even if the target
                    // left range: committing to the swing is what makes
                    // the windup cancel above meaningful.
                    m_phaseTimer += dt;
                    if (m_phaseTimer >= m_swing)
                    {
                        Phase = NpcStrikePhase.Recover;
                        m_phaseTimer = 0f;
                    }
                    return false;

                case NpcStrikePhase.Recover:
                    // Recover lasts the full cooldown from the swing
                    // end; a restart is blocked until it finishes.
                    m_phaseTimer += dt;
                    if (m_phaseTimer >= m_cooldown)
                    {
                        Phase = NpcStrikePhase.Idle;
                        m_phaseTimer = 0f;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Back to Idle with the cooldown elapsed: the next in-range
        /// Tick starts a windup immediately.
        /// </summary>
        public void Reset()
        {
            Phase = NpcStrikePhase.Idle;
            m_phaseTimer = 0f;
        }

        private static float Progress(float elapsed, float duration)
        {
            if (duration <= 0f)
                return 1f;
            float p = elapsed / duration;
            if (p < 0f)
                return 0f;
            return p > 1f ? 1f : p;
        }
    }
}
