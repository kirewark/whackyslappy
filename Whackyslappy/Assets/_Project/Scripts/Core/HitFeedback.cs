using System.Collections;
using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Hit feedback: camera kick (pitch/roll) and optional hit-stop.
    /// Design 3.7: ~1-2 degrees, recovers in 0.1 s.
    /// Optional hit-stop: timeScale 0.05 for ~0.04 s, toggled with a key.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [Header("Camera Kick")]
        [SerializeField] private float m_kickPitch = 1.5f;
        [SerializeField] private float m_kickRoll = 0.5f;
        [SerializeField] private float m_kickDuration = 0.1f;

        [Header("Hit-Stop")]
        [SerializeField] private bool m_hitStopEnabled = true;
        [SerializeField] private float m_hitStopTimeScale = 0.05f;
        [SerializeField] private float m_hitStopDuration = 0.04f;
        [SerializeField] private KeyCode m_hitStopToggleKey = KeyCode.F1;

        private float m_currentKickPitch;
        private float m_currentKickRoll;
        private float m_kickElapsed;
        private float m_basePitch;
        private Coroutine m_hitStopCoroutine;

        /// <summary>
        /// Call this on every hit to trigger camera kick and hit-stop.
        /// Safe against overlapping calls: reuses existing kick state,
        /// restarts hit-stop coroutine idempotently.
        /// </summary>
        public static void Trigger(float strength = 1f)
        {
            var instance = FindObjectOfType<HitFeedback>();
            if (instance == null) return;
            instance.TriggerLocally(strength);
        }

        private void TriggerLocally(float strength)
        {
            // Enable the component (may have been disabled since last hit).
            enabled = true;

            // Camera kick: accumulate into current offset (each hit adds more shake).
            // strength scales the kick amount so harder hits feel bigger.
            m_currentKickPitch += m_kickPitch * strength;
            m_currentKickRoll += m_kickRoll * strength;
            m_kickElapsed = 0f;

            // Record the base pitch so the shake adds to (not replaces) the current view.
            m_basePitch = CameraPivotAngle();

            // Hit-stop: cancel any existing one so the latest hit wins.
            if (m_hitStopCoroutine != null)
            {
                StopCoroutine(m_hitStopCoroutine);
            }

            if (m_hitStopEnabled)
            {
                m_hitStopCoroutine = StartCoroutine(HitStopCoroutine());
            }
        }

        private IEnumerator HitStopCoroutine()
        {
            var prevTimeScale = Time.timeScale;
            var prevFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = m_hitStopTimeScale;
            Time.fixedDeltaTime = prevTimeScale * m_hitStopDuration;
            yield return new WaitForSeconds(m_hitStopDuration);
            Time.timeScale = prevTimeScale;
            Time.fixedDeltaTime = prevFixedDelta;
            m_hitStopCoroutine = null;
        }

        private void Update()
        {
            // Toggle hit-stop at runtime with the configured key.
            if (Input.GetKeyDown(m_hitStopToggleKey))
            {
                m_hitStopEnabled = !m_hitStopEnabled;
            }

            // Decay camera kick exponentially.
            if (m_kickElapsed < m_kickDuration)
            {
                m_kickElapsed += Time.unscaledDeltaTime;
                float t = m_kickElapsed / m_kickDuration;
                float decay = 1f - t * t; // quadratic ease-out

                // Add shake offset to the base pitch so it doesn't fight
                // the FPS controller's look code (which sets CameraPivot
                // localEulerAngles.yaw=0, pitch=m_pitch, roll=0).
                transform.localEulerAngles = new Vector3(
                    m_basePitch + m_currentKickPitch * decay,
                    transform.localEulerAngles.y,
                    m_currentKickRoll * decay
                );
            }
            else
            {
                // Reset kick to zero and restore base pitch.
                if (m_currentKickPitch != 0f || m_currentKickRoll != 0f)
                {
                    m_currentKickPitch = 0f;
                    m_currentKickRoll = 0f;
                    transform.localEulerAngles = new Vector3(
                        m_basePitch,
                        transform.localEulerAngles.y,
                        0f
                    );
                }
                // After shake is done, disable so it stops fighting look code.
                enabled = false;
            }
        }

        /// <summary>
        /// Read the current pitch angle from the camera pivot.
        /// The FPS controller stores pitch in CameraPivot.localEulerAngles.x.
        /// </summary>
        private float CameraPivotAngle()
        {
            return transform.parent != null
                ? transform.parent.localEulerAngles.x
                : transform.localEulerAngles.x;
        }
    }
}
