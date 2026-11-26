using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Debug FPS readout that exists ONLY when the WebGL page URL carries
    /// ?fps=1: the Install hook creates the GameObject itself, so in
    /// normal play no FpsCounter object ever exists. Draws one small
    /// OnGUI label top-left with a smoothed FPS value and the current
    /// grass tier, refreshed twice per second from a cached string.
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;      // twice per second
        private const float SmoothPerSecond = 4f;        // ~0.25 s time constant

        private float m_avgUnscaledDelta = 1f / 60f;
        private float m_refreshTimer;                    // 0 -> label on frame 1
        private string m_label = "FPS";

        /// <summary>
        /// Creates the counter at AfterSceneLoad, but only in a WebGL
        /// player whose URL contains fps=1. Everything else (Editor,
        /// standalone, plain WebGL URL) compiles the body away entirely.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GraphicsQuality.QueryValue(Application.absoluteURL, "fps") != "1")
                return;

            var counter = new GameObject("FpsCounter");
            counter.AddComponent<FpsCounter>();
#endif
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Exponential moving average of the frame time; alpha picks a
            // ~0.25 s time constant independent of the frame rate.
            float alpha = Mathf.Clamp(dt * SmoothPerSecond, 0f, 1f);
            m_avgUnscaledDelta += (dt - m_avgUnscaledDelta) * alpha;

            m_refreshTimer -= dt;
            if (m_refreshTimer <= 0f)
            {
                m_refreshTimer = RefreshInterval;
                int fps = Mathf.RoundToInt(1f / Mathf.Max(1e-4f, m_avgUnscaledDelta));
                m_label = "FPS " + fps + " | grass: " + GraphicsQuality.Level.ToString().ToLowerInvariant();
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10f, 10f, 320f, 26f), m_label);
        }
    }
}
