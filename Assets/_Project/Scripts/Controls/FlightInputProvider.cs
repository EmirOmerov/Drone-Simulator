using DroneSim.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSim.Controls
{
    public enum InputDeviceKind
    {
        None,
        Gamepad,
        Keyboard
    }

    /// <summary>
    /// Reads the PS5 DualSense (or any gamepad the Input System recognises) and the keyboard,
    /// and turns them into one normalized <see cref="FlightInput"/>.
    ///
    /// Gamepad, Mode 2: left stick = throttle (Y) / yaw (X), right stick = pitch (Y) / roll (X).
    /// Keyboard: W/S throttle, A/D yaw, arrow keys pitch/roll.
    /// Buttons: see the *Requested properties (sport mode, camera, pause, reset).
    ///
    /// Whichever device was used last is active. If the gamepad disconnects, input drops to zero,
    /// which the angle-mode controller turns into a level hover.
    /// </summary>
    public sealed class FlightInputProvider : MonoBehaviour, IFlightInputSource
    {
        [Header("Gamepad")]
        [SerializeField, Range(0f, 0.5f)] float stickDeadzone = 0.08f;
        [SerializeField, Range(0f, 1f)] float throttleExpo = 0.2f;
        [SerializeField, Range(0f, 1f)] float yawExpo = 0.3f;
        [SerializeField, Range(0f, 1f)] float pitchRollExpo = 0.35f;

        [Header("Keyboard")]
        [Tooltip("How fast a held key ramps the axis toward full deflection (units/s).")]
        [SerializeField] float keyRiseRate = 2.5f;
        [Tooltip("How fast the axis returns to centre when the key is released (units/s).")]
        [SerializeField] float keyReturnRate = 5f;

        const float ActivityThreshold = 0.25f;

        public FlightInput Current { get; private set; } = FlightInput.Zero;

        /// <summary>Unprocessed device values of the active device, for the debug overlay.</summary>
        public FlightInput Raw { get; private set; } = FlightInput.Zero;

        public InputDeviceKind ActiveDevice { get; private set; } = InputDeviceKind.None;
        public string ActiveDeviceName { get; private set; } = "none";

        // One-frame button events. Gamepad / keyboard:
        /// <summary>Reset drone to the start pad: R (gamepad: via pause menu).</summary>
        public bool ResetRequested { get; private set; }
        /// <summary>Toggle Normal / Sport flight mode: R1 / Left Shift.</summary>
        public bool SportToggleRequested { get; private set; }
        /// <summary>Toggle chase / FPV camera: Triangle / C.</summary>
        public bool CameraToggleRequested { get; private set; }
        /// <summary>Open / close the pause menu: Options / Esc.</summary>
        public bool PauseRequested { get; private set; }

        float keyThrottle, keyYaw, keyPitch, keyRoll;

        void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
        void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                Debug.Log($"[DroneSim] Input device lost: {device.displayName}");
            else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                Debug.Log($"[DroneSim] Input device connected: {device.displayName} ({device.layout})");
        }

        void Update() => Poll(Time.unscaledDeltaTime);

        /// <summary>Reads devices and updates <see cref="Current"/>. Called every frame; public so tests can drive it.</summary>
        public void Poll(float deltaTime)
        {
            var gamepad = Gamepad.current;
            var keyboard = Keyboard.current;

            FlightInput gamepadRaw = gamepad != null ? ReadGamepadRaw(gamepad) : FlightInput.Zero;
            FlightInput keyboardRaw = keyboard != null ? ReadKeyboardRaw(keyboard) : FlightInput.Zero;

            UpdateKeyboardSmoothing(keyboardRaw, deltaTime);
            UpdateActiveDevice(gamepad, gamepadRaw, keyboard, keyboardRaw);

            switch (ActiveDevice)
            {
                case InputDeviceKind.Gamepad when gamepad != null:
                    Raw = gamepadRaw;
                    Current = ShapeGamepad(gamepad);
                    break;
                case InputDeviceKind.Keyboard when keyboard != null:
                    Raw = keyboardRaw;
                    Current = new FlightInput(keyThrottle, keyYaw, keyPitch, keyRoll);
                    break;
                default:
                    // Active device vanished (e.g. Bluetooth dropped): neutral sticks = hover.
                    Raw = FlightInput.Zero;
                    Current = FlightInput.Zero;
                    break;
            }

            ResetRequested = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            SportToggleRequested =
                (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame) ||
                (keyboard != null && keyboard.leftShiftKey.wasPressedThisFrame);
            CameraToggleRequested =
                (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame) ||
                (keyboard != null && keyboard.cKey.wasPressedThisFrame);
            PauseRequested =
                (gamepad != null && gamepad.startButton.wasPressedThisFrame) ||
                (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);

            if (GameState.IsPaused)
            {
                // Menu navigation keys must not fly the drone, and nothing should be "held" on resume.
                Current = FlightInput.Zero;
                keyThrottle = keyYaw = keyPitch = keyRoll = 0f;
            }
        }

        void UpdateActiveDevice(Gamepad gamepad, FlightInput gamepadRaw, Keyboard keyboard, FlightInput keyboardRaw)
        {
            if (gamepad != null && IsActive(gamepadRaw))
            {
                ActiveDevice = InputDeviceKind.Gamepad;
                ActiveDeviceName = gamepad.displayName;
            }
            else if (keyboard != null && IsActive(keyboardRaw))
            {
                ActiveDevice = InputDeviceKind.Keyboard;
                ActiveDeviceName = "Keyboard";
            }
            else if (ActiveDevice == InputDeviceKind.Gamepad && gamepad == null)
            {
                ActiveDevice = InputDeviceKind.None;
                ActiveDeviceName = "none (gamepad disconnected)";
            }
            else if (ActiveDevice == InputDeviceKind.None && gamepad != null)
            {
                ActiveDevice = InputDeviceKind.Gamepad;
                ActiveDeviceName = gamepad.displayName;
            }
        }

        static bool IsActive(FlightInput input) =>
            Mathf.Abs(input.Throttle) > ActivityThreshold ||
            Mathf.Abs(input.Yaw) > ActivityThreshold ||
            Mathf.Abs(input.Pitch) > ActivityThreshold ||
            Mathf.Abs(input.Roll) > ActivityThreshold;

        static FlightInput ReadGamepadRaw(Gamepad gamepad)
        {
            // Unprocessed values: no Input System deadzone, so the debug overlay shows what the device really sends.
            Vector2 left = gamepad.leftStick.ReadUnprocessedValue();
            Vector2 right = gamepad.rightStick.ReadUnprocessedValue();
            return new FlightInput(left.y, left.x, right.y, right.x);
        }

        FlightInput ShapeGamepad(Gamepad gamepad)
        {
            Vector2 left = InputShaping.RadialDeadzone(gamepad.leftStick.ReadUnprocessedValue(), stickDeadzone);
            Vector2 right = InputShaping.RadialDeadzone(gamepad.rightStick.ReadUnprocessedValue(), stickDeadzone);

            return new FlightInput(
                throttle: InputShaping.Expo(left.y, throttleExpo),
                yaw: InputShaping.Expo(left.x, yawExpo),
                // Stick pushed forward (+Y) = nose down = fly forward.
                pitch: InputShaping.Expo(right.y, pitchRollExpo),
                roll: InputShaping.Expo(right.x, pitchRollExpo));
        }

        static FlightInput ReadKeyboardRaw(Keyboard keyboard)
        {
            return new FlightInput(
                throttle: Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed),
                yaw: Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                pitch: Axis(keyboard.upArrowKey.isPressed, keyboard.downArrowKey.isPressed),
                roll: Axis(keyboard.rightArrowKey.isPressed, keyboard.leftArrowKey.isPressed));
        }

        void UpdateKeyboardSmoothing(FlightInput target, float deltaTime)
        {
            keyThrottle = InputShaping.Smooth(keyThrottle, target.Throttle, keyRiseRate, keyReturnRate, deltaTime);
            keyYaw = InputShaping.Smooth(keyYaw, target.Yaw, keyRiseRate, keyReturnRate, deltaTime);
            keyPitch = InputShaping.Smooth(keyPitch, target.Pitch, keyRiseRate, keyReturnRate, deltaTime);
            keyRoll = InputShaping.Smooth(keyRoll, target.Roll, keyRiseRate, keyReturnRate, deltaTime);
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
