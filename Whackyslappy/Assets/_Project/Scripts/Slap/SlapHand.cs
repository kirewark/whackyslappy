using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private float m_reach = 0.8f;
        [SerializeField] private float m_windUpDuration = 0.08f;
        [SerializeField] private float m_strikeDuration = 0.12f;
        [SerializeField] private float m_recoverDuration = 0.20f;
        [SerializeField] private float m_sweepRadius = 0.2f;
        [SerializeField] private float m_forceMultiplier = 1.5f;
        [SerializeField] private float m_maxImpulse = 30f;
        [SerializeField] private float m_upBias = 0.4f;

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

                // 1) SphereCastAll: use actual hit point if non-zero
                var hitPoints = new Dictionary<Collider, Vector3>();
                foreach (var h in Physics.SphereCastAll(m_prevWorldPos, m_sweepRadius, direction, dist, GameLayers.MaskNPC, QueryTriggerInteraction.Ignore))
                {
                    if (h.point.sqrMagnitude > 0.0001f)
                        hitPoints[h.collider] = h.point;
                }

                // 2) OverlapSphere at current position for start-overlap
                foreach (var col in Physics.OverlapSphere(cur, m_sweepRadius, GameLayers.MaskNPC))
                {
                    if (!hitPoints.ContainsKey(col))
                        hitPoints[col] = cur;
                }

                foreach (var kvp in hitPoints)
                {
                    var rb = kvp.Key.attachedRigidbody;
                    if (rb == null || !m_hitThisSwing.Add(kvp.Key))
                        continue;

                    var handVelocity = delta / Time.fixedDeltaTime;
                    var impulse = handVelocity * m_forceMultiplier;
                    var magnitude = impulse.magnitude;
                    if (magnitude > 0.0001f)
                        impulse = impulse / magnitude * Mathf.Clamp(magnitude, 6f, m_maxImpulse);
                    impulse += Vector3.up * m_upBias;

                    rb.AddForceAtPosition(impulse, kvp.Value, ForceMode.Impulse);
                    var slappable = kvp.Key.GetComponent<ISlappable>();
                    slappable?.OnSlapped(impulse, kvp.Value);
                    m_lastHitPoint = kvp.Value;
                    m_lastHitImpulse = impulse;
                }
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
