using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.UI;
using Diablerie.Engine.Utility;
using Diablerie.Engine.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI.Inventory
{
    public class InventoryGrid :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        // Set right before AddComponent to make the grid show an inventory other than the player's own
        public static Engine.Inventory pendingInventory;

        // The stash turns this off: no using or identifying items straight from it
        public bool allowRightClick = true;

        // Shops take over the grid: every click goes here instead of moving items, and the tooltip can be
        // replaced (to show a price and keep gambled items unknown).
        public System.Action<Vector2i, PointerEventData.InputButton> clickOverride;
        public System.Func<Item, string> describeOverride;

        private Player player;
        private Engine.Inventory _inventory;

        private float cellSizeX;
        private float cellSizeY;

        // Shops know the exact size of the artwork's cells. When set, it is used instead of dividing the
        // rect by the inventory size, so the items always land on the cells that are drawn.
        private bool _fixedCellSize;
        private float _fixedCellX;
        private float _fixedCellY;

        public void SetCellSize(float width, float height)
        {
            _fixedCellSize = true;
            _fixedCellX = width;
            _fixedCellY = height;
            if (_inventory != null && highlighter != null)
            {
                RefreshCellSize();
                OnInventoryUpdate();
            }
        }

        private RawImage highlighter;
        private RectTransform rectTransform;
        private bool pointerOver;

        private List<Image> itemsImages = new List<Image>();
        private List<RawImage> itemsBackgrounds = new List<RawImage>();

        public Engine.Inventory inventory
        {
            get { return _inventory; }
            set
            {
                if (_inventory == value)
                    return;

                if (_inventory != null)
                    _inventory.OnUpdate -= OnInventoryUpdate;

                _inventory = value;
                if (_inventory != null)
                    _inventory.OnUpdate += OnInventoryUpdate;

                RefreshCellSize();
                OnInventoryUpdate();
            }
        }

        private void RefreshCellSize()
        {
            if (_fixedCellSize)
            {
                cellSizeX = _fixedCellX;
                cellSizeY = _fixedCellY;
                return;
            }
            if (_inventory == null || rectTransform == null)
                return;
            cellSizeX = rectTransform.rect.width / _inventory.sizeX;
            cellSizeY = rectTransform.rect.height / _inventory.sizeY;
        }

        // Every child (items, backgrounds, highlighter) is placed from the bottom left corner of the grid rect
        // with explicit anchors, so it no longer matters where the grid's own pivot or anchors are.
        private static void SetBottomLeft(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(x, y);
        }
    
        private Image CreateItemImage(Item item)
        {
            GameObject gameObject = new GameObject();
            gameObject.name = item.info.name;
            var image = gameObject.AddComponent<Image>();
            image.transform.SetParent(transform, false);
            SetBottomLeft(image.rectTransform, 0, 0);
            return image;
        }

        private void Awake()
        {
            rectTransform = transform as RectTransform;

            highlighter = CreateHighlighter();
            highlighter.gameObject.SetActive(false);

            // TODO pass the player and the inventory to the constructor (it's required to get rid of prefabs first)
            player = Diablerie.Game.Coop.CoopUi.Owner ?? WorldState.instance.Player;
            if (pendingInventory != null)
            {
                inventory = pendingInventory;
                pendingInventory = null;
            }
            else
            {
                inventory = player.inventory;
            }
        }

        public void Bind(Player newPlayer, Engine.Inventory newInventory)
        {
            player = newPlayer;
            // Not awake yet: Awake takes the inventory of the window owner
            if (highlighter == null)
                return;
            inventory = newInventory;
        }

        RawImage CreateHighlighter()
        {
            GameObject gameObject = new GameObject("highlighter");
            var image = gameObject.AddComponent<RawImage>();
            image.transform.SetParent(transform, false);
            image.color = Colors.InvItemHighlight;
            SetBottomLeft(image.rectTransform, 0, 0);
            return image;
        }

        private void OnEnable()
        {
            if (_inventory == null || highlighter == null)
                return;
            RefreshCellSize();
            OnInventoryUpdate();
        }

        // The stash panel is sized after it is built, so follow any later change of the grid rect
        private void OnRectTransformDimensionsChange()
        {
            if (_inventory == null || highlighter == null)
                return;
            RefreshCellSize();
            OnInventoryUpdate();
        }

        void OnInventoryUpdate()
        {
            for(int i = 0; i < _inventory.entries.Count; ++i)
            {
                var entry = _inventory.entries[i];
                Image image;
                RawImage bg;
                if (i >= itemsImages.Count)
                {
                    bg = CreateHighlighter();
                    itemsBackgrounds.Add(bg);
                    image = CreateItemImage(entry.item);
                    itemsImages.Add(image);
                }
                else
                {
                    bg = itemsBackgrounds[i];
                    bg.gameObject.SetActive(true);
                    image = itemsImages[i];
                    image.gameObject.SetActive(true);
                }
            
                image.sprite = entry.item.invSprite;
                image.SetNativeSize();
                SetBottomLeft(image.rectTransform, entry.x * cellSizeX, entry.y * cellSizeY);

                bg.color = Colors.InvItemBackground;
                SetRect(bg, entry.x, entry.y, entry.item.info.invWidth, entry.item.info.invHeight);
            }

            for (int i = _inventory.entries.Count; i < itemsImages.Count; ++i)
            {
                itemsImages[i].gameObject.SetActive(false);
                itemsBackgrounds[i].gameObject.SetActive(false);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (clickOverride != null)
            {
                clickOverride(GetPointerCell(Diablerie.Engine.UI.GamepadCursor.Position), eventData.button);
                return;
            }

            var cell = MouseCell();

            // A Scroll of Identify is waiting: left click picks the item, right click puts the scroll away
            if (player.IdentifyScroll != null)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    player.CancelIdentify();
                }
                else if (eventData.button == PointerEventData.InputButton.Left && allowRightClick)
                {
                    var target = _inventory.ItemAt(cell.x, cell.y);
                    if (ItemIdentification.NeedsIdentifying(target) && player.IdentifyWithScroll(target))
                    {
                        AudioManager.instance.Play("cursor_identify_item");
                        OnInventoryUpdate();
                    }
                }
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (player.HandsItem != null)
                {
                    Item poppedItem;
                    if (_inventory.Put(player.HandsItem, cell.x, cell.y, out poppedItem))
                    {
                        AudioManager.instance.Play(player.HandsItem.dropSound);
                        player.HandsItem = poppedItem;
                    }
                }
                else
                {
                    Item item = _inventory.Take(cell.x, cell.y);
                    if (item != null)
                    {
                        player.HandsItem = item;
                        AudioManager.instance.Play(SoundInfo.itemPickup);
                    }
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right && allowRightClick)
            {
                Item item = _inventory.ItemAt(cell.x, cell.y);
                if (item != null)
                {
                    if (ItemIdentification.IsIdentifyItem(item))
                    {
                        // The scroll stays where it is until it is spent on an item
                        if (player.BeginIdentify(item))
                            AudioManager.instance.Play("cursor_identify_ready");
                    }
                    else if (!item.identified)
                    {
                        // Unidentified items can't be used. Identify them with Cain or a scroll.
                    }
                    else if (item.info.misc != null && item.info.misc.useable)
                    {
                        AudioManager.instance.Play(item.useSound);
                        _inventory.Take(cell.x, cell.y);
                        player.Use(item.info.misc);
                    }
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerOver = false;
            ClearHighlights();
        }

        private void OnDisable()
        {
            pointerOver = false;
            ClearHighlights();
        }

        private void ClearHighlights()
        {
            highlighter.gameObject.SetActive(false);
            Ui.HideScreenLabel();
            for (int i = 0; i < _inventory.entries.Count; ++i)
            {
                RawImage bg = itemsBackgrounds[i];
                bg.color = Colors.InvItemBackground;
            }
        }

        private Vector2i MouseCell()
        {
            Vector3 pointerPosition = Diablerie.Engine.UI.GamepadCursor.Position;
            if (player.HandsItem != null)
            {
                pointerPosition -= new Vector3(player.HandsItem.info.invWidth * cellSizeX, player.HandsItem.info.invHeight * cellSizeY) / 2;
                pointerPosition += new Vector3(cellSizeX, cellSizeY) * 0.5f; // adjust to center of cell
            }
            return GetPointerCell(pointerPosition);
        }

        private Vector2i GetPointerCell(Vector3 pointerPosition)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pointerPosition, Camera.main, out pos);
            // The local point is relative to the pivot, make it relative to the bottom left corner
            pos -= rectTransform.rect.min;
            int cellX = Mathf.FloorToInt(pos.x / cellSizeX);
            int cellY = Mathf.FloorToInt(pos.y / cellSizeY);
            return new Vector2i(cellX, cellY);
        }

        private void SetRect(Graphic graphic, int x, int y, int width, int height)
        {
            SetBottomLeft(graphic.rectTransform, x * cellSizeX, y * cellSizeY);
            graphic.rectTransform.sizeDelta = new Vector2(width * cellSizeX, height * cellSizeY);
        }

        private void Update()
        {
            if (_inventory == null || !pointerOver)
                return;
        
            ClearHighlights();

            var cell = clickOverride != null ? GetPointerCell(Diablerie.Engine.UI.GamepadCursor.Position) : MouseCell();
            if (player.HandsItem != null && clickOverride == null)
            {
                SetRect(highlighter, cell.x, cell.y, player.HandsItem.info.invWidth, player.HandsItem.info.invHeight);
                List<int> coveredEntries;
                highlighter.gameObject.SetActive(_inventory.Fit(player.HandsItem, cell.x, cell.y, out coveredEntries));
                if (coveredEntries.Count > 1)
                {
                    highlighter.color = Colors.InvItemHighlightForbid;
                }
                else
                {
                    highlighter.color = Colors.InvItemHighlight;
                    foreach(int entryIndex in coveredEntries)
                    {
                        RawImage bg = itemsBackgrounds[entryIndex];
                        bg.color = Colors.InvItemHighlightSwap;
                    }
                }
            }
            else
            {
                int entryIndex = _inventory.At(cell.x, cell.y);
                if (entryIndex != -1)
                {
                    RawImage bg = itemsBackgrounds[entryIndex];
                    bg.color = Colors.InvItemHighlight;
                    Engine.Inventory.Entry entry = _inventory.entries[entryIndex];
                    Ui.ShowScreenLabel(Diablerie.Engine.UI.GamepadCursor.Position, describeOverride != null ? describeOverride(entry.item) : entry.item.GetDescription());
                }
            }
        }
    }
}