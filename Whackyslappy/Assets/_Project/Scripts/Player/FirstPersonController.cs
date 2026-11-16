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

        private PlayerInputReader m_inputReader;
        private CharacterController m_controller;
        private float m_verticalVelocity;
        private float m_pitch;

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
        /// When true, mouse look input is ignored (movement is unaffected).
        /// The directional slap sets this while the left mouse button is held
        /// so the drag feeds the aim vector instead of turning the camera.
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

            // While a slap aim drag is in progress, the mouse delta belongs
            // to the aim vector, not the camera. Skip applying look input.
            if (LookSuppressed)
                return;

            // On the frame the slap button is first pressed, the mouse delta
            // of that frame must not turn the camera.
            if (m_inputReader.SlapPressed)
                return;

            var look = m_inputReader.Look;
            if (look == Vector2.zero)
                return;

            // Yaw: rotate player body around local Y.
            transform.Rotate(0f, look.x * m_mouseSensitivity, 0f);

            // Pitch: clamp and apply to camera pivot.
            m_pitch = Mathf.Clamp(m_pitch - look.y * m_mouseSensitivity, -m_pitchClamp, m_pitchClamp);
            if (CameraPivot != null)
                CameraPivot.localEulerAngles = new Vector3(m_pitch, 0f, 0f);
        }
    }
}
