using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using WhackySlappy.Player;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Hit feedback: camera kick (pitch/roll) and optional hit-stop.
    /// Design 3.7: ~1-2 degrees, recovers in 0.1 s.
    /// Optional hit-stop: timeScale 0.05 for ~0.04 s, toggled with F1.
    /// Lives on the camera pivot. In LateUpdate it reads the controller's
    /// base pitch and adds a decaying kick offset on top, after the FPS
    /// controller has written its pose in Update, so the two never fight.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        /// <summary>
        /// Strength floor: even a zero-strength hit gets a visible kick
        /// and hit-stop, so weak hits never feel like dead inputs.
        /// </summary>
        private const float MinStrength = 0.25f;

        [Header("Camera Kick")]
        [SerializeField] private float m_kickPitch = 1.5f;
        [SerializeField] private float m_kickRoll = 0.5f;
        [SerializeField] private float m_kickDuration = 0.1f;

        [Header("Hit-Stop")]
        [SerializeField] private bool m_hitStopEnabled = true;
        [SerializeField] private float m_hitStopTimeScale = 0.05f;
        [SerializeField] private float m_hitStopDuration = 0.04f;

        private FirstPersonController m_player;
        private float m_currentKickPitch;
        private float m_currentKickRoll;
        private float m_kickElapsed;
        private bool m_kickActive;

        private Coroutine m_hitStopCoroutine;
        private bool m_inHitStop;
        private float m_originalTimeScale = 1f;

        /// <summary>
        /// Call this on every hit to trigger camera kick and hit-stop.
        /// Safe against overlapping calls: the kick accumulates, and a hit
        /// during hit-stop restarts the freeze timer without recapturing
        /// the already-slowed timeScale.
        /// </summary>
        public static void Trigger(float strength = 1f)
        {
            var instance = FindAnyObjectByType<HitFeedback>();
            if (instance == null) return;
            instance.TriggerLocally(strength);
        }

        private void Awake()
        {
            // HitFeedback sits on the camera pivot, a child of the player.
            m_player = GetComponentInParent<FirstPersonController>();
        }

        private void TriggerLocally(float strength)
        {
            // The bootstrap starts us disabled; the first hit turns us on.
            enabled = true;

            strength = Mathf.Max(strength, MinStrength);

            // Camera kick: accumulate into current offset (each hit adds more shake).
            m_currentKickPitch += m_kickPitch * strength;
            m_currentKickRoll += m_kickRoll * strength;
            m_kickElapsed = 0f;
            m_kickActive = true;

            // Hit-stop: a new hit restarts the timer. Stopping the running
            // coroutine skips its restore line, but m_inHitStop stays true,
            // so the restart keeps the real timeScale and stays frozen.
            if (m_hitStopCoroutine != null)
            {
                StopCoroutine(m_hitStopCoroutine);
                m_hitStopCoroutine = null;
            }

            if (m_hitStopEnabled)
            {
                m_hitStopCoroutine = StartCoroutine(HitStopCoroutine());
            }
            else
            {
                RestoreTimeScale();
            }
        }

        private IEnumerator HitStopCoroutine()
        {
            // Capture the real timeScale only when not already frozen, so a
            // re-trigger during hit-stop cannot store the slowed value.
            // Time.fixedDeltaTime is left alone: physics just steps less often.
            if (!m_inHitStop)
            {
                m_originalTimeScale = Time.timeScale;
                m_inHitStop = true;
            }
            Time.timeScale = m_hitStopTimeScale;

            // Wait in realtime: scaled time is nearly frozen, so a scaled
            // WaitForSeconds would take ~20x longer than intended.
            yield return new WaitForSecondsRealtime(m_hitStopDuration);

            m_hitStopCoroutine = null;
            RestoreTimeScale();
        }

        private void RestoreTimeScale()
        {
            if (!m_inHitStop) return;
            m_inHitStop = false;
            Time.timeScale = m_originalTimeScale;
        }

        private void OnDisable()
        {
            // Disabling stops the coroutine, so restore here or the game
            // stays frozen forever. We never self-disable during hit-stop.
            RestoreTimeScale();
        }

        private void OnDestroy()
        {
            RestoreTimeScale();
        }

        private void Update()
        {
            // Toggle hit-stop at runtime with F1 (Input System only:
            // legacy UnityEngine.Input throws in this project).
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                m_hitStopEnabled = !m_hitStopEnabled;
                if (!m_hitStopEnabled)
                {
                    if (m_hitStopCoroutine != null)
                    {
                        StopCoroutine(m_hitStopCoroutine);
                        m_hitStopCoroutine = null;
                    }
                    RestoreTimeScale();
                }
            }
        }

        private void LateUpdate()
        {
            if (!m_kickActive) return;

            m_kickElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_kickElapsed / m_kickDuration);
            float decay = 1f - t * t; // quadratic ease-out

            // Kick is an offset on top of the controller's own pitch, so the
            // base view is never snapped to 0 and the controller's next Update
            // simply rewrites the pitch underneath our offset in LateUpdate.
            float basePitch = m_player != null ? m_player.Pitch : 0f;
            transform.localEulerAngles = new Vector3(
                basePitch + m_currentKickPitch * decay,
                0f,
                m_currentKickRoll * decay
            );

            if (t >= 1f)
            {
                // Kick finished: hand the pivot back to the plain look pose.
                m_kickActive = false;
                m_currentKickPitch = 0f;
                m_currentKickRoll = 0f;
                transform.localEulerAngles = new Vector3(basePitch, 0f, 0f);
            }
        }
    }
}
