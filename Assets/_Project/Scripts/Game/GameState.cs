using System;
using UnityEngine;

namespace DroneSim.Game
{
    /// <summary>Global pause state. Pausing freezes physics via timeScale; menus keep running on unscaled time.</summary>
    public static class GameState
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> PauseChanged;

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            PauseChanged?.Invoke(paused);
        }

        /// <summary>Leaves Play mode in the editor, quits the application in a build.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Static state survives domain-reload-free Play mode; reset it on every start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            PauseChanged = null;
            Time.timeScale = 1f;
        }
    }
}
