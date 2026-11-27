using UnityEngine;
using UnityEngine.InputSystem;

namespace WhackySlappy.Player
{
    /// <summary>
    /// First-person character controller using CharacterController.
    /// Requires a PlayerInputReader component on the same GameObject.
    /// Supports walk, sprint, jump, and mouse look.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private float m_walkSpeed = 5f;
        [SerializeField] private float m_sprintMultiplier = 1.6f;
        [SerializeField] private float m_jumpHeight = 1f;
        [SerializeField] private float m_gravity = -20f;
        [SerializeField] private float m_mouseSensitivity = 0.1f;
        [SerializeField] private float m_pitchClamp = 85f;
        [SerializeField] private float m_slapHeldLookScale = 0.5f;
        [SerializeField] private float m_bumpMaxDeg = 5f;

        private PlayerInputReader m_inputReader;
        private CharacterController m_controller;
        private float m_verticalVelocity;
        private float m_pitch;

        // Additive camera-bump state (NPC strike feedback); never touches
        // m_pitch itself, so mouse-look keeps working during the kick.
        private float m_bumpPitch;
        private float m_bumpRoll;
        private float m_bumpDuration = 0.12f;
        private float m_bumpElapsed;
        private bool m_bumpActive;

        /// <summary>
        /// The camera pivot transform to rotate for pitch. Set by GameBootstrap.
        /// </summary>
        public Transform CameraPivot { get; set; }

        /// <summary>
        /// The player's current world-space velocity (set each Update).
        /// </summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>
        /// Current clamped look pitch in degrees, before camera-kick offsets.
        /// HitFeedback adds its kick on top of this in LateUpdate.
        /// </summary>
        public float Pitch => m_pitch;

        /// <summary>
        /// When true, mouse look input is scaled by m_slapHeldLookScale
        /// instead of applied at full speed (movement is unaffected).
        /// The directional slap sets this while the left mouse button is held
        /// so the drag feeds the aim vector while the view still drifts slowly.
        /// </summary>
        public bool LookSuppressed { get; set; }

        private void Awake()
        {
            m_controller = GetComponent<CharacterController>();
            m_inputReader = GetComponent<PlayerInputReader>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleCursorLock();
            HandleLook();
            bool grounded = m_controller.isGrounded;
            if (grounded && m_verticalVelocity < 0f) m_verticalVelocity = -2f;
            if (grounded && m_inputReader.JumpPressedThisFrame) m_verticalVelocity = Mathf.Sqrt(2f * -m_gravity * m_jumpHeight);
            m_verticalVelocity += m_gravity * Time.deltaTime;
            Vector2 input = m_inputReader.Move;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y);
            if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();
            float speed = m_walkSpeed * (m_inputReader.SprintHeld ? m_sprintMultiplier : 1f);
            Vector3 velocity = horizontal * speed + Vector3.up * m_verticalVelocity;
            m_controller.Move(velocity * Time.deltaTime);
            Velocity = m_controller.velocity;
        }

        /// <summary>
        /// Handles cursor lock/unlock with Escape and left mouse click.
        /// </summary>
        private void HandleCursorLock()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            // Re-lock on left mouse click when unlocked.
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame &&
                    Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        /// <summary>
        /// Applies mouse look: yaw on the player body, pitch on the camera pivot.
        /// Only runs when the cursor is locked.
        /// </summary>
        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            // On the frame the slap button is first pressed, the mouse delta
            // of that frame must not turn the camera.
            if (m_inputReader.SlapPressed)
                return;

            // While a slap aim hold is in progress the mouse delta also feeds
            // the aim vector; slow look instead of freezing it so the view
            // drifts with the drag and does not jump when the hold ends.
            float lookScale = LookSuppressed ? m_slapHeldLookScale : 1f;

            var look = m_inputReader.Look;
            if (look == Vector2.zero)
                return;

            // Yaw: rotate player body around local Y.
            transform.Rotate(0f, look.x * m_mouseSensitivity * lookScale, 0f);

            // Pitch: clamp and apply to camera pivot.
            m_pitch = Mathf.Clamp(m_pitch - look.y * m_mouseSensitivity * lookScale, -m_pitchClamp, m_pitchClamp);
            if (CameraPivot != null)
                CameraPivot.localEulerAngles = new Vector3(m_pitch, 0f, 0f);
        }

        /// <summary>
        /// Queue a short decaying pitch/roll kick on the camera pivot, used
        /// for player-side NPC-strike feedback (StrikeFeedback). The offset
        /// is added on top of m_pitch in LateUpdate and decays to zero, so
        /// the stored pitch/yaw are never mutated and mouse-look continues
        /// to work normally during and after the bump. Re-triggers add up
        /// but are clamped to m_bumpMaxDeg per axis.
        /// </summary>
        /// <param name="pitchDeg">Signed pitch offset in degrees (positive = up).</param>
        /// <param name="rollDeg">Signed roll offset in degrees.</param>
        /// <param name="duration">Decay duration in seconds; defaults to 0.12 s if not positive.</param>
        public void AddCameraBump(float pitchDeg, float rollDeg, float duration = 0.12f)
        {
            if (duration <= 0f)
                duration = 0.12f;
            m_bumpPitch = Mathf.Clamp(m_bumpPitch + pitchDeg, -m_bumpMaxDeg, m_bumpMaxDeg);
            m_bumpRoll = Mathf.Clamp(m_bumpRoll + rollDeg, -m_bumpMaxDeg, m_bumpMaxDeg);
            m_bumpDuration = duration;
            m_bumpElapsed = 0f;
            m_bumpActive = true;
        }

        private void LateUpdate()
        {
            if (!m_bumpActive)
                return;

            m_bumpElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_bumpElapsed / m_bumpDuration);

            // Quadratic ease-out; at t=1 the offsets are zeroed and the
            // component stops writing the pivot until the next bump.
            float decay = 1f - t * t;
            float pitchKick = m_bumpPitch * decay;
            float rollKick = m_bumpRoll * decay;

            if (CameraPivot != null)
                CameraPivot.localEulerAngles = new Vector3(m_pitch + pitchKick, 0f, rollKick);

            if (t >= 1f)
            {
                m_bumpActive = false;
                m_bumpPitch = 0f;
                m_bumpRoll = 0f;
            }
        }
    }
}
