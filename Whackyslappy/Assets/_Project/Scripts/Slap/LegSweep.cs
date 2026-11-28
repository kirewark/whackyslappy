using System.Collections.Generic;
using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Leg sweep on right mouse: a short low horizontal arc at ankle height
    /// that trips standing NPCs. Mirrors SlapHand's structure: detection runs
    /// in FixedUpdate with SphereCastNonAlloc / OverlapSphereNonAlloc on the
    /// NPC layer, each NPC is hit once per sweep, and every hit builds a
    /// SlapHitInfo with Kind = Sweep and passes it to ISlappable.OnHit.
    /// The sweep itself does NOT apply forces — the NPC's sweep handler
    /// applies the impulse at its feet (see NpcController).
    /// </summary>
    public class LegSweep : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float m_cooldown = 0.6f;      // s between sweeps
        [SerializeField] private float m_duration = 0.25f;     // s of the arc
        [SerializeField] private float m_arcStartDeg = -60f;   // arc start, degrees off player yaw
        [SerializeField] private float m_arcEndDeg = 60f;      // arc end, degrees off player yaw
        [SerializeField] private float m_ankleHeight = 0.3f;   // cast height above the feet
        [SerializeField] private float m_radius = 0.4f;        // sweep sphere radius
        [SerializeField] private float m_range = 1.6f;         // arc cast distance
        [SerializeField] private float m_sweepImpulse = 9f;    // tangential impulse to the feet (knockdown-strong)
        [SerializeField] private float m_upBias = 1.0f;        // small upward impulse component
        [SerializeField] private float m_strength = 0.7f;      // feedback scale for a sweep hit
        [SerializeField] private float m_legUpTilt = 0.12f;    // sweep leg tilt-up (added to level dir)

        /// <summary>Set by SlapBootstrap after AddComponent, read in Update.</summary>
        public WhackySlappy.Player.PlayerInputReader InputReader { get; set; }

        private enum SweepPhase { Idle, Active }

        private SweepPhase m_phase = SweepPhase.Idle;
        private float m_sweepStartTime = -1000f;
        private Vector3 m_anchor;         // ankle-height world point above the player
        private Vector3 m_flatForward;    // player yaw direction, captured at sweep start
        private readonly HashSet<Rigidbody> m_hitThisSweep = new HashSet<Rigidbody>();
        private readonly RaycastHit[] m_castBuffer = new RaycastHit[32];
        private readonly Collider[] m_overlapBuffer = new Collider[32];
        private CharacterController m_playerCC;
        private Transform m_playerTf;
        private GameObject m_legVisual;
        private WhackySlappy.Player.PlayerBody m_body;
        private bool m_usesNpcLeg;
        private float m_lastSweepHitTime = -1f;
        private Vector3 m_lastSweepPoint;
        private Vector3 m_lastSweepImpulse;

        private void Awake()
        {
            // This transform is a child of the camera rig under the player,
            // so the parent chain reaches the CharacterController.
            m_playerCC = GetComponentInParent<CharacterController>();
            m_playerTf = m_playerCC != null ? m_playerCC.transform : transform;
            m_body = GetComponentInParent<WhackySlappy.Player.PlayerBody>();
            BuildLegVisual();
            if (m_legVisual != null)
                m_legVisual.SetActive(false);
        }

        /// <summary>Resets the sweep so a re-enable starts clean, and
        /// hides the leg visual.</summary>
        private void OnDisable()
        {
            m_phase = SweepPhase.Idle;
            m_sweepStartTime = -1000f;
            m_hitThisSweep.Clear();
            if (m_body != null)
                m_body.SetRightLegVisible(true);
            if (m_legVisual != null)
                m_legVisual.SetActive(false);
        }

        private void Update()
        {
            DrawLastHit();

            if (m_phase == SweepPhase.Idle)
            {
                if (InputReader != null && InputReader.SweepPressed &&
                    Time.time - m_sweepStartTime >= m_cooldown)
                {
                    StartSweep();
                }
                return;
            }

            float t = (Time.time - m_sweepStartTime) / m_duration;
            if (t >= 1f)
            {
                m_phase = SweepPhase.Idle;
                if (m_body != null)
                    m_body.SetRightLegVisible(true);
                if (m_legVisual != null)
                    m_legVisual.SetActive(false);
                return;
            }
            MoveLegVisual(t);
        }

        private void FixedUpdate()
        {
            if (m_phase != SweepPhase.Active)
                return;

            float t = Mathf.Clamp01((Time.time - m_sweepStartTime) / m_duration);
            float angle = Mathf.SmoothStep(m_arcStartDeg, m_arcEndDeg, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * m_flatForward;

            // Overlap first: catches NPCs already inside the sphere at the
            // anchor (SphereCast ignores starting overlaps for some shapes).
            int overlapCount = Physics.OverlapSphereNonAlloc(
                m_anchor, m_radius, m_overlapBuffer,
                GameLayers.MaskNPC, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = m_overlapBuffer[i];
                if (col == null)
                    continue;
                ProcessHit(col, col.ClosestPoint(m_anchor), dir);
            }

            // Radial sweep along the current arc angle out to m_range.
            // Small angle steps per tick plus the 0.4 m sphere radius give
            // continuous tangential coverage across the whole pie slice.
            int hitCount = Physics.SphereCastNonAlloc(
                m_anchor, m_radius, dir, m_castBuffer, m_range,
                GameLayers.MaskNPC, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var hitInfo = m_castBuffer[i];
                if (hitInfo.collider == null)
                    continue;
                ProcessHit(hitInfo.collider, hitInfo.point, dir);
            }
        }

        /// <summary>
        /// Starts a sweep: capture player yaw and the ankle-height anchor,
        /// clear the per-sweep hit set, and show the leg visual.
        /// </summary>
        private void StartSweep()
        {
            Vector3 fwd = m_playerTf.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.forward;
            }
            fwd.Normalize();

            float feetY;
            if (m_playerCC != null)
                feetY = m_playerTf.position.y + m_playerCC.center.y - m_playerCC.height * 0.5f;
            else
                feetY = m_playerTf.position.y - 1.6f;
            m_anchor = new Vector3(m_playerTf.position.x, feetY + m_ankleHeight, m_playerTf.position.z);

            m_flatForward = fwd;
            m_sweepStartTime = Time.time;
            SlapAudio.Play(SlapAudio.SoundKind.Whoosh, transform.position, 0.8f);
            m_phase = SweepPhase.Active;
            m_hitThisSweep.Clear();
            if (m_legVisual != null)
            {
                m_legVisual.SetActive(true);
                if (m_body != null)
                    m_body.SetRightLegVisible(false);
                MoveLegVisual(0f);
            }
        }

        /// <summary>
        /// One hit per Rigidbody per sweep (same dedupe style as SlapHand).
        /// Impulse is the arc tangent at the hit point plus a small up bias;
        /// the NPC's own sweep handler applies it at the feet.
        /// </summary>
        private void ProcessHit(Collider col, Vector3 point, Vector3 fallbackDir)
        {
            Rigidbody rb = col.attachedRigidbody;
            rb = ResolveNpcRoot(col, rb);
            if (rb == null)
                return;
            if (!m_hitThisSweep.Add(rb))
                return;

            // NPC colliders are child GameObjects (body capsule, head
            // sphere); the ISlappable sits on the Rigidbody's root GO.
            // Resolve from the rb, same lookup style as SlapHand.
            ISlappable slappable = rb.GetComponent<ISlappable>();
            if (slappable == null)
                return;

            Vector3 radial = point - m_anchor;
            radial.y = 0f;
            Vector3 tangent = radial.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, radial.normalized)
                : fallbackDir;

            Vector3 impulse = tangent * m_sweepImpulse + Vector3.up * m_upBias;
            var hitInfo = new SlapHitInfo(point, impulse, SlapKind.Sweep, m_strength);
            slappable.OnHit(in hitInfo);

            m_lastSweepPoint = point;
            m_lastSweepImpulse = impulse;
            m_lastSweepHitTime = Time.time;

            SlapAudio.Play(point, m_strength);
            SlapAudio.Play(SlapAudio.SoundKind.Boing, point, m_strength);
            HitFeedback.Trigger(m_strength * 0.7f);
        }

        /// <summary>
        /// Places the sweep leg on the arc at the current sweep angle.
        /// Set in world space so camera pitch on the parent rig does not tilt it.
        /// </summary>
        private void MoveLegVisual(float t)
        {
            if (m_legVisual == null)
                return;
            float angle = Mathf.SmoothStep(m_arcStartDeg, m_arcEndDeg, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * m_flatForward;
            if (m_usesNpcLeg)
            {
                Vector3 aim = (dir + Vector3.up * m_legUpTilt).normalized;
                m_legVisual.transform.SetPositionAndRotation(
                    m_anchor, Quaternion.FromToRotation(Vector3.down, aim));
                return;
            }
            Vector3 center = m_anchor + dir * (m_range * 0.5f);
            m_legVisual.transform.SetPositionAndRotation(center, Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>
        /// Builds the sweep visual: PlayerBody's real NPC-style leg when
        /// present, the old flat capsule as fallback otherwise. No collider
        /// on it: detection is the casts, as before.
        /// </summary>
        private void BuildLegVisual()
        {
            if (m_body != null)
            {
                m_legVisual = m_body.CreateSweepLeg();
                m_legVisual.transform.SetParent(m_playerTf, false);
                m_usesNpcLeg = true;
                return;
            }
            var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leg.name = "LegSweepVisual";
            Object.Destroy(leg.GetComponent<Collider>());

            var mat = DefaultLitMaterial.Create();
            if (mat != null)
            {
                mat.color = new Color(0.55f, 0.35f, 0.2f);
                leg.GetComponent<Renderer>().material = mat;
            }

            leg.transform.SetParent(transform, false);
            leg.transform.localScale = new Vector3(0.3f, 0.08f, 1.0f);
            leg.layer = GameLayers.PlayerHand;
            m_legVisual = leg;
        }

        private static Rigidbody ResolveNpcRoot(Collider col, Rigidbody rb)
        {
            var slappable = col.GetComponentInParent<ISlappable>();
            var comp = slappable as Component;
            if (comp != null)
            {
                var rootRb = comp.GetComponent<Rigidbody>();
                if (rootRb != null) return rootRb;
            }
            return rb;
        }

        private void DrawLastHit()
        {
            float age = Time.time - m_lastSweepHitTime;
            if (age < 1f && m_lastSweepImpulse != Vector3.zero)
                Debug.DrawRay(m_lastSweepPoint, m_lastSweepImpulse.normalized * 1.0f, Color.red, 1f - age);
        }
    }
}
