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
        [SerializeField] private bool m_debugLog = false;
        [SerializeField] private float m_cooldown = 0.4f;
        [SerializeField] private float m_reach = 1.95f;
        [SerializeField] private float m_windUpDuration = 0.08f;
        [SerializeField] private float m_strikeDuration = 0.12f;
        [SerializeField] private float m_recoverDuration = 0.20f;
        [SerializeField] private float m_sweepRadius = 0.45f;
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
        [SerializeField] private float m_minDragPixels = 10f;

        /// <summary>Pull-back distance (m, camera space) used by the
        /// m_strikeAlongDrag wind-up. The default slingshot preview uses
        /// the elliptical m_aimOffsetExtent fields below instead.</summary>
        [SerializeField] private float m_aimStartOffset = 0.9f;

        /// <summary>Weight of the strike direction added to the impulse
        /// direction of a directional swing.</summary>
        [SerializeField] private float m_directionalBias = 0.8f;

        /// <summary>When true, the strike travels along the drag direction
        /// (and its wind-up sits opposite the drag, through the rest point).
        /// When false (default), slingshot: the pull-back is the wind-up and
        /// the release strikes back through the rest point to the opposite
        /// side.</summary>
        [SerializeField] private bool m_strikeAlongDrag = false;

        /// <summary>Exponential smoothing rate (1/s, unscaled time) for the
        /// live aim preview and the rotation reset after a swing.</summary>
        [SerializeField] private float m_aimResponse = 14f;

        /// <summary>Max yaw (deg, about camera up) the hand turns toward the
        /// pulled side while aiming, at full aim strength.</summary>
        [SerializeField] private float m_aimYawDeg = 55f;

        /// <summary>Max pitch (deg, about camera right) the hand tilts while
        /// aiming at full aim strength. Dragging up lifts the fingertips
        /// up, dragging down tips them down (drag-up = up rule).</summary>
        [SerializeField] private float m_aimPitchDeg = 45f;

        /// <summary>Clamp (deg, both signs) for the mirror-symmetric aim
        /// roll about camera forward. Replaces the old hard +-70.</summary>
                [SerializeField] private float m_aimRollDeg = 70f;

        /// <summary>Drag distance (accumulated pixels) that saturates the
        /// aim: the aim factor k reaches 1 (full deflection) here. The old
        /// hard 60 px saturated too early and felt like a narrow cone.</summary>
        [SerializeField] private float m_aimFullDragPixels = 160f;

        /// <summary>Elliptical aim-preview half-axes (m, camera right/up):
        /// horizontal reach up to X, vertical up to Y, so the wider drag
        /// keeps the hand inside the view at max deflection.</summary>
        [SerializeField] private float m_aimOffsetExtentX = 1.0f;
        [SerializeField] private float m_aimOffsetExtentY = 0.75f;

        /// <summary>Frustum containment caps for the aim preview: the hand
        /// pivot is clamped to this factor times its own z (0.95 x the
        /// half-width 1.03 per z = 0.9785, 0.8 x the half-height 0.577 per
        /// z = 0.4616, measured at rest z = 1.26 with half-extents 1.3 / 0.73).</summary>
        [SerializeField] private float m_frustumClampXFactor = 0.9785f;
        [SerializeField] private float m_frustumClampYFactor = 0.4616f;

        /// <summary>Strike-arc plane radius as a fraction of m_reach, split
        /// X/Y so the wider horizontal arc stays visually plausible.</summary>
        [SerializeField] private float m_strikeArcReachFactorX = 0.9f;
        [SerializeField] private float m_strikeArcReachFactorY = 0.75f;

        /// <summary>Amplitude (deg) of the small wrist wobble added to the
        /// aim roll while aiming, so the held hand feels alive.</summary>
        [SerializeField] private float m_aimWobbleDeg = 1.2f;

        /// <summary>Frequency (rad/s, unscaled time) of the wrist wobble.</summary>
        [SerializeField] private float m_aimWobbleFreq = 18f;

        // -------------------------------------------------------------------------
        // Tuning: drag-contact strikes (hand touches an NPC while aiming)
        // -------------------------------------------------------------------------

        /// <summary>Drag sweeps use a smaller sphere than a swing
        /// (m_sweepRadius * this) so only a real contact registers.</summary>
        [SerializeField] private float m_dragHitRadiusScale = 0.8f;

        /// <summary>Impulse per m/s of hand world speed during a drag hit
        /// (handSpeed * this, then clamped between m_min/m_maxImpulse).</summary>
        [SerializeField] private float m_dragImpulsePerSpeed = 1.6f;

        /// <summary>Hand world speed (m/s) below which a drag contact does
        /// nothing, so a resting hand brushing an NPC never hits it.</summary>
        [SerializeField] private float m_dragMinHitSpeed = 1.2f;

        /// <summary>Seconds an NPC must wait before a drag can hit it
        /// again inside the same hold.</summary>
        [SerializeField] private float m_dragRehitCooldown = 0.35f;

        /// <summary>Euler angles (deg) applied to the hand model root so the
        /// Inspector can fix the model's facing if the rest pose looks wrong.
        /// The model is now a RIGHT hand (thumb -X, palm normal -Y, fingers
        /// +Z). Default (-20, -15, -90): Unity applies the Z roll first,
        /// then the X pitch, then the Y yaw. Roll -90 maps the palm normal
        /// -Y to -X (palm faces the screen centre, inward since the hand
        /// sits at the lower right) and the thumb -X to +Y (thumb up).
        /// Pitch -20 tilts model +Z toward +Y, so the fingers reach forward
        /// and 20 degrees up. Yaw -15 then turns the fingers 15 degrees
        /// toward the screen centre.</summary>
        [SerializeField] private Vector3 m_handModelEuler =
            new Vector3(-20f, -15f, -90f);

        /// <summary>Local scale of the HandModelPivot on a 1.0-uniform cube
        /// anchor. The model now carries true cartoon-glove proportions
        /// (~0.13 x 0.078 x 0.21 m at scale 1), so a UNIFORM 3 keeps the
        /// finger capsules and cuff round instead of squashing them into
        /// a fat tube: about 0.39 x 0.23 x 0.6 m in camera space (model Z
        /// stays forward; the Rz(-90) roll maps model X to camera Y and
        /// model Y to camera X). Replaces the old anisotropic
        /// (4.5, 2.5, 1.4) that caused the tube look.</summary>
        [SerializeField] private Vector3 m_handModelScale =
            new Vector3(3.2f, 3.2f, 3.2f);

        // -------------------------------------------------------------------------
        // Debug: impulse vector (Plan B4)
        // -------------------------------------------------------------------------

        /// <summary>Timestamp of the last slap hit for DrawRay visualisation.</summary>
        private float m_lastHitTime = 0f;

        public WhackySlappy.Player.PlayerInputReader InputReader { get; set; }
        public WhackySlappy.Player.FirstPersonController Player { get; set; }

        // Rest anchor (wrist pivot) in camera space: with the model's wrist
        // at local z = 0 and uniform scale 3.2, the preview-measured glove
        // box is x ~0.10..0.85 (crosshair stays clear), y -0.60..-0.23
        // (finger tips well below the screen-centre horizon), z ~0.46..1.50
        // (sleeve back to middle fingertip): the ~60 deg vertical FOV gives
        // half-height 0.32 at the near end, and the hand sits fully in the
        // lower-right quadrant with the wrist running off the bottom edge.
        // zmin ~0.46 keeps everything clear of the 0.4 near clip. The cube
        // anchor sits at local zero, so this is the single rest offset the
        // aim/swing code lerps the root to (no double offset).
        private readonly Vector3 m_restPosition = new Vector3(0.50f, -0.62f, 1.26f);
        private readonly Vector3 m_windUpPosition = new Vector3(0.55f, -0.35f, 0.7f);

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
        private Vector2 m_dirAimNorm;               // strike direction (x=right, y=up, camera space)
        private float m_swingRollDeg;               // hand roll held during a directional swing: top edge leans toward the pull
        private Vector3 m_swingFromLocal;           // hand local pose captured when a swing starts
        private Quaternion m_swingFromRot = Quaternion.identity;  // root rotation when a swing starts
        private Quaternion m_aimPose = Quaternion.identity;       // yaw*pitch*roll aimed pose held through wind-up
        private Quaternion m_followPose = Quaternion.identity;    // mirrored follow-through pose at strike end

        // Cached query buffers: avoid per-physics-tick allocations.
        private readonly Dictionary<Collider, Vector3> m_hitPoints = new();
        private readonly RaycastHit[] m_sphereCastBuffer = new RaycastHit[32];
        private readonly Collider[] m_overlapBuffer = new Collider[32];

        // Drag-contact dedupe/cooldown: separate from the release-swing
        // dedupe (m_hitThisSwing) so the release strike can still hit an
        // NPC the drag already struck. Reused containers, cleared on press.
        private readonly HashSet<Rigidbody> m_hitThisDrag = new();
        private readonly Dictionary<Rigidbody, float> m_dragRehitAt = new();

        private void Awake()
        {
            // Build visual: a Cube child kept as the hand anchor, but now
            // at local zero — the SlapHand root itself carries the rest
            // offset (set at the end of Awake), so aim/swing lerps never
            // double-count it. Its cube renderer stays disabled and its
            // collider destroyed; it only exists to carry the model pivot.
            var handVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handVisual.transform.SetParent(transform, false);
            handVisual.transform.localPosition = Vector3.zero;
            handVisual.layer = GameLayers.PlayerHand;
            var scale = handVisual.transform.localScale;
            // Uniform 1.0 anchor: the old 0.12/0.04/0.18 factors
            // multiplied into the model chain (times the old uniform 6)
            // squashed its width to 0.05 m, a pencil-like sliver that
            // looked tiny end-on. The anchor carries no renderer or
            // collider, so it is purely the model chain's parent here.
            scale.Set(1f, 1f, 1f);
            handVisual.transform.localScale = scale;
            Destroy(handVisual.GetComponent<Collider>());
            handVisual.GetComponent<MeshRenderer>().enabled = false;

            // Pivot under the uniform-scale anchor; m_handModelScale is a
            // per-axis vector (thickness, upright width, forward length in
            // camera-aligned axes) so the glove is wide and chunky, not a
            // foreshortened sliver. The sweep uses casts only, so no
            // collider is needed on the model.
            var handModelPivot = new GameObject("HandModelPivot");
            handModelPivot.transform.SetParent(handVisual.transform, false);
            handModelPivot.transform.localScale = m_handModelScale;
            var handModel = HandModelBuilder.Build(handModelPivot.transform);
            // Default m_handModelEuler holds the hand as an upright
            // fingers-forward blade: fingers forward (+Z, tilted ~8 deg
            // up), palm facing the screen centre, thumb up; tweak in the
            // Inspector if the pose looks wrong.
            handModel.localEulerAngles = m_handModelEuler;
            foreach (var t in handModel.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = GameLayers.PlayerHand;

            // Kinematic Rigidbody on the root.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // The root carries the rest offset (the anchor child sits at
            // local zero), matching what OnDisable and the swing-recover
            // end already assume.
            transform.localPosition = m_restPosition;
            transform.localRotation = Quaternion.identity;

            m_prevWorldPos = transform.position;
            m_hitThisSwing = new HashSet<Rigidbody>();

            // The default 0 would block the first swing for m_cooldown
            // seconds after boot (Time.time - 0 < m_cooldown early on),
            // so pretend the last swing ended long ago.
            m_swingStartTime = -m_cooldown;
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
                    rb = ResolveNpcRoot(kvp.Key, rb);
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

                        // Directional slap: the strike direction pushes the
                        // target the way the hand slaps (slingshot mode:
                        // opposite the mouse pull).
                        if (m_directional)
                            dir += m_directionalBias *
                                (transform.right * m_dirAimNorm.x + transform.up * m_dirAimNorm.y);

                        dir = dir.normalized;
                        float impulseMag = Mathf.Clamp(
                            rawMagnitude, m_minImpulse, m_maxImpulse);
                        impulse = dir * impulseMag;

                        // Plan B3: log hand speed and impulse for tuning.
                        if (m_debugLog)
                            Debug.Log(
                            $"Slap: handSpeed={magnitude:F1} m/s, impulseMag={impulseMag:F1}, "
                            + $"strength={strength:F2}");
                    }

                    rb.AddForceAtPosition(impulse, kvp.Value, ForceMode.Impulse);
                    // NPC colliders are child GameObjects; the ISlappable
                    // sits on the Rigidbody's root GO (same lookup as
                    // LegSweep).
                    var slappable = rb.GetComponent<ISlappable>();
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

            // Drag-contact strike: while LMB is held (aim preview) and the
            // hand sweeps through an NPC, make contact. Same gates as the
            // swing: no drag hits while a swing is Active or inside its
            // post-swing cooldown.
            if (m_aiming && m_phase == SwingPhase.Idle
                && dist > 0.0001f
                && Time.time - m_swingStartTime >= m_cooldown)
            {
                TryDragContactHit(cur, delta, dist);
            }

            // Plan B4: draw impulse vector for one second after each hit.
            if (m_debugLog
                && m_lastHitImpulse.sqrMagnitude > 0.0001f
                && Time.time - m_lastHitTime < 1f)
            {
                Debug.DrawRay(m_lastHitPoint, m_lastHitImpulse, Color.red, 1f);
            }

            m_prevWorldPos = cur;
        }

        /// <summary>
        /// Drag-contact strike: sphere-casts the hand's motion since the
        /// last physics tick while aiming and runs the same hit pipeline
        /// as a release strike, scaled by hand speed instead of the swing
        /// force multiplier.
        /// </summary>
        private void TryDragContactHit(Vector3 cur, Vector3 delta, float dist)
        {
            var handVelocity = delta / Time.fixedDeltaTime;
            float handSpeed = handVelocity.magnitude;
            if (handSpeed < m_dragMinHitSpeed)
                return;

            float now = Time.time;
            var sweepDir = delta / dist;
            float dragRadius = m_sweepRadius * m_dragHitRadiusScale;

            // Same two-query sweep as the swing path (buffers are safe to
            // reuse: this runs only when the swing block did not).
            m_hitPoints.Clear();
            int castCount = Physics.SphereCastNonAlloc(
                m_prevWorldPos, dragRadius, sweepDir, m_sphereCastBuffer,
                dist, GameLayers.MaskNPC, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < castCount; i++)
            {
                var h = m_sphereCastBuffer[i];
                if (h.collider != null && h.point.sqrMagnitude > 0.0001f)
                    m_hitPoints[h.collider] = h.point;
            }
            int overlapCount = Physics.OverlapSphereNonAlloc(
                cur, dragRadius, m_overlapBuffer, GameLayers.MaskNPC);
            for (int i = 0; i < overlapCount; i++)
            {
                var col = m_overlapBuffer[i];
                if (col != null && !m_hitPoints.ContainsKey(col))
                    m_hitPoints[col] = cur;
            }

            foreach (var kvp in m_hitPoints)
            {
                var rb = kvp.Key.attachedRigidbody;
                if (rb == null)
                    continue;
                rb = ResolveNpcRoot(kvp.Key, rb);

                // One strike per NPC per hold, plus a short re-hit gate so
                // a hand parked against an NPC but wiggling still works.
                if (m_hitThisDrag.Contains(rb))
                    continue;
                if (m_dragRehitAt.TryGetValue(rb, out float nextAllowed)
                    && now < nextAllowed)
                    continue;
                m_hitThisDrag.Add(rb);
                m_dragRehitAt[rb] = now + m_dragRehitCooldown;

                float rawMagnitude =
                    handSpeed * m_dragImpulsePerSpeed * SillyPhysics.ImpulseMultiplier;
                float strength = Mathf.Clamp01(
                    (rawMagnitude - m_minImpulse) / (m_maxImpulse - m_minImpulse));
                strength = Mathf.Max(strength, 0.25f);

                Vector3 dir =
                    handVelocity.normalized
                    + m_forwardBias * transform.forward
                    + m_upBias * transform.up;
                if (m_directional)
                    dir += m_directionalBias *
                        (transform.right * m_dirAimNorm.x + transform.up * m_dirAimNorm.y);
                dir = dir.normalized;

                float impulseMag = Mathf.Clamp(
                    handSpeed * m_dragImpulsePerSpeed, m_minImpulse, m_maxImpulse)
                    * SillyPhysics.ImpulseMultiplier;
                Vector3 dragImpulse = dir * impulseMag;

                if (m_debugLog)
                    Debug.Log(
                        $"DragHit: handSpeed={handSpeed:F1} m/s, impulseMag={impulseMag:F1}, "
                        + $"strength={strength:F2}");

                rb.AddForceAtPosition(dragImpulse, kvp.Value, ForceMode.Impulse);
                var slappable = rb.GetComponent<ISlappable>();
                if (slappable != null)
                {
                    var hit = new SlapHitInfo(kvp.Value, dragImpulse, SlapKind.Slap, strength);
                    slappable.OnHit(in hit);
                }

                m_lastHitPoint = kvp.Value;
                m_lastHitImpulse = dragImpulse;
                m_lastHitTime = now;

                SlapAudio.Play(kvp.Value, strength);
                HitFeedback.Trigger(strength);
            }
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
                    // Fresh drag strike window: clear the drag dedupe and
                    // re-hit cooldown, and re-capture the hand position so
                    // the first drag tick has no phantom delta.
                    m_hitThisDrag.Clear();
                    m_dragRehitAt.Clear();
                    m_prevWorldPos = transform.position;
                    if (Player != null)
                        Player.LookSuppressed = true;
                }

                if (m_aiming && InputReader.SlapReleased)
                {
                    m_aiming = false;
                    // Aiming ended: re-capture the hand position so no
                    // stale drag delta leaks into the next physics tick.
                    m_prevWorldPos = transform.position;
                    if (Player != null)
                        Player.LookSuppressed = false;

                    Vector2 aim = InputReader.ConsumeAim();
                    if (aim.magnitude < m_minDragPixels)
                        StartSwing();
                    else
                        StartDirectionalSwing(aim);
                    return;
                }

                // Live aim preview: while the drag is real, the hand
                // follows the mouse pull (left = left, up = up) and rolls
                // so its top edge leans toward the pulled side, making the
                // slingshot direction visible before release. A near-zero
                // drag keeps the hand at rest with no roll, so a plain
                // click looks exactly like the default swing.
                if (m_aiming)
                {
                    Vector2 aimVec = InputReader.AimDelta;
                    float aimMag = aimVec.magnitude;
                    Vector3 followTarget = m_restPosition;
                    Quaternion rotTarget = Quaternion.identity;
                    if (aimMag > 0.0001f)
                    {
                        float k = Mathf.Min(1f, aimMag / m_aimFullDragPixels);
                        Vector3 dirLocal = new Vector3(aimVec.x / aimMag, aimVec.y / aimMag, 0f);
                        // As the drag grows, the aim anchor eases from the
                        // off-centre rest x to the camera forward axis
                        // (x = 0), so a left pull and its mirrored right
                        // pull reach mirror-image poses instead of one
                        // wide path and one cramped path.
                        Vector3 aimAnchor = new Vector3(
                            Mathf.Lerp(m_restPosition.x, 0f, k),
                            m_restPosition.y, m_restPosition.z);
                        // Elliptical offset: wider horizontal (extent X)
                        // than vertical (extent Y) so max deflection stays
                        // in view, then clamped inside the camera frustum
                        // using the actual pivot z of the preview target.
                        followTarget = aimAnchor + new Vector3(
                            dirLocal.x * m_aimOffsetExtentX,
                            dirLocal.y * m_aimOffsetExtentY,
                            0f) * k;
                        float previewZ = followTarget.z;
                        followTarget.x = Mathf.Clamp(followTarget.x,
                            -m_frustumClampXFactor * previewZ,
                            m_frustumClampXFactor * previewZ);
                        followTarget.y = Mathf.Clamp(followTarget.y,
                            -m_frustumClampYFactor * previewZ,
                            m_frustumClampYFactor * previewZ);
                        if (aimMag >= m_minDragPixels)
                        {
                            // Roll that points the hand's top edge (local
                            // +Y) along the pull in the camera right/up
                            // plane (drag-up = up convention). The
                            // DeltaAngle formula is mirror-symmetric
                            // (roll(-x) = -roll(x)); the +-m_aimRollDeg
                            // clamp keeps both sides presenting the same
                            // amount of palm/back to the target. A small
                            // wobble rides on top so the held hand feels
                            // alive.
                            float rollDeg = Mathf.Clamp(
                                Mathf.DeltaAngle(0f, 90f -
                                    Mathf.Atan2(aimVec.y, aimVec.x) * Mathf.Rad2Deg),
                                -m_aimRollDeg, m_aimRollDeg);
                            rollDeg += Mathf.Sin(Time.unscaledTime * m_aimWobbleFreq)
                                * m_aimWobbleDeg * k;
                            // Yaw about camera up: the hand turns toward
                            // the pulled side. Pitch about camera right:
                            // -aim.y keeps the verified drag-up = up rule
                            // (negative angle about +X lifts the
                            // forward-facing fingertips up).
                            float yawDeg = Mathf.Sign(aimVec.x) * m_aimYawDeg * k;
                            float pitchDeg = -(aimVec.y / aimMag) * m_aimPitchDeg * k;
                            rotTarget = Quaternion.AngleAxis(yawDeg, Vector3.up)
                                * Quaternion.AngleAxis(pitchDeg, Vector3.right)
                                * Quaternion.AngleAxis(rollDeg, Vector3.forward);
                        }
                    }
                    float blend = 1f - Mathf.Exp(-m_aimResponse * Time.unscaledDeltaTime);
                    transform.localPosition = Vector3.Lerp(
                        transform.localPosition, followTarget, blend);
                    transform.localRotation = Quaternion.Slerp(
                        transform.localRotation, rotTarget, blend);
                }
            }

            if (m_phase != SwingPhase.Active)
                return;

            float elapsed = Time.time - m_swingStartTime;
            float strikeEndZ = 0.75f + m_reach * 0.6f;
            Vector3 windUpPos = m_directional ? m_aimStartLocal : m_windUpPosition;
            Vector3 strikeEndPos = m_directional
                ? m_dirStrikeLocal
                : new Vector3(-0.35f, m_restPosition.y, strikeEndZ);
            float t;

            if (elapsed < m_windUpDuration)
            {
                // Wind-up phase: current pose (rest, or the live aim pose)
                // -> wind-up target.
                t = elapsed / m_windUpDuration;
                transform.localPosition = Vector3.Lerp(m_swingFromLocal, windUpPos, t);
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
                    transform.localPosition = m_restPosition;
                    transform.localRotation = Quaternion.identity;
                }
            }

            // Hand rotation: a directional swing blends from the rotation
            // the aim preview left the hand in, through the aimed
            // yaw/pitch/roll pose during wind-up, to the mirrored
            // follow-through pose by the strike end, then eases back to
            // identity during recover. The default swing never touches
            // rotation.
            if (m_directional)
            {
                if (elapsed < m_windUpDuration)
                {
                    transform.localRotation = Quaternion.Slerp(
                        m_swingFromRot, m_aimPose, t);
                }
                else if (elapsed < m_windUpDuration + m_strikeDuration)
                {
                    transform.localRotation = Quaternion.Slerp(
                        m_aimPose, m_followPose, t);
                }
                else
                {
                    float rotBlend = 1f - Mathf.Exp(
                        -m_aimResponse * Time.unscaledDeltaTime);
                    transform.localRotation = Quaternion.Slerp(
                        transform.localRotation, Quaternion.identity, rotBlend);
                }
            }
        }

        /// <summary>
        /// Public entry point for headless photo mode (and any scripted
        /// swing): runs the exact plain-swing start an Attack release with
        /// no drag performs, guarded by the same idle + cooldown checks
        /// Update uses. Adds no behavior of its own - the windup/strike/
        /// recover animation, the hit scan and the slap audio all follow
        /// through StartSwing/FixedUpdate exactly like a real click.
        /// </summary>
        public void TriggerPlainSwing()
        {
            if (m_phase != SwingPhase.Idle)
                return;
            if (Time.time - m_swingStartTime < m_cooldown)
                return;

            StartSwing();
        }

        private void StartSwing()
        {
            m_swingStartTime = Time.time;
            m_swingFromLocal = transform.localPosition;
            m_swingFromRot = transform.localRotation;
            m_prevWorldPos = transform.position;
            m_phase = SwingPhase.Active;
            m_directional = false;
            m_hitThisSwing.Clear();
            // Release strike starts: drop the drag dedupe so an NPC the
            // drag already struck can still be hit by this swing.
            m_hitThisDrag.Clear();
        }

        /// <summary>
        /// Starts a directional swing. Default (slingshot): the live aim
        /// pose — rest PLUS the pull offset, hand following the mouse — is
        /// the wind-up, and the release strikes back through the rest point
        /// out the opposite side. With m_strikeAlongDrag the strike instead
        /// travels along the drag direction, winding up opposite it as
        /// before. The impulse direction follows the strike direction.
        /// </summary>
        private void StartDirectionalSwing(Vector2 aim)
        {
            float aimMag = aim.magnitude;
            float angle = Mathf.Atan2(aim.y, aim.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float aimK = Mathf.Min(1f, aimMag / m_aimFullDragPixels);
            // Same mirror-symmetric lean roll the preview showed, clamped
            // to +-m_aimRollDeg; the sign flips with the pull side, so the
            // backhand presents the back of the hand symmetrically.
            m_swingRollDeg = Mathf.Clamp(
                Mathf.DeltaAngle(0f, 90f - angle * Mathf.Rad2Deg),
                -m_aimRollDeg, m_aimRollDeg);

            // The same yaw/pitch tilt the live preview drove: yaw toward
            // the pulled side about camera up, pitch about camera right
            // with the drag-up = up rule (-sin lifts the fingertips up).
            float swingYawDeg = Mathf.Sign(aim.x) * m_aimYawDeg * aimK;
            float swingPitchDeg = -sin * m_aimPitchDeg * aimK;
            m_aimPose = Quaternion.AngleAxis(swingYawDeg, Vector3.up)
                * Quaternion.AngleAxis(swingPitchDeg, Vector3.right)
                * Quaternion.AngleAxis(m_swingRollDeg, Vector3.forward);
            // Follow-through: mirrored yaw/pitch at 60% magnitude, held
            // roll unchanged, reached at the strike end.
            m_followPose = Quaternion.AngleAxis(-swingYawDeg * 0.6f, Vector3.up)
                * Quaternion.AngleAxis(-swingPitchDeg * 0.6f, Vector3.right)
                * Quaternion.AngleAxis(m_swingRollDeg, Vector3.forward);

            // Pull direction in the camera right/up plane (hand local x/y);
            // drag-up = up convention (not flipped).
            Vector3 pullDir = new Vector3(cos, sin, 0f);
            Vector3 strikeDir = m_strikeAlongDrag ? pullDir : -pullDir;

            if (m_strikeAlongDrag)
            {
                // Old arc: wind up opposite the drag so the strike sweeps
                // along the drag direction, from the same x-centring
                // anchor the live preview used (mirror-symmetric about x=0).
                m_aimStartLocal =
                    new Vector3(Mathf.Lerp(m_restPosition.x, 0f, aimK),
                        m_restPosition.y, m_restPosition.z) -
                    pullDir * (m_aimStartOffset * aimK);
            }
            else
            {
                // Wind-up target = the live aim pose the preview left the
                // hand in, so the swing continues smoothly from what the
                // player already sees, then whips through centre to the
                // opposite side.
                m_aimStartLocal = transform.localPosition;
            }
            // Never let a downward drag's wind-up pose sink through the floor.
            m_aimStartLocal.y = Mathf.Max(m_aimStartLocal.y, -0.5f);

            // Strike end: same plane reach and forward z as the default
            // swing, but anchored on the camera forward axis (x = 0)
            // instead of the off-centre rest x. Measured from rest x=+0.3,
            // a left-to-right strike ended far right of screen (the
            // backhand's weird angle) while its mirror crossed the centre.
            // Anchoring at x = 0 makes horizontal strikes exact mirror
            // images sweeping through the same screen-centre target zone.
            float strikeEndZ = 0.75f + m_reach * 0.6f;
            float arcRadiusX = m_reach * m_strikeArcReachFactorX;
            float arcRadiusY = m_reach * m_strikeArcReachFactorY;
            m_dirStrikeLocal = new Vector3(
                strikeDir.x * arcRadiusX,
                m_restPosition.y + strikeDir.y * arcRadiusY,
                strikeEndZ);
            m_dirAimNorm = strikeDir; // already unit length

            StartSwing();
            m_directional = true;
        }

        private void OnDisable()
        {
            // Never leave the camera stuck with look suppressed.
            m_aiming = false;
            // Aiming ended: re-capture the hand position for the same
            // no-phantom-delta reason as the release branch.
            m_prevWorldPos = transform.position;
            if (Player != null)
                Player.LookSuppressed = false;
            if (m_phase == SwingPhase.Idle)
                transform.localPosition = m_restPosition;
            transform.localRotation = Quaternion.identity;
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

        private void OnDrawGizmos()
        {
            if (!m_debugLog)
                return;
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
