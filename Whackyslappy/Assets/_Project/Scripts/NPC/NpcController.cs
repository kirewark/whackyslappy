using System.Collections;
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

        /// <summary>Self-righting torque multiplier in Wander state.</summary>
        [SerializeField] private float m_uprightStrengthWander = 8.0f;

        /// <summary>Self-righting angular-velocity damping.</summary>
        [SerializeField] private float m_uprightDampingWander = 2.0f;

        /// <summary>Self-righting torque multiplier in GettingUp state.</summary>
        [SerializeField] private float m_uprightStrengthGettingUp = 15.0f;

        /// <summary>Self-righting angular-velocity damping in GettingUp state.</summary>
        [SerializeField] private float m_uprightDampingGettingUp = 3.0f;

        /// <summary>Speed threshold (m/s) considered "settled" for state transitions.</summary>
        [SerializeField] private float m_settleSpeed = 0.3f;

        /// <summary>Time (s) the NPC must stay below settleSpeed before GettingUp.</summary>
        [SerializeField] private float m_settleTime = 1.0f;

        /// <summary>Cooldown (s) after a hit during which the NPC ignores new hits.</summary>
        [SerializeField] private float m_hitCooldown = 0.1f;

        /// <summary>Kill height (y) below which the NPC despawns.</summary>
        [SerializeField] private float m_killHeight = -10.0f;

        /// <summary>Platform half-size for wander bounds (m). Platform is 20x20 per §6.</summary>
        [SerializeField] private float m_platformHalfSize = 10.0f;

        /// <summary>Margin (m) from platform edge for safe wander points.</summary>
        [SerializeField] private float m_wanderMargin = 1.5f;

        /// <summary>Color for the head mesh (randomized on spawn).</summary>
        [SerializeField] private Color m_headColor = Color.cyan;

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
        private Collider m_headCollider;
        private NpcSpawner m_spawner;
        private float m_settleTimer = 0f;
        private float m_hitCooldownTimer = 0f;
        private Vector3 m_wanderTarget = Vector3.zero;

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
        }

        private void Start()
        {
            // Grab the spawner reference (created once per scene by NpcBootstrap).
            m_spawner = Object.FindAnyObjectByType<NpcSpawner>();

            // Assign head collider for hit-cooldown filtering.
            m_headCollider = GetComponentInChildren<Collider>();
        }

        private void FixedUpdate()
        {
            // Hit cooldown countdown.
            if (m_hitCooldownTimer > 0f)
                m_hitCooldownTimer -= Time.fixedDeltaTime;

            switch (m_state)
            {
                case State.Wander:
                    UpdateWander();
                    ApplySelfRighting(m_uprightStrengthWander, m_uprightDampingWander);
                    CheckTransitionToTumbling();
                    break;

                case State.Tumbling:
                    // Pure physics — do nothing here.
                    CheckTransitionToGettingUp();
                    CheckDespawn();
                    break;

                case State.GettingUp:
                    ApplySelfRighting(m_uprightStrengthGettingUp, m_uprightDampingGettingUp);
                    CheckTransitionToWander();
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // ISlappable implementation
        // -------------------------------------------------------------------------

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

        // -------------------------------------------------------------------------
        // Wander logic
        // -------------------------------------------------------------------------

        private void UpdateWander()
        {
            // Find the player for distance check and direction.
            var player = Object.FindAnyObjectByType<CharacterController>();

            if (player != null)
            {
                float dist = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(player.transform.position.x, 0, player.transform.position.z));

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
        /// Applies a torque that keeps the NPC upright.
        /// torque = Cross(up, up_world) * strength - angularVelocity * damping
        /// </summary>
        private void ApplySelfRighting(float strength, float damping)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            Vector3 torque = cross * strength - m_rb.angularVelocity * damping;
            m_rb.AddTorque(torque, ForceMode.Force);
        }

        // -------------------------------------------------------------------------
        // State transitions
        // -------------------------------------------------------------------------

        /// <summary>
        /// In Wander: if the NPC gets hit (detected by tumbling detection)
        /// it transitions to Tumbling. The actual hit goes through OnSlapped,
        /// but this acts as a safety net if the hit was missed by the slap system.
        /// </summary>
        private void CheckTransitionToTumbling()
        {
            // If speed spikes significantly, the NPC was likely hit by something.
            // However, the primary path is via OnSlapped from the slap system.
            // We'll just check for a high speed as a fallback.
            if (m_rb.linearVelocity.magnitude > 4.0f)
            {
                m_state = State.Tumbling;
                m_hitCooldownTimer = m_hitCooldown;
                m_settleTimer = 0f;
            }
        }

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

            if (m_rb.linearVelocity.magnitude < m_settleSpeed)
            {
                m_settleTimer += Time.fixedDeltaTime;
                if (m_settleTimer >= m_settleTime)
                {
                    m_state = State.GettingUp;
                    m_settleTimer = 0f;
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
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.identity;
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
            var bodyMat = new Material(body.GetComponent<Renderer>().sharedMaterial);
            bodyMat.color = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
            body.GetComponent<Renderer>().material = bodyMat;

            // Sphere head child.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, 1f, 0);
            head.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            head.layer = GameLayers.NPC;
            var headMat = new Material(head.GetComponent<Renderer>().sharedMaterial);
            headMat.color = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
            head.GetComponent<Renderer>().material = headMat;

            // One non-kinematic Rigidbody on root.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var controller = go.AddComponent<NpcController>();
            controller.m_spawner = spawner;

            return controller;
        }
    }
}
