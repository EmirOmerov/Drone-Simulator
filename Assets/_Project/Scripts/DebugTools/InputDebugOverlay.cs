using DroneSim.Controls;
using DroneSim.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSim.DebugTools
{
    /// <summary>
    /// F1 (or the DualSense Create button) toggles a debug panel:
    /// active device, raw vs processed axes, FPS / frame time and flight state.
    /// Uses IMGUI on purpose: zero assets, always works, never shipped as product UI.
    /// </summary>
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        [SerializeField] FlightInputProvider inputProvider;
        [SerializeField] AngleModeController drone;
        [SerializeField] bool visible;

        const float ReferenceHeight = 1080f;
        static readonly Color RawColor = new Color(1f, 0.62f, 0.2f);
        static readonly Color ProcessedColor = new Color(0.3f, 0.85f, 1f);
        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);

        float smoothedDeltaTime = 1f / 60f;
        float worstFrameTime;
        float worstFrameResetTimer;
        GUIStyle labelStyle;

        public void Configure(FlightInputProvider provider, AngleModeController controller)
        {
            inputProvider = provider;
            drone = controller;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.f1Key.wasPressedThisFrame) ||
                (gamepad != null && gamepad.selectButton.wasPressedThisFrame))
                visible = !visible;

            float deltaTime = Time.unscaledDeltaTime;
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, deltaTime, 0.05f);
            worstFrameTime = Mathf.Max(worstFrameTime, deltaTime);
            worstFrameResetTimer += deltaTime;
            if (worstFrameResetTimer > 2f)
            {
                worstFrameResetTimer = 0f;
                worstFrameTime = deltaTime;
            }
        }

        void OnGUI()
        {
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };

            if (!visible)
            {
                GUI.Label(new Rect(16, 12, 400, 30), "<color=#ffffffaa>F1: debug</color>", labelStyle);
                return;
            }

            var panel = new Rect(16, 12, 470, 500);
            DrawRect(panel, PanelColor);
            float x = panel.x + 14, y = panel.y + 10;

            string device = inputProvider != null
                ? $"{inputProvider.ActiveDevice} – {inputProvider.ActiveDeviceName}"
                : "no input provider";
            Line(ref y, x, $"<b>Device:</b> {device}");
            Line(ref y, x, $"<b>FPS:</b> {1f / smoothedDeltaTime:0}   " +
                           $"<b>frame:</b> {smoothedDeltaTime * 1000f:0.0} ms  (worst {worstFrameTime * 1000f:0.0} ms)");
            Line(ref y, x, $"<b>Resolution:</b> {Screen.width}×{Screen.height}   " +
                           $"<b>physics:</b> {1f / Time.fixedDeltaTime:0} Hz");
            y += 8;

            Line(ref y, x, $"<color=#{ColorUtility.ToHtmlStringRGB(RawColor)}>■ raw</color>   " +
                           $"<color=#{ColorUtility.ToHtmlStringRGB(ProcessedColor)}>■ processed</color>");
            FlightInput raw = inputProvider != null ? inputProvider.Raw : FlightInput.Zero;
            FlightInput processed = inputProvider != null ? inputProvider.Current : FlightInput.Zero;
            AxisRow(ref y, x, "Throttle", raw.Throttle, processed.Throttle);
            AxisRow(ref y, x, "Yaw", raw.Yaw, processed.Yaw);
            AxisRow(ref y, x, "Pitch", raw.Pitch, processed.Pitch);
            AxisRow(ref y, x, "Roll", raw.Roll, processed.Roll);
            y += 8;

            if (drone != null && drone.Body != null)
            {
                Vector3 velocity = drone.Body.linearVelocity;
                Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
                Line(ref y, x, $"<b>Altitude:</b> {drone.transform.position.y:0.0} m   " +
                               $"<b>V/S:</b> {velocity.y:+0.0;-0.0} m/s");
                Line(ref y, x, $"<b>Speed:</b> {horizontal.magnitude:0.0} m/s   " +
                               $"<b>Thrust:</b> {drone.ThrustRelativeToHover * 100f:0}% of hover");
                Line(ref y, x, $"<b>Altitude hold:</b> {(drone.HoldingAltitude ? "ON" : "off")}");
            }

            y += 8;
            Line(ref y, x, $"<b>Flight mode:</b> {(drone != null && drone.SportMode ? "Sport" : "Normal")}");
            Line(ref y, x, "<color=#ffffffaa>Toggle: F1 / Create</color>");
        }

        void Line(ref float y, float x, string text)
        {
            GUI.Label(new Rect(x, y, 450, 26), text, labelStyle);
            y += 26;
        }

        void AxisRow(ref float y, float x, string name, float raw, float processed)
        {
            GUI.Label(new Rect(x, y, 90, 26), name, labelStyle);
            var bar = new Rect(x + 90, y + 4, 250, 18);
            DrawRect(bar, new Color(1f, 1f, 1f, 0.12f));
            DrawRect(new Rect(bar.center.x - 1, bar.y, 2, bar.height), new Color(1f, 1f, 1f, 0.5f));
            DrawAxisBar(new Rect(bar.x, bar.y, bar.width, 8), raw, RawColor);
            DrawAxisBar(new Rect(bar.x, bar.y + 10, bar.width, 8), processed, ProcessedColor);
            GUI.Label(new Rect(bar.xMax + 10, y, 120, 26), $"{raw:+0.00;-0.00} / {processed:+0.00;-0.00}", labelStyle);
            y += 28;
        }

        static void DrawAxisBar(Rect area, float value, Color color)
        {
            float half = area.width * 0.5f;
            float length = Mathf.Clamp(value, -1f, 1f) * half;
            var rect = length >= 0f
                ? new Rect(area.x + half, area.y, length, area.height)
                : new Rect(area.x + half + length, area.y, -length, area.height);
            DrawRect(rect, color);
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
