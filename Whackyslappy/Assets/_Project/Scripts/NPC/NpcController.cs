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

        /// <summary>Horizontal walking pace used by the wander steering
        /// (m/s). Velocity steering only ever drives up to this speed, so a
        /// knockback impulse above it is never speed-capped back down.</summary>
        [SerializeField] private float m_wanderSpeed = 1.6f;

        /// <summary>Horizontal-velocity acceleration (m/s^2) the wander
        /// steering applies toward the desired velocity (NpcWander.
        /// SteerHorizontal max step per fixed tick is this times the fixed
        /// timestep). Replace the old 5 N wander push, which friction and
        /// damping swamped at mass 1.5.</summary>
        [SerializeField] private float m_wanderAccel = 8f;

        /// <summary>Idle pause (s) after reaching a wander target, before
        /// starting to walk to the next one. Range endpoints.</summary>
        [SerializeField] private float m_idlePauseMin = 0.5f;
        [SerializeField] private float m_idlePauseMax = 2.5f;

        /// <summary>Distance (m) to the wander target counted as arrived.</summary>
        [SerializeField] private float m_wanderArriveDistance = 0.8f;

        /// <summary>Minimum horizontal distance (m) from the current
        /// position a fresh wander target must be, so the NPC never picks a
        /// point it is already standing on.</summary>
        [SerializeField] private float m_wanderMinTargetDistance = 3f;

        /// <summary>Horizontal speed (m/s) under which the NPC counts as
        /// blocked while it should be walking (wedged against a prop).</summary>
        [SerializeField] private float m_stuckSpeed = 0.2f;

        /// <summary>How long (s) the NPC must stay below m_stuckSpeed while
        /// walking before it gives up on the target and picks a new one.
        /// m_stuckBrakeTime is the short standstill after that.</summary>
        [SerializeField] private float m_stuckTime = 1f;
        [SerializeField] private float m_stuckBrakeTime = 0.3f;

        /// <summary>Max yaw rate (deg/s) the facing controller turns the
        /// NPC toward its travel direction, and the P gain (1/s) turning
        /// the yaw error into that rate.</summary>
        [SerializeField] private float m_turnSpeedDeg = 360f;
        [SerializeField] private float m_turnProportional = 4f;

        /// <summary>Self-righting angular-velocity damping in Wander state.</summary>
        [SerializeField] private float m_uprightDampingWander = 2.0f;

        /// <summary>Self-righting angular-velocity damping in GettingUp state.</summary>
        [SerializeField] private float m_uprightDampingGettingUp = 3.0f;

        /// <summary>Speed threshold (m/s) considered "settled" for state transitions.</summary>
        [SerializeField] private float m_settleSpeed = 0.5f;

        /// <summary>Time (s) the NPC must stay below settleSpeed before GettingUp.</summary>
        [SerializeField] private float m_settleTime = 0.4f;

        /// <summary>Safety cap (s) on time spent in Tumbling. A slowly rocking
        /// NPC can keep its speed just above settleSpeed forever, so the
        /// settle timer never fills and the NPC lies on the ground (Tumbling
        /// applies no righting torque). Past this long the GettingUp
        /// transition is forced. Never cuts the sweep knockdown window short.</summary>
        [SerializeField] private float m_maxTumbleTime = 3.5f;

        /// <summary>Cooldown (s) after a hit during which the NPC ignores new hits.</summary>
        [SerializeField] private float m_hitCooldown = 0.1f;

        /// <summary>Short cooldown (s) after a leg sweep, separate from the
        /// slap hit-cooldown so one sweep does not re-trip every physics tick
        /// while leaving the NPC still slap-able.</summary>
        [SerializeField] private float m_sweepCooldown = 0.3f;

        /// <summary>Kill height (y) below which the NPC despawns.</summary>
        [SerializeField] private float m_killHeight = -10.0f;

        /// <summary>Platform half-size for wander bounds (m). Reads the
        /// shared GameBootstrap arena constant.</summary>
        [SerializeField] private float m_platformHalfSize = GameBootstrap.FloorHalf;

        /// <summary>Margin (m) from platform edge for safe wander points.
        /// 3 m keeps the target limit (±17 m) clear of the fence line at
        /// 19.4 m even when the prop-rejection probe bulges past it.</summary>
        [SerializeField] private float m_wanderMargin = 3f;

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
        // Body proportions (NpcController.Create and NpcBodyBuilder must agree)
        // -------------------------------------------------------------------------

        /// <summary>Distance (m) from the root pivot down to the foot level.
        /// The jointed legs reach this far below the pivot; the torso sits
        /// above it, from hip level (y = 0) to the neck (y = 0.9). Raising
        /// the pivot by exactly this height snaps the feet to the ground on
        /// spawn.</summary>
        private const float kFeetBelowRoot = 0.8f;

        /// <summary>Radius (m) of the invisible support capsule: exactly
        /// the old full-height torso collider's radius (primitive capsule
        /// radius 0.5 scaled by the old 0.7 x/z).</summary>
        private const float kSupportRadius = 0.35f;

        /// <summary>Height (m) of the invisible support capsule: exactly
        /// the old torso collider's world height (2 m primitive scaled
        /// 0.8 tall), centred on the pivot, so its bottom sits at foot
        /// level and the standing root y stays kFeetBelowRoot.</summary>
        private const float kSupportHeight = 2f * kFeetBelowRoot;

        // -------------------------------------------------------------------------
        // Internal state
        // -------------------------------------------------------------------------

        private enum State
        {
            Wander,
            Tumbling,
            GettingUp,
            // Death: the body stays down as a passive ragdoll. Wander
            // forces, self-righting torque and the GettingUp transition
            // are all skipped; only gravity and collisions keep acting.
            Dead
        }

        private State m_state = State.Wander;
        private Rigidbody m_rb;
        private NpcSpawner m_spawner;
        private float m_settleTimer = 0f;
        // Counts up while in Tumbling; forces GettingUp past m_maxTumbleTime.
        private float m_tumbleTimer = 0f;
        private float m_hitCooldownTimer = 0f;
        private float m_sweepCooldownTimer = 0f;
        // Knockdown window: > 0 while a swept NPC must stay on the ground
        // (self-righting suppressed, GettingUp transition blocked).
        private float m_knockdownTimer = 0f;
        private Vector3 m_wanderTarget = Vector3.zero;
        // Phase 5a wander steering state: > 0 while pausing at an arrival,
        // accumulates while walking below m_stuckSpeed, false once a valid
        // target exists. All reset by ResetToWander and state transitions.
        private bool m_hasWanderTarget = false;
        private float m_idleTimer = 0f;
        private float m_stuckTimer = 0f;
        private bool m_isWalking = false;

        // World distance from the root pivot down to the feet. The jointed
        // legs, NOT the torso capsule, reach the floor in the current body
        // proportions; sweep impulses push exactly there.
        private float m_feetHalfHeight = kFeetBelowRoot;

        // The NPC's own torso capsule, built by Create(). Once
        // NpcBodyBuilder.AddLimbs runs, every capsule collider under the
        // root belongs to a limb rigidbody EXCEPT the torso's and the
        // invisible support's (see m_supportCollider), which both attach
        // to the root rigidbody — so GetComponentInChildren<
        // CapsuleCollider>() is no longer safe and every read goes through
        // this cached reference (see SetTorso / EnsureTorsoCollider).
        private CapsuleCollider m_torsoCollider;

        // Invisible full-height support capsule on the root Rigidbody
        // GameObject (no renderer). The short torso no longer reaches the
        // floor and the jointed limbs cannot carry it (loose joints), so
        // the NPC crumples without it; this reproduces the old full-height
        // torso collider exactly (centre on the pivot, radius 0.35, height
        // 1.6, bottom at foot level). Same NPC layer as the torso, so it
        // is also part of the slap hitbox. Assigned by Create().
        private CapsuleCollider m_supportCollider;

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

        // Dedicated skate material for the support capsule's floor contact.
        [System.NonSerialized]
        private PhysicsMaterial m_supportMaterial;

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

        // Health and death (Phase 2b): starts at NpcDamage.MaxHealth and
        // drops per accepted slap; 0 means dead. m_isDead doubles as the
        // Die() re-entry guard and the gate that turns every later OnHit
        // into a no-op.
        private float m_health = NpcDamage.MaxHealth;
        private bool m_isDead = false;

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
        /// Plan A1: place the rigidbody center of mass just above the feet.
        /// The feet sit m_feetHalfHeight below the root pivot; with the
        /// short torso the legs (not the torso capsule) reach the floor, so
        /// the foot level comes from that cached depth instead of the torso
        /// extent. The NPC stays bottom-heavy like an inflatable punching
        /// clown (COM ends up at about -0.5 local Y, unchanged by this
        /// proportions change).
        /// </summary>
        private void ApplyLowCenterOfMass()
        {
            float feetLocalY = -m_feetHalfHeight;
            m_rb.centerOfMass = new Vector3(0f, feetLocalY + m_centerOfMassOffset, 0f);
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
        /// Create(): the torso is the only body collider (besides the
        /// invisible support, excluded via m_supportCollider) whose
        /// attached rigidbody is the NPC root (each limb capsule has its
        /// own rigidbody), so the root-rigidbody match identifies it
        /// unambiguously. Idempotent.
        /// </summary>
        private void EnsureTorsoCollider()
        {
            if (m_torsoCollider != null)
                return;
            CapsuleCollider[] caps = GetComponentsInChildren<CapsuleCollider>();
            for (int i = 0; i < caps.Length; i++)
            {
                if (caps[i] != null && caps[i] != m_supportCollider
                    && caps[i].attachedRigidbody == m_rb)
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

            // The feet sit m_feetHalfHeight (= kFeetBelowRoot) below the
            // pivot by body design: the jointed legs reach the floor, the
            // short torso does not, so there is no collider formula here.
            // The cached torso collider (NOT GetComponentInChildren) keeps
            // limb capsules out and only feeds the bounce material.
            EnsureTorsoCollider();
            var bodyCapsule = m_torsoCollider;

            // Silly physics: one bouncy material on the body collider, plus a
            // squash pivot that is safe to scale (null for primitive NPCs).
            SetupBounceMaterial(bodyCapsule);
            // The support capsule (not the short torso) is what stands on
            // the floor. It gets a dedicated low-friction 'skate' material,
            // NOT the shared bounce instance: with that one (dynamicFriction
            // 0.6, Average combine against the floor's default 0.6) ground
            // friction removes up to mu*N*dt = 0.6*3.1*9.81*0.02 = 0.36 N*s
            // of momentum per physics step - more than SteerHorizontal can
            // add (8 m/s^2 * 0.02 s on the 1.5 kg root = 0.24 N*s) - so
            // static friction re-grips every step and the NPC never passes
            // ~0.2 m/s. Skate friction (0.05, Minimum combine) removes
            // 0.03 N*s per step. Bounce feel stays on the torso material.
            if (m_supportCollider != null)
                m_supportCollider.material = CreateSupportMaterial();
            SetupSquashPivot();
        }

        /// <summary>
        /// Low-friction 'skate' material for the support capsule. Created
        /// once per controller. Minimum combine so the low value wins
        /// against the floor's default 0.6 material.
        /// </summary>
        private PhysicsMaterial CreateSupportMaterial()
        {
            if (m_supportMaterial == null)
            {
                m_supportMaterial = new PhysicsMaterial(
                    name + "_SupportSkate");
                m_supportMaterial.staticFriction = 0.05f;
                m_supportMaterial.dynamicFriction = 0.05f;
                m_supportMaterial.frictionCombine =
                    PhysicsMaterialCombine.Minimum;
                m_supportMaterial.bounciness = 0f;
                m_supportMaterial.bounceCombine =
                    PhysicsMaterialCombine.Minimum;
            }
            return m_supportMaterial;
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

                case State.Dead:
                    // Passive ragdoll: gravity and collisions keep acting,
                    // and the unconditional CheckDespawn above still
                    // recycles a corpse that slides off the platform, but
                    // there is no wander push, no self-righting torque and
                    // no transition check — the body stays exactly where
                    // the killing slap left it.
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

        /// <summary>True while the NPC is actively steering toward its
        /// wander target (false during idle pauses). Read by photo mode
        /// and EditMode tests.</summary>
        public bool IsWalking => m_isWalking;

        /// <summary>The point the NPC is walking to this phase (read
        /// only; photo mode / EditMode tests).</summary>
        public Vector3 WanderTarget => m_wanderTarget;

        /// <summary>Angle (degrees) between this NPC's up axis and world up.</summary>
        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);

        /// <summary>Rigidbody speed (m/s); 0 before the body is cached.</summary>
        public float Speed => m_rb != null ? m_rb.linearVelocity.magnitude : 0f;

        /// <summary>Current health (NpcDamage.MaxHealth when fresh, 0 once dead).</summary>
        public float Health => m_health;

        /// <summary>True once this NPC has died and stays down as a ragdoll.</summary>
        public bool IsDead => m_isDead;

        /// <summary>Raised exactly once when this NPC's health reaches 0.</summary>
        public event System.Action<NpcController> Died;

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
            m_tumbleTimer = 0f;
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
            // Dead NPCs ignore every further hit: the corpse is scenery.
            if (m_isDead)
                return;

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

                    // Health (Phase 2b): an accepted slap subtracts
                    // NpcDamage damage; a sweep never reaches this branch,
                    // and Compute returns 0 for any non-slap kind anyway.
                    // Zero health kills the NPC in place.
                    m_health = NpcDamage.Apply(m_health,
                        NpcDamage.Compute(hit.Kind, hit.Strength));
                    if (NpcDamage.IsDead(m_health))
                        Die();
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
            m_tumbleTimer = 0f;

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
                m_tumbleTimer = 0f;
                m_isWalking = false;
                return; // no wander push while fallen
            }

            // Phase 5a: the cached player lookup is retained for the
            // later aggro work, but wandering no longer depends on the
            // player existing or being near, and no longer stops when a
            // player stands in front of the NPC.
            GetCachedPlayerTransform();

            float dt = Time.fixedDeltaTime;

            // Idle pause after reaching a target (also the short brake
            // after getting wedged on a prop): drive the horizontal
            // velocity to zero and stand still.
            if (m_idleTimer > 0f)
            {
                m_idleTimer -= dt;
                m_isWalking = false;
                m_stuckTimer = 0f;
                SteerWander(Vector3.zero, dt);
                return;
            }

            Vector3 flatToTarget = m_wanderTarget - transform.position;
            flatToTarget.y = 0f; // Only move on XZ plane.

            // No target yet (fresh spawn / recycled NPC), the target
            // turned invalid, or close enough to call it an arrival:
            // pick a fresh point, then idle a moment before walking.
            if (!m_hasWanderTarget
                || flatToTarget.magnitude < m_wanderArriveDistance
                || !IsValidWanderPoint(m_wanderTarget))
            {
                m_wanderTarget = RandomWanderPoint();
                m_hasWanderTarget = true;
                m_idleTimer = Random.Range(m_idlePauseMin, m_idlePauseMax);
                m_isWalking = false;
                m_stuckTimer = 0f;
                SteerWander(Vector3.zero, dt);
                return;
            }

            Vector3 dir = flatToTarget.normalized;

            // Walking straight into a prop (trees/rocks are Environment;
            // a horizontal ray 0.5 m up never clips the floor collider,
            // whose top sits at y 0): drop this target immediately and
            // steer toward a fresh one, no idle pause.
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir,
                                1f, GameLayers.MaskEnvironment))
            {
                m_wanderTarget = RandomWanderPoint();
                m_hasWanderTarget = true;
                flatToTarget = m_wanderTarget - transform.position;
                flatToTarget.y = 0f;
                dir = flatToTarget.normalized;
            }

            // Stuck handling: should be walking but barely moving
            // (wedged against a prop): after m_stuckTime, take a new
            // target and brake for m_stuckBrakeTime.
            Vector3 velocity = m_rb.linearVelocity;
            Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
            if (horizVel.magnitude < m_stuckSpeed)
            {
                m_stuckTimer += dt;
                if (m_stuckTimer >= m_stuckTime)
                {
                    m_wanderTarget = RandomWanderPoint();
                    m_hasWanderTarget = true;
                    m_idleTimer = m_stuckBrakeTime;
                    m_stuckTimer = 0f;
                    m_isWalking = false;
                    SteerWander(Vector3.zero, dt);
                    return;
                }
            }
            else
            {
                m_stuckTimer = 0f;
            }

            // Velocity steering (Phase 5a): the capsule collider's
            // ground friction plus linear damping swamps any push force
            // a mass-1.5 body can apply (the old 5 N AddForce produced
            // ~0 m/s), so the horizontal velocity is driven directly
            // toward the walk pace. linearVelocity.y is left untouched
            // for gravity, collisions and hang-time.
            Vector3 steered = NpcWander.SteerHorizontal(
                horizVel, dir * m_wanderSpeed, m_wanderAccel, dt);
            m_rb.linearVelocity = new Vector3(steered.x, velocity.y, steered.z);
            m_isWalking = true;

            // Facing: turn toward the travel direction by yaw only —
            // +z forward faces the target (NpcBodyBuilder's layout).
            // Torque about world up, not MoveRotation: MoveRotation
            // would teleport a dynamic body and fight collisions. The
            // P control adds the righting damping's yaw bite back
            // (ApplySelfRighting subtracts angularVelocity * damping;
            // its own yaw component is ~0 for a near-upright body, so
            // the two torques compose without fighting).
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                float yawErrorDeg =
                    Vector3.SignedAngle(flatForward, dir, Vector3.up);
                float desiredSpinRad = Mathf.Deg2Rad * Mathf.Clamp(
                    yawErrorDeg * m_turnProportional,
                    -m_turnSpeedDeg, m_turnSpeedDeg);
                float spinError = desiredSpinRad
                    + m_uprightDampingWander
                      * Vector3.Dot(m_rb.angularVelocity, Vector3.up);
                m_rb.AddTorque(Vector3.up * (spinError * m_rb.mass),
                    ForceMode.Force);
            }
        }

        /// <summary>
        /// Drives the horizontal velocity toward desiredHorizontal via
        /// the shared pure helper; linearVelocity.y stays untouched.
        /// </summary>
        private void SteerWander(Vector3 desiredHorizontal, float dt)
        {
            Vector3 velocity = m_rb.linearVelocity;
            Vector3 steered = NpcWander.SteerHorizontal(
                new Vector3(velocity.x, 0f, velocity.z), desiredHorizontal,
                m_wanderAccel, dt);
            m_rb.linearVelocity = new Vector3(steered.x, velocity.y, steered.z);
        }

        private Vector3 RandomWanderPoint()
        {
            Vector3 point = Vector3.zero;
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

        /// <summary>
        /// A wander target must sit on the platform, at least
        /// m_wanderMinTargetDistance from where the NPC stands, and away
        /// from Environment props: tree trunks (2.2 m cylinders) and
        /// rocks are Environment colliders, so the probe is a 1.2 m
        /// radius sphere centred 1.5 m up (its bottom at 0.3 m stays
        /// clear of the floor collider — GameBootstrap.CreateFloor puts a
        /// unit cube centred at y -0.5, top at y 0 — while clipping
        /// every prop; 1.2 m is the ~1.5 m specced stand-off).
        /// </summary>
        private bool IsValidWanderPoint(Vector3 point)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.magnitude < m_wanderMinTargetDistance)
                return false;
            if (Mathf.Abs(point.x) > m_platformHalfSize
                || Mathf.Abs(point.z) > m_platformHalfSize)
                return false; // Ensure it's on the platform.
            return !Physics.CheckSphere(
                point + Vector3.up * 1.5f, 1.2f, GameLayers.MaskEnvironment);
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

            // Max-tumble safety: a slowly rocking NPC can hold its speed just
            // above settleSpeed forever, so the settle timer below never
            // fills and the body would lie flat (Tumbling applies no righting
            // torque). Cap the Tumbling time and force the same GettingUp
            // transition the settle path performs. The knockdown countdown
            // returned above, so it is never cut short, and the m_isDead
            // check keeps a dead NPC passive.
            m_tumbleTimer += Time.fixedDeltaTime;
            if (!m_isDead && m_tumbleTimer > m_maxTumbleTime)
            {
                EnterGettingUp();
                return;
            }

            if (m_rb.linearVelocity.magnitude < m_settleSpeed)
            {
                m_settleTimer += Time.fixedDeltaTime;
                if (m_settleTimer >= m_settleTime)
                    EnterGettingUp();
            }
            else
            {
                m_settleTimer = 0f;
            }
        }

        /// <summary>
        /// Shared Tumbling -> GettingUp transition, used by the normal
        /// settle path and by the m_maxTumbleTime safety above.
        /// </summary>
        private void EnterGettingUp()
        {
            m_state = State.GettingUp;
            m_settleTimer = 0f;
            m_tumbleTimer = 0f;
            // The recovery animation plays with a strained face.
            if (m_face != null)
                m_face.SetExpression(NpcFace.Expression.Recover, 1.5f);
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
                    // Phase 5a: start the steering clean; a fresh target
                    // is picked on the first UpdateWander tick.
                    m_hasWanderTarget = false;
                    m_idleTimer = 0f;
                    m_stuckTimer = 0f;
                    m_isWalking = false;
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

        /// <summary>
        /// Kill the NPC in place (Phase 2b): it stays down as a passive
        /// ragdoll until the kill-height path in CheckDespawn or the
        /// spawner's corpse cap eventually destroys it. Idempotent —
        /// later hits never even reach it because OnHit early-outs once
        /// the NPC is dead.
        /// </summary>
        private void Die()
        {
            if (m_isDead)
                return;
            m_isDead = true;
            m_state = State.Dead;
            // End the silly hang-time window so kill gravity pulls the body
            // straight down; the knockdown/settle/tumble timers no longer
            // matter because the Dead state skips every transition check.
            m_hangTimer = 0f;
            m_tumbleTimer = 0f;

            // NpcFace has no dead expression: Dizzy wins, with a
            // PositiveInfinity duration that never counts below zero in its
            // return-to-Idle countdown. m_face is a MonoBehaviour: explicit
            // null check, never ?..
            if (m_face != null)
                m_face.SetExpression(NpcFace.Expression.Dizzy, float.PositiveInfinity);

            // The killing slap already yelped in OnHit's accepted branch;
            // no second sound here (keeping it simple).

            // Announce the death, then tell the spawner this NPC no longer
            // counts as alive and hand the corpse to its cap. Do NOT
            // Destroy here and do NOT set m_despawned: a body that later
            // slides below m_killHeight must still go through Despawn(),
            // and the spawner's m_alive set dedupes that NotifyDespawn.
            Died?.Invoke(this);
            if (m_spawner != null)
            {
                m_spawner.NotifyDespawn(this);
                m_spawner.RegisterCorpse(this);
            }
        }

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
            m_tumbleTimer = 0f;
            m_hitCooldownTimer = 0f;
            m_sweepCooldownTimer = 0f;
            m_knockdownTimer = 0f;
            m_despawned = false;
            // Phase 5a: clear the wander steering state so a recycled
            // NPC picks a fresh target instead of inheriting a pause.
            m_hasWanderTarget = false;
            m_idleTimer = 0f;
            m_stuckTimer = 0f;
            m_isWalking = false;
            // Phase 2b: a recycled NPC must not inherit a dead flag or
            // missing health.
            m_isDead = false;
            m_health = NpcDamage.MaxHealth;
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

            // Short torso: radius 0.35, spanning y = 0 (hip level) up to
            // 0.9 (neck), so the jointed legs hanging from the hips down to
            // -0.8 stay visible for their whole reach. The primitive
            // CapsuleCollider (height 2, radius 0.5, never overridden here)
            // scales with the transform, so it still matches the mesh. The
            // head (unchanged: y = 1, radius 0.3, bottom at 0.7) overlaps
            // the torso top at 0.9; head top stays 1.3, total height 2.1 m.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);
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

            // Invisible support capsule on the root Rigidbody GameObject
            // (AddComponent: no renderer, never visible). Before the torso
            // was shortened, the torso collider itself spanned y = -0.8 to
            // +0.8 around the pivot and held the NPC up; now only the
            // jointed limb rigidbodies reach the floor and they cannot
            // carry the torso through their loose joints, so the NPC
            // crumples. Recreate the old extents exactly: centre on the
            // pivot, radius 0.35, height 1.6, so the bottom sits at foot
            // level (-0.8) and the standing root y is kFeetBelowRoot. Must
            // run BEFORE NpcBodyBuilder.AddLimbs so the ignore-collision
            // pass (GetComponentsInChildren<Collider>) covers it and limbs
            // hang through it. Being on the root GameObject it shares the
            // NPC layer (slap queries use GameLayers.MaskNPC) and the root
            // carries NpcController, so a hit here resolves through
            // GetComponentInParent<ISlappable> exactly like a torso hit.
            var support = go.AddComponent<CapsuleCollider>();
            support.radius = kSupportRadius;
            support.height = kSupportHeight;
            support.center = Vector3.zero;

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

            // Plan A4: snap feet to y=0 on spawn. The foot level sits
            // kFeetBelowRoot below the pivot (the legs reach there, the
            // short torso no longer does), so raise the pivot exactly that
            // high — the same 0.8 m the old full-height torso collider gave
            // via bounds.extents.y. No collider/bounds read here, so the
            // stale-bounds-after-creation trap and the fresh limb capsules
            // cannot change the snap value.
            var capsule = body.GetComponent<CapsuleCollider>();
            go.transform.position = new Vector3(position.x, kFeetBelowRoot, position.z);

            var controller = go.AddComponent<NpcController>();
            controller.m_spawner = spawner;
            // Cache the physics-critical/behaviour references before any
            // controller code can run (Awake already fired ApplyLowCenter-
            // OfMass during AddComponent, and Create repeats it below).
            controller.SetTorso(capsule);
            controller.m_supportCollider = support;
            controller.SetFace(face);

            // Plan A1: set center of mass after controller is created.
            controller.ApplyLowCenterOfMass();

            return controller;
        }
    }
}
