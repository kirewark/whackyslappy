using UnityEngine;
using WhackySlappy.Core;
using WhackySlappy.Slap;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Controls a single goofy NPC: body + head, wandering, tumbling,
    /// self-righting, despawn and respawn. Implements ISlappable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NpcController : MonoBehaviour, ISlappable
    {
        // -------------------------------------------------------------------------
        // Tuning (Design.md §4.3, §4.4)
        // -------------------------------------------------------------------------

        /// <summary>Speed at which the NPC wanders toward the player (m/s).</summary>
        [SerializeField] private float m_wanderSpeed = 1.0f;

        /// <summary>Force multiplier applied during wandering.</summary>
        [SerializeField] private float m_wanderForce = 5.0f;

        /// <summary>Minimum distance (m) to the player before stopping the push.</summary>
        [SerializeField] private float m_wanderStopDistance = 1.2f;

        /// <summary>Self-righting angular-velocity damping in Wander state.</summary>
        [SerializeField] private float m_uprightDampingWander = 2.0f;

        /// <summary>Self-righting angular-velocity damping in GettingUp state.</summary>
        [SerializeField] private float m_uprightDampingGettingUp = 3.0f;

        /// <summary>Speed threshold (m/s) considered "settled" for state transitions.</summary>
        [SerializeField] private float m_settleSpeed = 0.3f;

        /// <summary>Time (s) the NPC must stay below settleSpeed before GettingUp.</summary>
        [SerializeField] private float m_settleTime = 1.0f;

        /// <summary>Cooldown (s) after a hit during which the NPC ignores new hits.</summary>
        [SerializeField] private float m_hitCooldown = 0.1f;

        /// <summary>Short cooldown (s) after a leg sweep, separate from the
        /// slap hit-cooldown so one sweep does not re-trip every physics tick
        /// while leaving the NPC still slap-able.</summary>
        [SerializeField] private float m_sweepCooldown = 0.3f;

        /// <summary>Kill height (y) below which the NPC despawns.</summary>
        [SerializeField] private float m_killHeight = -10.0f;

        /// <summary>Platform half-size for wander bounds (m). Platform is 20x20 per §6.</summary>
        [SerializeField] private float m_platformHalfSize = 10.0f;

        /// <summary>Margin (m) from platform edge for safe wander points.</summary>
        [SerializeField] private float m_wanderMargin = 1.5f;

        /// <summary>Color for the head mesh (randomized on spawn).</summary>
        [SerializeField] private Color m_headColor = Color.cyan;

        // -------------------------------------------------------------------------
        // Balance tuning (Plan A1, A3)
        // -------------------------------------------------------------------------

        /// <summary>Offset of the rigidbody's center of mass above the feet (m).
        /// Lower values keep the NPC bottom-heavy so it rights itself like a
        /// inflatable punching clown.</summary>
        [SerializeField] private float m_centerOfMassOffset = 0.3f;

        /// <summary>Angle (degrees) between the NPC's up and world up at which
        /// Wander treats the NPC as fallen and switches to Tumbling.</summary>
        [SerializeField] private float m_tiltedThreshold = 45f;

        /// <summary>Base self-righting torque multiplier (wider range 25-40 for
        /// stronger upright recovery in both Wander and GettingUp).</summary>
        [SerializeField] private float m_uprightStrength = 30f;

        // -------------------------------------------------------------------------
        // Sweep reaction tuning
        // -------------------------------------------------------------------------

        /// <summary>Topple torque added about the horizontal axis perpendicular
        /// to the sweep tangent when this NPC is hit by a leg sweep. Strong
        /// enough to pitch the body past 90 degrees so it lies flat over
        /// rather than just wobbling.</summary>
        [SerializeField] private float m_sweepTorque = 18f;

        /// <summary>Upward velocity (m/s) a sweep hit is clamped to, so a
        /// tripped NPC slides and falls instead of being launched far.</summary>
        [SerializeField] private float m_maxSweepUpVelocity = 3f;

        /// <summary>Duration (s) after a sweep hit during which the NPC is
        /// held down: self-righting is off (the NPC stays in Tumbling, which
        /// is pure physics) and the GettingUp transition is blocked, so the
        /// knocked-over body stays on the ground briefly. A second sweep
        /// simply refreshes this window.</summary>
        [SerializeField] private float m_knockdownTime = 1.6f;

        // -------------------------------------------------------------------------
        // Silly physics (Pass 2, Phase 4). All lerp endpoints for the feel
        // multipliers live in SillyPhysics; these are NPC-local values.
        // -------------------------------------------------------------------------

        /// <summary>Extra angular-velocity headroom granted per unit of
        /// SillyPhysics.SpinTorque, so wild launch spin is not capped away.</summary>
        private const float kSpinAngularVelocityBonus = 2f;

        /// <summary>Duration (s) of one squash-and-stretch pulse. Fixed feel
        /// constant (not silliness-scaled), as specced.</summary>
        private const float kSquashDuration = 0.25f;

        // -------------------------------------------------------------------------
        // Internal state
        // -------------------------------------------------------------------------

        private enum State
        {
            Wander,
            Tumbling,
            GettingUp
        }

        private State m_state = State.Wander;
        private Rigidbody m_rb;
        private NpcSpawner m_spawner;
        private float m_settleTimer = 0f;
        private float m_hitCooldownTimer = 0f;
        private float m_sweepCooldownTimer = 0f;
        // Knockdown window: > 0 while a swept NPC must stay on the ground
        // (self-righting suppressed, GettingUp transition blocked).
        private float m_knockdownTimer = 0f;
        private Vector3 m_wanderTarget = Vector3.zero;

        // World half-height from the root pivot down to the feet, cached in
        // Start from the body capsule; sweep impulses push exactly there.
        private float m_feetHalfHeight = 0.8f;

        // The NPC's own torso capsule, built by Create(). Once
        // NpcBodyBuilder.AddLimbs runs, every capsule collider under the
        // root belongs to a limb rigidbody EXCEPT the torso, whose collider
        // attaches to the root rigidbody — so GetComponentInChildren<
        // CapsuleCollider>() is no longer safe and every read goes through
        // this cached reference (see SetTorso / EnsureTorsoCollider).
        private CapsuleCollider m_torsoCollider;

        // Reactive face built by Create() through NpcFace.Build(); may be
        // null (e.g. hand-placed prefab NPCs). NpcFace is a MonoBehaviour,
        // so never use ?. / ?? on it (UnityEngine.Object semantics): use
        // explicit != null checks instead.
        [System.NonSerialized] private NpcFace m_face;

        // Silly physics: post-launch hang-time countdown in physics seconds;
        // while > 0, part of this NPC's gravity is cancelled in FixedUpdate.
        private float m_hangTimer = 0f;

        // Silly physics: bounce material created once in Start; its
        // bounciness tracks SillyPhysics.BounceFactor each physics tick.
        private PhysicsMaterial m_bounceMaterial;

        // Rigidbody angular-velocity cap captured at Awake; the spin boost is
        // added on top of it every tick and vanishes with Silliness 0.
        private float m_baseMaxAngularVelocity = 7f;

        // Squash-and-stretch pulse. m_visualPivot is a renderer-only child
        // transform (never the rigidbody root, never a transform that also
        // carries a collider), so scaling it cannot affect physics. It stays
        // null for the primitive-built NPCs, whose colliders sit on the same
        // child GameObjects as their meshes: scaling those would scale the
        // colliders, so squash is skipped for them (SetupSquashPivot).
        private Transform m_visualPivot;
        private Vector3 m_visualPivotBaseScale = Vector3.one;
        private Vector3 m_squashDirLocal = Vector3.up;
        private float m_squashAmount = 0f;
        private float m_squashTime = 1f;
        private bool m_squashActive = false;

        // Re-entry guard for Despawn: Tumbling exit and CheckDespawn can both
        // fire in one tick, which would double-notify the spawner.
        private bool m_despawned = false;

        // Cached player transform: found once, reused every physics tick
        // instead of FindAnyObjectByType per tick. Unity's null check
        // re-finds it if the player object is ever destroyed.
        private static Transform s_playerTransform;

        private static Transform GetCachedPlayerTransform()
        {
            if (s_playerTransform == null)
            {
                var cc = Object.FindAnyObjectByType<CharacterController>();
                if (cc != null)
                    s_playerTransform = cc.transform;
            }
            return s_playerTransform;
        }

        // -------------------------------------------------------------------------
        // MonoBehaviour
        // -------------------------------------------------------------------------

        private void Awake()
        {
            m_rb = GetComponent<Rigidbody>();
            m_rb.mass = 1.5f;
            m_rb.linearDamping = 0.1f;
            m_rb.angularDamping = 0.5f;
            m_rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Silly physics: remember the spawn angular-velocity cap so the
            // spin boost is relative to it (returns to it at Silliness 0).
            m_baseMaxAngularVelocity = m_rb.maxAngularVelocity;
            // Plan A1: lower center of mass so gravity doesn't tip the NPC.
            ApplyLowCenterOfMass();
        }

        /// <summary>
        /// Plan A1: place the rigidbody center of mass just above the feet,
        /// measured relative to the capsule collider (not the root pivot),
        /// so the NPC stays bottom-heavy like an inflatable punching clown.
        /// </summary>
        private void ApplyLowCenterOfMass()
        {
            float localY = m_centerOfMassOffset;
            EnsureTorsoCollider();
            var capsule = m_torsoCollider;
            if (capsule != null)
            {
                // CapsuleCollider.height is unscaled; world half-height is
                // height * Y scale / 2. For a 2 m tall primitive scaled 0.8
                // this puts the COM at about -0.5 local Y.
                float halfHeightWorld = capsule.height * capsule.transform.lossyScale.y * 0.5f;
                float feetLocalY = capsule.transform.localPosition.y - halfHeightWorld;
                localY = feetLocalY + m_centerOfMassOffset;
            }
            m_rb.centerOfMass = new Vector3(0f, localY, 0f);
        }

        /// <summary>
        /// Assigns the torso capsule collider built by Create(). Called from
        /// Create() right after the controller exists, so every physics read
        /// (center of mass, feet height, bounce material) targets the torso
        /// even though the ragdoll-style limbs also carry CapsuleColliders.
        /// </summary>
        internal void SetTorso(CapsuleCollider torso)
        {
            m_torsoCollider = torso;
        }

        /// <summary>
        /// Assigns the face built by Create() via NpcFace.Build().
        /// </summary>
        internal void SetFace(NpcFace face)
        {
            m_face = face;
        }

        /// <summary>
        /// Fallback for m_torsoCollider when the NPC was not built by
        /// Create(): the torso is the ONLY body collider whose attached
        /// rigidbody is the NPC root (each limb capsule has its own
        /// rigidbody), so the root-rigidbody match identifies it
        /// unambiguously. Idempotent.
        /// </summary>
        private void EnsureTorsoCollider()
        {
            if (m_torsoCollider != null)
                return;
            CapsuleCollider[] caps = GetComponentsInChildren<CapsuleCollider>();
            for (int i = 0; i < caps.Length; i++)
            {
                if (caps[i] != null && caps[i].attachedRigidbody == m_rb)
                {
                    m_torsoCollider = caps[i];
                    break;
                }
            }
        }

        private void Start()
        {
            // Grab the spawner reference (created once per scene by NpcBootstrap).
            m_spawner = Object.FindAnyObjectByType<NpcSpawner>();

            // Cache the feet offset for sweep impulses: same world
            // half-height formula as ApplyLowCenterOfMass. The cached torso
            // collider (NOT GetComponentInChildren) keeps limb capsules out.
            EnsureTorsoCollider();
            var bodyCapsule = m_torsoCollider;
            if (bodyCapsule != null)
                m_feetHalfHeight = bodyCapsule.height * bodyCapsule.transform.lossyScale.y * 0.5f;

            // Silly physics: one bouncy material on the body collider, plus a
            // squash pivot that is safe to scale (null for primitive NPCs).
            SetupBounceMaterial(bodyCapsule);
            SetupSquashPivot();
        }

        /// <summary>
        /// Attach a bouncy material to the body collider exactly once. Unity 6
        /// uses PhysicsMaterial (NOT the legacy PhysicMaterial name). Bounce
        /// combine is Maximum only while BounceFactor > 0 so the value wins
        /// against the platform's low-bounce material; at 0 it drops to
        /// Average so Silliness 0 behaves like default physics. Bounciness
        /// and combine are kept in sync with Silliness in FixedUpdate.
        /// </summary>
        private void SetupBounceMaterial(Collider bodyCollider)
        {
            if (bodyCollider == null)
                return;
            m_bounceMaterial = new PhysicsMaterial(name + "_SillyBounce");
            float bounce = SillyPhysics.BounceFactor;
            m_bounceMaterial.bounciness = bounce;
            m_bounceMaterial.dynamicFriction = 0.6f;
            m_bounceMaterial.staticFriction = 0.6f;
            m_bounceMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            m_bounceMaterial.bounceCombine = bounce > 0f
                ? PhysicsMaterialCombine.Maximum
                : PhysicsMaterialCombine.Average;
            bodyCollider.material = m_bounceMaterial;
        }

        /// <summary>
        /// Find a child transform that covers every renderer but no collider:
        /// only such a subtree may be squash-scaled, because scaling the
        /// rigidbody root or any collider-bearing transform changes physics.
        /// The primitive test NPCs fail this check and keep squash disabled
        /// until art splits meshes away from colliders.
        /// </summary>
        private void SetupSquashPivot()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Transform candidate = renderers[0].transform.parent;
            if (candidate == null || candidate == transform)
                return; // only the root visual exists: never scale the rb root

            foreach (var r in renderers)
                if (!r.transform.IsChildOf(candidate))
                    return; // renderers scattered: no single visual subtree

            foreach (var c in GetComponentsInChildren<Collider>())
                if (c.transform.IsChildOf(candidate))
                    return; // scaling would shrink/grow colliders: skip

            m_visualPivot = candidate;
            m_visualPivotBaseScale = candidate.localScale;
        }

        private void FixedUpdate()
        {
            // Hit cooldown countdown.
            if (m_hitCooldownTimer > 0f)
                m_hitCooldownTimer -= Time.fixedDeltaTime;

            // Sweep cooldown countdown (separate timer, see m_sweepCooldown).
            if (m_sweepCooldownTimer > 0f)
                m_sweepCooldownTimer -= Time.fixedDeltaTime;

            // Knockdown window countdown. While it runs, Tumbling stays
            // pure physics (no self-righting), so the body lies flat.
            if (m_knockdownTimer > 0f)
                m_knockdownTimer -= Time.fixedDeltaTime;

            // Out-of-arena kill check runs every tick regardless of state, so
            // an NPC sliding off the platform mid-Wander or mid-GettingUp
            // still despawns (previously only checked in Tumbling).
            CheckDespawn();

            // Silly physics: hard global speed cap so launches never become
            // rockets (MaxSpeed 12-22 m/s; far above the wander cap, so
            // wander behaviour is untouched).
            float maxSpeed = SillyPhysics.MaxSpeed;
            if (m_rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                m_rb.linearVelocity = m_rb.linearVelocity.normalized * maxSpeed;

            // Silly physics: allow wild launch spin; returns to the spawn cap
            // exactly when Silliness is 0.
            m_rb.maxAngularVelocity = m_baseMaxAngularVelocity
                + SillyPhysics.SpinTorque * kSpinAngularVelocityBonus;

            // Silly physics: hang time is applied as a counter-force instead
            // of toggling useGravity, so contacts and sleeping stay intact.
            // Cancels (1 - HangGravityScale) of gravity; plain arithmetic,
            // GC-free. At Silliness 0 the cancel term is exactly 0.
            if (m_hangTimer > 0f)
            {
                m_hangTimer -= Time.fixedDeltaTime;
                float cancel = m_rb.mass * (1f - SillyPhysics.HangGravityScale);
                m_rb.AddForce(-Physics.gravity * cancel, ForceMode.Force);
            }

            // Keep the one-shot bounce material in sync with the master
            // slider (float/enum writes per tick; never re-created). While
            // BounceFactor is 0 (Silliness 0) the combine drops to Average so
            // the material cannot out-bounce the platform: bounciness 0 with
            // Average matches Unity's default physics.
            if (m_bounceMaterial != null)
            {
                float bounce = SillyPhysics.BounceFactor;
                m_bounceMaterial.bounciness = bounce;
                m_bounceMaterial.bounceCombine = bounce > 0f
                    ? PhysicsMaterialCombine.Maximum
                    : PhysicsMaterialCombine.Average;
            }

            switch (m_state)
            {
                case State.Wander:
                    UpdateWander();
                    ApplySelfRighting(m_uprightStrength, m_uprightDampingWander);
                    break;

                case State.Tumbling:
                    // Pure physics — do nothing here. (The kill-height check
                    // runs unconditionally earlier in FixedUpdate.)
                    CheckTransitionToGettingUp();
                    break;

                case State.GettingUp:
                    ApplySelfRighting(m_uprightStrength, m_uprightDampingGettingUp);
                    CheckTransitionToWander();
                    break;
            }
        }

        /// <summary>
        /// Landing clears the silly hang-time window: once the NPC actually
        /// touches ground (a mostly-upward contact normal against anything
        /// that is not another NPC), the low-gravity countdown stops so it
        /// cannot hover on contact or get its gravity cancelled again while
        /// sitting still.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (m_hangTimer <= 0f)
                return;
            if (collision.gameObject.layer == GameLayers.NPC)
                return;

            var contacts = collision.contacts;
            for (int i = 0; i < contacts.Length; i++)
            {
                if (contacts[i].normal.y > 0.5f)
                {
                    m_hangTimer = 0f;
                    return;
                }
            }
        }

        // -------------------------------------------------------------------------
        // ISlappable implementation
        // -------------------------------------------------------------------------

        /// <summary>
        /// Read-only views of the private state machine and the cached
        /// body for headless photo mode / diagnostics. Pure getters:
        /// they add no behavior of their own.
        /// </summary>
        public bool IsTumbling => m_state == State.Tumbling;
        public bool IsGettingUp => m_state == State.GettingUp;

        /// <summary>Angle (degrees) between this NPC's up axis and world up.</summary>
        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);

        /// <summary>Rigidbody speed (m/s); 0 before the body is cached.</summary>
        public float Speed => m_rb != null ? m_rb.linearVelocity.magnitude : 0f;

        /// <summary>
        /// Called by the slap system when this NPC is hit.
        /// Applies impulse and switches to Tumbling state.
        /// </summary>
        public void OnSlapped(Vector3 impulse, Vector3 point)
        {
            // Ignore if we are in hit-cooldown (prevent double hits).
            if (m_hitCooldownTimer > 0f)
                return;

            // Force is applied by the slap hand; here we only change state.

            m_state = State.Tumbling;
            m_hitCooldownTimer = m_hitCooldown;
            m_settleTimer = 0f;
        }

        /// <summary>
        /// Explicit interface entry point: SlapHand and LegSweep call OnHit
        /// through ISlappable, and interface default methods are not callable
        /// on the concrete type, so we dispatch on Kind here. Slap keeps the
        /// existing OnSlapped path (the hand applies its own force); Sweep
        /// runs the new trip handler below.
        /// </summary>
        void ISlappable.OnHit(in SlapHitInfo hit)
        {
            if (hit.Kind == SlapKind.Sweep)
            {
                // Sweeps respect the slap hit-cooldown AND their own short
                // sweep cooldown, so one continuous sweep leg cannot re-trip
                // the NPC every physics tick.
                if (m_hitCooldownTimer > 0f || m_sweepCooldownTimer > 0f)
                    return;
                OnSwept(in hit);
                NpcVoice.Yelp(transform.position, hit.Strength);
            }
            else
            {
                // Slap path: the hand has already applied the (silliness-
                // scaled) impulse; run the existing state reaction, then add
                // the silly launch layer only when the hit was actually
                // accepted (OnSlapped ignores hits during cooldown).
                bool accepted = m_hitCooldownTimer <= 0f;
                OnSlapped(hit.Impulse, hit.Point);
                if (accepted)
                {
                    ApplyLaunchReaction(hit.Impulse, hit.Strength);
                    NpcVoice.Yelp(transform.position, hit.Strength);
                    // Facial reaction only for accepted hits; cooldown-gated
                    // slaps stay expression-free. m_face is a MonoBehaviour:
                    // explicit null check, never ?..
                    if (m_face != null)
                        m_face.SetExpression(NpcFace.Expression.Hit, 0.8f);
                }
            }
        }

        /// <summary>
        /// Leg-sweep reaction: push at the feet so the base slides out from
        /// under the body, add topple torque about the sweep tangent's
        /// horizontal axis, clamp upward velocity so the NPC is not launched
        /// far, and force Tumbling so the normal self-righting sequence runs.
        /// </summary>
        private void OnSwept(in SlapHitInfo hit)
        {
            // Reached only after both cooldown gates passed in OnHit, so a
            // Dizzy face here always accompanies an accepted sweep.
            if (m_face != null)
                m_face.SetExpression(NpcFace.Expression.Dizzy, 3f);

            Vector3 impulse = hit.Impulse;

            // Horizontal part of the impulse is the sweep tangent.
            Vector3 tangent = impulse;
            tangent.y = 0f;
            bool hasTangent = tangent.sqrMagnitude > 0.0001f;
            if (hasTangent)
                tangent.Normalize();

            float prevUp = m_rb.linearVelocity.y;

            // Impulse at the feet, not the body centre.
            Vector3 feet = m_rb.position - Vector3.up * m_feetHalfHeight;
            m_rb.AddForceAtPosition(impulse, feet, ForceMode.Impulse);

            // Topple spin: axis perpendicular to the tangent, pitching the
            // body back as the feet slide forward.
            if (hasTangent)
                m_rb.AddTorque(Vector3.Cross(tangent, Vector3.up) * m_sweepTorque, ForceMode.Force);

            // Clamp upward velocity the sweep itself adds (a slap may already
            // have launched us higher — then leave that launch alone).
            if (prevUp < m_maxSweepUpVelocity && m_rb.linearVelocity.y > m_maxSweepUpVelocity)
            {
                Vector3 v = m_rb.linearVelocity;
                v.y = m_maxSweepUpVelocity;
                m_rb.linearVelocity = v;
            }

            // Sweep-specific cooldown, NOT the slap hit-cooldown: the NPC
            // must stay slap-able right after a trip.
            m_sweepCooldownTimer = m_sweepCooldown;
            m_state = State.Tumbling;
            m_settleTimer = 0f;

            // Knockdown window: re-sweeping a downed NPC just refreshes the
            // timer, so it stays held down for m_knockdownTime from the last
            // hit. The upward clamp above keeps it a knockdown, not a launch.
            m_knockdownTimer = m_knockdownTime;

            // Silly physics: a sweep gets the hang window and squash only.
            // No random-axis spin here — the trip already got its topple
            // torque, and a random spin would fight the fall.
            m_hangTimer = SillyPhysics.HangTimeSeconds;
            BeginSquash(hasTangent ? tangent : impulse, hit.Strength);
        }

        // -------------------------------------------------------------------------
        // Silly physics launch reaction (Pass 2, Phase 4)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Extra reaction applied after a slap launch impulse is in the body:
        /// random-axis spin torque scaled by SillyPhysics.SpinTorque and hit
        /// strength, a hang-time window reducing this NPC's gravity, and a
        /// squash-and-stretch pulse. Does nothing at Silliness 0.
        /// </summary>
        private void ApplyLaunchReaction(Vector3 impulse, float strength)
        {
            float spinTorque = SillyPhysics.SpinTorque;
            float hang = SillyPhysics.HangTimeSeconds;
            if (spinTorque <= 0f && hang <= 0f && SillyPhysics.SquashAmount <= 0f)
                return;

            strength = Mathf.Clamp01(strength);

            // Random-axis spin, scaled by spin torque and hit strength.
            if (spinTorque > 0f && strength > 0f)
                m_rb.AddTorque(Random.onUnitSphere * (spinTorque * strength),
                    ForceMode.Impulse);

            // Hang-time window: gravity partially cancelled for
            // HangTimeSeconds of physics ticks (countdown in FixedUpdate).
            if (hang > 0f)
                m_hangTimer = hang;

            BeginSquash(impulse, strength);
        }

        /// <summary>
        /// Begin a decaying squash-and-stretch pulse along the impulse
        /// direction. No-op without a safe visual pivot (see SetupSquashPivot)
        /// or at Silliness 0.
        /// </summary>
        private void BeginSquash(Vector3 impulse, float strength)
        {
            if (m_visualPivot == null)
                return;

            float amount = SillyPhysics.SquashAmount * Mathf.Clamp01(strength);
            if (amount <= 0.001f)
                return;

            // Snap the impulse to the nearest local axis: a clean diagonal
            // localScale pulse without building a rotation (GC-free).
            Vector3 local = transform.InverseTransformDirection(impulse);
            Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            if (abs.x >= abs.y && abs.x >= abs.z)
                m_squashDirLocal = new Vector3(Mathf.Sign(local.x), 0f, 0f);
            else if (abs.y >= abs.z)
                m_squashDirLocal = new Vector3(0f, Mathf.Sign(local.y), 0f);
            else
                m_squashDirLocal = new Vector3(0f, 0f, Mathf.Sign(local.z));

            m_squashAmount = amount;
            m_squashTime = 0f;
            m_squashActive = true;
        }

        private void Update()
        {
            if (!m_squashActive)
                return;

            // Hit-stop scales Time.timeScale, so drive the pulse with
            // unscaled delta time: it always restores in kSquashDuration
            // real seconds even while the world is slowed.
            m_squashTime += Time.unscaledDeltaTime;
            float t = m_squashTime / kSquashDuration;

            if (t >= 1f)
            {
                m_squashActive = false;
                m_visualPivot.localScale = m_visualPivotBaseScale;
                return;
            }

            // Quadratic decay pulse. m_squashDirLocal is an axis-aligned
            // local-space unit vector, so the pulse is a pure diagonal
            // localScale: stretch along the impulse direction, squash the
            // two perpendicular axes by half the stretch (volume-preserving).
            float decay = (1f - t) * (1f - t);
            float a = m_squashAmount * decay;
            Vector3 s = Vector3.one + m_squashDirLocal * a
                - (Vector3.one - m_squashDirLocal) * (0.5f * a);
            m_visualPivot.localScale = Vector3.Scale(m_visualPivotBaseScale, s);
        }

        // -------------------------------------------------------------------------
        // Wander logic
        // -------------------------------------------------------------------------

        private void UpdateWander()
        {
            // Plan A2: if the NPC is tilted more than the threshold, it has
            // fallen over gently (not from a slap) — go to Tumbling so the
            // normal recovery sequence runs.
            float tiltAngle = Vector3.Angle(transform.up, Vector3.up);
            if (tiltAngle > m_tiltedThreshold)
            {
                m_state = State.Tumbling;
                m_settleTimer = 0f;
                return; // no wander push while fallen
            }

            // Cached player transform for distance check and direction.
            var player = GetCachedPlayerTransform();

            if (player != null)
            {
                float dist = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(player.position.x, 0, player.position.z));

                if (dist > m_wanderStopDistance)
                {
                    // Pick a new wander target if needed.
                    if (Vector3.Distance(transform.position, m_wanderTarget) < 0.5f
                        || !IsValidWanderPoint(m_wanderTarget))
                    {
                        m_wanderTarget = RandomWanderPoint();
                    }

                    Vector3 direction = (m_wanderTarget - transform.position);
                    direction.y = 0f; // Only move on XZ plane.
                    direction = direction.normalized;

                    m_rb.AddForce(direction * m_wanderForce, ForceMode.Force);

                    // Cap speed.
                    if (m_rb.linearVelocity.magnitude > m_wanderSpeed * 3f)
                        m_rb.linearVelocity = m_rb.linearVelocity.normalized * m_wanderSpeed * 3f;
                }
            }
        }

        private Vector3 RandomWanderPoint()
        {
            Vector3 point;
            int attempts = 0;
            do
            {
                float x = Random.Range(-m_platformHalfSize + m_wanderMargin,
                                        m_platformHalfSize - m_wanderMargin);
                float z = Random.Range(-m_platformHalfSize + m_wanderMargin,
                                        m_platformHalfSize - m_wanderMargin);
                point = new Vector3(x, transform.position.y, z);
                attempts++;
            } while (!IsValidWanderPoint(point) && attempts < 10);
            return point;
        }

        private bool IsValidWanderPoint(Vector3 point)
        {
            return point.y >= -0.5f; // Ensure it's on the platform.
        }

        // -------------------------------------------------------------------------
        // Self-righting (inflatable clown)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Applies a torque that keeps the NPC upright (Plan A3: tilt-proportional).
        /// torque = Cross(up, up_world) * strength * (1 + tilt/90°) - angularVelocity * damping
        /// The tilt factor doubles the righting torque when the NPC is near-flat (90°),
        /// so even a gently tipped NPC recovers firmly.
        /// </summary>
        private void ApplySelfRighting(float strength, float damping)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            float tiltFactor = 1f + tilt / 90f; // 1.0 at upright → ~2.0 at 90° flat
            Vector3 torque = cross * strength * tiltFactor - m_rb.angularVelocity * damping;
            m_rb.AddTorque(torque, ForceMode.Force);
        }

        // -------------------------------------------------------------------------
        // State transitions
        // -------------------------------------------------------------------------

        /// <summary>
        /// In Tumbling: if speed drops below settleSpeed and the NPC is on ground,
        /// transition to GettingUp. If below kill height, despawn.
        /// </summary>
        private void CheckTransitionToGettingUp()
        {
            if (transform.position.y < m_killHeight)
            {
                Despawn();
                return;
            }

            // Knockdown lockout: while a swept NPC is still held down, stay
            // in Tumbling (pure physics, no self-righting torque is applied
            // there) and do not let the settle timer accumulate, so the
            // full m_knockdownTime passes before normal GettingUp recovery.
            if (m_knockdownTimer > 0f)
            {
                m_settleTimer = 0f;
                return;
            }

            if (m_rb.linearVelocity.magnitude < m_settleSpeed)
            {
                m_settleTimer += Time.fixedDeltaTime;
                if (m_settleTimer >= m_settleTime)
                {
                    m_state = State.GettingUp;
                    m_settleTimer = 0f;
                    // The recovery animation plays with a strained face.
                    if (m_face != null)
                        m_face.SetExpression(NpcFace.Expression.Recover, 1.5f);
                }
            }
            else
            {
                m_settleTimer = 0f;
            }
        }

        /// <summary>
        /// In GettingUp: if the NPC is roughly upright, transition back to Wander.
        /// </summary>
        private void CheckTransitionToWander()
        {
            // Check if upright: the angle between transform.up and Vector3.up should be small.
            float dot = Vector3.Dot(transform.up, Vector3.up);
            if (dot > 0.9f) // Within ~25 degrees of upright.
            {
                // Also ensure speed is low.
                if (m_rb.linearVelocity.magnitude < m_settleSpeed * 2f)
                {
                    m_state = State.Wander;
                    m_settleTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Check if the NPC fell off the platform and needs despawning.
        /// </summary>
        private void CheckDespawn()
        {
            if (transform.position.y < m_killHeight)
            {
                Despawn();
            }
        }

        // -------------------------------------------------------------------------
        // Despawn / respawn
        // -------------------------------------------------------------------------

        private void Despawn()
        {
            if (m_despawned)
                return;
            m_despawned = true;
            Debug.Log("[NpcController] Despawn " + name + " at " + transform.position + " state=" + m_state);
            // Notify the spawner before destroying.
            if (m_spawner != null)
            {
                m_spawner.NotifyDespawn(this);
            }
            Destroy(gameObject);
        }

        // -------------------------------------------------------------------------
        // Public helpers (used by spawner / bootstrap)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Called by the spawner after an NPC is destroyed and being replaced.
        /// Resets the NPC to its initial state.
        /// </summary>
        public void ResetToWander()
        {
            m_state = State.Wander;
            m_settleTimer = 0f;
            m_hitCooldownTimer = 0f;
            m_sweepCooldownTimer = 0f;
            m_knockdownTimer = 0f;
            m_despawned = false;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // Silly physics: clear hang time and any live squash pulse so a
            // recycled NPC spawns at rest and visually neutral.
            m_hangTimer = 0f;
            m_squashActive = false;
            if (m_visualPivot != null)
                m_visualPivot.localScale = m_visualPivotBaseScale;
        }

        // -------------------------------------------------------------------------
        // Factory
        // -------------------------------------------------------------------------

        /// <summary>
        /// Factory that builds an NPC from code primitives: capsule body,
        /// sphere head child, and one Rigidbody on the root.
        /// </summary>
        public static NpcController Create(Vector3 position, NpcSpawner spawner)
        {
            var go = new GameObject("Npc");
            go.transform.position = position;
            go.layer = GameLayers.NPC;

            // Capsule body.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            body.layer = GameLayers.NPC;
            var bodyMat = DefaultLitMaterial.Create();
            if (bodyMat != null)
            {
                bodyMat.color = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
                body.GetComponent<Renderer>().material = bodyMat;
            }

            // Sphere head child.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, 1f, 0);
            head.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            head.layer = GameLayers.NPC;
            var headMat = DefaultLitMaterial.Create();
            if (headMat != null)
            {
                headMat.color = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
                head.GetComponent<Renderer>().material = headMat;
            }

            // One non-kinematic Rigidbody on root.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Ragdoll-style arms and legs (8 jointed capsules, each with its
            // own Rigidbody + CapsuleCollider, children of the root). Must
            // run after the root Rigidbody exists (the joints connect to it)
            // and before the feet snap below. Limbs get a slightly lighter
            // tint of the body colour: Create already tints, so a matched
            // variant instead of the exact body material.
            Material limbMat = null;
            if (bodyMat != null)
            {
                limbMat = new Material(bodyMat);
                Color limbColor = bodyMat.color;
                limbMat.color = new Color(
                    Mathf.Clamp01(limbColor.r * 1.15f + 0.05f),
                    Mathf.Clamp01(limbColor.g * 1.15f + 0.05f),
                    Mathf.Clamp01(limbColor.b * 1.15f + 0.05f),
                    limbColor.a);
            }
            NpcBodyBuilder.AddLimbs(go, limbMat, body.transform);

            // Reactive cartoon face parented to the head, tinted to match
            // the head. Built after the limbs so the face's primitive
            // colliders (destroyed inside Build) never enter the limb
            // collision-ignore matrix; it stays collider-free afterwards.
            NpcFace face = NpcFace.Build(go.transform, head.transform, headMat);

            // Plan A4: snap feet to y=0 on spawn using the scaled collider
            // half-height (capsule.bounds.extents.y), not unscaled height/2.
            // Read from the TORSO collider directly (body.GetComponent), so
            // the freshly added limb capsules cannot change the snap value.
            var capsule = body.GetComponent<CapsuleCollider>();
            go.transform.position = new Vector3(position.x, capsule.bounds.extents.y, position.z);

            var controller = go.AddComponent<NpcController>();
            controller.m_spawner = spawner;
            // Cache the two physics-critical/behaviour references before any
            // controller code can run (Awake already fired ApplyLowCenter-
            // OfMass during AddComponent, and Create repeats it below).
            controller.SetTorso(capsule);
            controller.SetFace(face);

            // Plan A1: set center of mass after controller is created.
            controller.ApplyLowCenterOfMass();

            return controller;
        }
    }
}
