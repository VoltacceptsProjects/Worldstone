using System.Collections.Generic;
using Diablerie.Game.UI.ControlPanel;
using Diablerie.Game.UI.Inventory;
using UnityEngine;

namespace Diablerie.Game.UI
{
    // Puts a code-built panel on the same UI level as the panels of the original game: next to the
    // inventory panel, under the item tooltip, the cursor and everything else that is drawn later.
    public static class PanelLayer
    {
        private static readonly List<Transform> Panels = new List<Transform>();
        private static Transform _hud;

        public static void Place(RectTransform root)
        {
            var inventory = InventoryPanel.instance;
            if (inventory == null)
                return;

            PanelFrame.Ensure();
            var reference = inventory.transform;
            if (root.parent != reference.parent)
                root.SetParent(reference.parent, false);
            // Call this right after creating the panel, while it is still the last child
            root.SetSiblingIndex(reference.GetSiblingIndex() + 1);
            if (!Panels.Contains(root))
                Panels.Add(root);
            KeepHudOnTop();
        }

        // The HUD bar at the bottom (life and mana bulbs, belt, skill buttons, mini panel) is drawn over every panel
        // and over the border, but still under the item tooltip and the cursor. Cheap enough to call every frame.
        public static void KeepHudOnTop()
        {
            var inventory = InventoryPanel.instance;
            if (inventory == null)
                return;
            var parent = inventory.transform.parent;

            if (_hud == null)
            {
                var bulb = Object.FindObjectOfType<LifeBulb>();
                if (bulb == null)
                    return;
                _hud = bulb.transform;
                while (_hud.parent != null && _hud.parent != parent)
                    _hud = _hud.parent;
                if (_hud.parent != parent)
                {
                    _hud = null;
                    return;
                }
            }

            int top = -1;
            Consider(inventory.transform, parent, ref top);
            if (CharstatPanel.instance != null && CharstatPanel.instance.panel != null)
                Consider(CharstatPanel.instance.panel.transform, parent, ref top);
            foreach (var panel in Panels)
            {
                if (panel != null)
                    Consider(panel, parent, ref top);
            }
            var frame = parent.Find("Panel Frame");
            if (frame != null)
                top = Mathf.Max(top, frame.GetSiblingIndex());

            // Only ever moves the HUD up, to just above the topmost panel
            if (_hud.GetSiblingIndex() < top)
                _hud.SetSiblingIndex(top);
        }

        // Panels may be nested, so use the child of the common parent that contains them
        private static void Consider(Transform panel, Transform parent, ref int top)
        {
            while (panel != null && panel.parent != parent)
                panel = panel.parent;
            if (panel != null && panel != _hud)
                top = Mathf.Max(top, panel.GetSiblingIndex());
        }
    }
}