using System.Collections.Generic;
using System.IO;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.UI;
using Diablerie.Game.UI.Menu.CharacterSelect;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Game.UI
{
    public class NpcDialog
    {
        // Try each of these in order; use the first one whose frames classify
        private static readonly string[] FramePaths =
        {
            @"data\global\ui\menu\boxpieces",         // base-D2 NPC menu frame
            @"data\global\ui\FrontEnd\TileableDialog",// LOD tileable frame
            @"data\global\ui\menu\helpborder",        // thin dialogue/text frame
        };
        private const bool UseFrameArt = true;

        private const bool DumpFrameSheets = false;
        private static readonly string[] DumpPaths =
        {
            @"data\global\ui\menu\boxpieces",
            @"data\global\ui\menu\dialogbackground",
            @"data\global\ui\menu\helpborder",
            @"data\global\ui\FrontEnd\TileableDialog",
        };

        // Layout (unchanged - already matched in the screenshot)
        private const float CloseDistance = 10;
        private const float VerticalShift = 60;
        private const float PaddingX = 8;
        private const float PaddingY = 8;
        private const float MinContentWidth = 60;
        private const float TitleHeight = 16;
        private const float TitleGap = 4;
        private const float RowHeight = 14;
        private const float RowGap = 2;
        private const float FallbackBorder = 2;

        // Colours
        private static readonly Color TitleColor = new Color(0.92f, 0.9f, 0.82f);
        private static readonly Color RowColor = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color HoverColor = new Color(0.41f, 0.41f, 1f);
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color FillColor = new Color(0.03f, 0.03f, 0.03f, 0.95f);
        // Tan/gold border that matches image #2 (1 px line)
        private static readonly Color GoldBorder = new Color(0.72f, 0.63f, 0.44f, 1f);
        private const float ManualBorderWidth = 1f;

        private const int TL = 0, T = 1, TR = 2, L = 3, C = 4, R = 5, BL = 6, B = 7, BR = 8;

        private static NpcDialog _instance;

        private readonly RectTransform _root;
        private readonly RectTransform _box;
        private readonly PanelSheet _frame;
        private readonly int[] _roles;
        private readonly List<GameObject> _parts = new List<GameObject>();

        private Player _player;
        private Unit _npc;
        private AudioSource _voice;
        private MenuRow _talkRow;

        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

        public static bool IsOpenFor(Unit npc)
        {
            return IsVisible && npc != null && _instance._npc == npc;
        }

        public static void Show(Player player, Unit npc, bool trade, bool gamble)
        {
            if (_instance == null || _instance._root == null)
                _instance = new NpcDialog();
            _instance.ShowInternal(player, npc, trade, gamble);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance.HideInternal();
        }

        private NpcDialog()
        {
            _frame = null;
            _roles = null;

            if (UseFrameArt)
            {
                foreach (var path in FramePaths)
                {
                    var sheet = PanelSheet.Load(path);
                    if (sheet == null)
                        continue;

                    var roles = DetectRoles(sheet);
                    if (roles != null)
                    {
                        _frame = sheet;
                        _roles = roles;
                        Debug.Log("NpcDialog frame art " + path + "\n" + sheet.Describe());
                        break;
                    }
                }
            }

            if (_roles == null)
                Debug.LogWarning("NpcDialog: no valid frame sheet found, using tan-line fallback");
            if (DumpFrameSheets)
                DumpSheets();

            _root = MenuWidgets.NewRect("Npc Dialog", Ui.instance.RectTransform);
            PanelLayer.Place(_root);
            MenuWidgets.Stretch(_root);

            var blocker = MenuWidgets.Box("Blocker", _root, new Color(0, 0, 0, 0));
            MenuWidgets.Stretch(blocker.rectTransform);
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blocker;
            blockerButton.onClick.AddListener(HideInternal);

            _box = MenuWidgets.NewRect("Box", _root);

            _root.gameObject.AddComponent<Watcher>().dialog = this;
            _root.gameObject.SetActive(false);
        }

        // Frame pieces are classified by where the opaque pixels sit. Note that
        // the Y formula returns *positive* for the top half, so all the branch
        // tests here read "dy > 0 = top".
        private static int[] DetectRoles(PanelSheet sheet)
        {
            if (sheet == null || sheet.frames == null || sheet.sprites == null)
                return null;

            var map = new[] { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
            int n = Mathf.Min(sheet.frames.Length, sheet.sprites.Length);
            for (int i = 0; i < n; ++i)
            {
                if (sheet.sprites[i] == null || sheet.frames[i].width <= 0 || sheet.frames[i].height <= 0)
                    continue;

                int role = Classify(sheet.sprites[i]);
                if (role >= 0 && map[role] < 0)
                    map[role] = i;
            }

            foreach (int role in new[] { TL, T, TR, L, R, BL, B, BR })
            {
                if (map[role] < 0)
                    return null;
            }

            int Width(int role) => sheet.frames[map[role]].width;
            int Height(int role) => sheet.frames[map[role]].height;
            bool Near(int a, int b) => Mathf.Abs(a - b) <= 1;
            bool sizesFit = Near(Height(TL), Height(T)) && Near(Height(T), Height(TR)) &&
                            Near(Height(BL), Height(B)) && Near(Height(B), Height(BR)) &&
                            Near(Width(TL), Width(L)) && Near(Width(L), Width(BL)) &&
                            Near(Width(TR), Width(R)) && Near(Width(R), Width(BR));
            return sizesFit ? map : null;
        }

        private static int Classify(Sprite sprite)
        {
            var pixels = ReadSprite(sprite, out int width, out int height);
            if (pixels == null)
                return -1;

            long sumX = 0, sumY = 0;
            int opaque = 0;
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    if (pixels[y * width + x].a <= 16)
                        continue;
                    sumX += x;
                    sumY += y;
                    ++opaque;
                }
            }

            if (opaque == 0 || opaque > width * height * 0.6f)
                return -1;

            // Pixel-space centroid, normalised to [-0.5..0.5]
            float dx = (sumX / (float) opaque + 0.5f) / width - 0.5f;
            // dy > 0 => pixels are in the TOP half, dy < 0 => BOTTOM half
            float dy = 0.5f - (sumY / (float) opaque + 0.5f) / height;

            const float far = 0.15f, near = 0.12f;
            if (Mathf.Abs(dx) > far && Mathf.Abs(dy) > far)
                return dy > 0
                    ? (dx < 0 ? TL : TR)
                    : (dx < 0 ? BL : BR);

            if (Mathf.Abs(dy) > far && Mathf.Abs(dx) <= near)
                return dy > 0 ? T : B;

            if (Mathf.Abs(dx) > far && Mathf.Abs(dy) <= near)
                return dx < 0 ? L : R;

            return -1;
        }

        private static Color32[] ReadSprite(Sprite sprite, out int width, out int height)
        {
            var rect = sprite.textureRect;
            width = (int) rect.width;
            height = (int) rect.height;
            var texture = sprite.texture;
            if (texture == null || width <= 0 || height <= 0)
                return null;

            var temp = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            var previous = RenderTexture.active;
            Graphics.Blit(texture, temp);
            RenderTexture.active = temp;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
            var pixels = readable.GetPixels32();
            Object.Destroy(readable);
            return pixels;
        }

        private static void DumpSheets()
        {
            foreach (var path in DumpPaths)
            {
                try
                {
                    var sheet = PanelSheet.Load(path);
                    if (sheet == null)
                    {
                        Debug.Log("NpcDialog dump: " + path + " not found");
                        continue;
                    }
                    DumpSheet(path, sheet);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("NpcDialog dump failed for " + path + ": " + e.Message);
                }
            }
        }

        private static void DumpSheet(string path, PanelSheet sheet)
        {
            const int gap = 8, wrap = 900;
            var positions = new List<Vector2Int>();
            int x = 0, y = 0, rowHeight = 0, canvasWidth = 0;
            for (int i = 0; i < sheet.frames.Length; ++i)
            {
                var f = sheet.frames[i];
                if (x > 0 && x + f.width > wrap)
                {
                    x = 0;
                    y += rowHeight + gap;
                    rowHeight = 0;
                }
                positions.Add(new Vector2Int(x, y));
                x += f.width + gap;
                canvasWidth = Mathf.Max(canvasWidth, x);
                rowHeight = Mathf.Max(rowHeight, f.height);
            }
            int canvasHeight = y + rowHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            var canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
            var background = new Color32[canvasWidth * canvasHeight];
            for (int i = 0; i < background.Length; ++i)
                background[i] = new Color32(70, 70, 90, 255);
            canvas.SetPixels32(background);

            for (int i = 0; i < sheet.sprites.Length && i < positions.Count; ++i)
            {
                var pixels = ReadSprite(sheet.sprites[i], out int w, out int h);
                if (pixels == null)
                    continue;
                for (int row = 0; row < h; ++row)
                {
                    for (int col = 0; col < w; ++col)
                    {
                        var pixel = pixels[row * w + col];
                        if (pixel.a == 0)
                            continue;
                        canvas.SetPixel(positions[i].x + col, canvasHeight - 1 - (positions[i].y + (h - 1 - row)), pixel);
                    }
                }
            }
            canvas.Apply();

            string file = Path.Combine(Application.persistentDataPath, "NpcDialogFrames_" + Path.GetFileName(path.Replace('\\', '/')) + ".png");
            File.WriteAllBytes(file, canvas.EncodeToPNG());
            Object.Destroy(canvas);
            Debug.Log("NpcDialog dump: " + path + " (" + sheet.frames.Length + " frames) written to " + file + "\n" + sheet.Describe());
        }

        private void ShowInternal(Player player, Unit npc, bool trade, bool gamble)
        {
            StopVoice();
            _player = player;
            _npc = npc;

            WaypointPanel.Hide();
            StashPanel.Hide();
            GamblePanel.Hide();

            Build(trade, gamble);
            _root.gameObject.SetActive(true);
            _voice = NpcVoice.PlayGreeting(npc);
        }

        private void Build(bool trade, bool gamble)
        {
            foreach (var part in _parts)
                Object.Destroy(part);
            _parts.Clear();
            _talkRow = null;

            var labels = new List<string>();
            var actions = new List<System.Action>();
            labels.Add(Label("TalkMenu", "talk"));
            actions.Add(OnTalk);
            if (trade)
            {
                labels.Add(Label("NPCMenuTrade", "trade"));
                actions.Add(OnTrade);
            }
            if (gamble)
            {
                labels.Add(Label("gamble", "gamble"));
                actions.Add(OnGamble);
            }
            labels.Add(Label("cancel", "cancel"));
            actions.Add(HideInternal);

            string npcName = _npc.monStat != null && !string.IsNullOrEmpty(_npc.monStat.name) ? _npc.monStat.name : _npc.monStat.id;
            npcName = npcName.ToUpperInvariant();

            var title = MenuWidgets.Label("Title", _box, npcName, Fonts.GetFont16(), TitleColor, TextAnchor.MiddleCenter);
            _parts.Add(title.gameObject);
            var rowTexts = new List<Text>();
            float contentWidth = Mathf.Max(MinContentWidth, title.preferredWidth);
            foreach (var label in labels)
            {
                var text = MenuWidgets.Label(label, _box, label, Fonts.GetFont16(), RowColor, TextAnchor.MiddleCenter);
                rowTexts.Add(text);
                contentWidth = Mathf.Max(contentWidth, text.preferredWidth);
            }

            int count = labels.Count;
            float contentHeight = TitleHeight + TitleGap + count * RowHeight + (count - 1) * RowGap;

            float left = FallbackBorder, right = FallbackBorder, top = FallbackBorder, bottom = FallbackBorder;
            if (_roles != null)
            {
                left = _frame.frames[_roles[L]].width;
                right = _frame.frames[_roles[R]].width;
                top = _frame.frames[_roles[T]].height;
                bottom = _frame.frames[_roles[B]].height;
            }

            float width = left + PaddingX + contentWidth + PaddingX + right;
            float height = top + PaddingY + contentHeight + PaddingY + bottom;
            MenuWidgets.Center(_box, new Vector2(0, VerticalShift), new Vector2(width, height));

            BuildFrame(width, height, left, right, top, bottom);

            float centerTop = height / 2;
            float y = centerTop - top - PaddingY;
            MenuWidgets.PlaceText(title, new Vector2(0, y - TitleHeight / 2), new Vector2(contentWidth, TitleHeight));
            title.transform.SetAsLastSibling();
            y -= TitleHeight + TitleGap;

            for (int i = 0; i < count; ++i)
            {
                var action = actions[i];
                var text = rowTexts[i];
                _parts.Add(text.gameObject);
                var row = BuildRow(text, new Vector2(0, y - RowHeight / 2), new Vector2(contentWidth, RowHeight), () =>
                {
                    MenuWidgets.PlayClick();
                    action();
                });
                if (i == 0)
                    _talkRow = row;
                y -= RowHeight + RowGap;
            }
        }

        private void BuildFrame(float width, float height, float left, float right, float top, float bottom)
        {
            if (_roles == null)
            {
                BuildFallbackFrame(width, height);
                return;
            }

            var fill = MenuWidgets.Box("Fill", _box, FillColor);
            PlaceFromTopLeft(fill.rectTransform, left, top, width - left - right, height - top - bottom);
            fill.raycastTarget = true;
            _parts.Add(fill.gameObject);

            float innerWidth = width - left - right;
            float innerHeight = height - top - bottom;
            var f = _frame.frames;
            AddPiece("Top", _roles[T], left, 0, innerWidth, f[_roles[T]].height, tiled: true);
            AddPiece("Bottom", _roles[B], left, height - f[_roles[B]].height, innerWidth, f[_roles[B]].height, tiled: true);
            AddPiece("Left", _roles[L], 0, top, f[_roles[L]].width, innerHeight, tiled: true);
            AddPiece("Right", _roles[R], width - f[_roles[R]].width, top, f[_roles[R]].width, innerHeight, tiled: true);
            AddPiece("TopLeft", _roles[TL], 0, 0, f[_roles[TL]].width, f[_roles[TL]].height);
            AddPiece("TopRight", _roles[TR], width - f[_roles[TR]].width, 0, f[_roles[TR]].width, f[_roles[TR]].height);
            AddPiece("BottomLeft", _roles[BL], 0, height - f[_roles[BL]].height, f[_roles[BL]].width, f[_roles[BL]].height);
            AddPiece("BottomRight", _roles[BR], width - f[_roles[BR]].width, height - f[_roles[BR]].height, f[_roles[BR]].width, f[_roles[BR]].height);
        }

        // Fallback that visually matches the quest-log dialog in image #2:
        // near-black fill + 1-px tan/gold border.
        private void BuildFallbackFrame(float width, float height)
        {
            var fill = MenuWidgets.Box("Fill", _box, FillColor);
            MenuWidgets.Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();
            _parts.Add(fill.gameObject);

            float t = ManualBorderWidth;
            AddManualBorder("BorderTop", 0, 0, width, t);
            AddManualBorder("BorderBottom", 0, height - t, width, t);
            AddManualBorder("BorderLeft", 0, t, t, height - 2 * t);
            AddManualBorder("BorderRight", width - t, t, t, height - 2 * t);
        }

        private void AddManualBorder(string name, float x, float y, float w, float h)
        {
            var rect = MenuWidgets.NewRect(name, _box);
            PlaceFromTopLeft(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = GoldBorder;
            image.raycastTarget = false;
            _parts.Add(rect.gameObject);
        }

        private void AddPiece(string name, int frameIndex, float x, float y, float width, float height, bool tiled = false)
        {
            var rect = MenuWidgets.NewRect(name, _box);
            PlaceFromTopLeft(rect, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _frame.sprites[frameIndex];
            image.type = tiled ? Image.Type.Tiled : Image.Type.Simple;
            image.raycastTarget = false;
            _parts.Add(rect.gameObject);
        }

        private static void PlaceFromTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private MenuRow BuildRow(Text text, Vector2 position, Vector2 size, System.Action onClick)
        {
            var rect = MenuWidgets.NewRect("Row", _box);
            MenuWidgets.Center(rect, position, size);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            text.transform.SetParent(rect, false);
            MenuWidgets.StretchText(text);

            var row = rect.gameObject.AddComponent<MenuRow>();
            row.text = text;
            row.onClick = onClick;
            row.Refresh();
            _parts.Add(rect.gameObject);
            return row;
        }

        private static string Label(string key, string fallback)
        {
            return Translation.Find(key, fallback).ToUpperInvariant();
        }

        private void OnTalk()
        {
            StopVoice();
            _voice = NpcVoice.PlayTalk(_npc, _player);
        }

        private void OnTrade()
        {
            var player = _player;
            var vendorId = _npc.monStat.id;
            HideInternal();
            GamblePanel.Show(player, vendorId, trade: true);
        }

        private void OnGamble()
        {
            var player = _player;
            var vendorId = _npc.monStat.id;
            HideInternal();
            GamblePanel.Show(player, vendorId);
        }

        private void StopVoice()
        {
            if (_voice != null)
                Object.Destroy(_voice.gameObject);
            _voice = null;
        }

        private void HideInternal()
        {
            StopVoice();
            _npc = null;
            _player = null;
            if (_root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
        }

        private void Tick()
        {
            if (_npc == null || _player == null || _player.unit == null || !_npc.selectable)
            {
                HideInternal();
                return;
            }

            if (Vector3.Distance(_npc.iso.pos, _player.unit.iso.pos) > CloseDistance)
            {
                HideInternal();
                return;
            }

            if (_talkRow != null)
            {
                bool speaking = _voice != null && _voice.isPlaying;
                if (_talkRow.rowEnabled == speaking)
                    _talkRow.SetEnabled(!speaking);
            }
        }

        private class MenuRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public Text text;
            public System.Action onClick;
            public bool rowEnabled = true;
            private bool _hover;

            public void SetEnabled(bool value)
            {
                rowEnabled = value;
                Refresh();
            }

            public void Refresh()
            {
                if (text != null)
                    text.color = !rowEnabled ? DisabledColor : _hover ? HoverColor : RowColor;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _hover = true;
                Refresh();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _hover = false;
                Refresh();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (rowEnabled && eventData.button == PointerEventData.InputButton.Left)
                    onClick?.Invoke();
            }

            private void OnDisable()
            {
                _hover = false;
            }
        }

        private class Watcher : MonoBehaviour
        {
            public NpcDialog dialog;

            private void Update()
            {
                dialog.Tick();
            }
        }
    }
}