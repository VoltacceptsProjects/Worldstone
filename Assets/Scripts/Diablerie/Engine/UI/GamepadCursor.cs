using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Diablerie.Game.Coop;
using Diablerie.Game.UI;
using Diablerie.Game.UI.Inventory;

namespace Diablerie.Engine.UI
{
    // Makes the whole interface usable with the gamepad of player 1, like the console interface of Diablo II Resurrected:
    // a cursor that the sticks move. A selects (left click), X is the right click, B closes (Escape).
    // Everything that read the mouse reads this instead, the UI event system gets it through an input override.
    // The real mouse still works and takes over as soon as it moves.
    //
    // The cursor is on by itself in all menus and while any window of the game is open (game menu, inventory,
    // character, dialogs, waypoints, stash, quests, gambling). While it is on the gamepad does not control the
    // character. With a controller connected the system cursor is hidden and the circle is the only cursor.
    //
    //   Left / right stick  move the cursor
    //   A / X               left / right click
    //   B                   close (Escape)
    //   Menu (Start)        confirm in the menus of the title screen (Return)
    //   LB / RB             in the game: switch between inventory, character, quests and nothing
    [DefaultExecutionOrder(-1000)]
    public class GamepadCursor : MonoBehaviour
    {
        private const float DeadZone = 0.25f;
        private const float SpeedScreenHeights = 1.0f; // per second at full tilt

        private static GamepadCursor _instance;

        private Vector2 _position;
        private Vector2 _lastReal;
        private const float MouseGraceSeconds = 2f;
        private float _lastMouseMove = -100f;
        private bool _padDriven;
        private bool _systemCursorHidden;
        private bool _active;
        private bool _cancelDown;
        private bool _confirmDown;
        private readonly bool[] _held = new bool[2];
        private readonly bool[] _down = new bool[2];
        private readonly bool[] _up = new bool[2];
        private PadInput _input;

        public static bool Active => _instance != null && _instance._active;
        public static bool CancelDown => _instance != null && _instance._cancelDown;
        public static bool ConfirmDown => _instance != null && _instance._confirmDown;

        public static Vector3 Position
        {
            get
            {
                if (_instance == null || !_instance._padDriven)
                    return Input.mousePosition;
                return _instance._position;
            }
        }

        public static bool Held(int button)
        {
            return Input.GetMouseButton(button) || (button < 2 && _instance != null && _instance._held[button]);
        }

        public static bool ButtonDown(int button)
        {
            return Input.GetMouseButtonDown(button) || (button < 2 && _instance != null && _instance._down[button]);
        }

        public static bool ButtonUp(int button)
        {
            return Input.GetMouseButtonUp(button) || (button < 2 && _instance != null && _instance._up[button]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (_instance != null)
                return;
            var go = new GameObject("GamepadCursor");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GamepadCursor>();
        }

        private static bool InGame => SceneManager.GetActiveScene().name == "Game";

        private static bool ModalOpen()
        {
            return GameMenu.IsVisible() || NpcDialog.IsVisible || QuestPanel.IsVisible || WaypointPanel.IsVisible ||
                   StashPanel.IsVisible || GamblePanel.IsVisible ||
                   (InventoryPanel.instance != null && InventoryPanel.instance.visible) ||
                   (CharstatPanel.instance != null && CharstatPanel.instance.visible);
        }

        private void Awake()
        {
            _input = gameObject.AddComponent<PadInput>();
            _position = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        private void Update()
        {
            Vector2 real = Input.mousePosition;
            if ((real - _lastReal).sqrMagnitude > 1f)
            {
                _position = real;
                _padDriven = false;
                _lastMouseMove = Time.unscaledTime;
            }
            _lastReal = real;

            int pad = CoopPads.Joystick(1);
            bool inGame = InGame;

            bool wasActive = _active;
            _active = pad != 0 && (!inGame || ModalOpen());
            // The cursor shows up by itself, the sticks are not needed to find it. A mouse that was just used keeps it.
            if (_active && !wasActive && Time.unscaledTime - _lastMouseMove > MouseGraceSeconds)
            {
                if (!_padDriven)
                    _position = new Vector2(Screen.width / 2f, Screen.height / 2f);
                _padDriven = true;
            }

            _cancelDown = false;
            _confirmDown = false;
            var now = new bool[2];
            if (_active)
            {
                Vector2 stick = ReadSticks(1);
                if (stick != Vector2.zero)
                {
                    float speed = Screen.height * SpeedScreenHeights * Time.unscaledDeltaTime;
                    _position += stick * stick.magnitude * speed;
                    _position.x = Mathf.Clamp(_position.x, 0f, Screen.width - 1f);
                    _position.y = Mathf.Clamp(_position.y, 0f, Screen.height - 1f);
                    _padDriven = true;
                }

                now[0] = Input.GetKey(CoopPads.Key(1, 0));
                now[1] = Input.GetKey(CoopPads.Key(1, 2));
                _cancelDown = CoopPads.ButtonDown(1, 1);
                _confirmDown = !inGame && CoopPads.ButtonDown(1, 7);
                if (inGame)
                {
                    if (CoopPads.ButtonDown(1, 5))
                        CycleWindow(1);
                    else if (CoopPads.ButtonDown(1, 4))
                        CycleWindow(-1);
                }
            }

            for (int i = 0; i < 2; ++i)
            {
                _down[i] = now[i] && !_held[i];
                _up[i] = !now[i] && _held[i];
                _held[i] = now[i];
            }

            InstallInput();
        }

        // With a controller connected the system cursor is gone, the circle of this class takes its place
        private void LateUpdate()
        {
            if (CoopPads.Joystick(1) != 0)
            {
                Cursor.visible = false;
                _systemCursorHidden = true;
            }
            else if (_systemCursorHidden)
            {
                _systemCursorHidden = false;
                Cursor.visible = !SoftwareCursor.Showing;
            }
        }

        private static Vector2 ReadSticks(int player)
        {
            Vector2 value = new Vector2(CoopPads.Axis(player, "X"), CoopPads.Axis(player, "Y")) +
                            new Vector2(CoopPads.Axis(player, "RX"), CoopPads.Axis(player, "RY"));
            if (value.magnitude < DeadZone)
                return Vector2.zero;
            return value.normalized * Mathf.Min(1f, value.magnitude);
        }

        // Nothing -> inventory -> character -> quests -> nothing
        private static void CycleWindow(int direction)
        {
            int current = 0;
            if (InventoryPanel.instance.visible)
                current = 1;
            else if (CharstatPanel.instance.visible)
                current = 2;
            else if (QuestPanel.IsVisible)
                current = 3;

            int next = (current + direction + 4) % 4;
            if (GameMenu.IsVisible())
                GameMenu.Hide();
            InventoryPanel.instance.visible = next == 1;
            CharstatPanel.instance.visible = next == 2;
            if (next == 3)
            {
                QuestPanel.Show();
            }
            else
            {
                QuestPanel.Hide();
            }
        }

        private void InstallInput()
        {
            var system = EventSystem.current;
            if (system == null)
                return;
            var module = system.currentInputModule as StandaloneInputModule;
            if (module == null)
                module = system.GetComponent<StandaloneInputModule>();
            if (module != null && module.inputOverride != _input)
                module.inputOverride = _input;
        }

        private const int CursorTextureSize = 64;
        private const float CursorDrawSize = 36f;
        private Texture2D _cursorTexture;

        // A ring with a dot in the middle, white with a dark edge so it shows on every background
        private static Texture2D BuildCursorTexture()
        {
            const int size = CursorTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float center = (size - 1) / 2f;
            const float ringRadius = 23f;
            const float ringHalfWidth = 2.5f;
            const float dotRadius = 4.5f;
            const float edge = 2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float core = Mathf.Max(Coverage(Mathf.Abs(d - ringRadius), ringHalfWidth), Coverage(d, dotRadius));
                    float outline = Mathf.Max(Coverage(Mathf.Abs(d - ringRadius), ringHalfWidth + edge), Coverage(d, dotRadius + edge));
                    float alpha = Mathf.Max(core, outline);
                    byte shade = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(core / Mathf.Max(alpha, 0.001f)));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(255f * alpha * 0.95f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 1 inside the limit, falling off over one pixel at its edge
        private static float Coverage(float distance, float limit)
        {
            return Mathf.Clamp01(limit - distance + 0.5f);
        }

        private void OnGUI()
        {
            if (!_active || SoftwareCursor.Showing)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            if (_cursorTexture == null)
                _cursorTexture = BuildCursorTexture();

            float x = _position.x;
            float y = Screen.height - _position.y;
            var previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - CursorDrawSize / 2f, y - CursorDrawSize / 2f, CursorDrawSize, CursorDrawSize), _cursorTexture);
            GUI.color = previous;
        }

        private class PadInput : BaseInput
        {
            public override bool mousePresent => true;
            public override Vector2 mousePosition => Position;
            public override bool GetMouseButton(int button) => Held(button);
            public override bool GetMouseButtonDown(int button) => ButtonDown(button);
            public override bool GetMouseButtonUp(int button) => ButtonUp(button);
        }
    }
}
