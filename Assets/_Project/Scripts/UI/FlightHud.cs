using DroneSim.CameraRig;
using DroneSim.Flight;
using DroneSim.Game;
using UnityEngine;

namespace DroneSim.UI
{
    /// <summary>Minimal pilot HUD: flight mode, camera, speed, altitude, and the menu hint.</summary>
    public sealed class FlightHud : MonoBehaviour
    {
        [SerializeField] AngleModeController drone;
        [SerializeField] DroneCamera droneCamera;

        const float ReferenceHeight = 1080f;
        static readonly Color NormalColor = new Color(0.3f, 0.8f, 1f);
        static readonly Color SportColor = new Color(1f, 0.45f, 0.15f);

        GUIStyle badgeStyle, valueStyle, hintStyle;

        public void Configure(AngleModeController controller, DroneCamera cameraRig)
        {
            drone = controller;
            droneCamera = cameraRig;
        }

        void OnGUI()
        {
            if (GameState.IsPaused || drone == null || drone.Body == null)
                return;

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            EnsureStyles();

            float y = ReferenceHeight - 150f;
            Badge(new Rect(30, y, 150, 44), drone.SportMode ? "SPORT" : "NORMAL", drone.SportMode ? SportColor : NormalColor);
            string view = droneCamera != null && droneCamera.Mode == CameraMode.Fpv ? "FPV" : "CHASE";
            Badge(new Rect(190, y, 130, 44), view, new Color(1f, 1f, 1f, 0.25f));

            float speedKmh = Vector3.ProjectOnPlane(drone.Body.linearVelocity, Vector3.up).magnitude * 3.6f;
            GUI.Label(new Rect(30, y + 54, 600, 44),
                $"{speedKmh:0} km/h     {drone.transform.position.y:0.0} m", valueStyle);

            GUI.Label(new Rect(width - 430, ReferenceHeight - 60, 400, 40),
                "Esc / Options: meni", hintStyle);
        }

        void Badge(Rect rect, string text, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, badgeStyle);
        }

        void EnsureStyles()
        {
            if (badgeStyle != null)
                return;
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
        }
    }
}
