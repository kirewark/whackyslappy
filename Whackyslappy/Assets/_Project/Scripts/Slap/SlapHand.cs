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
        // Tuning: directional slap (Pass 2, Phase 2)
        // -------------------------------------------------------------------------

        /// <summary>Drag below this many accumulated mouse-delta pixels
        /// counts as a plain click and does the default swing.</summary>
        [SerializeField] private float m_minDragPixels = 25f;

        /// <summary>Distance (m, camera space) the hand starts offset opposite
        /// the drag direction before sweeping through the rest point.</summary>
        [SerializeField] private float m_aimStartOffset = 0.5f;

        /// <summary>Weight of the drag direction added to the impulse
        /// direction of a directional swing.</summary>
        [SerializeField] private float m_directionalBias = 0.8f;

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

        // Directional-slap state (reset when a swing starts).
        private bool m_aiming;                      // LMB held, accumulating drag
        private bool m_directional;                 // current swing is a directional one
        private Vector3 m_aimStartLocal;            // wind-up target, camera-local
        private Vector3 m_dirStrikeLocal;           // strike-end target, camera-local
        private Vector2 m_dirAimNorm;               // normalized drag (x=right, y=up, camera space)

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

                    // Silly physics (Pass 2, Phase 4): scale the computed
                    // impulse by the silliness multiplier before both the
                    // applied impulse and the strength normalisation below.
                    // Strength stays clamped to 0..1 (it just saturates more
                    // often when silly). ImpulseMultiplier is 1 at
                    // Silliness = 0, keeping the old numbers exactly.
                    float rawMagnitude =
                        magnitude * m_forceMultiplier * SillyPhysics.ImpulseMultiplier;
                    float strength = Mathf.Clamp01((rawMagnitude - m_minImpulse) / (m_maxImpulse - m_minImpulse));

                    Vector3 impulse = Vector3.zero;
                    if (magnitude > 0.0001f)
                    {
                        // Plan B1: full impulse direction formula
                        Vector3 dir =
                            handVelocity.normalized
                            + m_forwardBias * transform.forward
                            + m_upBias * transform.up;

                        // Directional slap: the drag direction pushes the
                        // target toward where the player dragged (right =
                        // backhand, up = uppercut, down = smash).
                        if (m_directional)
                            dir += m_directionalBias *
                                (transform.right * m_dirAimNorm.x + transform.up * m_dirAimNorm.y);

                        dir = dir.normalized;
                        float impulseMag = Mathf.Clamp(
                            rawMagnitude, m_minImpulse, m_maxImpulse);
                        impulse = dir * impulseMag;

                        // Plan B3: log hand speed and impulse for tuning.
                        Debug.Log(
                            $"Slap: handSpeed={magnitude:F1} m/s, impulseMag={impulseMag:F1}, "
                            + $"strength={strength:F2}");
                    }

                    rb.AddForceAtPosition(impulse, kvp.Value, ForceMode.Impulse);
                    var slappable = kvp.Key.GetComponent<ISlappable>();
                    if (slappable != null)
                    {
                        var hit = new SlapHitInfo(kvp.Value, impulse, SlapKind.Slap, strength);
                        slappable.OnHit(in hit);
                    }

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
            // Press starts an aim hold instead of an instant swing. Release
            // decides: a tiny drag swings exactly like a plain click did
            // before; a real drag swings along the drag direction.
            if (m_phase == SwingPhase.Idle && InputReader != null &&
                Time.time - m_swingStartTime >= m_cooldown)
            {
                if (InputReader.SlapPressed && !m_aiming)
                {
                    m_aiming = true;
                    InputReader.ResetAim();
                    if (Player != null)
                        Player.LookSuppressed = true;
                }

                if (m_aiming && InputReader.SlapReleased)
                {
                    m_aiming = false;
                    if (Player != null)
                        Player.LookSuppressed = false;

                    Vector2 aim = InputReader.ConsumeAim();
                    if (aim.magnitude < m_minDragPixels)
                        StartSwing();
                    else
                        StartDirectionalSwing(aim);
                    return;
                }
            }

            if (m_phase != SwingPhase.Active)
                return;

            float elapsed = Time.time - m_swingStartTime;
            float strikeEndZ = 0.5f + m_reach * 0.6f;
            Vector3 windUpPos = m_directional ? m_aimStartLocal : m_windUpPosition;
            Vector3 strikeEndPos = m_directional
                ? m_dirStrikeLocal
                : new Vector3(-0.35f, -0.2f, strikeEndZ);
            float t;

            if (elapsed < m_windUpDuration)
            {
                // Wind-up phase: rest -> wind-up (or aim-start for a drag swing)
                t = elapsed / m_windUpDuration;
                transform.localPosition = Vector3.Lerp(m_restPosition, windUpPos, t);
            }
            else if (elapsed < m_windUpDuration + m_strikeDuration)
            {
                // Active phase: wind-up -> strike end
                t = (elapsed - m_windUpDuration) / m_strikeDuration;
                transform.localPosition = Vector3.Lerp(windUpPos, strikeEndPos, t);
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
                    m_directional = false;
                    m_hitThisSwing.Clear();
                }
            }
        }

        private void StartSwing()
        {
            m_swingStartTime = Time.time;
            m_prevWorldPos = transform.position;
            m_phase = SwingPhase.Active;
            m_directional = false;
            m_hitThisSwing.Clear();
        }

        /// <summary>
        /// Starts a swing along the drag direction: the wind-up pose sits
        /// opposite the drag in the camera right/up plane, and the strike
        /// sweeps through the rest point out along the drag direction.
        /// </summary>
        private void StartDirectionalSwing(Vector2 aim)
        {
            float angle = Mathf.Atan2(aim.y, aim.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Path direction in the camera right/up plane (hand local x/y).
            Vector3 pathDir = new Vector3(cos, sin, 0f);

            m_aimStartLocal = m_restPosition - pathDir * m_aimStartOffset;

            // Strike end: same plane reach as the default swing's forward
            // extension, plus the same forward z so the arc stays in front.
            float strikeEndZ = 0.5f + m_reach * 0.6f;
            float planeRadius = m_reach * 0.6f;
            m_dirStrikeLocal = new Vector3(
                m_restPosition.x + pathDir.x * planeRadius,
                m_restPosition.y + pathDir.y * planeRadius,
                strikeEndZ);
            m_dirAimNorm = aim / aim.magnitude;

            StartSwing();
            m_directional = true;
        }

        private void OnDisable()
        {
            // Never leave the camera stuck with look suppressed.
            m_aiming = false;
            if (Player != null)
                Player.LookSuppressed = false;
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
