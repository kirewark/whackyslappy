using System.Collections.Generic;
using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Forehand slap hand: visual mesh on the camera pivot, timer-driven swing,
    /// sphere-cast hit detection in FixedUpdate during the active phase.
    /// </summary>
    public class SlapHand : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float m_cooldown = 0.4f;
        [SerializeField] private float m_reach = 1.3f;
        [SerializeField] private float m_windUpDuration = 0.08f;
        [SerializeField] private float m_strikeDuration = 0.12f;
        [SerializeField] private float m_recoverDuration = 0.20f;
        [SerializeField] private float m_sweepRadius = 0.35f;
        [SerializeField] private float m_forceMultiplier = 1.5f;
        [SerializeField] private float m_maxImpulse = 30f;

        // -------------------------------------------------------------------------
        // Tuning: impulse direction (Plan B1)
        // -------------------------------------------------------------------------

        /// <summary>Upward bias applied to the slap impulse direction.
        /// Combined with handVelocity + forwardBias*forward + upBias*up, then normalised.</summary>
        [SerializeField] private float m_upBias = 0.4f;

        /// <summary>Forward bias applied to the slap impulse direction.
        /// The hand velocity is biased toward the camera's forward (transform.forward).</summary>
        [SerializeField] private float m_forwardBias = 1.0f;

        /// <summary>Minimum impulse magnitude clamped to the slap impulse.</summary>
        [SerializeField] private float m_minImpulse = 6f;

        // -------------------------------------------------------------------------
        // Debug: impulse vector (Plan B4)
        // -------------------------------------------------------------------------

        /// <summary>Timestamp of the last slap hit for DrawRay visualisation.</summary>
        private float m_lastHitTime = 0f;

        public WhackySlappy.Player.PlayerInputReader InputReader { get; set; }
        public WhackySlappy.Player.FirstPersonController Player { get; set; }

        private readonly Vector3 m_restPosition = new Vector3(0.35f, -0.3f, 0.5f);
        private readonly Vector3 m_windUpPosition = new Vector3(0.55f, -0.2f, 0.35f);

        private enum SwingPhase { Idle, Active }
        private SwingPhase m_phase;

        private float m_swingStartTime;
        private Vector3 m_prevWorldPos;
        private HashSet<Rigidbody> m_hitThisSwing;
        private Vector3 m_lastHitPoint;
        private Vector3 m_lastHitImpulse;

        // Cached query buffers: avoid per-physics-tick allocations.
        private readonly Dictionary<Collider, Vector3> m_hitPoints = new();
        private readonly RaycastHit[] m_sphereCastBuffer = new RaycastHit[32];
        private readonly Collider[] m_overlapBuffer = new Collider[32];

        private void Awake()
        {
            // Build visual: a Cube primitive child scaled to hand dimensions.
            var handVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handVisual.transform.SetParent(transform, false);
            handVisual.transform.localPosition = m_restPosition;
            handVisual.layer = GameLayers.PlayerHand;
            var scale = handVisual.transform.localScale;
            scale.Set(0.12f, 0.04f, 0.18f);
            handVisual.transform.localScale = scale;
            Destroy(handVisual.GetComponent<Collider>());

            // Kinematic Rigidbody on the root.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            m_prevWorldPos = transform.position;
            m_hitThisSwing = new HashSet<Rigidbody>();
        }

        private void FixedUpdate()
        {
            var cur = transform.position;
            var delta = cur - m_prevWorldPos;
            var dist = delta.magnitude;

            if (dist > 0.0001f && m_phase == SwingPhase.Active)
            {
                var direction = delta / dist;

                // 1) SphereCast sweep: use actual hit point if non-zero
                // (NonAlloc into cached buffers, reused dictionary).
                m_hitPoints.Clear();
                int castCount = Physics.SphereCastNonAlloc(
                    m_prevWorldPos, m_sweepRadius, direction, m_sphereCastBuffer,
                    dist, GameLayers.MaskNPC, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < castCount; i++)
                {
                    var h = m_sphereCastBuffer[i];
                    if (h.collider != null && h.point.sqrMagnitude > 0.0001f)
                        m_hitPoints[h.collider] = h.point;
                }

                // 2) OverlapSphere at current position for start-overlap
                int overlapCount = Physics.OverlapSphereNonAlloc(
                    cur, m_sweepRadius, m_overlapBuffer, GameLayers.MaskNPC);
                for (int i = 0; i < overlapCount; i++)
                {
                    var col = m_overlapBuffer[i];
                    if (col != null && !m_hitPoints.ContainsKey(col))
                        m_hitPoints[col] = cur;
                }

                foreach (var kvp in m_hitPoints)
                {
                    var rb = kvp.Key.attachedRigidbody;
                    if (rb == null || !m_hitThisSwing.Add(rb))
                        continue;

                    var handVelocity = delta / Time.fixedDeltaTime;
                    var magnitude = handVelocity.magnitude;

                    // Strength (0..1) derived from raw impulse magnitude,
                    // computed before it is used in the debug log below.
                    float rawMagnitude = magnitude * m_forceMultiplier;
                    float strength = Mathf.Clamp01((rawMagnitude - m_minImpulse) / (m_maxImpulse - m_minImpulse));

                    Vector3 impulse = Vector3.zero;
                    if (magnitude > 0.0001f)
                    {
                        // Plan B1: full impulse direction formula
                        Vector3 dir =
                            (handVelocity.normalized
                             + m_forwardBias * transform.forward
                             + m_upBias * transform.up).normalized;
                        float impulseMag = Mathf.Clamp(magnitude * m_forceMultiplier, m_minImpulse, m_maxImpulse);
                        impulse = dir * impulseMag;

                        // Plan B3: log hand speed and impulse for tuning.
                        Debug.Log(
                            $"Slap: handSpeed={magnitude:F1} m/s, impulseMag={impulseMag:F1}, "
                            + $"strength={strength:F2}");
                    }

                    rb.AddForceAtPosition(impulse, kvp.Value, ForceMode.Impulse);
                    var slappable = kvp.Key.GetComponent<ISlappable>();
                    slappable?.OnSlapped(impulse, kvp.Value);

                    m_lastHitPoint = kvp.Value;
                    m_lastHitImpulse = impulse;
                    m_lastHitTime = Time.time;

                    // Feed impact strength into audio and visual feedback.
                    // Audio strength is floored at 0.25 so the softest hits
                    // are not silent; HitFeedback floors strength itself.
                    SlapAudio.Play(kvp.Value, Mathf.Max(0.25f, strength));
                    HitFeedback.Trigger(strength);
                }
            }

            // Plan B4: draw impulse vector for one second after each hit.
            if (m_lastHitImpulse.sqrMagnitude > 0.0001f
                && Time.time - m_lastHitTime < 1f)
            {
                Debug.DrawRay(m_lastHitPoint, m_lastHitImpulse, Color.red, 1f);
            }

            m_prevWorldPos = cur;
        }

        private void Update()
        {
            if (m_phase == SwingPhase.Idle && InputReader != null && InputReader.AttackPressedThisFrame &&
                Time.time - m_swingStartTime >= m_cooldown)
            {
                StartSwing();
                return;
            }

            if (m_phase != SwingPhase.Active)
                return;

            float elapsed = Time.time - m_swingStartTime;
            float strikeEndZ = 0.5f + m_reach * 0.6f;
            Vector3 strikeEndPos = new Vector3(-0.35f, -0.2f, strikeEndZ);
            float t;

            if (elapsed < m_windUpDuration)
            {
                // Wind-up phase: rest -> wind-up
                t = elapsed / m_windUpDuration;
                transform.localPosition = Vector3.Lerp(m_restPosition, m_windUpPosition, t);
            }
            else if (elapsed < m_windUpDuration + m_strikeDuration)
            {
                // Active phase: wind-up -> strike end
                t = (elapsed - m_windUpDuration) / m_strikeDuration;
                transform.localPosition = Vector3.Lerp(m_windUpPosition, strikeEndPos, t);
            }
            else
            {
                // Recover phase: strike end -> rest
                float recoverElapsed = elapsed - m_windUpDuration - m_strikeDuration;
                t = Mathf.Clamp01(recoverElapsed / m_recoverDuration);
                transform.localPosition = Vector3.Lerp(strikeEndPos, m_restPosition, t);

                if (elapsed >= m_windUpDuration + m_strikeDuration + m_recoverDuration)
                {
                    m_phase = SwingPhase.Idle;
                    m_hitThisSwing.Clear();
                }
            }
        }

        private void StartSwing()
        {
            m_swingStartTime = Time.time;
            m_prevWorldPos = transform.position;
            m_phase = SwingPhase.Active;
            m_hitThisSwing.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_sweepRadius);
            if (m_lastHitImpulse.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(m_lastHitPoint, m_lastHitPoint + m_lastHitImpulse * 0.1f);
            }
        }
    }
}
