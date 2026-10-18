using System;
using System.Collections.Generic;
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
        private const float SlotWidth = 330;
        private const float SlotHeight = 84;
        private const float SlotGap = 12;
        private const float GridCenterY = 20;

        public event Action<CharacterSave> OnPlay;
        public event Action OnCreateNew;
        public event Action OnExit;

        private GameObject _root;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private List<CharacterSaveInfo> _characters = new List<CharacterSaveInfo>();
        private int _selected = -1;
        private Text _status;
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

            var background = MenuWidgets.Box("Background", _root.transform, Color.black);
            MenuWidgets.Stretch(background.rectTransform);

            var title = MenuWidgets.Label("Title", _root.transform, "SELECT CHARACTER", Fonts.GetFont42(), MenuWidgets.Gold, TextAnchor.MiddleCenter);
            MenuWidgets.PlaceText(title, new Vector2(0, 250), new Vector2(600, 60));

            for (int i = 0; i < SaveManager.MaxCharacters; ++i)
            {
                int row = i / Columns;
                int col = i % Columns;
                float x = (col - (Columns - 1) / 2f) * (SlotWidth + SlotGap);
                float y = GridCenterY + ((Rows - 1) / 2f - row) * (SlotHeight + SlotGap);
                _slots.Add(SlotView.Create(_root.transform, i, new Vector2(x, y), new Vector2(SlotWidth, SlotHeight), OnSlotClicked));
            }

            float gridBottom = GridCenterY - (Rows * SlotHeight + (Rows - 1) * SlotGap) / 2;
            _status = MenuWidgets.Label("Status", _root.transform, "", Fonts.GetFont24(), MenuWidgets.Red, TextAnchor.MiddleCenter);
            MenuWidgets.PlaceText(_status, new Vector2(0, gridBottom - 26), new Vector2(700, 30));

            float buttonY = gridBottom - 75;
            var buttonSize = new Vector2(0, 44);  // zero width: sized to fit the label
            _createButton = MenuWidgets.TextButton(_root.transform, "CREATE NEW CHARACTER", Vector2.zero, buttonSize, CreateNew);
            _deleteButton = MenuWidgets.TextButton(_root.transform, "DELETE CHARACTER", Vector2.zero, buttonSize, AskDelete);
            _okButton = MenuWidgets.TextButton(_root.transform, "OK", Vector2.zero, new Vector2(90, 44), Play);
            var exitButton = MenuWidgets.TextButton(_root.transform, "EXIT", Vector2.zero, new Vector2(90, 44), Exit);
            MenuWidgets.LayoutRow(buttonY, 16, _createButton, _deleteButton, _okButton, exitButton);
        }

        private void UpdateView()
        {
            for (int i = 0; i < _slots.Count; ++i)
            {
                _slots[i].SetCharacter(i < _characters.Count ? _characters[i] : null);
                _slots[i].SetSelected(i == _selected);
            }

            MenuWidgets.SetButtonEnabled(_okButton, _selected >= 0);
            MenuWidgets.SetButtonEnabled(_deleteButton, _selected >= 0);
            MenuWidgets.SetButtonEnabled(_createButton, _characters.Count < SaveManager.MaxCharacters);
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Exit();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
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
            private Image _background;
            private Outline _outline;
            private Text _name;
            private Text _details;

            public static SlotView Create(Transform parent, int index, Vector2 position, Vector2 size, Action<int, int> onClick)
            {
                var image = MenuWidgets.Box("Slot " + index, parent, new Color(0.08f, 0.08f, 0.08f, 0.9f));
                MenuWidgets.Center(image.rectTransform, position, size);

                var view = image.gameObject.AddComponent<SlotView>();
                view._index = index;
                view._onClick = onClick;
                view._background = image;
                view._outline = image.gameObject.AddComponent<Outline>();
                view._outline.effectColor = MenuWidgets.Gold;
                view._outline.effectDistance = new Vector2(2, -2);
                view._outline.enabled = false;

                // Two lines, centered a fixed distance above and below the slot's middle
                var lineSize = new Vector2(size.x - 32, 34);
                view._name = MenuWidgets.Label("Name", image.transform, "", Fonts.GetFont30(), Color.white, TextAnchor.MiddleLeft);
                MenuWidgets.PlaceText(view._name, new Vector2(0, 18), lineSize);
                view._details = MenuWidgets.Label("Details", image.transform, "", Fonts.GetFont24(), MenuWidgets.Grey, TextAnchor.MiddleLeft);
                MenuWidgets.PlaceText(view._details, new Vector2(0, -18), lineSize);
                return view;
            }

            public void SetCharacter(CharacterSaveInfo info)
            {
                if (info == null)
                {
                    _name.text = "";
                    _details.text = "";
                    return;
                }

                _name.text = info.name;
                _details.text = "Level " + info.level + " " + info.className;
            }

            public void SetSelected(bool selected)
            {
                _outline.enabled = selected;
                _name.color = selected ? MenuWidgets.Gold : Color.white;
                _background.color = selected ? new Color(0.2f, 0.16f, 0.08f, 0.95f) : new Color(0.08f, 0.08f, 0.08f, 0.9f);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                _onClick?.Invoke(_index, eventData.clickCount);
            }
        }
    }
}
