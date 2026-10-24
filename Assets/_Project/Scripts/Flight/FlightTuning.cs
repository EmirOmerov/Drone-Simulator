using UnityEngine;

namespace DroneSim.Flight
{
    /// <summary>
    /// All flight-feel numbers in one asset so they can be tuned live in Play mode.
    /// Accelerations are in m/s² (or rad/s² for angular), independent of mass and collider shape.
    /// </summary>
    [CreateAssetMenu(menuName = "DroneSim/Flight Tuning", fileName = "FlightTuning")]
    public sealed class FlightTuning : ScriptableObject
    {
        [Header("Body")]
        [Min(0.05f)] public float mass = 1f;
        [Tooltip("Air drag. Also limits top horizontal speed (≈ g·tan(maxTilt) / drag).")]
        [Min(0f)] public float linearDrag = 0.6f;

        [Header("Attitude (angle mode)")]
        [Range(5f, 45f)] public float maxTiltDegrees = 30f;
        [Tooltip("Spring that pulls the drone toward the commanded tilt.")]
        [Min(0f)] public float attitudeP = 150f;
        [Tooltip("Damping on tilt rate. ≈ 2·sqrt(P) is critically damped.")]
        [Min(0f)] public float attitudeD = 24f;

        [Header("Yaw")]
        [Range(10f, 360f)] public float maxYawRateDegrees = 100f;
        [Min(0f)] public float yawRateP = 8f;

        [Header("Vertical (altitude hold)")]
        [Min(0.1f)] public float maxClimbRate = 3f;
        [Min(0.1f)] public float maxDescentRate = 2f;
        [Tooltip("How hard vertical speed tracks the throttle stick.")]
        [Min(0f)] public float climbRateP = 3f;
        [Tooltip("Throttle inside ±band counts as centred and holds the current altitude.")]
        [Range(0f, 0.3f)] public float throttleHoldBand = 0.05f;
        [Tooltip("After releasing throttle, how hard vertical speed is braked to zero before altitude is locked.")]
        [Min(0f)] public float verticalBrakeP = 6f;
        [Tooltip("Vertical speed (m/s) below which the current altitude is locked.")]
        [Min(0.01f)] public float altitudeLatchSpeed = 0.15f;
        [Min(0f)] public float altitudeP = 4f;
        [Min(0f)] public float altitudeD = 3.6f;
        [Tooltip("Upper limit on thrust, in m/s² (≈ 2.5 g).")]
        [Min(1f)] public float maxThrustAcceleration = 25f;
    }
}
