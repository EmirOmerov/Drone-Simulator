using UnityEngine;

namespace DroneSim.Flight
{
    /// <summary>Visual only: spins the rotors faster with more thrust, alternating direction per rotor.</summary>
    public sealed class PropSpinner : MonoBehaviour
    {
        [SerializeField] AngleModeController controller;
        [SerializeField] Transform[] rotors = new Transform[0];
        [SerializeField] float idleSpeedDegrees = 900f;
        [SerializeField] float hoverSpeedDegrees = 2600f;
        [SerializeField] float spinUpRate = 6f;

        float currentSpeed;

        public void Configure(AngleModeController drone, Transform[] rotorTransforms)
        {
            controller = drone;
            rotors = rotorTransforms;
        }

        void Update()
        {
            float relativeThrust = controller != null ? controller.ThrustRelativeToHover : 0f;
            float targetSpeed = Mathf.Lerp(idleSpeedDegrees, hoverSpeedDegrees, relativeThrust);
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 1f - Mathf.Exp(-spinUpRate * Time.deltaTime));

            for (int i = 0; i < rotors.Length; i++)
            {
                if (rotors[i] == null)
                    continue;
                float direction = i % 2 == 0 ? 1f : -1f;
                rotors[i].Rotate(0f, direction * currentSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }
    }
}
