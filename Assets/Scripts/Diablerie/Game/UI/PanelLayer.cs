using Diablerie.Game.UI.Inventory;
using UnityEngine;

namespace Diablerie.Game.UI
{
    // Puts a code-built panel on the same UI level as the panels of the original game: next to the
    // inventory panel, under the item tooltip, the cursor and everything else that is drawn later.
    public static class PanelLayer
    {
        public static void Place(RectTransform root)
        {
            var inventory = InventoryPanel.instance;
            if (inventory == null)
                return;

            var reference = inventory.transform;
            if (root.parent != reference.parent)
                root.SetParent(reference.parent, false);
            // Call this right after creating the panel, while it is still the last child
            root.SetSiblingIndex(reference.GetSiblingIndex() + 1);
        }
    }
}
