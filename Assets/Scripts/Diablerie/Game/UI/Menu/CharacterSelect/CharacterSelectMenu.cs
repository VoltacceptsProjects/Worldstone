using System;
using System.Collections.Generic;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.SaveGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI.Menu.CharacterSelect
{
    // Diablo II style character select screen: up to 8 saved characters in a 2x4 grid,
    // most recently played first. Double click or OK to play, plus create/delete/exit.
    public class CharacterSelectMenu : MonoBehaviour
    {
        private const int Columns = 2;
        private const int Rows = 4;

        // Layout in the game's 800x600 menu space, in pixels from the top left corner of the screen.
        // TUNE: these follow the original character select screen.
        private const float ScreenWidth = 800;
        private const float ScreenHeight = 600;
        private const float FrameX = 35;
        private const float FrameWidth = 563;
        private const float TitleY = 12;
        private const float TitleHeight = 70;
        private const float ListY = 86;
        private const float ListHeight = 374;
        private const float SlotX = 37;
        private const float SlotY = 87;
        private const float SlotWidth = 272;
        private const float SlotHeight = 90;
        // Left edge of the text inside of a slot. The original leaves room for the class figure here.
        private const float SlotTextLeft = 75;
        // Where the class figure stands inside of a slot (its feet), from the slot's top left corner
        private const float FigureFeetX = 38;
        private const float FigureFeetY = 80;
        private const float TallButtonY = 470;
        private const float TallButtonWidth = 165;
        private const float TallButtonHeight = 58;
        private const float ShortButtonY = 537;
        private const float ShortButtonWidth = 128;
        private const float ShortButtonHeight = 35;

        // Assets from the MPQs. The expansion has its own screen, classic ships charselectbckg.
        private const string ExpansionScenePath = @"data\global\ui\CharSelect\characterselectscreenEXP";
        private const string ClassicScenePath = @"data\global\ui\CharSelect\charselectbckg";
        private const string SelectBoxPath = @"data\global\ui\CharSelect\charselectbox";
        private const string TallButtonPath = @"data\global\ui\CharSelect\TallButtonBlank";
        private const string ShortButtonPath = @"data\global\ui\CharSelect\ShortButtonBlank";
        private const PaletteType FrontEndPalette = PaletteType.Sky;

        private static readonly Color FrameGold = new Color(0.55f, 0.47f, 0.28f);
        private static readonly Color FrameDark = new Color(0.16f, 0.13f, 0.08f);
        private static readonly Color NameTan = new Color(0.78f, 0.67f, 0.45f);
        private static readonly Color ExpansionGreen = new Color(0.1f, 0.9f, 0.1f);
        private static readonly Color ButtonText = new Color(0.8f, 0.76f, 0.66f);

        public event Action<CharacterSave> OnPlay;
        public event Action OnCreateNew;
        public event Action OnExit;

        private GameObject _root;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private List<CharacterSaveInfo> _characters = new List<CharacterSaveInfo>();
        private readonly List<CharacterSave> _saves = new List<CharacterSave>();
        private int _selected = -1;
        private Text _status;
        private Text _title;
        private Button _createButton;
        private Button _deleteButton;
        private Button _okButton;
        private MenuDialog _dialog;
        private int _ignoreInputUntilFrame;

        public static CharacterSelectMenu Create(Transform parent)
        {
            var go = new GameObject("Character Select");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CharacterSelectMenu>();
        }

        public void Show()
        {
            Refresh();
            _status.text = "";
            _root.SetActive(true);
            _ignoreInputUntilFrame = Time.frameCount + 1;
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        public void Refresh()
        {
            string previouslySelected = _selected >= 0 && _selected < _characters.Count ? _characters[_selected].name : null;

            _characters = SaveManager.List();
            if (_characters.Count > SaveManager.MaxCharacters)
                _characters.RemoveRange(SaveManager.MaxCharacters, _characters.Count - SaveManager.MaxCharacters);

            // The figures are drawn from the equipment, which only the full save has
            _saves.Clear();
            foreach (var info in _characters)
                _saves.Add(SaveManager.Load(info.name));

            _selected = _characters.Count > 0 ? 0 : -1;
            if (previouslySelected != null)
            {
                int index = _characters.FindIndex(c => c.name == previouslySelected);
                if (index >= 0)
                    _selected = index;
            }

            UpdateView();
        }

        private void Awake()
        {
            Build();
            _root.SetActive(false);
        }

        private void Build()
        {
            _root = MenuWidgets.CreateCanvas("Canvas", transform, sortingOrder: 100);

            // Everything below is laid out in the original 800x600 menu space. The scaler stretches that space
            // to fit the window (Expand keeps the 4:3 proportions, the black background fills any leftover bars).
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var black = MenuWidgets.Box("Background", _root.transform, Color.black);
            MenuWidgets.Stretch(black.rectTransform);

            // The MPQ screen already has the ornate frames in it. The PNG scene with drawn frames is the fallback.
            var screen = PanelSheet.Load(ExpansionScenePath, FrontEndPalette) ?? PanelSheet.Load(ClassicScenePath, FrontEndPalette);
            if (screen != null)
            {
                BuildScreen(screen);
            }
            else
            {
                Debug.LogWarning("CharacterSelectMenu: the character select screen art is missing, using the fallback");
                var scene = Resources.Load<Sprite>("Menu/class_select_screen");
                if (scene != null)
                {
                    var picture = MenuWidgets.Box("Scene", _root.transform, Color.white);
                    picture.sprite = scene;
                    picture.raycastTarget = false;
                    TopLeft(picture.rectTransform, 0, 0, ScreenWidth, ScreenHeight);
                }
                BuildFrame("Title Frame", FrameX, TitleY, FrameWidth, TitleHeight);
                BuildFrame("List Frame", FrameX, ListY, FrameWidth, ListHeight);
            }

            _title = MenuWidgets.Label("Title", _root.transform, "", Fonts.GetFont42(), NameTan, TextAnchor.MiddleCenter);
            PlaceText(_title, FrameX, TitleY, FrameWidth, TitleHeight);

            var selectBox = PanelSheet.Load(SelectBoxPath, FrontEndPalette);
            for (int i = 0; i < SaveManager.MaxCharacters; ++i)
            {
                int row = i / Columns;
                int col = i % Columns;
                _slots.Add(SlotView.Create(_root.transform, i, SlotX + col * SlotWidth, SlotY + row * SlotHeight,
                    SlotWidth, SlotHeight, selectBox, OnSlotClicked));
            }

            _status = MenuWidgets.Label("Status", _root.transform, "", Fonts.GetFont16(), MenuWidgets.Red, TextAnchor.MiddleCenter);
            PlaceText(_status, FrameX, ListY + ListHeight - 16, FrameWidth, 16);

            var tall = PanelSheet.Load(TallButtonPath, FrontEndPalette);
            var wide = Resources.LoadAll<Sprite>("Menu/button_wide");
            var shortArt = PanelSheet.Load(ShortButtonPath, FrontEndPalette);
            var medium = Resources.LoadAll<Sprite>("Menu/button_medium");

            // The original has Convert To Expansion in the middle. There is no classic/expansion split here,
            // so the two remaining buttons keep their places at the left and right of the row.
            float tallWidth = tall != null ? tall.frames[0].width : TallButtonWidth;
            float shortWidth = shortArt != null ? shortArt.frames[0].width : ShortButtonWidth;
            _createButton = ArtButton("Create", "CREATE NEW\nCHARACTER", tall, wide, FrameX, TallButtonY, TallButtonWidth, TallButtonHeight, CreateNew);
            _deleteButton = ArtButton("Delete", "DELETE\nCHARACTER", tall, wide, FrameX + FrameWidth - tallWidth, TallButtonY, TallButtonWidth, TallButtonHeight, AskDelete);
            ArtButton("Exit", "EXIT", shortArt, medium, FrameX, ShortButtonY, ShortButtonWidth, ShortButtonHeight, Exit);
            _okButton = ArtButton("OK", "OK", shortArt, medium, ScreenWidth - FrameX - shortWidth - 20, ShortButtonY, ShortButtonWidth, ShortButtonHeight, Play);
        }

        // Draws the screen's DC6 frames as tiles from the top left corner
        private void BuildScreen(PanelSheet screen)
        {
            var topBlank = new int[screen.frames.Length];
            var bottomBlank = new int[screen.frames.Length];
            for (int i = 0; i < screen.sprites.Length; ++i)
            {
                topBlank[i] = PanelSheet.CountBlankRows(screen.sprites[i], true);
                bottomBlank[i] = PanelSheet.CountBlankRows(screen.sprites[i], false);
            }
            var size = screen.TileLayout(ScreenWidth, topBlank, bottomBlank, out var positions);
            Debug.Log("CharacterSelectMenu screen " + size.x + "x" + size.y + " (" + screen.frames.Length + " frames)");
            for (int i = 0; i < screen.frames.Length; ++i)
            {
                var frame = screen.frames[i];
                var tile = MenuWidgets.Box("Screen " + i, _root.transform, Color.white);
                tile.sprite = screen.sprites[i];
                tile.raycastTarget = false;
                TopLeft(tile.rectTransform, positions[i].x, positions[i].y, frame.width, frame.height);
            }
        }

        // Pixel positions measured from the top left of the 800x600 menu space
        private static void TopLeft(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x - ScreenWidth / 2, ScreenHeight / 2 - y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // Centers a label in the given box, compensating for the bitmap font's vertical offset
        private static void PlaceText(Text text, float x, float y, float width, float height)
        {
            MenuWidgets.PlaceText(text, new Vector2(x + width / 2 - ScreenWidth / 2, ScreenHeight / 2 - y - height / 2), new Vector2(width, height));
        }

        // A black box with a gold edge, standing in for the ornate stone frame of the original
        private void BuildFrame(string name, float x, float y, float width, float height)
        {
            var outer = MenuWidgets.Box(name, _root.transform, FrameGold);
            outer.raycastTarget = false;
            TopLeft(outer.rectTransform, x, y, width, height);
            var mid = MenuWidgets.Box("Edge", outer.transform, FrameDark);
            mid.raycastTarget = false;
            TopLeft(mid.rectTransform, x + 2, y + 2, width - 4, height - 4);
            var inner = MenuWidgets.Box("Fill", outer.transform, Color.black);
            inner.raycastTarget = false;
            TopLeft(inner.rectTransform, x + 4, y + 4, width - 8, height - 8);
        }

        // Uses the MPQ button art when there is some (frame 0 idle, frame 1 pressed), the PNG sheet otherwise
        private Button ArtButton(string name, string label, PanelSheet art, Sprite[] fallback, float x, float y, float width, float height, Action onClick)
        {
            var image = MenuWidgets.Box(name, _root.transform, Color.white);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (art != null && art.sprites.Length >= 2)
            {
                image.sprite = art.sprites[0];
                width = art.frames[0].width;
                height = art.frames[0].height;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { pressedSprite = art.sprites[1], highlightedSprite = art.sprites[0] };
            }
            else if (fallback != null && fallback.Length >= 3)
            {
                // The sheet is normal, pressed, disabled, in the order of the frames
                var normal = FindSprite(fallback, "normal");
                image.sprite = normal;
                image.type = Image.Type.Sliced;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    pressedSprite = FindSprite(fallback, "pressed"),
                    highlightedSprite = normal
                };
            }
            else
            {
                button.transition = Selectable.Transition.ColorTint;
                image.color = new Color(0.14f, 0.14f, 0.14f, 0.95f);
            }
            TopLeft(image.rectTransform, x, y, width, height);

            var text = MenuWidgets.Label("Text", image.transform, label, Fonts.GetFont16(), ButtonText, TextAnchor.MiddleCenter);
            MenuWidgets.StretchText(text);
            button.onClick.AddListener(() =>
            {
                MenuWidgets.PlayClick();
                onClick?.Invoke();
            });
            return button;
        }

        // Greyed out buttons are dimmed, the DC6 sheets have no disabled frame
        private static void SetArtEnabled(Button button, bool enabled)
        {
            MenuWidgets.SetButtonEnabled(button, enabled);
            button.targetGraphic.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        private static Sprite FindSprite(Sprite[] sheet, string state)
        {
            foreach (var sprite in sheet)
            {
                if (sprite.name.EndsWith(state))
                    return sprite;
            }
            return sheet[0];
        }

        private void UpdateView()
        {
            for (int i = 0; i < _slots.Count; ++i)
            {
                _slots[i].SetCharacter(i < _characters.Count ? _characters[i] : null, i < _saves.Count ? _saves[i] : null);
                _slots[i].SetSelected(i == _selected);
            }

            _title.text = _selected >= 0 && _selected < _characters.Count ? _characters[_selected].name : "";

            SetArtEnabled(_okButton, _selected >= 0);
            SetArtEnabled(_deleteButton, _selected >= 0);
            SetArtEnabled(_createButton, _characters.Count < SaveManager.MaxCharacters);
        }

        private void OnSlotClicked(int index, int clickCount)
        {
            if (_dialog != null || index >= _characters.Count)
                return;

            _selected = index;
            _status.text = "";
            UpdateView();
            if (clickCount >= 2)
                Play();
        }

        private void Play()
        {
            if (_dialog != null || _selected < 0 || _selected >= _characters.Count)
                return;

            var save = SaveManager.Load(_characters[_selected].name);
            if (save == null)
            {
                _status.text = "Unable to load this character.";
                return;
            }

            Hide();
            OnPlay?.Invoke(save);
        }

        private void CreateNew()
        {
            if (_dialog != null)
                return;

            if (_characters.Count >= SaveManager.MaxCharacters)
            {
                _status.text = "You can't have more than " + SaveManager.MaxCharacters + " characters.";
                return;
            }

            Hide();
            OnCreateNew?.Invoke();
        }

        private void AskDelete()
        {
            if (_dialog != null || _selected < 0 || _selected >= _characters.Count)
                return;

            string name = _characters[_selected].name;
            _dialog = MenuDialog.ShowConfirm(_root.transform, "Delete " + name + "?",
                onYes: () =>
                {
                    SaveManager.Delete(name);
                    Refresh();
                },
                onClosed: OnDialogClosed);
        }

        private void Exit()
        {
            if (_dialog != null)
                return;

            Hide();
            OnExit?.Invoke();
        }

        private void OnDialogClosed()
        {
            _dialog = null;
            // The key that closed the dialog must not also trigger an action here
            _ignoreInputUntilFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (!_root.activeSelf || _dialog != null || Time.frameCount < _ignoreInputUntilFrame)
                return;

            if (Input.GetKeyDown(KeyCode.Escape) || Diablerie.Engine.UI.GamepadCursor.CancelDown)
            {
                Exit();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Diablerie.Engine.UI.GamepadCursor.ConfirmDown)
            {
                Play();
            }
            else if (Input.GetKeyDown(KeyCode.Delete))
            {
                AskDelete();
            }
            else if (_characters.Count > 0)
            {
                int next = _selected;
                if (Input.GetKeyDown(KeyCode.LeftArrow)) next -= 1;
                if (Input.GetKeyDown(KeyCode.RightArrow)) next += 1;
                if (Input.GetKeyDown(KeyCode.UpArrow)) next -= Columns;
                if (Input.GetKeyDown(KeyCode.DownArrow)) next += Columns;
                if (next != _selected && next >= 0 && next < _characters.Count)
                {
                    _selected = next;
                    UpdateView();
                }
            }
        }

        private class SlotView : MonoBehaviour, IPointerClickHandler
        {
            private int _index;
            private Action<int, int> _onClick;
            private GameObject _selection;
            private Text _name;
            private Text _details;
            private Text _expansion;
            private CharacterFigure _figure;

            public static SlotView Create(Transform parent, int index, float x, float y, float width, float height, PanelSheet selectBox, Action<int, int> onClick)
            {
                // Invisible, only there to receive clicks
                var image = MenuWidgets.Box("Slot " + index, parent, new Color(0, 0, 0, 0));
                TopLeft(image.rectTransform, x, y, width, height);

                var view = image.gameObject.AddComponent<SlotView>();
                view._index = index;
                view._onClick = onClick;

                RectTransform selection;
                if (selectBox != null && selectBox.sprites.Length > 0)
                {
                    // The game's own selection box. The art is split into several DC6 frames, so tile them
                    // from the top left corner of the slot like the screen itself.
                    selection = MenuWidgets.NewRect("Selection", image.transform);
                    selection.anchorMin = selection.anchorMax = new Vector2(0, 1);
                    selection.pivot = new Vector2(0, 1);
                    selection.anchoredPosition = Vector2.zero;
                    selection.sizeDelta = new Vector2(width, height);

                    var top = new int[selectBox.frames.Length];
                    var bottom = new int[selectBox.frames.Length];
                    selectBox.TileLayout(width, top, bottom, out var positions);
                    for (int i = 0; i < selectBox.frames.Length; ++i)
                    {
                        var tile = MenuWidgets.Box("Tile " + i, selection, Color.white);
                        tile.sprite = selectBox.sprites[i];
                        tile.raycastTarget = false;
                        var rt = tile.rectTransform;
                        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                        rt.pivot = new Vector2(0, 1);
                        rt.anchoredPosition = new Vector2(positions[i].x, -positions[i].y);
                        rt.sizeDelta = new Vector2(selectBox.frames[i].width, selectBox.frames[i].height);
                    }
                }
                else
                {
                    // Gold border around the selected slot
                    selection = MenuWidgets.NewRect("Selection", image.transform);
                    selection.anchorMin = Vector2.zero;
                    selection.anchorMax = Vector2.one;
                    selection.offsetMin = selection.offsetMax = Vector2.zero;
                    Strip(selection, 0, 2);   // top
                    Strip(selection, 1, 2);   // bottom
                    Strip(selection, 2, 2);   // left
                    Strip(selection, 3, 2);   // right
                }
                view._selection = selection.gameObject;

                // Three lines like the original: name, level and class, expansion marker
                float lineWidth = width - SlotTextLeft - 6;
                view._name = MenuWidgets.Label("Name", image.transform, "", Fonts.GetFont16(), NameTan, TextAnchor.MiddleLeft);
                PlaceInSlot(view._name, width, height, SlotTextLeft, 34, lineWidth, 16);
                view._details = MenuWidgets.Label("Details", image.transform, "", Fonts.GetFont16(), Color.white, TextAnchor.MiddleLeft);
                PlaceInSlot(view._details, width, height, SlotTextLeft, 48, lineWidth, 16);
                view._expansion = MenuWidgets.Label("Expansion", image.transform, "", Fonts.GetFont16(), ExpansionGreen, TextAnchor.MiddleLeft);
                PlaceInSlot(view._expansion, width, height, SlotTextLeft, 62, lineWidth, 16);
                return view;
            }

            // Labels are children of the slot, so they are placed from the slot's own top left corner.
            // (MenuWidgets.PlaceText works from the screen center, which is only right for children of the canvas.)
            private static void PlaceInSlot(Text text, float slotWidth, float slotHeight, float left, float centerY, float width, float height)
            {
                var rt = text.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(left + width / 2 - slotWidth / 2,
                    slotHeight / 2 - centerY - MenuWidgets.TextNudge);
            }

            // One edge of the selection border. side: 0 top, 1 bottom, 2 left, 3 right
            private static void Strip(RectTransform parent, int side, float thickness)
            {
                var strip = MenuWidgets.Box("Edge", parent, MenuWidgets.Gold);
                strip.raycastTarget = false;
                var rt = strip.rectTransform;
                rt.anchoredPosition = Vector2.zero;
                switch (side)
                {
                    case 0:
                        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                        rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, thickness);
                        break;
                    case 1:
                        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                        rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, thickness);
                        break;
                    case 2:
                        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
                        rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(thickness, 0);
                        break;
                    default:
                        rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                        rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(thickness, 0);
                        break;
                }
            }

            public void SetCharacter(CharacterSaveInfo info, CharacterSave save)
            {
                if (_figure != null)
                {
                    Destroy(_figure.gameObject);
                    _figure = null;
                }

                if (info == null)
                {
                    _name.text = "";
                    _details.text = "";
                    _expansion.text = "";
                    return;
                }

                _name.text = info.name;
                _details.text = "Level " + info.level + " " + info.className;
                _expansion.text = "EXPANSION CHARACTER";
                _figure = CharacterFigure.Create(transform, new Vector2(FigureFeetX, FigureFeetY), save);
                // Behind the text, in front of the selection box
                if (_figure != null)
                    _figure.transform.SetSiblingIndex(1);
            }

            public void SetSelected(bool selected)
            {
                _selection.SetActive(selected);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                _onClick?.Invoke(_index, eventData.clickCount);
            }
        }
    }
}