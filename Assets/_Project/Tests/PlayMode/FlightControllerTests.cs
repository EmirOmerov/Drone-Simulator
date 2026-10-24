using DroneSim.Controls;
using DroneSim.Flight;
using NUnit.Framework;
using UnityEngine;

namespace DroneSim.Tests
{
    /// <summary>
    /// Flight-feel acceptance tests. Physics is stepped manually (SimulationMode.Script),
    /// so the tests are fast, frame-rate independent and deterministic.
    /// </summary>
    public sealed class FlightControllerTests
    {
        const float Dt = 0.004f; // 250 Hz, same as the project setting

        sealed class ScriptedInput : IFlightInputSource
        {
            public FlightInput Current { get; set; } = FlightInput.Zero;
        }

        SimulationMode previousMode;
        GameObject drone;
        AngleModeController controller;
        ScriptedInput input;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            CreateDrone(new Vector3(0f, 5f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            if (drone != null)
                Object.DestroyImmediate(drone);
            Physics.simulationMode = previousMode;
        }

        void CreateDrone(Vector3 position)
        {
            if (drone != null)
                Object.DestroyImmediate(drone);

            drone = new GameObject("TestDrone");
            drone.transform.position = position;
            drone.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.12f, 0.5f);
            drone.AddComponent<Rigidbody>();
            controller = drone.AddComponent<AngleModeController>();
            controller.enabled = false; // the test drives Step() itself
            input = new ScriptedInput();
            controller.SetInputSource(input);
        }

        void Simulate(float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                controller.Step(Dt);
                Physics.Simulate(Dt);
            }
        }

        float TiltDegrees => Vector3.Angle(drone.transform.up, Vector3.up);

        [Test]
        public void NoInput_HoversWithin10Centimetres()
        {
            Simulate(5f);

            Vector3 position = drone.transform.position;
            Assert.That(position.y, Is.EqualTo(5f).Within(0.1f), "altitude drift");
            Assert.That(new Vector2(position.x, position.z).magnitude, Is.LessThan(0.1f), "horizontal drift");
            Assert.That(TiltDegrees, Is.LessThan(1f));
        }

        [Test]
        public void FullPitch_TiltsToMaxAngleAndFliesForward()
        {
            input.Current = new FlightInput(0f, 0f, 1f, 0f);
            Simulate(1.5f);

            Assert.That(TiltDegrees, Is.EqualTo(controller.Tuning.maxTiltDegrees).Within(3f));
            Assert.That(controller.Body.linearVelocity.z, Is.GreaterThan(2f), "should move forward (+Z)");
            Assert.That(drone.transform.position.y, Is.EqualTo(5f).Within(0.5f), "should keep altitude while tilted");
        }

        [Test]
        public void ReleasedStick_LevelsWithinOneSecond()
        {
            input.Current = new FlightInput(0f, 0f, 1f, 1f);
            Simulate(1f);
            Assume.That(TiltDegrees, Is.GreaterThan(20f));

            input.Current = FlightInput.Zero;
            Simulate(1f);

            Assert.That(TiltDegrees, Is.LessThan(3f));
        }

        [Test]
        public void RollRight_MovesRight()
        {
            input.Current = new FlightInput(0f, 0f, 0f, 1f);
            Simulate(1.5f);
            Assert.That(controller.Body.linearVelocity.x, Is.GreaterThan(2f));
        }

        [Test]
        public void YawRight_TurnsClockwiseSeenFromAbove()
        {
            input.Current = new FlightInput(0f, 1f, 0f, 0f);
            Simulate(0.5f);
            Assert.That(drone.transform.forward.x, Is.GreaterThan(0.3f));
            Assert.That(TiltDegrees, Is.LessThan(2f), "yaw must not tilt the drone");
        }

        [Test]
        public void FullThrottle_ClimbsAtMaxClimbRate()
        {
            input.Current = new FlightInput(1f, 0f, 0f, 0f);
            Simulate(3f);
            Assert.That(controller.Body.linearVelocity.y,
                Is.EqualTo(controller.Tuning.maxClimbRate).Within(0.3f));
        }

        [Test]
        public void ThrottleReleasedAfterClimb_HoldsNewAltitude()
        {
            input.Current = new FlightInput(1f, 0f, 0f, 0f);
            Simulate(2f);
            input.Current = FlightInput.Zero;
            Simulate(1f);
            float settled = drone.transform.position.y;
            Simulate(3f);
            Assert.That(drone.transform.position.y, Is.EqualTo(settled).Within(0.1f));
        }

        static FlightTuning SportTuning()
        {
            var sport = ScriptableObject.CreateInstance<FlightTuning>();
            sport.maxTiltDegrees = 45f;
            sport.attitudeP = 220f;
            sport.attitudeD = 29f;
            sport.linearDrag = 0.35f;
            sport.maxClimbRate = 6f;
            sport.maxThrustAcceleration = 35f;
            return sport;
        }

        float ForwardSpeedAfterFullPitch(float seconds)
        {
            input.Current = new FlightInput(0f, 0f, 1f, 0f);
            Simulate(seconds);
            return controller.Body.linearVelocity.z;
        }

        [Test]
        public void SportMode_FliesMuchFasterThanNormal()
        {
            float normalSpeed = ForwardSpeedAfterFullPitch(5f);

            CreateDrone(new Vector3(0f, 5f, 0f));
            controller.SetSportTuning(SportTuning());
            controller.SetSportMode(true);
            float sportSpeed = ForwardSpeedAfterFullPitch(5f);

            Assert.That(controller.SportMode, Is.True);
            Assert.That(sportSpeed, Is.GreaterThan(normalSpeed * 1.5f), $"normal {normalSpeed:0.0} m/s, sport {sportSpeed:0.0} m/s");
            Assert.That(drone.transform.position.y, Is.EqualTo(5f).Within(1f), "sport mode still holds altitude");
        }

        [Test]
        public void SportMode_ReleasedStick_StillLevelsOut()
        {
            controller.SetSportTuning(SportTuning());
            controller.SetSportMode(true);
            input.Current = new FlightInput(0f, 0f, 1f, 1f);
            Simulate(1f);
            input.Current = FlightInput.Zero;
            Simulate(1f);
            Assert.That(TiltDegrees, Is.LessThan(3f));
        }

        [Test]
        public void SportMode_WithoutSportTuning_StaysNormal()
        {
            controller.SetSportMode(true);
            Assert.That(controller.SportMode, Is.False);
            Assert.That(controller.HasSportMode, Is.False);
        }

        [Test]
        public void SameInputSequence_ProducesSameTrajectory()
        {
            Vector3 first = RunManoeuvre();
            CreateDrone(new Vector3(0f, 5f, 0f));
            Vector3 second = RunManoeuvre();

            Assert.That(Vector3.Distance(first, second), Is.LessThan(1e-4f));
        }

        Vector3 RunManoeuvre()
        {
            input.Current = new FlightInput(0.5f, 0.3f, 1f, -0.4f);
            Simulate(1f);
            input.Current = new FlightInput(-0.5f, -0.6f, 0f, 0.8f);
            Simulate(1f);
            input.Current = FlightInput.Zero;
            Simulate(1f);
            return drone.transform.position;
        }
    }
}
