using UnityEngine;

namespace DroneSim.Controls
{
    /// <summary>Pure input-shaping functions. No state, no device access, so they are trivially testable.</summary>
    public static class InputShaping
    {
        /// <summary>
        /// Radial deadzone for a stick: inside the deadzone returns zero, outside it rescales
        /// so the output still starts at 0 and reaches 1 at full deflection.
        /// </summary>
        public static Vector2 RadialDeadzone(Vector2 stick, float deadzone)
        {
            deadzone = Mathf.Clamp(deadzone, 0f, 0.95f);
            float magnitude = stick.magnitude;
            if (magnitude <= deadzone)
                return Vector2.zero;

            float scaled = Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
            return stick / magnitude * scaled;
        }

        /// <summary>Single-axis deadzone with the same rescaling as <see cref="RadialDeadzone"/>.</summary>
        public static float AxialDeadzone(float value, float deadzone)
        {
            deadzone = Mathf.Clamp(deadzone, 0f, 0.95f);
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone)
                return 0f;

            return Mathf.Sign(value) * Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
        }

        /// <summary>
        /// Expo curve: blends linear and cubic response. expo = 0 is linear, expo = 1 is fully cubic.
        /// Softens the centre of the stick while keeping full deflection at ±1.
        /// </summary>
        public static float Expo(float value, float expo)
        {
            value = Mathf.Clamp(value, -1f, 1f);
            expo = Mathf.Clamp01(expo);
            return (1f - expo) * value + expo * value * value * value;
        }

        /// <summary>
        /// Ramps a digital (keyboard) axis toward its target. Moving away from zero uses riseRate,
        /// returning toward zero or reversing uses returnRate, so releasing a key feels snappy.
        /// Rates are in units per second.
        /// </summary>
        public static float Smooth(float current, float target, float riseRate, float returnRate, float deltaTime)
        {
            bool returning = Mathf.Abs(target) < Mathf.Abs(current) || Mathf.Sign(target) != Mathf.Sign(current);
            float rate = returning ? returnRate : riseRate;
            return Mathf.Clamp(Mathf.MoveTowards(current, target, rate * deltaTime), -1f, 1f);
        }
    }
}
