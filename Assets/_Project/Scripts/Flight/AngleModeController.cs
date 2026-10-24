using DroneSim.Controls;
using UnityEngine;

namespace DroneSim.Flight
{
    /// <summary>
    /// Beginner "angle mode" flight controller.
    /// - Pitch/roll stick commands a tilt angle; releasing the stick levels the drone.
    /// - Yaw stick commands a rotation rate.
    /// - Throttle stick commands climb/descent speed; centred throttle holds altitude.
    ///
    /// Runs on the fixed physics step, independent of frame rate. Forces and torques are applied
    /// as accelerations, so tuning does not depend on mass or collider shape.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AngleModeController : MonoBehaviour
    {
        [Tooltip("Normal (beginner) flight feel.")]
        [SerializeField] FlightTuning tuning;
        [Tooltip("Sport mode: more tilt, speed and yaw rate. Optional.")]
        [SerializeField] FlightTuning sportTuning;
        [Tooltip("Optional. Defaults to a FlightInputProvider on the same GameObject.")]
        [SerializeField] FlightInputProvider inputProvider;

        Rigidbody body;
        IFlightInputSource input;
        bool holdingAltitude;
        float heldAltitude;

        /// <summary>The tuning currently in effect (normal or sport).</summary>
        public FlightTuning Tuning => SportMode && sportTuning != null ? sportTuning : tuning;
        public bool SportMode { get; private set; }
        public bool HasSportMode => sportTuning != null;
        public bool HoldingAltitude => holdingAltitude;

        public void SetSportMode(bool enabled)
        {
            SportMode = enabled && sportTuning != null;
            ApplyBodySettings();
        }

        public void SetSportTuning(FlightTuning newSportTuning)
        {
            sportTuning = newSportTuning;
            ApplyBodySettings();
        }

        /// <summary>Thrust actually applied on the last step, in m/s².</summary>
        public float ThrustAcceleration { get; private set; }

        /// <summary>Thrust relative to hover: 1 = exactly hovering. Drives prop animation.</summary>
        public float ThrustRelativeToHover => ThrustAcceleration / Mathf.Max(0.01f, -Physics.gravity.y);

        public Rigidbody Body => body;

        /// <summary>Overrides the input source, e.g. with a scripted source in tests.</summary>
        public void SetInputSource(IFlightInputSource source) => input = source;

        /// <summary>Overrides tuning (tests) and re-applies body settings.</summary>
        public void SetTuning(FlightTuning newTuning)
        {
            tuning = newTuning;
            ApplyBodySettings();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (tuning == null)
                tuning = ScriptableObject.CreateInstance<FlightTuning>();
            if (input == null)
                input = inputProvider != null ? inputProvider : GetComponent<FlightInputProvider>();
            ApplyBodySettings();
        }

        void ApplyBodySettings()
        {
            if (body == null)
                return;
            body.mass = Tuning.mass;
            body.linearDamping = Tuning.linearDrag;
            body.angularDamping = 0.05f;
            body.maxAngularVelocity = 20f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One controller update. Public so tests can drive physics manually.</summary>
        public void Step(float deltaTime)
        {
            FlightInput command = input != null ? input.Current : FlightInput.Zero;
            ApplyAttitude(command);
            ApplyThrust(command);
        }

        /// <summary>Teleports the drone, clears its motion and drops the altitude-hold latch.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            holdingAltitude = false;
            ThrustAcceleration = 0f;
        }

        void ApplyAttitude(FlightInput command)
        {
            Vector3 up = Vector3.up;
            Quaternion rotation = body.rotation;

            // Keep the current heading; tilt is commanded relative to it.
            Vector3 flatForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            if (flatForward.sqrMagnitude < 1e-4f)
                flatForward = Vector3.ProjectOnPlane(rotation * Vector3.up, up);
            if (flatForward.sqrMagnitude < 1e-4f)
                flatForward = Vector3.forward;
            Quaternion heading = Quaternion.LookRotation(flatForward.normalized, up);

            // +X rotation = nose down (forward), -Z rotation = bank right.
            float maxTilt = Tuning.maxTiltDegrees;
            Quaternion target = heading * Quaternion.Euler(command.Pitch * maxTilt, 0f, -command.Roll * maxTilt);

            Vector3 tiltError = Vector3.ProjectOnPlane(RotationError(target, rotation), up);

            Vector3 angularVelocity = body.angularVelocity;
            Vector3 tiltRate = Vector3.ProjectOnPlane(angularVelocity, up);
            float yawRate = Vector3.Dot(angularVelocity, up);
            float targetYawRate = command.Yaw * Tuning.maxYawRateDegrees * Mathf.Deg2Rad;

            Vector3 angularAcceleration =
                Tuning.attitudeP * tiltError
                - Tuning.attitudeD * tiltRate
                + up * (Tuning.yawRateP * (targetYawRate - yawRate));

            body.AddTorque(angularAcceleration, ForceMode.Acceleration);
        }

        void ApplyThrust(FlightInput command)
        {
            float gravity = -Physics.gravity.y;
            float altitude = body.position.y;
            float verticalSpeed = body.linearVelocity.y;
            float verticalAccelerationCommand;

            if (Mathf.Abs(command.Throttle) <= Tuning.throttleHoldBand)
            {
                // Brake vertical motion first, then lock the altitude where the drone stopped.
                // Latching immediately would make it overshoot and spring back, which feels wrong.
                if (!holdingAltitude && Mathf.Abs(verticalSpeed) <= Tuning.altitudeLatchSpeed)
                {
                    holdingAltitude = true;
                    heldAltitude = altitude;
                }

                verticalAccelerationCommand = holdingAltitude
                    ? Tuning.altitudeP * (heldAltitude - altitude) - Tuning.altitudeD * verticalSpeed
                    : -Tuning.verticalBrakeP * verticalSpeed;
            }
            else
            {
                holdingAltitude = false;
                float targetSpeed = command.Throttle > 0f
                    ? command.Throttle * Tuning.maxClimbRate
                    : command.Throttle * Tuning.maxDescentRate;
                // Feed-forward the drag so the drone actually reaches the commanded speed.
                verticalAccelerationCommand =
                    Tuning.climbRateP * (targetSpeed - verticalSpeed) + Tuning.linearDrag * targetSpeed;
            }

            // Thrust acts along the body's up axis; tilt it and you need more of it to stay level.
            float cosTilt = Vector3.Dot(body.rotation * Vector3.up, Vector3.up);
            float thrust = (gravity + verticalAccelerationCommand) / Mathf.Max(cosTilt, 0.5f);
            thrust = Mathf.Clamp(thrust, 0f, Tuning.maxThrustAcceleration);

            body.AddForce(body.rotation * Vector3.up * thrust, ForceMode.Acceleration);
            ThrustAcceleration = thrust;
        }

        /// <summary>Smallest rotation from current to target, as an axis * angle vector (radians), world space.</summary>
        static Vector3 RotationError(Quaternion target, Quaternion current)
        {
            Quaternion delta = target * Quaternion.Inverse(current);
            if (delta.w < 0f)
                delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || angleDegrees < 1e-4f)
                return Vector3.zero;

            return axis.normalized * (angleDegrees * Mathf.Deg2Rad);
        }
    }
}
