using System;
using Diablerie.Engine;
using Diablerie.Engine.UI;
using Diablerie.Game.UI.Inventory;
using Diablerie.Game.UI.Menu.CharacterSelect;
using Diablerie.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    // The stash screen, built in code from the original MPQ assets (same approach as WaypointPanel).
    // Opened by objects.txt "bank" (OperateFn 32). The grid size and position come from inventory.txt.
    public class StashPanel
    {
        public const int OperateFn = 32;

        // Asset paths. The expansion ships TradeStash, classic has bank.
        private const string ExpansionBackgroundPath = @"data\global\ui\panel\tradestash";
        private const string ClassicBackgroundPath = @"data\global\ui\panel\bank";
        private const string GoldButtonPath = @"data\global\ui\panel\goldcoinbtn";
        private const string ButtonsPath = @"data\global\ui\panel\buysellbtn";

        // Same frames the waypoint panel uses for its close button
        private const int CloseFrameIdle = 10;
        private const int CloseFramePressed = 11;
        private const int GoldFrameIdle = 0;

        // Same placement as the other left side panel
        private const float PanelWidth = 320;
        private const float PanelHeight = 432;
        private const float PanelLeftEdge = -320;
        private const float PanelVerticalShift = 0;
        private const float CloseDistance = 10;

        // TUNE: positions inside of the panel, in pixels from the top left corner. They depend on the artwork.
        private const bool ShowTitle = true;
        private const string TitleText = "Stash";
        // The art has two black bars at the top: the upper one has a small socket for the coin button on its
        // left and holds the gold amount, the lower one holds the title
        private const float TitleCenterY = 58;
        private const float GoldButtonCenterX = 83;
        private const float GoldCenterY = 33;
        private const float GoldTextX = 100;
        private const float TextRaise = 2;
        // TUNE: fine adjustment of the item grid against the artwork's cells, in panel pixels.
        // The art has a small margin inside of its frame that inventory.txt does not include.
        // Positive X moves the grid right, positive Y moves it up.
        private const float GridNudgeX = 3;
        private const float GridNudgeY = -10;
        private const float CloseCenterFromRight = 30;
        private const float CloseCenterFromBottom = 30;

        // The expansion's stash gold limit
        private const int StashGoldLimit = 2500000;

        private static readonly Color TitleColor = new Color(0.85f, 0.85f, 0.85f);

        private static StashPanel _instance;

        private readonly RectTransform _root;
        private readonly PanelSheet _buttons;
        private readonly PanelSheet _goldButton;
        private readonly Vector2 _size;
        private readonly StashLayout _layout;
        private InventoryGrid _grid;
        private Text _goldLabel;

        private Player _player;
        private Vector3 _openedAt;

        // Public API
        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

        public static void Show(Player player)
        {
            GetInstance(player).ShowInternal(player);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance.HideInternal();
        }

        private static StashPanel GetInstance(Player player)
        {
            if (_instance == null || _instance._root == null)
                _instance = new StashPanel(player);
            return _instance;
        }

        // Construction
        private StashPanel(Player player)
        {
            _layout = StashLayout.Get();
            var background = PanelSheet.Load(ExpansionBackgroundPath) ?? PanelSheet.Load(ClassicBackgroundPath);
            _buttons = PanelSheet.Load(ButtonsPath);
            _goldButton = PanelSheet.Load(GoldButtonPath);

            Debug.Log("StashPanel layout " + _layout + ", background " + (background == null ? "missing" : background.Describe()) +
                      ", gold button " + (_goldButton == null ? "missing" : _goldButton.Describe()));

            _root = MenuWidgets.NewRect("Stash Panel", Ui.instance.RectTransform);
            PanelLayer.Place(_root);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0, 0.5f);
            _root.anchoredPosition = new Vector2(PanelLeftEdge, PanelVerticalShift);

            _size = BuildBackground(background);
            _root.sizeDelta = _size;

            if (ShowTitle)
                BuildTitle();
            BuildGrid(player);
            BuildGold();
            BuildCloseButton();

            _root.gameObject.AddComponent<Watcher>().panel = this;
            _root.gameObject.SetActive(false);
        }

        private Vector2 BuildBackground(PanelSheet background)
        {
            if (background == null || background.frames.Length == 0)
            {
                var box = MenuWidgets.Box("Background", _root, new Color(0.05f, 0.05f, 0.05f, 0.92f));
                MenuWidgets.Stretch(box.rectTransform);
                box.raycastTarget = true;
                return new Vector2(PanelWidth, PanelHeight);
            }

            var topBlank = new int[background.sprites.Length];
            var bottomBlank = new int[background.sprites.Length];
            for (int i = 0; i < background.sprites.Length; ++i)
            {
                topBlank[i] = PanelSheet.CountBlankRows(background.sprites[i], true);
                bottomBlank[i] = PanelSheet.CountBlankRows(background.sprites[i], false);
            }

            Vector2 size = background.TileLayout(PanelWidth, topBlank, bottomBlank, out var positions);
            Debug.Log("StashPanel tiled background size " + size.x + "x" + size.y + " (expected " + PanelWidth + "x" + PanelHeight + ")");
            for (int i = 0; i < background.frames.Length; ++i)
            {
                var frame = background.frames[i];
                var image = CreateFrameImage("Background " + i, _root, background.sprites[i], new Vector2(frame.width, frame.height), positions[i]);
                image.raycastTarget = true;
            }
            return size;
        }

        private void BuildTitle()
        {
            var title = MenuWidgets.Label("Title", _root, TitleText, Fonts.GetFont16(), TitleColor, TextAnchor.MiddleCenter);
            var rect = title.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_size.x, 30);
            rect.anchoredPosition = new Vector2(_size.x / 2, -TitleCenterY - (MenuWidgets.TextNudge - TextRaise));
        }

        private void BuildGrid(Player player)
        {
            float width = _layout.columns * _layout.cellWidth;
            float height = _layout.rows * _layout.cellHeight;

            // InventoryGrid works from the bottom left corner of its rect
            var rect = MenuWidgets.NewRect("Stash Grid", _root);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(_layout.gridLeft + GridNudgeX, -(_layout.gridTop + height) + GridNudgeY);

            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            InventoryGrid.pendingInventory = player.stash;
            _grid = rect.gameObject.AddComponent<InventoryGrid>();
            _grid.allowRightClick = false; // no using or identifying items from the stash
        }

        private void BuildGold()
        {
            var rect = MenuWidgets.NewRect("Gold Button", _root);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(GoldButtonCenterX, -GoldCenterY);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            if (_goldButton != null && _goldButton.sprites.Length > GoldFrameIdle)
            {
                var sprite = _goldButton.sprites[GoldFrameIdle];
                image.sprite = sprite;
                rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            }
            else
            {
                image.color = new Color(0, 0, 0, 0.01f);
                rect.sizeDelta = new Vector2(32, 32);
            }

            // Left click deposits all carried gold, right click withdraws everything from the stash
            var clickable = rect.gameObject.AddComponent<Clickable>();
            clickable.onClick = OnGoldClicked;

            _goldLabel = MenuWidgets.Label("Gold Amount", _root, "0", Fonts.GetFont16(), MenuWidgets.Gold, TextAnchor.MiddleLeft);
            var labelRect = _goldLabel.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.sizeDelta = new Vector2(140, 24);
            labelRect.anchoredPosition = new Vector2(GoldTextX, -GoldCenterY - (MenuWidgets.TextNudge - TextRaise));
        }

        private void BuildCloseButton()
        {
            if (_buttons == null || _buttons.sprites.Length <= CloseFramePressed)
            {
                Debug.LogWarning("Stash close button frames are missing, the panel can still be closed with Escape");
                return;
            }

            var idle = _buttons.sprites[CloseFrameIdle];
            var pressed = _buttons.sprites[CloseFramePressed];

            var rect = MenuWidgets.NewRect("Close Button", _root);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(idle.rect.width, idle.rect.height);
            rect.anchoredPosition = new Vector2(_size.x - CloseCenterFromRight, -(_size.y - CloseCenterFromBottom));

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = idle;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = pressed };
            button.onClick.AddListener(() =>
            {
                AudioManager.instance.Play("cursor_select");
                HideInternal();
            });
        }

        // Behavior
        private void ShowInternal(Player player)
        {
            _player = player;
            _openedAt = player.unit.iso.pos;

            _grid.Bind(player, player.stash);
            WaypointPanel.Hide();
            // The carried inventory is needed next to the stash to move items between them
            InventoryPanel.instance.visible = true;
            CharstatPanel.instance.visible = false;

            _root.gameObject.SetActive(true);
            RefreshGold();
        }

        private void HideInternal()
        {
            if (!_root.gameObject.activeSelf)
                return;

            _root.gameObject.SetActive(false);
            // The inventory opened together with the stash closes with it
            InventoryPanel.instance.visible = false;
            Ui.HideScreenLabel();
            WorldBuilder.SaveCurrentPlayer();
        }

        private void OnGoldClicked(PointerEventData.InputButton button)
        {
            if (_player == null)
                return;

            var carried = _player.inventory;
            var stash = _player.stash;
            if (button == PointerEventData.InputButton.Left)
            {
                int amount = Mathf.Min(carried.gold, StashGoldLimit - stash.gold);
                if (amount <= 0)
                    return;
                carried.gold -= amount;
                stash.gold += amount;
            }
            else if (button == PointerEventData.InputButton.Right)
            {
                if (stash.gold <= 0)
                    return;
                carried.gold += stash.gold;
                stash.gold = 0;
            }
            else
            {
                return;
            }

            AudioManager.instance.Play("cursor_select");
            RefreshGold();
        }

        private void RefreshGold()
        {
            if (_player != null)
                _goldLabel.text = _player.stash.gold.ToString();
        }

        private void Tick()
        {
            if (_player == null)
                return;
            RefreshGold();
            // Closing the inventory (key, button, anything) closes the stash too
            if (!InventoryPanel.instance.visible)
            {
                HideInternal();
                return;
            }
            if (Vector3.Distance(_openedAt, _player.unit.iso.pos) > CloseDistance)
                HideInternal();
        }

        // Helpers
        private static Image CreateFrameImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 topLeft)
        {
            var rect = MenuWidgets.NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // Types
        private class Clickable : MonoBehaviour, IPointerClickHandler
        {
            public Action<PointerEventData.InputButton> onClick;

            public void OnPointerClick(PointerEventData eventData)
            {
                onClick?.Invoke(eventData.button);
            }
        }

        private class Watcher : MonoBehaviour
        {
            public StashPanel panel;

            private void Update()
            {
                panel.Tick();
            }
        }
    }
}