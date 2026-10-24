using UnityEngine;

namespace DroneSim.Controls
{
    /// <summary>
    /// Normalized pilot command, every axis in [-1, 1].
    /// Throttle: +1 climb, -1 descend, 0 hold altitude.
    /// Yaw: +1 rotate right. Pitch: +1 nose down (fly forward). Roll: +1 bank right.
    /// </summary>
    public readonly struct FlightInput
    {
        public readonly float Throttle;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly float Roll;

        public static readonly FlightInput Zero = new FlightInput(0f, 0f, 0f, 0f);

        public FlightInput(float throttle, float yaw, float pitch, float roll)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Yaw = Mathf.Clamp(yaw, -1f, 1f);
            Pitch = Mathf.Clamp(pitch, -1f, 1f);
            Roll = Mathf.Clamp(roll, -1f, 1f);
        }

        public override string ToString() =>
            $"T {Throttle:+0.00;-0.00} Y {Yaw:+0.00;-0.00} P {Pitch:+0.00;-0.00} R {Roll:+0.00;-0.00}";
    }

    /// <summary>Anything that can feed the flight controller (live devices, scripted tests, replays).</summary>
    public interface IFlightInputSource
    {
        FlightInput Current { get; }
    }
}
