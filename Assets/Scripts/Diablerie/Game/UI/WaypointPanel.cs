using System;
using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.UI;
using Diablerie.Game.UI.Inventory;
using Diablerie.Game.UI.Menu.CharacterSelect;
using Diablerie.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    public class WaypointPanel
    {
        // Asset paths
        private const string BackgroundPath = @"data\global\ui\menu\waygatebackground";
        private const string IconsPath = @"data\global\ui\menu\waygateicons";
        private const string ExpansionTabsPath = @"data\global\ui\menu\expwaygatetabs";
        private const string ClassicTabsPath = @"data\global\ui\menu\waygatetabs";
        private const string ButtonsPath = @"data\global\ui\panel\buysellbtn";

        // Frame layout of the DC6 sheets, adjust here if the assets turn out to use another order
        private const int IconFrameIdle = 0;
        private const int IconFrameActive = 1;
        // Every tab has a pair of frames, the first one is the selected look and the second one the idle look
        private const bool TabPairStartsWithSelected = true;
        // The close button lives in the buy/sell button sheet, the pressed look follows the idle one
        private const int CloseFrameIdle = 10;
        private const int CloseFramePressed = 11;

        // Panel placement, same size and position as the inventory panel but on the left side of the screen
        private const float PanelWidth = 320;
        private const float PanelHeight = 432;
        private const float PanelLeftEdge = -320;
        private const float PanelVerticalShift = 0;
        private const float CloseDistance = 10;

        // Layout inside of the panel, in pixels measured from the top left corner
        private const int RowCount = 9;
        private const float TabBottomY = 36;
        private const float TitleCenterY = 45;
        private const float FirstRowCenterY = 80;
        // The bitmap font still sits a little low after the shared TextNudge, this lifts text onto the slot centers
        private const float TextRaise = 2;
        private const float RowHeight = 36;
        private const float RowLeftX = 10;
        private const float RowIconCenterX = 32;
        // The slot squares are baked into the background, the icon is centered on them
        private const float RowIconRaise = 4;
        private const float RowTextX = 78;
        private const float CloseCenterFromRight = 30;
        private const float CloseCenterFromBottom = 30;

        private const string TitleText = "Choose your destination";

        private static readonly Color UsableColor = Color.white;
        private static readonly Color HoverColor = MenuWidgets.Gold;
        private static readonly Color LockedColor = new Color(0.35f, 0.35f, 0.35f);
        private static readonly Color UnreachableColor = MenuWidgets.Grey;
        private static readonly Color TitleColor = new Color(0.85f, 0.85f, 0.85f);

        private static WaypointPanel _instance;

        private readonly RectTransform _root;
        private readonly Sheet _icons;
        private readonly Sheet _tabs;
        private readonly Sheet _buttons;
        private readonly int _actCount;
        private readonly Vector2 _size;
        private readonly List<TabView> _tabViews = new List<TabView>();
        private readonly List<RowView> _rowViews = new List<RowView>();

        private Player _player;
        private Waypoints.Entry _origin;
        private Vector3 _openedAt;
        private int _selectedAct;

        // Public API
        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

        public static void Show(Player player, Waypoints.Entry origin)
        {
            GetInstance().ShowInternal(player, origin);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance.HideInternal();
        }

        private static WaypointPanel GetInstance()
        {
            if (_instance == null || _instance._root == null)
                _instance = new WaypointPanel();
            return _instance;
        }

        // Construction
        private WaypointPanel()
        {
            var background = Sheet.Load(BackgroundPath);
            _icons = Sheet.Load(IconsPath);
            var expansionTabs = Sheet.Load(ExpansionTabsPath);
            _tabs = expansionTabs ?? Sheet.Load(ClassicTabsPath);
            _buttons = Sheet.Load(ButtonsPath);
            _actCount = expansionTabs != null ? 5 : 4;

            Debug.Log("WaypointPanel assets: background " + Describe(background) + ", icons " + Describe(_icons) + ", tabs " + Describe(_tabs));

            _root = MenuWidgets.NewRect("Waypoint Panel", Ui.instance.RectTransform);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0, 0.5f);
            _root.anchoredPosition = new Vector2(PanelLeftEdge, PanelVerticalShift);

            _size = BuildBackground(background);
            _root.sizeDelta = _size;

            BuildTabs();
            BuildTitle();
            BuildRows();
            BuildCloseButton();

            _root.gameObject.AddComponent<Watcher>().panel = this;
            _root.gameObject.SetActive(false);
        }

        private Vector2 BuildBackground(Sheet background)
        {
            if (background == null || background.frames.Length == 0)
            {
                var box = MenuWidgets.Box("Background", _root, new Color(0.05f, 0.05f, 0.05f, 0.92f));
                MenuWidgets.Stretch(box.rectTransform);
                box.raycastTarget = true;
                return new Vector2(PanelWidth, PanelHeight);
            }

            // The frames of a panel are tiles of one big picture, not free floating sprites. Their offsets
            // can't be trusted for that (they are usually all the same), so lay the tiles out as a grid.
            var topBlank = new int[background.sprites.Length];
            var bottomBlank = new int[background.sprites.Length];
            for (int i = 0; i < background.sprites.Length; ++i)
            {
                topBlank[i] = CountBlankRows(background.sprites[i], true);
                bottomBlank[i] = CountBlankRows(background.sprites[i], false);
            }

            Vector2 size = background.TileLayout(PanelWidth, topBlank, bottomBlank, out var positions);
            Debug.Log("WaypointPanel tiled background size " + size.x + "x" + size.y + " (expected " + PanelWidth + "x" + PanelHeight + "), blank rows top [" + string.Join(",", topBlank) + "] bottom [" + string.Join(",", bottomBlank) + "]");
            for (int i = 0; i < background.frames.Length; ++i)
            {
                var frame = background.frames[i];
                var image = CreateFrameImage("Background " + i, _root, background.sprites[i], new Vector2(frame.width, frame.height), positions[i]);
                image.raycastTarget = true;
            }
            return size;
        }

        private void BuildTabs()
        {
            bool hasSheet = _tabs != null && _tabs.sprites.Length >= _actCount;
            if (_tabs != null && !hasSheet)
                Debug.LogWarning("Waypoint tab sheet has " + _tabs.sprites.Length + " frames, falling back to text tabs");

            int perTab = hasSheet ? _tabs.sprites.Length / _actCount : 0;
            float tabWidth = hasSheet ? _tabs.frames[0].width : _size.x * 0.18f;
            float startX = (_size.x - tabWidth * _actCount) / 2;

            for (int act = 0; act < _actCount; ++act)
            {
                var view = new TabView { act = act, perTab = perTab };

                // Selected and idle looks have different heights, so tabs are anchored by their bottom edge
                var rect = MenuWidgets.NewRect("Tab " + (act + 1), _root);
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0);
                rect.sizeDelta = new Vector2(tabWidth, 24);
                rect.anchoredPosition = new Vector2(startX + tabWidth * act, -TabBottomY);
                view.rect = rect;

                view.image = rect.gameObject.AddComponent<Image>();
                view.image.raycastTarget = true;
                if (hasSheet)
                {
                    view.sheet = _tabs;
                }
                else
                {
                    view.image.color = new Color(0, 0, 0, 0.01f);
                    view.label = MenuWidgets.Label("Label", rect, "ACT " + (act + 1), Fonts.GetFont16(), UnreachableColor, TextAnchor.MiddleCenter);
                    MenuWidgets.StretchText(view.label);
                }

                int capturedAct = act;
                var clickable = rect.gameObject.AddComponent<Clickable>();
                clickable.onClick = () => OnTabClicked(capturedAct);
                _tabViews.Add(view);
            }
        }

        private void BuildTitle()
        {
            var title = MenuWidgets.Label("Title", _root, TitleText, Fonts.GetFont16(), TitleColor, TextAnchor.MiddleCenter);
            var rect = title.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_size.x - RowLeftX * 2, RowHeight);
            rect.anchoredPosition = new Vector2(_size.x / 2, -TitleCenterY - (MenuWidgets.TextNudge - TextRaise));
        }

        private void BuildRows()
        {
            for (int i = 0; i < RowCount; ++i)
            {
                var view = new RowView();
                float centerY = FirstRowCenterY + i * RowHeight;

                var rect = MenuWidgets.NewRect("Row " + i, _root);
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.sizeDelta = new Vector2(_size.x - RowLeftX * 2, RowHeight);
                rect.anchoredPosition = new Vector2(RowLeftX, -centerY);
                view.root = rect.gameObject;

                var hit = rect.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = true;

                if (_icons != null && _icons.sprites.Length > 0)
                {
                    var iconRect = MenuWidgets.NewRect("Icon", rect);
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(RowIconCenterX - RowLeftX, RowIconRaise);
                    view.icon = iconRect.gameObject.AddComponent<Image>();
                    view.icon.raycastTarget = false;
                }

                view.label = MenuWidgets.Label("Name", rect, "", Fonts.GetFont16(), UsableColor, TextAnchor.MiddleLeft);
                var labelRect = view.label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0, 0.5f);
                labelRect.pivot = new Vector2(0, 0.5f);
                labelRect.sizeDelta = new Vector2(_size.x - RowTextX - RowLeftX, RowHeight);
                labelRect.anchoredPosition = new Vector2(RowTextX - RowLeftX, -(MenuWidgets.TextNudge - TextRaise));

                var clickable = rect.gameObject.AddComponent<Clickable>();
                var capturedView = view;
                clickable.onClick = () => OnRowClicked(capturedView);
                clickable.onHover = hovered =>
                {
                    capturedView.hovered = hovered;
                    ApplyRowStyle(capturedView);
                };

                _rowViews.Add(view);
            }
        }

        private void BuildCloseButton()
        {
            if (_buttons == null || _buttons.sprites.Length <= CloseFramePressed)
            {
                Debug.LogWarning("Waypoint close button frames are missing, the panel can still be closed with Escape");
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
        private void ShowInternal(Player player, Waypoints.Entry origin)
        {
            _player = player;
            _origin = origin;
            _openedAt = player.unit.iso.pos;

            InventoryPanel.instance.visible = false;
            CharstatPanel.instance.visible = false;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            SelectAct(Mathf.Clamp(origin.act, 0, _actCount - 1));
        }

        private void HideInternal()
        {
            foreach (var row in _rowViews)
                row.hovered = false;
            _root.gameObject.SetActive(false);
            Ui.HideScreenLabel();
        }

        private void SelectAct(int act)
        {
            _selectedAct = act;
            RefreshTabs();
            RefreshRows();
        }

        private void OnTabClicked(int act)
        {
            if (act == _selectedAct)
                return;
            AudioManager.instance.Play("cursor_select");
            SelectAct(act);
        }

        private void OnRowClicked(RowView row)
        {
            if (row.entry == null || row.entry.index == _origin.index)
                return;
            if (!Waypoints.CanTravel(_player, row.entry, WorldBuilder.CurrentActNumber - 1))
                return;

            AudioManager.instance.Play("cursor_select");
            var destination = row.entry;
            var player = _player;
            HideInternal();
            WorldBuilder.TravelToWaypoint(player, destination);
        }

        private void Tick()
        {
            if (_player == null)
                return;
            if (Vector3.Distance(_openedAt, _player.unit.iso.pos) > CloseDistance)
                HideInternal();
        }

        // Refresh
        private void RefreshTabs()
        {
            foreach (var view in _tabViews)
            {
                bool selected = view.act == _selectedAct;
                if (view.sheet != null)
                {
                    var sprite = view.sheet.sprites[view.FrameIndex(selected)];
                    view.image.sprite = sprite;
                    view.image.color = Color.white;
                    view.rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
                }
                else
                {
                    view.label.color = selected ? HoverColor : UnreachableColor;
                }
            }
        }

        private void RefreshRows()
        {
            var entries = Waypoints.ForAct(_selectedAct);
            for (int i = 0; i < _rowViews.Count; ++i)
            {
                var row = _rowViews[i];
                row.entry = i < entries.Count ? entries[i] : null;
                row.root.SetActive(row.entry != null);
                if (row.entry == null)
                    continue;

                string name = string.IsNullOrEmpty(row.entry.name) ? row.entry.level.name : row.entry.name;
                row.label.text = name;
                ApplyRowStyle(row);
            }
        }

        private void ApplyRowStyle(RowView row)
        {
            if (row.entry == null)
                return;

            bool usable = Waypoints.IsUsable(_player, row.entry);
            bool current = row.entry.index == _origin.index;
            bool reachable = usable && !current && Waypoints.CanTravel(_player, row.entry, WorldBuilder.CurrentActNumber - 1);
            bool highlighted = current || (reachable && row.hovered);

            Color color = UnreachableColor;
            if (!usable)
                color = LockedColor;
            else if (highlighted)
                color = HoverColor;
            else if (reachable)
                color = UsableColor;
            row.label.color = color;

            if (row.icon != null)
            {
                row.icon.gameObject.SetActive(usable);
                int frame = Mathf.Min(highlighted ? IconFrameActive : IconFrameIdle, _icons.sprites.Length - 1);
                var sprite = _icons.sprites[frame];
                row.icon.sprite = sprite;
                row.icon.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
                row.icon.color = reachable || current ? Color.white : new Color(1, 1, 1, 0.5f);
            }
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

        // Counts fully transparent pixel rows at the top or bottom edge of a sprite. The sprite loader leaves
        // such a row at the edge of frames, and between stacked tiles it shows the world through as a thin line.
        private static int CountBlankRows(Sprite sprite, bool fromTop)
        {
            var texture = sprite.texture;
            if (texture == null || !texture.isReadable)
                return 0;

            var rect = sprite.textureRect;
            var pixels = texture.GetPixels32();
            int x0 = (int)rect.x, y0 = (int)rect.y, width = (int)rect.width, height = (int)rect.height;
            int count = 0;
            for (int i = 0; i < height; ++i)
            {
                int row = fromTop ? y0 + height - 1 - i : y0 + i;
                for (int x = 0; x < width; ++x)
                {
                    if (pixels[row * texture.width + x0 + x].a != 0)
                        return Mathf.Min(count, 4);
                }
                ++count;
            }
            return 0;
        }

        private static string Describe(Sheet sheet)
        {
            if (sheet == null)
                return "missing";
            var text = sheet.frames.Length + " frames";
            for (int i = 0; i < sheet.frames.Length; ++i)
            {
                var f = sheet.frames[i];
                text += "\n  [" + i + "] size " + f.width + "x" + f.height + " offset " + f.offsetX + "," + f.offsetY;
            }
            return text;
        }

        // Types
        private class Sheet
        {
            public Sprite[] sprites;
            public DC6.Frame[] frames;

            public static Sheet Load(string path)
            {
                var dc6 = Spritesheet.Load(path) as DC6;
                if (dc6 == null)
                    return null;

                var sprites = dc6.GetSprites(0);
                if (sprites == null || sprites.Length == 0)
                    return null;
                // Pixel art drawn at fractional screen positions blends tile edges with bilinear filtering,
                // which shows up as thin seams between the background tiles
                foreach (var sprite in sprites)
                {
                    if (sprite != null && sprite.texture != null)
                        sprite.texture.filterMode = FilterMode.Point;
                }
                return new Sheet { sprites = sprites, frames = dc6.directions[0].frames };
            }

            // Lays the frames out as tiles, left to right and top to bottom, starting a new row when the next
            // frame would not fit in maxWidth. That is how the game assembles its panels from DC6 sheets.
            // Blank edge rows are overlapped so that no seam shows between rows of tiles.
            // Returns the size of the whole picture and the top left corner of every frame inside of it.
            public Vector2 TileLayout(float maxWidth, int[] topBlank, int[] bottomBlank, out Vector2[] positions)
            {
                positions = new Vector2[frames.Length];
                float x = 0, y = 0, rowHeight = 0, width = 0;
                int rowBottomBlank = 0;
                for (int i = 0; i < frames.Length; ++i)
                {
                    var frame = frames[i];
                    if (x > 0 && x + frame.width > maxWidth)
                    {
                        x = 0;
                        y += rowHeight - rowBottomBlank - topBlank[i];
                        rowHeight = 0;
                    }

                    if (x == 0)
                        rowBottomBlank = bottomBlank[i];

                    positions[i] = new Vector2(x, y);
                    x += frame.width;
                    width = Mathf.Max(width, x);
                    rowHeight = Mathf.Max(rowHeight, frame.height);
                }
                return new Vector2(width, y + rowHeight);
            }
        }

        private class TabView
        {
            public int act;
            public int perTab;
            public RectTransform rect;
            public Image image;
            public Text label;
            public Sheet sheet;

            public int FrameIndex(bool selected)
            {
                int index = act * perTab;
                if (perTab >= 2)
                {
                    bool useSecond = TabPairStartsWithSelected ? !selected : selected;
                    index += useSecond ? 1 : 0;
                }
                return Mathf.Min(index, sheet.sprites.Length - 1);
            }
        }

        private class RowView
        {
            public GameObject root;
            public Image icon;
            public Text label;
            public Waypoints.Entry entry;
            public bool hovered;
        }

        private class Clickable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public Action onClick;
            public Action<bool> onHover;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                    onClick?.Invoke();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                onHover?.Invoke(true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                onHover?.Invoke(false);
            }
        }

        private class Watcher : MonoBehaviour
        {
            public WaypointPanel panel;

            private void Update()
            {
                panel.Tick();
            }
        }
    }
}