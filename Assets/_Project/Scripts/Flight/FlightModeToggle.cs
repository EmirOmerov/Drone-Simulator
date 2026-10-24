using DroneSim.Controls;
using DroneSim.Game;
using UnityEngine;

namespace DroneSim.Flight
{
    /// <summary>Switches between Normal and Sport flight mode (R1 / Left Shift).</summary>
    [RequireComponent(typeof(AngleModeController))]
    public sealed class FlightModeToggle : MonoBehaviour
    {
        [SerializeField] FlightInputProvider inputProvider;

        AngleModeController controller;

        void Awake()
        {
            controller = GetComponent<AngleModeController>();
            if (inputProvider == null)
                inputProvider = GetComponent<FlightInputProvider>();
        }

        void Update()
        {
            if (GameState.IsPaused || inputProvider == null || !inputProvider.SportToggleRequested)
                return;
            controller.SetSportMode(!controller.SportMode);
        }
    }
}
