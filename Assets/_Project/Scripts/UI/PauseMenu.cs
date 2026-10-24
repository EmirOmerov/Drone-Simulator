using DroneSim.Controls;
using DroneSim.Flight;
using DroneSim.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DroneSim.UI
{
    /// <summary>
    /// Esc / Options opens the pause menu: continue, reset drone, quit.
    /// Navigate with arrows / W S / D-pad, confirm with Enter / Space / Cross, close with Esc / Options / Circle.
    /// The mouse works too.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] FlightInputProvider inputProvider;
        [SerializeField] DroneRespawn respawn;

        static readonly string[] Items = { "Nastavi", "Reset drona", "Izađi iz programa" };
        const int Continue = 0, ResetDrone = 1, Quit = 2;
        const float ReferenceHeight = 1080f;

        int selected;
        GUIStyle titleStyle, itemStyle, helpStyle;

        public void Configure(FlightInputProvider provider, DroneRespawn droneRespawn)
        {
            inputProvider = provider;
            respawn = droneRespawn;
        }

        void Start() => Cursor.visible = false;

        void Update()
        {
            bool toggle = inputProvider != null && inputProvider.PauseRequested;
            if (!GameState.IsPaused)
            {
                if (toggle)
                    Open();
                return;
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (toggle || Pressed(gamepad?.buttonEast))
            {
                Close();
                return;
            }

            if (Pressed(keyboard?.upArrowKey) || Pressed(keyboard?.wKey) || Pressed(gamepad?.dpad.up))
                selected = (selected + Items.Length - 1) % Items.Length;
            if (Pressed(keyboard?.downArrowKey) || Pressed(keyboard?.sKey) || Pressed(gamepad?.dpad.down))
                selected = (selected + 1) % Items.Length;

            if (Pressed(keyboard?.enterKey) || Pressed(keyboard?.numpadEnterKey) || Pressed(keyboard?.spaceKey) ||
                Pressed(gamepad?.buttonSouth))
                Activate(selected);
        }

        static bool Pressed(ButtonControl button) => button != null && button.wasPressedThisFrame;

        void Open()
        {
            selected = Continue;
            GameState.SetPaused(true);
            Cursor.visible = true;
        }

        void Close()
        {
            GameState.SetPaused(false);
            Cursor.visible = false;
        }

        void Activate(int item)
        {
            switch (item)
            {
                case Continue:
                    Close();
                    break;
                case ResetDrone:
                    Close();
                    if (respawn != null)
                        respawn.Respawn();
                    break;
                case Quit:
                    GameState.Quit();
                    break;
            }
        }

        void OnGUI()
        {
            if (!GameState.IsPaused)
                return;

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            EnsureStyles();

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, width, ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float panelWidth = 520f;
            float x = (width - panelWidth) * 0.5f;
            float y = 220f;
            GUI.Label(new Rect(x, y, panelWidth, 70), "PAUZA", titleStyle);
            y += 100;

            for (int i = 0; i < Items.Length; i++)
            {
                var rect = new Rect(x, y, panelWidth, 64);
                if (Event.current.type == EventType.MouseMove && rect.Contains(Event.current.mousePosition))
                    selected = i;

                GUI.color = i == selected ? new Color(1f, 0.6f, 0.15f, 0.95f) : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(rect, Items[i], itemStyle))
                    Activate(i);
                y += 78;
            }

            y += 30;
            const float helpWidth = 760f;
            float hx = (width - helpWidth) * 0.5f;
            HelpRow(hx, ref y, "<b>Kontrole</b>", "<b>DualSense</b>", "<b>Tastatura</b>");
            HelpRow(hx, ref y, "Gore / dolje", "lijeva palica ↑ ↓", "W / S");
            HelpRow(hx, ref y, "Okretanje", "lijeva palica ← →", "A / D");
            HelpRow(hx, ref y, "Naprijed / nazad / strane", "desna palica", "strelice");
            HelpRow(hx, ref y, "Sport mod", "R1", "Left Shift");
            HelpRow(hx, ref y, "Kamera (chase / FPV)", "Trokut", "C");
            HelpRow(hx, ref y, "Reset drona", "meni", "R");
            HelpRow(hx, ref y, "Meni", "Options", "Esc");
        }

        void HelpRow(float x, ref float y, string action, string gamepad, string keyboard)
        {
            GUI.Label(new Rect(x, y, 320, 30), action, helpStyle);
            GUI.Label(new Rect(x + 330, y, 240, 30), gamepad, helpStyle);
            GUI.Label(new Rect(x + 580, y, 180, 30), keyboard, helpStyle);
            y += 30;
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }, hover = { textColor = Color.white }
            };
            helpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, richText = true, normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
            };
        }
    }
}
