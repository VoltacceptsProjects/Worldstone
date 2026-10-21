using Diablerie.Engine;
using Diablerie.Engine.UI;
using Diablerie.Game.UI;
using Diablerie.Game.UI.Inventory;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Decides when skills must not fire: while a menu or window is open, and for a short moment after one closed
    // (the button that closed it is usually still down). Callers add their own "released since" latch on top,
    // so a button that was held through a menu or an interaction has to be let go before it casts again.
    public static class SkillGate
    {
        private const float GraceSeconds = 0.25f;

        private static int _frame = -1;
        private static bool _menuBlocked;
        private static bool _worldBlocked;
        private static float _lastMenuOpen = -100f;
        private static float _lastWorldBlocked = -100f;

        // Any window of the game (or the gamepad cursor) is open, or one just closed. Player 1 casts nothing then.
        public static bool Blocked
        {
            get
            {
                Refresh();
                return _menuBlocked;
            }
        }

        // The game is paused (game menu) or typing in the console, or it just stopped being so. Nobody casts then.
        public static bool WorldBlocked
        {
            get
            {
                Refresh();
                return _worldBlocked;
            }
        }

        private static void Refresh()
        {
            if (_frame == Time.frameCount)
                return;
            _frame = Time.frameCount;

            float now = Time.unscaledTime;

            bool world = Time.timeScale <= 0f || GameMenu.IsVisible() ||
                         (CommandPrompt.instance != null && CommandPrompt.instance.visible);
            if (world)
                _lastWorldBlocked = now;
            _worldBlocked = world || now - _lastWorldBlocked < GraceSeconds;

            bool menu = world || GamepadCursor.Active || NpcDialog.IsVisible || QuestPanel.IsVisible ||
                        WaypointPanel.IsVisible || StashPanel.IsVisible || GamblePanel.IsVisible ||
                        (InventoryPanel.instance != null && InventoryPanel.instance.visible) ||
                        (CharstatPanel.instance != null && CharstatPanel.instance.visible);
            if (menu)
                _lastMenuOpen = now;
            _menuBlocked = menu || now - _lastMenuOpen < GraceSeconds;
        }
    }
}
