using DroneSim.Controls;
using DroneSim.Game;
using UnityEngine;

namespace DroneSim.Flight
{
    /// <summary>Puts the drone back on its start pad when the pilot presses reset or it falls out of the world.</summary>
    [RequireComponent(typeof(AngleModeController))]
    public sealed class DroneRespawn : MonoBehaviour
    {
        [SerializeField] FlightInputProvider inputProvider;
        [SerializeField] float killHeight = -20f;

        AngleModeController controller;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public event System.Action Respawned;

        void Awake()
        {
            controller = GetComponent<AngleModeController>();
            if (inputProvider == null)
                inputProvider = GetComponent<FlightInputProvider>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Update()
        {
            if (GameState.IsPaused)
                return;
            bool requested = inputProvider != null && inputProvider.ResetRequested;
            if (requested || transform.position.y < killHeight)
                Respawn();
        }

        public void Respawn()
        {
            controller.ResetTo(spawnPosition, spawnRotation);
            Respawned?.Invoke();
        }
    }
}
