using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Quest;
using Diablerie.Engine.UI;
using Diablerie.Game.UI.Inventory;
using Diablerie.Game.UI.Menu.CharacterSelect;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    // The Quest Log (Q key or the quest button of the mini panel), built in code from MPQ assets like the
    // other side panels. Shows the six Act 1 quests with their names (qstsa1qN) and the current objective text
    // (qstsa1qNN) from string.tbl, depending on the quest state.
    public class QuestPanel
    {
        private const string BackgroundPath = @"data\global\ui\menu\questbackground";
        private const string DonePath = @"data\global\ui\menu\questdone";

        private const float PanelWidth = 320;
        private const float PanelHeight = 432;
        private const float PanelLeftEdge = -320;

        // Quest icons are the original a1qN sheets drawn inside the arched sockets of questsockets.dc6,
        // three to a row, with the name of the selected quest and its objective below
        private const string IconPath = @"data\global\ui\menu\a1q";
        private const string SocketPath = @"data\global\ui\menu\questsockets";
        private const float Margin = 14;
        private const float GridTop = 34;
        private const float CellHeight = 60;
        private const float CellGap = 4;
        private const float NameBarHeight = 20;
        private const float RefreshInterval = 0.4f;

        private static readonly Color Title = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color Dimmed = new Color(0.35f, 0.35f, 0.35f);
        private static readonly Color Locked = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color Open = new Color(1f, 1f, 1f);
        private static readonly Color Done = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color Selected = new Color(0.78f, 0.65f, 0.35f);
        private static readonly Color Body = new Color(0.8f, 0.8f, 0.8f);

        private static QuestPanel _instance;

        private readonly RectTransform _root;
        private readonly Vector2 _size;
        private readonly PanelSheet _done;
        private readonly Image[] _sockets = new Image[Act1Dialog.Quests.Length];
        private readonly Image[] _icons = new Image[Act1Dialog.Quests.Length];
        private readonly Image[] _rowMarks = new Image[Act1Dialog.Quests.Length];
        private Sprite _socketNormal;
        private Sprite _socketSelected;
        private Text _name;
        private Text _detail;
        private int _selected;
        private float _nextRefresh;

        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

        public static void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        public static void Show()
        {
            if (Ui.instance == null || InventoryPanel.instance == null)
                return;
            if (_instance == null || _instance._root == null)
                _instance = new QuestPanel();
            _instance.ShowInternal();
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance.HideInternal();
        }

        // Hooks up the original mini panel's Quest Log button (it exists in the control panel prefab but has no handler)
        public static void BindMinipanelButton()
        {
            foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button == null || button.name != "ButtonMinipanelQuestLog" || !button.gameObject.scene.IsValid())
                    continue;
                if (button.GetComponent<QuestButtonMarker>() != null)
                    continue;
                button.gameObject.AddComponent<QuestButtonMarker>();
                button.onClick.AddListener(() =>
                {
                    AudioManager.instance.Play("cursor_select");
                    Toggle();
                });
            }
        }

        private class QuestButtonMarker : MonoBehaviour { }

        private class Clickable : MonoBehaviour, IPointerClickHandler
        {
            public System.Action<PointerEventData.InputButton> onClick;

            public void OnPointerClick(PointerEventData eventData)
            {
                onClick?.Invoke(eventData.button);
            }
        }

        private QuestPanel()
        {
            var background = PanelSheet.Load(BackgroundPath);
            _done = PanelSheet.Load(DonePath);
            Debug.Log("QuestPanel background " + (background == null ? "missing" : background.Describe()));

            _root = MenuWidgets.NewRect("Quest Panel", Ui.instance.RectTransform);
            PanelLayer.Place(_root);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0, 0.5f);
            _root.anchoredPosition = new Vector2(PanelLeftEdge, 0);

            _size = BuildBackground(background);
            _root.sizeDelta = _size;

            var sockets = PanelSheet.Load(SocketPath);
            if (sockets != null)
            {
                _socketNormal = sockets.sprites[0];
                _socketSelected = sockets.sprites.Length > 1 ? sockets.sprites[1] : sockets.sprites[0];
            }

            for (int i = 0; i < Act1Dialog.Quests.Length; ++i)
                BuildCell(i);

            float gridBottom = GridTop + 2 * CellHeight + CellGap;
            float nameTop = gridBottom + 6;
            float textTop = nameTop + NameBarHeight + 2;
            float textHeight = Mathf.Max(40, _size.y - textTop - 10);
            float boxWidth = _size.x - 2 * Margin;

            var nameBar = MenuWidgets.Box("Name Bar", _root, new Color(0, 0, 0, 0.9f));
            nameBar.raycastTarget = false;
            Place(nameBar.rectTransform, new Vector2(Margin, -nameTop), new Vector2(boxWidth, NameBarHeight), false);
            _name = MenuWidgets.Label("Name", _root, "", Fonts.GetFont16(), Title, TextAnchor.MiddleCenter);
            Place(_name.rectTransform, new Vector2(Margin, -nameTop), new Vector2(boxWidth, NameBarHeight), false);

            var textBox = MenuWidgets.Box("Text Box", _root, new Color(0, 0, 0, 0.9f));
            textBox.raycastTarget = false;
            Place(textBox.rectTransform, new Vector2(Margin, -textTop), new Vector2(boxWidth, textHeight), false);

            _detail = MenuWidgets.Label("Detail", _root, "", Fonts.GetFont16(), Body, TextAnchor.UpperLeft);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detail.verticalOverflow = VerticalWrapMode.Truncate;
            Place(_detail.rectTransform, new Vector2(Margin + 6, -(textTop + 4)), new Vector2(boxWidth - 12, textHeight - 8), false);

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
            for (int i = 0; i < background.frames.Length; ++i)
            {
                var frame = background.frames[i];
                var image = NewImage("Background " + i, _root, background.sprites[i]);
                Place(image.rectTransform, positions[i], new Vector2(frame.width, frame.height), false);
                image.raycastTarget = true;
            }
            return size;
        }

        private void BuildCell(int index)
        {
            float cellWidth = (_size.x - 2 * Margin - 2 * CellGap) / 3;
            float x = Margin + (index % 3) * (cellWidth + CellGap);
            float y = -(GridTop + (index / 3) * (CellHeight + CellGap));
            var cellSize = new Vector2(cellWidth, CellHeight);

            var socket = NewImage("Socket " + (index + 1), _root, _socketNormal);
            socket.preserveAspect = true;
            socket.raycastTarget = true;
            Place(socket.rectTransform, new Vector2(x, y), cellSize, false);
            _sockets[index] = socket;

            var iconSheet = PanelSheet.Load(IconPath + Act1Dialog.Quests[index].number);
            var icon = NewImage("Icon " + (index + 1), _root, iconSheet != null ? iconSheet.sprites[0] : null);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;
            Place(icon.rectTransform, new Vector2(x + cellWidth * 0.15f, y - CellHeight * 0.15f), new Vector2(cellWidth * 0.7f, CellHeight * 0.7f), false);
            _icons[index] = icon;

            // Completed quests get the "done" mark from the original art, when the sheet is available
            var mark = NewImage("Done Mark " + (index + 1), _root, _done != null && _done.sprites.Length > 0 ? _done.sprites[0] : null);
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            if (mark.sprite != null)
                Place(mark.rectTransform, new Vector2(x + cellWidth - 22, y - 2), new Vector2(20, 20), false);
            mark.enabled = false;
            _rowMarks[index] = mark;

            int captured = index;
            var clickable = socket.gameObject.AddComponent<Clickable>();
            clickable.onClick = button =>
            {
                MenuWidgets.PlayClick();
                _selected = captured;
                Refresh();
            };
        }

        // Quest state to objective text, using the string.tbl keys
        public static List<string> DescribeKeys(QuestManager qm, Act1Dialog.QuestDef def)
        {
            var keys = new List<string>();
            var quest = qm.GetQuest(def.id);
            if (quest == null)
                return keys;

            string prefix = "qstsa1q" + def.number;
            switch (quest.state)
            {
                case QuestState.Initiated:
                    keys.Add(prefix + "1");
                    break;

                case QuestState.InProgress:
                    switch (def.number)
                    {
                        case 1:
                            keys.Add(prefix + "2");
                            break;
                        case 2:
                            keys.Add(prefix + "2");
                            break;
                        case 3:
                            var malus = Object.FindObjectOfType<HoradricMalusQuestLogic>();
                            keys.Add(malus != null && malus.HasMalus() ? prefix + "2" : prefix + "1");
                            break;
                        case 4:
                            var tree = Object.FindObjectOfType<TreeOfInifussLogic>();
                            keys.Add(tree != null && tree.HasBark() ? prefix + "2" : prefix + "1");
                            break;
                        case 5:
                            keys.Add(prefix + "2");
                            break;
                        case 6:
                            keys.Add(prefix + "2");
                            break;
                    }
                    break;

                case QuestState.Completed:
                    if (!Act1Dialog.RewardCollected(def))
                    {
                        switch (def.number)
                        {
                            case 1: keys.Add("qstsa1q15"); break;
                            case 2: keys.Add("qstsa1q23"); break;
                            case 3: keys.Add("qstsa1q32b"); break;
                            case 4: keys.Add("qstsa1q46b"); break;
                            case 6: keys.Add("qstsa1q63"); break;
                        }
                    }
                    break;
            }
            return keys;
        }

        private static string Str(string key)
        {
            string value = Translation.Find(key, "");
            if (string.IsNullOrEmpty(value))
                return "";
            // Some strings carry a leading sound id of the form "43}"; the line breaks of the original
            // text boxes are spaces here because the panel wraps by itself
            int brace = value.IndexOf('}');
            if (brace > 0 && brace < 6 && int.TryParse(value.Substring(0, brace), out _))
                value = value.Substring(brace + 1);
            return value.Replace("}", "").Trim();
        }

        private void Refresh()
        {
            var qm = QuestManager.Instance;
            if (qm == null)
                return;

            for (int i = 0; i < Act1Dialog.Quests.Length; ++i)
            {
                var def = Act1Dialog.Quests[i];
                var quest = qm.GetQuest(def.id);
                bool completed = quest != null && quest.state == QuestState.Completed && Act1Dialog.RewardCollected(def);
                bool started = quest != null && quest.state != QuestState.NotStarted;
                bool selected = i == _selected;

                if (_socketNormal != null)
                {
                    _sockets[i].sprite = selected ? _socketSelected : _socketNormal;
                    _sockets[i].color = selected && _socketSelected == _socketNormal ? Selected : Color.white;
                }
                _icons[i].color = started ? Color.white : Dimmed;
                if (_rowMarks[i].sprite != null)
                    _rowMarks[i].enabled = completed;
            }

            var current = qm.GetQuest(Act1Dialog.Quests[_selected].id);
            string selectedName = Str("qstsa1q" + Act1Dialog.Quests[_selected].number);
            if (string.IsNullOrEmpty(selectedName))
                selectedName = current != null ? current.name : Act1Dialog.Quests[_selected].id;
            _name.text = selectedName;

            var selectedDef = Act1Dialog.Quests[_selected];
            var lines = new List<string>();
            foreach (var key in DescribeKeys(qm, selectedDef))
            {
                string text = Str(key);
                if (!string.IsNullOrEmpty(text))
                    lines.Add(text);
            }

            // Den of Evil counts the monsters that are left
            if (selectedDef.number == 1 && qm.GetQuest("A1Q1").state == QuestState.InProgress)
            {
                var den = Object.FindObjectOfType<DenOfEvilQuestLogic>();
                int left = den != null ? den.MonstersRemaining : -1;
                if (left == 1)
                    lines.Add(Str("qstsa1q140"));
                else if (left > 1)
                    lines.Add(Str("qstsa1q14") + left);
            }

            _detail.text = lines.Count > 0 ? string.Join("\n\n", lines) : "";
        }

        private void ShowInternal()
        {
            WaypointPanel.Hide();
            StashPanel.Hide();
            GamblePanel.Hide();
            CharstatPanel.instance.visible = false;
            _root.gameObject.SetActive(true);
            Refresh();
            AudioManager.instance.Play("cursor_select");
        }

        private void HideInternal()
        {
            if (!_root.gameObject.activeSelf)
                return;
            _root.gameObject.SetActive(false);
            Ui.HideScreenLabel();
        }

        private void Tick()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var rect = MenuWidgets.NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        // Positions a rect from the top left corner of the panel; "center" treats the position as the rect's center
        private static void Place(RectTransform rect, Vector2 position, Vector2 size, bool center)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private class Watcher : MonoBehaviour
        {
            public QuestPanel panel;

            private void Update()
            {
                panel.Tick();
            }
        }
    }
}
