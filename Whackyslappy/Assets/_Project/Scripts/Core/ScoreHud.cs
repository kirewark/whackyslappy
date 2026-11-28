using UnityEngine;
using WhackySlappy.NPC;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Top-right score HUD drawn with OnGUI: a large 'SCORE 1200' line,
    /// a 'COMBO x3' line shown only while the last hit's combo window is
    /// still open, and a small 'KOs 7' line. Accepted hits and kills
    /// arrive through the static NpcController.AnyHit / AnyDied events
    /// and are scored by a plain hit-based ScoreKeeper. Label strings
    /// are rebuilt only when a value changes, so the draw pass itself
    /// allocates nothing per frame.
    /// </summary>
    public class ScoreHud : MonoBehaviour
    {
        [SerializeField] private int m_scoreFontSize = 32;   // large score line
        [SerializeField] private int m_comboFontSize = 20;   // combo line
        [SerializeField] private int m_killsFontSize = 14;   // KO count line
        [SerializeField] private int m_panelWidth = 280;     // right-anchored box
        [SerializeField] private int m_margin = 12;          // px from screen edge
        [SerializeField] private int m_lineGap = 10;         // px between lines

        /// <summary>
        /// Score source. The bootstrap adds this component by itself, so
        /// a default keeper is created in Awake when nobody assigned one.
        /// </summary>
        public ScoreKeeper Keeper;

        private GUIStyle m_scoreStyle;
        private GUIStyle m_comboStyle;
        private GUIStyle m_killsStyle;

        // Cached label strings plus the values they were built from, so
        // each string is rebuilt only when its number actually changes.
        private string m_scoreText = "";
        private string m_comboText = "";
        private string m_killsText = "";
        private int m_shownScore = -1;
        private int m_shownKills = -1;
        private int m_shownCombo = -1;
        private bool m_shownComboActive;

        private void Awake()
        {
            if (Keeper == null)
                Keeper = new ScoreKeeper();
        }

        private void OnEnable()
        {
            NpcController.AnyDied += OnNpcDied;
            NpcController.AnyHit += OnNpcHit;
        }

        private void OnDisable()
        {
            NpcController.AnyDied -= OnNpcDied;
            NpcController.AnyHit -= OnNpcHit;
        }

        private void OnNpcDied(NpcController npc)
        {
            Keeper.RegisterKill(Time.time);
        }

        private void OnNpcHit(NpcController npc, float strength, bool isSweep)
        {
            Keeper.RegisterHit(strength, isSweep, Time.time);
        }

        private void OnGUI()
        {
            if (m_scoreStyle == null)
            {
                m_scoreStyle = MakeLabelStyle(m_scoreFontSize);
                m_comboStyle = MakeLabelStyle(m_comboFontSize);
                m_killsStyle = MakeLabelStyle(m_killsFontSize);
            }

            if (Keeper.Score != m_shownScore)
            {
                m_shownScore = Keeper.Score;
                m_scoreText = "SCORE " + m_shownScore;
            }
            if (Keeper.Kills != m_shownKills)
            {
                m_shownKills = Keeper.Kills;
                m_killsText = "KOs " + m_shownKills;
            }

            bool comboActive = Keeper.Combo > 1 && Keeper.ComboActive(Time.time);
            if (comboActive && (Keeper.Combo != m_shownCombo || !m_shownComboActive))
            {
                m_shownCombo = Keeper.Combo;
                m_comboText = "COMBO x" + m_shownCombo;
            }
            m_shownComboActive = comboActive;

            // Right-anchored column: score, then combo, then the KO count.
            float x = Screen.width - m_panelWidth - m_margin;
            float y = m_margin;
            GUI.Label(new Rect(x, y, m_panelWidth, m_scoreFontSize + 8f),
                m_scoreText, m_scoreStyle);

            y += m_scoreFontSize + m_lineGap;
            if (comboActive)
            {
                GUI.Label(new Rect(x, y, m_panelWidth, m_comboFontSize + 8f),
                    m_comboText, m_comboStyle);
            }

            y += m_comboFontSize + m_lineGap;
            GUI.Label(new Rect(x, y, m_panelWidth, m_killsFontSize + 8f),
                m_killsText, m_killsStyle);
        }

        /// <summary>Right-aligned label style built once from GUI.skin.</summary>
        private static GUIStyle MakeLabelStyle(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.alignment = TextAnchor.UpperRight;
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
