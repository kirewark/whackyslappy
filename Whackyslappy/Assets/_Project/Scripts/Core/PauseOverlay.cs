using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>Click-to-play / paused OnGUI screen, WebGL-player-only like PhotoMode:
    /// shown while the pointer is unlocked (the browser boots unlocked and Escape
    /// releases it) and photo mode is off, freezing timeScale to 0 until a left
    /// click re-locks the cursor via FirstPersonController.HandleCursorLock.</summary>
    public class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private int m_titleFontSize = 42;
        [SerializeField] private int m_hintFontSize = 24;
        [SerializeField] private int m_controlsFontSize = 15;
        [SerializeField, Range(0f, 1f)] private float m_dimAlpha = 0.55f;

        // Keys verified against PlayerInputReader bindings and README controls.
        private const string kTitle = "WHACKYSLAPPY";
        private const string kHintFirst = "Click to play";
        private const string kHintResume = "Paused - click to resume";
        private const string kControls =
            "WASD move    Space jump    Left Shift sprint\n" +
            "Hold Left Mouse slap (drag steers the swing)    Right Mouse leg sweep\n" +
            "Esc pause    Click to resume";

        private GUIStyle m_titleStyle, m_hintStyle, m_controlsStyle;
        private bool m_shown, m_played;

        private void Update()
        {
            bool show = ShouldShow();
            if (show && !m_shown) { m_shown = true; Time.timeScale = 0f; }
            else if (!show && m_shown) { m_shown = false; m_played = true; Time.timeScale = 1f; }
            else if (m_shown && Time.timeScale != 0f)
                Time.timeScale = 0f; // HitFeedback hit-stop restores timeScale mid-pause; keep frozen.
        }

        private static bool ShouldShow()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PhotoMode.Active)
                return false;
            return Cursor.lockState != CursorLockMode.Locked;
#else
            return false; // inert in editor/test players: they never lock, pause would freeze tests
#endif
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }

        private void Restore()
        {
            if (m_shown) { m_shown = false; Time.timeScale = 1f; }
        }

        private void OnGUI()
        {
            if (!m_shown)
                return;
            if (m_titleStyle == null)
            {
                m_titleStyle = MakeCenteredStyle(m_titleFontSize);
                m_hintStyle = MakeCenteredStyle(m_hintFontSize);
                m_controlsStyle = MakeCenteredStyle(m_controlsFontSize);
            }
            float w = Screen.width;
            float h = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, m_dimAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float y = h * 0.30f;
            GUI.Label(new Rect(0f, y, w, m_titleFontSize + 10f), kTitle, m_titleStyle);
            y += m_titleFontSize + 20f;
            GUI.Label(new Rect(0f, y, w, m_hintFontSize + 10f), m_played ? kHintResume : kHintFirst, m_hintStyle);
            GUI.Label(new Rect(0f, h * 0.55f, w, 3f * (m_controlsFontSize + 8f)), kControls, m_controlsStyle);
        }

        private static GUIStyle MakeCenteredStyle(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
