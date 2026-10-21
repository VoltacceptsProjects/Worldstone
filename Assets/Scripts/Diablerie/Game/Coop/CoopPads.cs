using System.Collections.Generic;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Unity numbers its joysticks by the order the system reports them, and that order can contain gaps or
    // devices that are not gamepads. So "player 1 = pad 1" means the first connected pad and "player 2" the second.
    public static class CoopPads
    {
        public const int MaxJoysticks = 8;

        private static readonly int[] Map = new int[3];
        private static float _nextRefresh;

        private static void Refresh()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f;

            int old1 = Map[1], old2 = Map[2];
            Map[1] = Map[2] = 0;
            var names = Input.GetJoystickNames();
            int found = 0;
            for (int i = 0; i < names.Length && i < MaxJoysticks; ++i)
            {
                // Virtual joysticks (vJoy and the like) are always "connected" and would take a player's place
                if (string.IsNullOrEmpty(names[i]) || names[i].ToLowerInvariant().Contains("vjoy"))
                    continue;
                ++found;
                if (found <= 2)
                    Map[found] = i + 1;
            }

            if (old1 != Map[1] || old2 != Map[2])
            {
                Debug.Log("Co-op pads: player 1 = " + Describe(names, Map[1]) + ", player 2 = " + Describe(names, Map[2]));
            }
        }

        private static string Describe(string[] names, int joystick)
        {
            return joystick == 0 ? "none" : "joystick " + joystick + " (" + names[joystick - 1] + ")";
        }

        // 1-based joystick number of the pad of this player (1 or 2), 0 when there is none
        public static int Joystick(int player)
        {
            Refresh();
            return Map[player];
        }

        private static readonly HashSet<string> MissingAxes = new HashSet<string>();

        // suffix: X, Y (left stick), RX, RY (right stick), DX, DY (D-pad), LT, RT (triggers)
        public static float Axis(int player, string suffix)
        {
            int joystick = Joystick(player);
            if (joystick == 0)
                return 0f;
            string name = "CoopJoy" + joystick + suffix;
            if (MissingAxes.Contains(name))
                return 0f;
            try
            {
                return Input.GetAxisRaw(name);
            }
            catch (System.ArgumentException)
            {
                MissingAxes.Add(name);
                Debug.LogWarning("Co-op: the input axis " + name + " is missing, use the menu Tools > Co-op > Create Gamepad Axes");
                return 0f;
            }
        }

        public static bool ButtonDown(int player, int button)
        {
            return Input.GetKeyDown(Key(player, button));
        }

        public static KeyCode Key(int player, int button)
        {
            int joystick = Joystick(player);
            if (joystick == 0)
                return KeyCode.None;
            return (KeyCode)((int)KeyCode.Joystick1Button0 + (joystick - 1) * 20 + button);
        }
    }
}
