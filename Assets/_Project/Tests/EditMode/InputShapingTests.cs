using DroneSim.Controls;
using NUnit.Framework;
using UnityEngine;

namespace DroneSim.Tests
{
    public sealed class InputShapingTests
    {
        const float Tolerance = 1e-5f;

        [TestCase(0f, 0f)]
        [TestCase(0.05f, 0f)]
        [TestCase(0.08f, 0f)]
        public void RadialDeadzone_InsideDeadzone_ReturnsZero(float x, float y)
        {
            Assert.That(InputShaping.RadialDeadzone(new Vector2(x, y), 0.08f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RadialDeadzone_FullDeflection_StaysAtOne()
        {
            Vector2 result = InputShaping.RadialDeadzone(new Vector2(0f, 1f), 0.1f);
            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void RadialDeadzone_JustOutside_StartsNearZero()
        {
            Vector2 result = InputShaping.RadialDeadzone(new Vector2(0.11f, 0f), 0.1f);
            Assert.That(result.x, Is.GreaterThan(0f).And.LessThan(0.02f));
        }

        [Test]
        public void RadialDeadzone_KeepsDirection()
        {
            Vector2 input = new Vector2(0.5f, 0.5f);
            Vector2 result = InputShaping.RadialDeadzone(input, 0.1f);
            Assert.That(Vector2.Angle(input, result), Is.LessThan(0.01f));
        }

        [Test]
        public void RadialDeadzone_DiagonalOverOne_IsClampedToUnitLength()
        {
            // Some pads report ~1.0 on both axes at the corners.
            Vector2 result = InputShaping.RadialDeadzone(new Vector2(1f, 1f), 0.1f);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [TestCase(-0.04f, 0f)]
        [TestCase(0.5f, 0.444444f)]
        [TestCase(-1f, -1f)]
        public void AxialDeadzone_RescalesOutsideDeadzone(float input, float expected)
        {
            Assert.That(InputShaping.AxialDeadzone(input, 0.1f), Is.EqualTo(expected).Within(1e-4f));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void Expo_KeepsEndpointsAndCentre(float expo)
        {
            Assert.That(InputShaping.Expo(0f, expo), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(InputShaping.Expo(1f, expo), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(InputShaping.Expo(-1f, expo), Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void Expo_SoftensCentre()
        {
            Assert.That(InputShaping.Expo(0.3f, 0.5f), Is.LessThan(0.3f));
            Assert.That(InputShaping.Expo(0.3f, 0f), Is.EqualTo(0.3f).Within(Tolerance));
        }

        [Test]
        public void Expo_IsMonotonic()
        {
            float previous = -1f;
            for (float x = -1f; x <= 1f; x += 0.05f)
            {
                float y = InputShaping.Expo(x, 1f);
                Assert.That(y, Is.GreaterThanOrEqualTo(previous - Tolerance));
                previous = y;
            }
        }

        [Test]
        public void Smooth_RampsUpAtRiseRate()
        {
            float value = InputShaping.Smooth(0f, 1f, riseRate: 2f, returnRate: 5f, deltaTime: 0.1f);
            Assert.That(value, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Smooth_ReturnsToCentreAtReturnRate()
        {
            float value = InputShaping.Smooth(1f, 0f, riseRate: 2f, returnRate: 5f, deltaTime: 0.1f);
            Assert.That(value, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Smooth_ConvergesAndNeverOvershoots()
        {
            float value = 0f;
            for (int i = 0; i < 200; i++)
            {
                value = InputShaping.Smooth(value, 1f, 2.5f, 5f, 1f / 60f);
                Assert.That(value, Is.LessThanOrEqualTo(1f));
            }
            Assert.That(value, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void FlightInput_ClampsToUnitRange()
        {
            var input = new FlightInput(2f, -3f, 1.5f, -1.01f);
            Assert.That(input.Throttle, Is.EqualTo(1f));
            Assert.That(input.Yaw, Is.EqualTo(-1f));
            Assert.That(input.Pitch, Is.EqualTo(1f));
            Assert.That(input.Roll, Is.EqualTo(-1f));
        }
    }
}
