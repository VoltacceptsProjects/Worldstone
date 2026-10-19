using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.UI;
using Diablerie.Engine.Utility;
using Diablerie.Game.UI.Inventory;
using Diablerie.Game.UI.Menu.CharacterSelect;
using Diablerie.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    // The gambling screen, built in code from the original MPQ art (same approach as StashPanel).
    // Left click an item to buy it: the price is taken and the item, now identified, is on your cursor.
    // In trade mode the stock is the vendor's regular wares, and clicking with an item on the cursor sells it.
    public class GamblePanel
    {
        // Asset paths. buysell.dc6 is the merchant screen (d2data, shared by classic and the expansion).
        // trade.dc6 is player to player trading and NpcInv.dc6 is the follower inventory, so neither is used.
        private const string VendorBackgroundPath = @"data\global\ui\panel\buysell";
        private const string GoldButtonPath = @"data\global\ui\panel\goldcoinbtn";
        private const string ButtonsPath = @"data\global\ui\panel\buysellbtn";

        private const int CloseFrameIdle = 10;
        private const int CloseFramePressed = 11;
        private const int GoldFrameIdle = 0;

        private const float PanelWidth = 320;
        private const float PanelHeight = 432;
        private const float PanelLeftEdge = -320;
        private const float PanelVerticalShift = PanelFrame.UiShift;
        private const float CloseDistance = 10;

        // TUNE: positions inside of the panel, in pixels from the top left corner. They depend on the artwork.
        private const string TitleText = "Gamble";
        private const string TradeTitleText = "Trade";
        // The art has an empty text bar above the buttons. Like the stash, the title sits in it, left aligned.
        private const float TitleLeftX = 22;
        private const float TitleCenterY = 368;
        private const float GoldButtonCenterX = 40;
        private const float GoldCenterY = 400;
        private const float GoldTextX = 62;
        private const float TextRaise = 2;
        // Fine adjustment of the item grid against the artwork's cells. Positive X moves it right, positive Y up.
        private const float GridNudgeX = 0;
        private const float GridNudgeY = 0;
        private const float CloseCenterFromRight = 30;
        private const float CloseCenterFromBottom = 30;

        private static readonly Color TitleColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly string PriceHex = "ffd24a";

        private static GamblePanel _instance;

        private readonly RectTransform _root;
        private readonly PanelSheet _buttons;
        private readonly PanelSheet _goldButton;
        private readonly Vector2 _size;
        private readonly StashLayout _layout;
        private InventoryGrid _grid;
        private Text _goldLabel;

        private Player _player;
        private Engine.Inventory _stock;
        private Text _title;
        private bool _trade;
        private Vector3 _openedAt;

        // Public API
        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

        public static void Show(Player player, string vendorId, bool trade = false)
        {
            GetInstance(player).ShowInternal(player, vendorId, trade);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance.HideInternal();
        }

        private static GamblePanel GetInstance(Player player)
        {
            if (_instance == null || _instance._root == null)
                _instance = new GamblePanel(player);
            return _instance;
        }

        // Construction
        private GamblePanel(Player player)
        {
            _layout = StashLayout.GetVendor();
            var background = PanelSheet.Load(VendorBackgroundPath);
            _buttons = PanelSheet.Load(ButtonsPath);
            _goldButton = PanelSheet.Load(GoldButtonPath);

            Debug.Log("GamblePanel layout " + _layout + ", background " + (background == null ? "missing" : VendorBackgroundPath + " " + background.Describe()));

            _root = MenuWidgets.NewRect("Gamble Panel", Ui.instance.RectTransform);
            PanelLayer.Place(_root);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0, 0.5f);
            _root.anchoredPosition = new Vector2(PanelLeftEdge, PanelVerticalShift);

            _size = BuildBackground(background);
            _root.sizeDelta = _size;
            PanelFrame.StoneBase(_root);

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
            Debug.Log("GamblePanel tiled background size " + size.x + "x" + size.y + " (expected " + PanelWidth + "x" + PanelHeight + ")");
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
            _title = MenuWidgets.Label("Title", _root, TitleText, Fonts.GetFont16(), TitleColor, TextAnchor.MiddleLeft);
            var rect = _title.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(180, 30);
            rect.anchoredPosition = new Vector2(TitleLeftX, -TitleCenterY - (MenuWidgets.TextNudge - TextRaise));
        }

        private void BuildGrid(Player player)
        {
            float width = _layout.columns * _layout.cellWidth;
            float height = _layout.rows * _layout.cellHeight;

            // InventoryGrid works from the bottom left corner of its rect
            var rect = MenuWidgets.NewRect("Gamble Grid", _root);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(_layout.gridLeft + GridNudgeX, -(_layout.gridTop + height) + GridNudgeY);

            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            // The grid starts out on the player's inventory and is pointed at the vendor's stock when shown
            _grid = rect.gameObject.AddComponent<InventoryGrid>();
            _grid.allowRightClick = false;
            // Cells are exactly as big as the artwork's, whatever the rect or inventory.txt say
            _grid.SetCellSize(_layout.cellWidth, _layout.cellHeight);
            _grid.clickOverride = OnGridClicked;
            _grid.describeOverride = Describe;
        }

        private void BuildGold()
        {
            // Only a picture here, gold is never moved on this screen
            var rect = MenuWidgets.NewRect("Gold Icon", _root);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(GoldButtonCenterX, -GoldCenterY);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (_goldButton != null && _goldButton.sprites.Length > GoldFrameIdle)
            {
                var sprite = _goldButton.sprites[GoldFrameIdle];
                image.sprite = sprite;
                rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            }
            else
            {
                image.color = Color.clear;
                rect.sizeDelta = new Vector2(32, 32);
            }

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
                Debug.LogWarning("Gamble close button frames are missing, the panel can still be closed with Escape");
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
        private void ShowInternal(Player player, string vendorId, bool trade)
        {
            _player = player;
            _trade = trade;
            _openedAt = player.unit.iso.pos;
            _stock = trade ? VendorStock.GetStock(vendorId, player) : Gambling.GetStock(vendorId, player);
            _title.text = trade ? TradeTitleText : TitleText;

            _grid.Bind(player, _stock);
            WaypointPanel.Hide();
            StashPanel.Hide();
            // The carried inventory is needed next to the shop: bought items go there
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
            // The inventory opened together with the shop closes with it
            InventoryPanel.instance.visible = false;
            Ui.HideScreenLabel();
        }

        private string Describe(Item item)
        {
            if (_trade)
            {
                int tradePrice = VendorStock.BuyPrice(item);
                bool canPay = _player != null && _player.inventory.gold >= tradePrice;
                return item.GetDescription() + "\n<color=#" + (canPay ? PriceHex : Colors.ItemRedHex) + ">Price: " + tradePrice + " Gold</color>";
            }

            int price = Gambling.Price(item);
            bool affordable = _player != null && _player.inventory.gold >= price;
            string color = affordable ? PriceHex : Colors.ItemRedHex;
            return item.GetDescription(hideQuality: true) + "\n<color=#" + color + ">Price: " + price + " Gold</color>";
        }

        private void OnGridClicked(Vector2i cell, PointerEventData.InputButton button)
        {
            if (_player == null || _stock == null || button != PointerEventData.InputButton.Left)
                return;

            if (_trade)
            {
                OnTradeClicked(cell);
                return;
            }

            switch (Gambling.Buy(_player, _stock, cell.x, cell.y))
            {
                case Gambling.BuyResult.Bought:
                    AudioManager.instance.Play("item_gold");
                    AudioManager.instance.Play(SoundInfo.itemPickup);
                    Ui.HideScreenLabel();
                    RefreshGold();
                    break;
                case Gambling.BuyResult.NotEnoughGold:
                    AudioManager.instance.Play("cursor_error");
                    break;
                case Gambling.BuyResult.HandsNotEmpty:
                    AudioManager.instance.Play("cursor_error");
                    break;
            }
        }

        private void OnTradeClicked(Vector2i cell)
        {
            if (_player.HandsItem != null)
            {
                if (VendorStock.Sell(_player, _stock))
                {
                    AudioManager.instance.Play("item_gold");
                    Ui.HideScreenLabel();
                    RefreshGold();
                }
                return;
            }

            switch (VendorStock.Buy(_player, _stock, cell.x, cell.y))
            {
                case VendorStock.BuyResult.Bought:
                    AudioManager.instance.Play("item_gold");
                    AudioManager.instance.Play(SoundInfo.itemPickup);
                    Ui.HideScreenLabel();
                    RefreshGold();
                    break;
                case VendorStock.BuyResult.NotEnoughGold:
                    AudioManager.instance.Play("cursor_error");
                    break;
                case VendorStock.BuyResult.HandsNotEmpty:
                    AudioManager.instance.Play("cursor_error");
                    break;
            }
        }

        private void RefreshGold()
        {
            if (_player != null)
                _goldLabel.text = _player.inventory.gold.ToString();
        }

        private void Tick()
        {
            if (_player == null)
                return;
            RefreshGold();
            // Closing the inventory (key, button, anything) closes the shop too
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

        private class Watcher : MonoBehaviour
        {
            public GamblePanel panel;

            private void Update()
            {
                panel.Tick();
            }
        }
    }
}