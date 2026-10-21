using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Engine.UI;
using Diablerie.Engine.World;
using Diablerie.Game.UI;
using Diablerie.Game.UI.Inventory;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // The windows of the game exist once and are drawn once, so they belong to one player at a time.
    // Whoever opens a window (inventory, dialog, stash, waypoints...) becomes the owner: the windows show
    // that player's things and that player's gamepad drives the cursor. The other player keeps playing.
    public static class CoopUi
    {
        public static Player Owner { get; private set; }

        public static bool WindowOpen =>
            NpcDialog.IsVisible || QuestPanel.IsVisible || WaypointPanel.IsVisible || StashPanel.IsVisible ||
            GamblePanel.IsVisible ||
            (InventoryPanel.instance != null && InventoryPanel.instance.visible) ||
            (CharstatPanel.instance != null && CharstatPanel.instance.visible);

        // Player 2 has windows open right now (and the game is not paused by the game menu)
        public static bool GuestOwns
        {
            get
            {
                var world = WorldState.instance;
                return world != null && world.Player2 != null && Owner == world.Player2 &&
                       WindowOpen && !GameMenu.IsVisible();
            }
        }

        // Call before showing a window for this player. False when the windows are busy with the other player.
        public static bool TryClaim(Player player)
        {
            var world = WorldState.instance;
            if (player == null || world == null)
                return false;
            if (world.Player2 == null)
                return player == world.Player;
            if (Owner != player && Owner != null && WindowOpen)
                return false;
            Bind(player);
            return true;
        }

        private static void Bind(Player player)
        {
            if (Owner == player)
                return;
            Owner = player;
            if (PlayerController.instance != null)
                PlayerController.instance.SetUiPlayer(player);
        }

        // Windows are all closed (or player 2 left): hand the windows back to player 1
        public static void Reset()
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null)
                return;
            if (Owner != null && Owner != world.Player)
                ReleaseHands(Owner);
            Bind(world.Player);
        }

        public static void ReleaseHands(Player player)
        {
            if (player == null || player.unit == null)
                return;
            player.CancelIdentify();
            var item = player.HandsItem;
            if (item == null)
                return;
            player.HandsItem = null;
            if (!player.inventory.Put(item))
                Loot.Create(player.transform.position, item);
        }

        // Player 2 clicked into the world with an item on the cursor: it drops at his feet
        public static void DropGuestHands()
        {
            var world = WorldState.instance;
            var guest = world != null ? world.Player2 : null;
            if (guest == null || guest.HandsItem == null || guest.unit == null)
                return;
            Loot.Create(guest.transform.position, guest.HandsItem);
            guest.HandsItem = null;
        }

        // Window toggle of player 2: opens his inventory, or closes whatever he has open
        public static void ToggleGuestMenu(Player guest)
        {
            if (WindowOpen)
            {
                if (Owner == guest)
                    CloseAll();
                return;
            }
            if (!TryClaim(guest))
                return;
            InventoryPanel.instance.visible = true;
        }

        public static void CloseAll()
        {
            NpcDialog.Hide();
            QuestPanel.Hide();
            WaypointPanel.Hide();
            GamblePanel.Hide();
            StashPanel.Hide();
            if (InventoryPanel.instance != null)
                InventoryPanel.instance.visible = false;
            if (CharstatPanel.instance != null)
                CharstatPanel.instance.visible = false;
        }
    }

    [DefaultExecutionOrder(-900)]
    public class CoopUiDriver : MonoBehaviour
    {
        private void Update()
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null)
                return;

            if (world.Player2 == null)
            {
                if (CoopUi.Owner != world.Player)
                    CoopUi.Reset();
                return;
            }

            if (!CoopUi.WindowOpen)
            {
                if (CoopUi.Owner != world.Player)
                    CoopUi.Reset();
                return;
            }

            if (CoopUi.GuestOwns && world.Player2.HandsItem != null &&
                GamepadCursor.ButtonDown(0) && !Ui.Hover)
                CoopUi.DropGuestHands();
        }
    }
}
