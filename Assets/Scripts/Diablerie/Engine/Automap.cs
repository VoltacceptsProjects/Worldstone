using System;
using System.Collections.Generic;
using Diablerie.Engine.Entities;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.UI;
using Diablerie.Engine.World;
using Diablerie.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Diablerie.Engine
{
    // Diablo II style automap.
    //  - Tab shows / hides it. It is an overlay drawn over the game view, in the same isometric projection,
    //    with the player in the middle (white cross).
    //  - Only what the player has explored is drawn. Exploration is tracked even while the map is hidden.
    //  - Each level has its own map, only the map of the level the player is in is shown.
    //  - Walls are drawn along the edges of the wall tiles (left wall, right wall, corners, doors).
    //  - Markers: white cross = you, green cross + name = NPC, red cross = monster, blue cross = ally,
    //    blue diamond = waypoint, yellow diamond = level exit.
    //  - Top right: area name, difficulty and the clock, like the original.
    // Lines are batched into a single GL call from the camera post render callback (before the UI canvas is
    // drawn, so panels stay on top). Nothing is allocated per frame.
    public class Automap : MonoBehaviour
    {
        public static Automap Instance { get; private set; }
        public static bool IsVisible => Instance != null && Instance.visible;

        // Size of one map tile on the automap in pixels of the 800x600 view (the game view uses 160x80)
        private const float TileW = 16f;
        private const float TileH = 8f;
        private const float HalfW = TileW / 2f;
        private const float HalfH = TileH / 2f;

        // What the player explores around him: window measured in tiles along the screen axes
        // (dx - dy is horizontal on the screen, dx + dy vertical)
        private const int RevealHalfWidth = 10;
        private const int RevealHalfHeight = 14;
        private const int RevealRange = 12;

        private const int ChunkShift = 4;
        private const float WallWidth = 1.6f;
        private const float MarkerWidth = 1.6f;
        private const int MaxNameLabels = 16;

        private static readonly Color WallColor = new Color(0.78f, 0.78f, 0.80f, 0.85f);
        private static readonly Color DoorColor = new Color(0.85f, 0.65f, 0.25f, 0.95f);
        private static readonly Color PlayerColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color NpcColor = new Color(0.25f, 0.95f, 0.25f, 1f);
        private static readonly Color MonsterColor = new Color(1f, 0.15f, 0.15f, 1f);
        private static readonly Color AllyColor = new Color(0.35f, 0.5f, 1f, 1f);
        private static readonly Color WaypointColor = new Color(0.3f, 0.65f, 1f, 1f);
        private static readonly Color ExitColor = new Color(1f, 0.9f, 0.2f, 1f);

        private struct Seg
        {
            public float x0, y0, x1, y1;
            public bool door;
        }

        private class LevelMap
        {
            public List<Seg>[] chunks;
            public readonly HashSet<long> edges = new HashSet<long>();
        }

        private enum MarkerKind { Npc, Monster, Ally, Waypoint, Exit }

        private struct Marker
        {
            public float x, y;
            public MarkerKind kind;
            public string name;
        }

        private bool visible;
        private Camera mainCamera;
        private Material lineMaterial;
        private bool materialFailed;

        private WorldGrid grid;
        private byte[] revealed;
        private int chunkCount;
        private readonly Dictionary<int, LevelMap> maps = new Dictionary<int, LevelMap>();
        private int lastTileX = int.MinValue;
        private int lastTileY = int.MinValue;
        private int lastLevelId = int.MinValue;
        private int currentLevelId = -1;

        // Player position in tile units (tile centers are on integer coordinates), updated every frame
        private float playerTx;
        private float playerTy;
        private bool havePlayer;
        private readonly List<Marker> markers = new List<Marker>(64);

        // UI text
        private RectTransform textRoot;
        private Text areaText;
        private Text difficultyText;
        private Text clockText;
        private readonly List<Text> nameTexts = new List<Text>();
        private int lastClockSecond = -1;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Camera.onPostRender += OnCameraPostRender;
        }

        private void OnDisable()
        {
            Camera.onPostRender -= OnCameraPostRender;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (grid != null)
                grid.OnReset -= OnGridReset;
            if (lineMaterial != null)
                Destroy(lineMaterial);
            if (textRoot != null)
                Destroy(textRoot.gameObject);
        }

        public static void Toggle()
        {
            if (Instance != null)
                Instance.visible = !Instance.visible;
        }

        // ------------------------------------------------------------------------------------ update

        private void Update()
        {
            var world = WorldState.instance;
            if (world == null)
                return;

            EnsureGrid(world.Grid);

            var player = world.Player;
            if (player == null || player.unit == null)
            {
                havePlayer = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab) && InputAllowed())
                visible = !visible;

            currentLevelId = Level.Current != null ? Level.Current.id : -1;
            UpdateReveal(player.unit);
        }

        private void LateUpdate()
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null || world.Player.unit == null)
            {
                havePlayer = false;
                HideOverlay();
                return;
            }

            if (mainCamera == null)
                mainCamera = Camera.main;

            // Same source the camera follows, so the map and the sprites never drift apart
            Vector3 iso = Iso.MapToIso(world.Player.unit.transform.position);
            playerTx = iso.x / Iso.SubTileCount;
            playerTy = iso.y / Iso.SubTileCount;
            havePlayer = true;

            if (!visible || currentLevelId < 0)
            {
                HideOverlay();
                return;
            }

            BuildMarkers(world);
            UpdateTexts();
        }

        private static bool InputAllowed()
        {
            if (PlayerController.instance == null || !PlayerController.instance.enabled)
                return false;
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.GetComponent<InputField>() != null)
                return false;
            return true;
        }

        private void EnsureGrid(WorldGrid worldGrid)
        {
            if (grid == worldGrid)
                return;
            if (grid != null)
                grid.OnReset -= OnGridReset;
            grid = worldGrid;
            grid.OnReset += OnGridReset;
            revealed = new byte[grid.Width * grid.Height];
            chunkCount = (Mathf.Max(grid.Width, grid.Height) >> ChunkShift) + 1;
            OnGridReset();
        }

        // A new act starts with an empty map
        private void OnGridReset()
        {
            maps.Clear();
            if (revealed != null)
                Array.Clear(revealed, 0, revealed.Length);
            lastTileX = int.MinValue;
            lastTileY = int.MinValue;
            lastLevelId = int.MinValue;
        }

        // ------------------------------------------------------------------------------------ exploring

        private void UpdateReveal(Unit unit)
        {
            if (currentLevelId < 0)
                return;

            Vector2 pos = unit.iso.pos;
            int px = Mathf.RoundToInt(pos.x / Iso.SubTileCount);
            int py = Mathf.RoundToInt(pos.y / Iso.SubTileCount);
            if (px == lastTileX && py == lastTileY && currentLevelId == lastLevelId)
                return;
            lastTileX = px;
            lastTileY = py;
            lastLevelId = currentLevelId;

            if (!maps.TryGetValue(currentLevelId, out var map))
            {
                map = new LevelMap { chunks = new List<Seg>[chunkCount * chunkCount] };
                maps.Add(currentLevelId, map);
            }

            int width = grid.Width;
            int height = grid.Height;
            for (int y = py - RevealRange; y <= py + RevealRange; ++y)
            {
                if (y < 0 || y >= height - 1)
                    continue;
                for (int x = px - RevealRange; x <= px + RevealRange; ++x)
                {
                    if (x < 0 || x >= width - 1)
                        continue;
                    int dx = x - px;
                    int dy = y - py;
                    if (Mathf.Abs(dx - dy) > RevealHalfWidth || Mathf.Abs(dx + dy) > RevealHalfHeight)
                        continue;
                    int index = y * width + x;
                    if (revealed[index] != 0)
                        continue;
                    if (TileLevel(x, y) != currentLevelId)
                        continue;

                    revealed[index] = 1;
                    AddTile(map, x, y, index);
                }
            }
        }

        private int TileLevel(int x, int y)
        {
            var streamer = LevelStreamer.Current;
            return streamer != null ? streamer.LevelIdAt(x, y) : currentLevelId;
        }

        private bool IsRevealed(float tx, float ty)
        {
            int x = Mathf.RoundToInt(tx);
            int y = Mathf.RoundToInt(ty);
            if (x < 0 || y < 0 || x >= grid.Width || y >= grid.Height)
                return false;
            return revealed[y * grid.Width + x] != 0;
        }

        private void AddTile(LevelMap map, int x, int y, int index)
        {
            var layers = grid.Walls;
            for (int l = 0; l < layers.Length; ++l)
            {
                var tile = layers[l][index];
                if (tile == null)
                    continue;

                switch (tile.orientation)
                {
                    case 1: // left wall
                    case 4: // left part of a north corner
                    case 5: // left end wall
                        if (Blocks(tile)) AddEdge(map, x, y, Edge.West, false);
                        break;
                    case 2: // right wall
                    case 3: // right part of a north corner
                    case 6: // right end wall
                        if (Blocks(tile)) AddEdge(map, x, y, Edge.North, false);
                        break;
                    case 7: // south corner
                        if (Blocks(tile))
                        {
                            AddEdge(map, x, y, Edge.South, false);
                            AddEdge(map, x, y, Edge.East, false);
                        }
                        break;
                    case 8: // left wall with a door
                        AddEdge(map, x, y, Edge.West, true);
                        break;
                    case 9: // right wall with a door
                        AddEdge(map, x, y, Edge.North, true);
                        break;
                    case 12: // pillar
                        if (Blocks(tile)) AddPillar(map, x, y);
                        break;
                }
            }
        }

        private static bool Blocks(DT1.Tile tile)
        {
            var flags = tile.flags;
            if (flags == null)
                return false;
            const DT1.BlockFlags mask = DT1.BlockFlags.Walk | DT1.BlockFlags.PlayerWalk;
            for (int i = 0; i < flags.Length; ++i)
            {
                if ((flags[i] & mask) != 0)
                    return true;
            }
            return false;
        }

        private enum Edge { West, North, East, South }

        // A tile covers [x - 0.5, x + 0.5] x [y - 0.5, y + 0.5]. West / East edges run along y, North / South along x.
        // Edges shared by two tiles are stored once.
        private void AddEdge(LevelMap map, int x, int y, Edge edge, bool door)
        {
            int axis;
            int boundary;
            int span;
            switch (edge)
            {
                case Edge.West: axis = 0; boundary = x; span = y; break;
                case Edge.East: axis = 0; boundary = x + 1; span = y; break;
                case Edge.North: axis = 1; boundary = y; span = x; break;
                default: axis = 1; boundary = y + 1; span = x; break;
            }

            long key = ((long) axis << 42) | ((long) boundary << 21) | (long) span;
            if (!map.edges.Add(key))
                return;

            if (axis == 0)
                AddSeg(map, x, y, boundary - 0.5f, span - 0.5f, boundary - 0.5f, span + 0.5f, door);
            else
                AddSeg(map, x, y, span - 0.5f, boundary - 0.5f, span + 0.5f, boundary - 0.5f, door);
        }

        private void AddPillar(LevelMap map, int x, int y)
        {
            const float r = 0.18f;
            AddSeg(map, x, y, x - r, y - r, x + r, y - r, false);
            AddSeg(map, x, y, x + r, y - r, x + r, y + r, false);
            AddSeg(map, x, y, x + r, y + r, x - r, y + r, false);
            AddSeg(map, x, y, x - r, y + r, x - r, y - r, false);
        }

        private void AddSeg(LevelMap map, int ownerX, int ownerY, float x0, float y0, float x1, float y1, bool door)
        {
            int chunk = (ownerY >> ChunkShift) * chunkCount + (ownerX >> ChunkShift);
            var list = map.chunks[chunk];
            if (list == null)
            {
                list = new List<Seg>(32);
                map.chunks[chunk] = list;
            }
            list.Add(new Seg { x0 = x0, y0 = y0, x1 = x1, y1 = y1, door = door });
        }

        // ------------------------------------------------------------------------------------ markers

        private bool InWindow(float tx, float ty)
        {
            float dx = tx - playerTx;
            float dy = ty - playerTy;
            return Mathf.Abs(dx - dy) <= RevealHalfWidth + 2 && Mathf.Abs(dx + dy) <= RevealHalfHeight + 2;
        }

        private void BuildMarkers(WorldState world)
        {
            markers.Clear();
            var playerUnit = world.Player.unit;
            foreach (var entity in world.Entities)
            {
                if (entity == null)
                    continue;

                if (entity is Unit unit)
                {
                    if (unit == playerUnit || unit.monStat == null)
                        continue;
                    string mode = unit.Mode;
                    if (mode == "DT" || mode == "DD")
                        continue;

                    Vector2 pos = unit.iso.pos;
                    float tx = pos.x / Iso.SubTileCount;
                    float ty = pos.y / Iso.SubTileCount;
                    if (!InWindow(tx, ty))
                        continue;

                    MarkerKind kind;
                    if (unit.monStat.npc)
                        kind = MarkerKind.Npc;
                    else if (unit.party == Party.Good)
                        kind = MarkerKind.Ally;
                    else if (unit.killable)
                        kind = MarkerKind.Monster;
                    else
                        continue;

                    AddMarker(tx, ty, kind, kind == MarkerKind.Npc ? unit.title : null);
                }
                else if (entity is Warp)
                {
                    TryAddStatic(entity, MarkerKind.Exit);
                }
                else if (entity is StaticObject staticObject && Waypoints.IsWaypoint(staticObject))
                {
                    TryAddStatic(entity, MarkerKind.Waypoint);
                }
            }
        }

        private void TryAddStatic(Entity entity, MarkerKind kind)
        {
            Vector3 iso = Iso.MapToIso(entity.transform.position);
            float tx = iso.x / Iso.SubTileCount;
            float ty = iso.y / Iso.SubTileCount;
            if (!IsRevealed(tx, ty))
                return;
            AddMarker(tx, ty, kind, null);
        }

        private void AddMarker(float tx, float ty, MarkerKind kind, string name)
        {
            markers.Add(new Marker
            {
                x = ScreenX(tx, ty),
                y = ScreenY(tx, ty),
                kind = kind,
                name = name
            });
        }

        private float ScreenX(float tx, float ty)
        {
            return CameraController.VirtualWidth * 0.5f + ((tx - playerTx) - (ty - playerTy)) * HalfW;
        }

        private float ScreenY(float tx, float ty)
        {
            return CameraController.VirtualHeight * 0.5f - ((tx - playerTx) + (ty - playerTy)) * HalfH;
        }

        // ------------------------------------------------------------------------------------ drawing

        private void OnCameraPostRender(Camera cam)
        {
            if (!visible || !havePlayer || cam != mainCamera || currentLevelId < 0 || grid == null)
                return;
            if (!EnsureMaterial())
                return;

            GL.PushMatrix();
            lineMaterial.SetPass(0);
            GL.LoadPixelMatrix(0, CameraController.VirtualWidth, 0, CameraController.VirtualHeight);
            GL.Begin(GL.QUADS);

            DrawWalls();
            DrawMarkers();

            GL.End();
            GL.PopMatrix();
        }

        private bool EnsureMaterial()
        {
            if (lineMaterial != null)
                return true;
            if (materialFailed)
                return false;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                materialFailed = true;
                Debug.LogWarning("Automap: Hidden/Internal-Colored shader not found, the automap is disabled");
                return false;
            }

            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int) UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int) UnityEngine.Rendering.CompareFunction.Always);
            return true;
        }

        private void DrawWalls()
        {
            if (!maps.TryGetValue(currentLevelId, out var map))
                return;

            // Tile rectangle covered by the screen, to pick the chunks that have to be drawn
            float halfWidth = CameraController.VirtualWidth * 0.5f / HalfW;   // |dx - dy| limit
            float halfHeight = CameraController.VirtualHeight * 0.5f / HalfH; // |dx + dy| limit
            float extent = (halfWidth + halfHeight) * 0.5f + 2;
            int minX = Mathf.FloorToInt(playerTx - extent) >> ChunkShift;
            int maxX = Mathf.CeilToInt(playerTx + extent) >> ChunkShift;
            int minY = Mathf.FloorToInt(playerTy - extent) >> ChunkShift;
            int maxY = Mathf.CeilToInt(playerTy + extent) >> ChunkShift;
            minX = Mathf.Clamp(minX, 0, chunkCount - 1);
            maxX = Mathf.Clamp(maxX, 0, chunkCount - 1);
            minY = Mathf.Clamp(minY, 0, chunkCount - 1);
            maxY = Mathf.Clamp(maxY, 0, chunkCount - 1);

            for (int cy = minY; cy <= maxY; ++cy)
            {
                for (int cx = minX; cx <= maxX; ++cx)
                {
                    var list = map.chunks[cy * chunkCount + cx];
                    if (list == null)
                        continue;
                    for (int i = 0; i < list.Count; ++i)
                    {
                        var seg = list[i];
                        GL.Color(seg.door ? DoorColor : WallColor);
                        Line(ScreenX(seg.x0, seg.y0), ScreenY(seg.x0, seg.y0),
                             ScreenX(seg.x1, seg.y1), ScreenY(seg.x1, seg.y1), WallWidth);
                    }
                }
            }
        }

        private void DrawMarkers()
        {
            for (int i = 0; i < markers.Count; ++i)
            {
                var marker = markers[i];
                switch (marker.kind)
                {
                    case MarkerKind.Npc: Cross(marker.x, marker.y, 3f, NpcColor); break;
                    case MarkerKind.Monster: Cross(marker.x, marker.y, 3f, MonsterColor); break;
                    case MarkerKind.Ally: Cross(marker.x, marker.y, 3f, AllyColor); break;
                    case MarkerKind.Waypoint: Diamond(marker.x, marker.y, 6f, WaypointColor, true); break;
                    case MarkerKind.Exit: Diamond(marker.x, marker.y, 6f, ExitColor, false); break;
                }
            }

            // You, always on top
            Cross(CameraController.VirtualWidth * 0.5f, CameraController.VirtualHeight * 0.5f, 4f, PlayerColor);
        }

        private static void Cross(float x, float y, float arm, Color color)
        {
            GL.Color(color);
            Line(x - arm, y, x + arm, y, MarkerWidth);
            Line(x, y - arm, x, y + arm, MarkerWidth);
        }

        private static void Diamond(float x, float y, float size, Color color, bool center)
        {
            GL.Color(color);
            Line(x - size, y, x, y + size * 0.5f, MarkerWidth);
            Line(x, y + size * 0.5f, x + size, y, MarkerWidth);
            Line(x + size, y, x, y - size * 0.5f, MarkerWidth);
            Line(x, y - size * 0.5f, x - size, y, MarkerWidth);
            if (center)
                Line(x - 1.5f, y, x + 1.5f, y, MarkerWidth);
        }

        // A thick line as a quad
        private static void Line(float x0, float y0, float x1, float y1, float width)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.0001f)
                return;
            float nx = -dy / length * width * 0.5f;
            float ny = dx / length * width * 0.5f;
            GL.Vertex3(x0 + nx, y0 + ny, 0);
            GL.Vertex3(x0 - nx, y0 - ny, 0);
            GL.Vertex3(x1 - nx, y1 - ny, 0);
            GL.Vertex3(x1 + nx, y1 + ny, 0);
        }

        // ------------------------------------------------------------------------------------ text

        private bool EnsureTexts()
        {
            if (textRoot != null)
                return true;
            if (Ui.instance == null)
                return false;

            var rootObject = new GameObject("Automap Text", typeof(RectTransform));
            textRoot = rootObject.GetComponent<RectTransform>();
            textRoot.SetParent(Ui.instance.RectTransform, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = Vector2.zero;
            textRoot.offsetMax = Vector2.zero;

            areaText = CreateText("Area", TextAnchor.UpperRight, Color.white);
            difficultyText = CreateText("Difficulty", TextAnchor.UpperRight, Color.white);
            clockText = CreateText("Clock", TextAnchor.UpperRight, Color.white);
            PlaceCorner(areaText, 0);
            PlaceCorner(difficultyText, 1);
            PlaceCorner(clockText, 2);
            difficultyText.text = "Normal";
            return true;
        }

        private Text CreateText(string objectName, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(textRoot, false);
            var text = textObject.GetComponent<Text>();
            text.font = Fonts.GetFont16();
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void PlaceCorner(Text text, int row)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(300, 20);
            rect.anchoredPosition = new Vector2(-12, -8 - row * 18);
        }

        private void UpdateTexts()
        {
            if (!EnsureTexts())
                return;

            areaText.gameObject.SetActive(true);
            difficultyText.gameObject.SetActive(true);
            clockText.gameObject.SetActive(true);

            string area = Level.Current != null ? Level.Current.levelName : string.Empty;
            if (areaText.text != area)
                areaText.text = area;

            var now = DateTime.Now;
            int second = now.Hour * 3600 + now.Minute * 60 + now.Second;
            if (second != lastClockSecond)
            {
                lastClockSecond = second;
                clockText.text = now.ToString("h:mm:ss tt");
            }

            // NPC names next to their markers
            int used = 0;
            for (int i = 0; i < markers.Count && used < MaxNameLabels; ++i)
            {
                var marker = markers[i];
                if (marker.kind != MarkerKind.Npc || string.IsNullOrEmpty(marker.name))
                    continue;
                var label = GetNameText(used++);
                label.gameObject.SetActive(true);
                if (label.text != marker.name)
                    label.text = marker.name;
                label.rectTransform.anchoredPosition = new Vector2(marker.x, marker.y + 6);
            }
            for (int i = used; i < nameTexts.Count; ++i)
                nameTexts[i].gameObject.SetActive(false);
        }

        private Text GetNameText(int index)
        {
            while (nameTexts.Count <= index)
            {
                var text = CreateText("Name", TextAnchor.LowerCenter, NpcColor);
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0);
                rect.sizeDelta = new Vector2(200, 20);
                nameTexts.Add(text);
            }
            return nameTexts[index];
        }

        private void HideOverlay()
        {
            if (textRoot == null)
                return;
            areaText.gameObject.SetActive(false);
            difficultyText.gameObject.SetActive(false);
            clockText.gameObject.SetActive(false);
            for (int i = 0; i < nameTexts.Count; ++i)
                nameTexts[i].gameObject.SetActive(false);
        }
    }
}
