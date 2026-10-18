using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine
{
    // Rules for identifying items. Deckard Cain identifies everything you carry for free,
    // a Scroll of Identify (or a charge of a Tome of Identify) identifies a single item.
    public static class ItemIdentification
    {
        public static bool IsIdentifyItem(Item item)
        {
            return item != null && item.info.misc != null && item.info.misc.useFunction == MiscInfo.UseFunction.IdentifyItem;
        }

        public static bool NeedsIdentifying(Item item)
        {
            return item != null && !item.identified;
        }

        // Identifies every unidentified item the player has: inventory, stash, cursor and equipment.
        // Returns how many items were identified.
        public static int IdentifyAll(Player player)
        {
            if (player == null)
                return 0;

            int count = IdentifyAll(player.inventory) + IdentifyAll(player.stash);

            if (Identify(player.HandsItem))
                ++count;

            if (player.equip != null && player.equip.items != null)
            {
                foreach (var item in player.equip.items)
                {
                    if (Identify(item))
                        ++count;
                }
            }

            return count;
        }

        private static int IdentifyAll(Inventory inventory)
        {
            if (inventory == null)
                return 0;

            int count = 0;
            foreach (var entry in inventory.entries)
            {
                if (Identify(entry.item))
                    ++count;
            }

            if (count > 0)
                inventory.NotifyChanged();
            return count;
        }

        private static bool Identify(Item item)
        {
            if (!NeedsIdentifying(item))
                return false;
            item.identified = true;
            return true;
        }

        // Uses up the scroll (or one charge of a tome) to identify the target. The scroll must be
        // in the player's inventory. Returns false if nothing was identified or nothing was spent.
        public static bool IdentifyWithScroll(Player player, Item scroll, Item target)
        {
            if (player == null || !IsIdentifyItem(scroll) || !NeedsIdentifying(target))
                return false;

            if (!ConsumeScroll(player, scroll))
                return false;

            target.identified = true;
            player.inventory.NotifyChanged();
            player.stash?.NotifyChanged();
            return true;
        }

        private static bool ConsumeScroll(Player player, Item scroll)
        {
            var entries = player.inventory.entries;
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries[i].item != scroll)
                    continue;

                // Tomes keep their place and lose a scroll, plain scrolls are used up
                if (scroll.info.code == "ibk")
                {
                    if (scroll.quantity <= 0)
                        return false;
                    --scroll.quantity;
                    player.inventory.NotifyChanged();
                }
                else if (scroll.quantity > 1)
                {
                    --scroll.quantity;
                    player.inventory.NotifyChanged();
                }
                else
                {
                    player.inventory.Take(entries[i].x, entries[i].y);
                }
                return true;
            }
            return false;
        }
    }
}
