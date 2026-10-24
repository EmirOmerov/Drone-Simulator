using DroneSim.Controls;
using DroneSim.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace DroneSim.Tests
{
    /// <summary>
    /// Uses virtual Input System devices, so no physical controller is needed.
    /// Synchronous [Test]s: Set() applies immediately and the provider is polled manually,
    /// which keeps the tests independent of the player loop (and of batchmode quirks).
    /// </summary>
    public sealed class FlightInputProviderTests : InputTestFixture
    {
        const float FrameTime = 1f / 60f;

        GameObject host;
        FlightInputProvider provider;

        public override void Setup()
        {
            base.Setup();
            host = new GameObject("InputHost");
            provider = host.AddComponent<FlightInputProvider>();
            provider.enabled = false; // the test calls Poll() itself
        }

        public override void TearDown()
        {
            GameState.SetPaused(false);
            Object.DestroyImmediate(host);
            base.TearDown();
        }

        [Test]
        public void ButtonEvents_FireOnPressOnly()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();

            Press(keyboard.leftShiftKey);
            provider.Poll(FrameTime);
            Assert.That(provider.SportToggleRequested, Is.True, "Left Shift = sport");

            InputSystem.Update(); // next frame, still held
            provider.Poll(FrameTime);
            Assert.That(provider.SportToggleRequested, Is.False, "only on the press, not while held");

            Press(pad.rightShoulder);
            provider.Poll(FrameTime);
            Assert.That(provider.SportToggleRequested, Is.True, "R1 = sport");

            Press(keyboard.cKey);
            provider.Poll(FrameTime);
            Assert.That(provider.CameraToggleRequested, Is.True, "C = camera");

            Press(pad.buttonNorth);
            provider.Poll(FrameTime);
            Assert.That(provider.CameraToggleRequested, Is.True, "Triangle = camera");

            Press(keyboard.escapeKey);
            provider.Poll(FrameTime);
            Assert.That(provider.PauseRequested, Is.True, "Esc = pause");

            Press(pad.startButton);
            provider.Poll(FrameTime);
            Assert.That(provider.PauseRequested, Is.True, "Options = pause");
        }

        [Test]
        public void WhilePaused_FlightInputIsNeutral()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            GameState.SetPaused(true);
            Set(pad.rightStick, new Vector2(0f, 1f));
            provider.Poll(FrameTime);

            Assert.That(provider.Current.Pitch, Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GameState.SetPaused(false);
            provider.Poll(FrameTime);
            Assert.That(provider.Current.Pitch, Is.EqualTo(1f).Within(0.02f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Gamepad_Mode2_MapsSticksToAxes()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.leftStick, new Vector2(0f, 1f));  // throttle up
            Set(pad.rightStick, new Vector2(1f, 0f)); // roll right
            provider.Poll(FrameTime);

            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(provider.Current.Throttle, Is.EqualTo(1f).Within(0.02f));
            Assert.That(provider.Current.Roll, Is.EqualTo(1f).Within(0.02f));
            Assert.That(provider.Current.Pitch, Is.EqualTo(0f).Within(0.02f));
            Assert.That(provider.Current.Yaw, Is.EqualTo(0f).Within(0.02f));
            Assert.That(provider.Raw.Roll, Is.EqualTo(1f).Within(0.02f));
        }

        // A virtual DualSense HID only accepts real HID reports, so stick values are tested on a generic
        // Gamepad above (the provider reads the Gamepad base class either way). Here we only check that a
        // DualSense is picked up as the active gamepad; the real Bluetooth pad is verified on the debug overlay.
        [Test]
        public void DualSense_IsPickedUpAsActiveGamepad()
        {
            var pad = InputSystem.AddDevice<DualSenseGamepadHID>();
            provider.Poll(FrameTime);

            Assert.That(Gamepad.current, Is.SameAs(pad));
            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(provider.Current.Throttle, Is.EqualTo(0f).Within(0.02f), "centred sticks = neutral");
        }

        [Test]
        public void Gamepad_RightStickForward_IsPositivePitch()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.rightStick, new Vector2(0f, 1f));
            provider.Poll(FrameTime);

            Assert.That(provider.Current.Pitch, Is.EqualTo(1f).Within(0.02f));
        }

        [Test]
        public void SmallStickNoise_IsRemovedByDeadzone()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.rightStick, new Vector2(0.04f, -0.03f));
            provider.Poll(FrameTime);

            Assert.That(provider.Current.Pitch, Is.EqualTo(0f));
            Assert.That(provider.Current.Roll, Is.EqualTo(0f));
        }

        [Test]
        public void GamepadDisconnect_MidFlight_DropsToNeutral()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.rightStick, new Vector2(0f, 1f));
            provider.Poll(FrameTime);
            Assume.That(provider.Current.Pitch, Is.GreaterThan(0.9f));

            InputSystem.RemoveDevice(pad);
            provider.Poll(FrameTime);

            Assert.That(provider.Current.Pitch, Is.EqualTo(0f));
            Assert.That(provider.Current.Throttle, Is.EqualTo(0f));
            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.None));
        }

        [Test]
        public void Keyboard_RampsInsteadOfJumping()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);
            provider.Poll(FrameTime);

            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.Keyboard));
            Assert.That(provider.Raw.Throttle, Is.EqualTo(1f));
            Assert.That(provider.Current.Throttle, Is.GreaterThan(0f).And.LessThan(0.2f));

            for (int i = 0; i < 60; i++)
                provider.Poll(FrameTime);
            Assert.That(provider.Current.Throttle, Is.EqualTo(1f).Within(1e-4f), "full after holding ~1 s");
        }

        [Test]
        public void SwitchingFromKeyboardToGamepad_FollowsLastUsedDevice()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();

            Press(keyboard.upArrowKey);
            provider.Poll(FrameTime);
            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.Keyboard));

            Release(keyboard.upArrowKey);
            Set(pad.leftStick, new Vector2(-1f, 0f));
            provider.Poll(FrameTime);
            Assert.That(provider.ActiveDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(provider.Current.Yaw, Is.EqualTo(-1f).Within(0.02f));
        }
    }
}
