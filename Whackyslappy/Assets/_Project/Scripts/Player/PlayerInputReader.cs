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
