using UnityEngine;
using UnityEngine.InputSystem;

namespace WhackySlappy.Player
{
    /// <summary>
    /// Reads input from the Player action map (Move, Look, Jump, Sprint, Attack).
    /// Creates and manages the input actions in code — does not require a
    /// generated C# class from the .inputactions file.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private InputActionMap m_map;
        private InputAction m_moveAction;
        private InputAction m_lookAction;
        private InputAction m_jumpAction;
        private InputAction m_sprintAction;
        private InputAction m_attackAction;
        private InputAction m_sweepAction;

        private Vector2 m_aimDelta;

        [SerializeField, Min(1f)] private float m_maxAimMagnitude = 300f;

        /// <summary>
        /// Horizontal + vertical move input (-1 to 1), from WASD / left stick.
        /// </summary>
        public Vector2 Move => m_moveAction != null ? m_moveAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>
        /// Mouse/gamepad look delta, from mouse delta / right stick.
        /// </summary>
        public Vector2 Look => m_lookAction != null ? m_lookAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>
        /// True during the frame Jump is pressed.
        /// </summary>
        public bool JumpPressedThisFrame => m_jumpAction != null && m_jumpAction.WasPressedThisFrame();

        /// <summary>
        /// True while Sprint is held.
        /// </summary>
        public bool SprintHeld => m_sprintAction != null && m_sprintAction.IsPressed();

        /// <summary>
        /// True during the frame Attack is pressed.
        /// </summary>
        public bool AttackPressedThisFrame => m_attackAction != null && m_attackAction.WasPressedThisFrame();

        /// <summary>
        /// True during the frame the right mouse button (leg sweep) is pressed.
        /// </summary>
        public bool SweepPressed => m_sweepAction != null && m_sweepAction.WasPressedThisFrame();

        /// <summary>
        /// True while the left mouse button (slap) is held.
        /// </summary>
        public bool SlapHeld => m_attackAction != null && m_attackAction.IsPressed();

        /// <summary>
        /// True during the frame the left mouse button (slap) is pressed.
        /// </summary>
        public bool SlapPressed => m_attackAction != null && m_attackAction.WasPressedThisFrame();

        /// <summary>
        /// True during the frame the left mouse button (slap) is released.
        /// </summary>
        public bool SlapReleased => m_attackAction != null && m_attackAction.WasReleasedThisFrame();

        /// <summary>
        /// Mouse look delta accumulated in pixels while SlapHeld, clamped to
        /// m_maxAimMagnitude. Reset by ResetAim or ConsumeAim.
        /// </summary>
        public Vector2 AimDelta => m_aimDelta;

        /// <summary>
        /// Clears the accumulated aim drag (call when a swing starts or is cancelled).
        /// </summary>
        public void ResetAim()
        {
            m_aimDelta = Vector2.zero;
        }

        /// <summary>
        /// Returns the accumulated aim drag and clears it in one step.
        /// </summary>
        public Vector2 ConsumeAim()
        {
            Vector2 aim = m_aimDelta;
            m_aimDelta = Vector2.zero;
            return aim;
        }

        private void Awake()
        {
            m_map = new InputActionMap("Player");

            m_moveAction = m_map.AddAction("Move", InputActionType.Value);
            m_moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            m_moveAction.AddBinding("<Gamepad>/leftStick");

            m_lookAction = m_map.AddAction("Look", InputActionType.Value);
            m_lookAction.AddBinding("<Mouse>/delta");
            m_lookAction.AddBinding("<Gamepad>/rightStick");

            m_jumpAction = m_map.AddAction("Jump", InputActionType.Button);
            m_jumpAction.AddBinding("<Keyboard>/space");
            m_jumpAction.AddBinding("<Gamepad>/buttonSouth");

            m_sprintAction = m_map.AddAction("Sprint", InputActionType.Button);
            m_sprintAction.AddBinding("<Keyboard>/leftShift");
            m_sprintAction.AddBinding("<Gamepad>/leftStickPress");

            m_attackAction = m_map.AddAction("Attack", InputActionType.Button);
            m_attackAction.AddBinding("<Mouse>/leftButton");
            m_attackAction.AddBinding("<Gamepad>/buttonWest");

            m_sweepAction = m_map.AddAction("Sweep", InputActionType.Button);
            m_sweepAction.AddBinding("<Mouse>/rightButton");
        }

        private void Update()
        {
            if (!SlapHeld || m_lookAction == null)
            {
                return;
            }

            m_aimDelta += m_lookAction.ReadValue<Vector2>();
            m_aimDelta = Vector2.ClampMagnitude(m_aimDelta, m_maxAimMagnitude);
        }

        private void OnEnable()
        {
            m_map?.Enable();
        }

        private void OnDisable()
        {
            m_map?.Disable();
        }

        private void OnDestroy()
        {
            m_map?.Dispose();
        }
    }
}
