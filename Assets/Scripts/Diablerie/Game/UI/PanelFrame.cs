using Diablerie.Game.UI.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    // The ornate stone border of the original 800x600 screen (800borderframe.dc6). It is drawn behind the
    // panels: the left half while a left panel (quest log, waypoints, stash, vendor, character) is open,
    // the right half while the inventory is open. The layout is the one of the original game:
    //   left  (400 wide): [0] 256x256 top left, [1] 144x66 top, [2] 85x231 left edge, [3] 256x71 and [4] 145x71 bottom
    //   right (400 wide): [5] 145x66 top, [6] 256x256 top right, [7] 87x231 right edge, [9] 145x71 and [8] 256x71 bottom
    public class PanelFrame : MonoBehaviour
    {
        private const string SheetPath = @"data\global\ui\panel\800borderframe";
        private const float HalfWidth = 400;
        private const float FrameHeight = 558;
        // The panels of the original game start at the very top of the 800x600 screen. Everything that is part of
        // the panel layout (this border, the inventory and character panels, the code-built left panels) is shifted
        // up by this much, so the border touches the top of the screen and no world shows above it.
        public const float UiShift = 25;
        private const float TopOffset = 275 + UiShift; // distance from the vertical center of the screen to the top of the border
        private const float InteriorHeight = 487; // row where the side pieces end and the bottom pieces begin
        private const float BottomPieceBlankFallback = 24; // blank rows at the top of the bottom pieces, if they cannot be measured
        private const byte AlphaCutoff = 8; // compressed textures never hit a perfect 0 alpha; anything below this is blank
        private const float StoneOverlap = 2; // extra rows of stone behind the ornament so a 1px measurement error can't reopen a seam

        // Same stone the panels use for their own backing
        private static readonly Color StoneColor = new Color(0.17f, 0.17f, 0.16f, 1f);

        private static PanelFrame _instance;

        private RectTransform _left;
        private RectTransform _right;

        // Call from anywhere once the inventory exists; creates the border once
        public static void Ensure()
        {
            if (_instance != null)
                return;
            var inventory = InventoryPanel.instance;
            if (inventory == null || !(inventory.transform.parent is RectTransform parent))
                return;
            var sheet = PanelSheet.Load(SheetPath);
            if (sheet == null || sheet.sprites.Length < 10)
                return;

            var go = new GameObject("Panel Frame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var host = go.AddComponent<PanelFrame>();
            host._left = Half("Frame Left", go.transform, -HalfWidth, sheet, new[]
            {
                new Piece(0, 0, 0), new Piece(1, 256, 0), new Piece(2, 0, 256), new Piece(3, 0, 487), new Piece(4, 256, 487)
            });
            host._right = Half("Frame Right", go.transform, 0, sheet, new[]
            {
                new Piece(5, 0, 0), new Piece(6, 145, 0), new Piece(7, 313, 256), new Piece(9, 0, 487), new Piece(8, 145, 487)
            });
            _instance = host;
            ShiftScenePanels(inventory);
            // Right below the inventory so every panel, tooltip and cursor stays above the border
            go.transform.SetSiblingIndex(inventory.transform.GetSiblingIndex());
            Stretch(go.GetComponent<RectTransform>());
        }

        // The inventory and the character panel come from the scene and sit where the border used to be, UiShift lower
        private static void ShiftScenePanels(InventoryPanel inventory)
        {
            Shift(inventory.panel);
            if (CharstatPanel.instance != null)
                Shift(CharstatPanel.instance.panel);
        }

        private static void Shift(GameObject panel)
        {
            if (panel != null && panel.transform is RectTransform rect)
                rect.anchoredPosition += new Vector2(0, UiShift);
        }

        // Fills the area behind a panel's art with stone grey, so parts of the sheet that are blank still look like stone
        public static void StoneBase(RectTransform root)
        {
            var go = new GameObject("Stone Base", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = StoneColor;
            image.raycastTarget = false;
            rect.SetAsFirstSibling();
        }

        private struct Piece
        {
            public int frame;
            public float x, y;
            public Piece(int frame, float x, float y) { this.frame = frame; this.x = x; this.y = y; }
        }

        private static RectTransform Half(string name, Transform parent, float left, PanelSheet sheet, Piece[] pieces)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(HalfWidth, FrameHeight);
            rect.anchoredPosition = new Vector2(left, TopOffset);

            // Measure how many transparent rows the bottom pieces have at their top. They are left in their original
            // position; the stone fill below covers those rows instead of pulling the pieces up, which would leave a
            // fresh gap at the very bottom next to the orbs.
            float blank = 0;
            foreach (var piece in pieces)
            {
                if (piece.y >= InteriorHeight)
                    blank = Mathf.Max(blank, TopBlankRows(sheet.sprites[piece.frame]));
            }

            foreach (var piece in pieces)
            {
                var sprite = sheet.sprites[piece.frame];
                var child = new GameObject("Border " + piece.frame, typeof(RectTransform));
                var r = child.GetComponent<RectTransform>();
                r.SetParent(rect, false);
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
                r.anchoredPosition = new Vector2(piece.x, -piece.y);
                var image = child.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
            }

            // Stone backing: covers the whole panel interior and the blank top rows of the bottom pieces, so the
            // panel background flows straight into the bottom ornament instead of showing the world through a strip
            // above the orbs. It sits behind all border art and only reaches up to where the ornament actually
            // starts, so the ornament's cut-outs stay see-through.
            var stone = new GameObject("Stone Fill", typeof(RectTransform));
            var stoneRect = stone.GetComponent<RectTransform>();
            stoneRect.SetParent(rect, false);
            stoneRect.anchorMin = stoneRect.anchorMax = new Vector2(0, 1);
            stoneRect.pivot = new Vector2(0, 1);
            stoneRect.sizeDelta = new Vector2(HalfWidth, InteriorHeight + blank + StoneOverlap);
            stoneRect.anchoredPosition = Vector2.zero;
            var stoneImage = stone.AddComponent<Image>();
            stoneImage.color = StoneColor;
            stoneImage.raycastTarget = false;
            stoneRect.SetAsFirstSibling();

            go.SetActive(false);
            return rect;
        }

        // Fully transparent pixel rows at the top edge of a sprite. Uses an alpha cutoff because compressed
        // textures store "transparent" pixels with small non-zero alpha values.
        private static float TopBlankRows(Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null || !texture.isReadable)
                return BottomPieceBlankFallback;

            var rect = sprite.textureRect;
            var pixels = texture.GetPixels32();
            int x0 = (int) rect.x, y0 = (int) rect.y, width = (int) rect.width, height = (int) rect.height;
            for (int i = 0; i < height; ++i)
            {
                int row = y0 + height - 1 - i;
                int offset = row * texture.width + x0;
                for (int x = 0; x < width; ++x)
                {
                    if (pixels[offset + x].a > AlphaCutoff)
                        return i;
                }
            }
            return 0;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            PanelLayer.KeepHudOnTop();
            bool leftOpen = QuestPanel.IsVisible || WaypointPanel.IsVisible || StashPanel.IsVisible || GamblePanel.IsVisible
                            || (CharstatPanel.instance != null && CharstatPanel.instance.visible);
            bool rightOpen = InventoryPanel.instance != null && InventoryPanel.instance.visible;
            if (_left.gameObject.activeSelf != leftOpen)
                _left.gameObject.SetActive(leftOpen);
            if (_right.gameObject.activeSelf != rightOpen)
                _right.gameObject.SetActive(rightOpen);
        }
    }
}