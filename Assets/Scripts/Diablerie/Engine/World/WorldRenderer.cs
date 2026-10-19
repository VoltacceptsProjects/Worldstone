using System;
using System.Collections;
using System.Collections.Generic;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.Utility;
using UnityEngine;

namespace Diablerie.Engine.World
{
    // Builds tile renderers.
    //  - After the grid is reset (new act) everything is built once, synchronously (the loading screen is up).
    //  - Levels streamed in later are built in small slices over several frames, only for their own rectangle,
    //    so walking into a new map never rebuilds the whole world and never freezes the game.
    // Tiles share materials (sharedMaterial) and meshes; no per-tile materials or names are allocated.
    public class WorldRenderer : MonoBehaviour
    {
        public static WorldRenderer Instance { get; private set; }

        // Milliseconds per frame spent creating tile objects of a streamed level
        private const double SliceMs = 3.0;
        private readonly System.Diagnostics.Stopwatch sliceClock = new System.Diagnostics.Stopwatch();

        private GameObject root;
        private bool needRebuild = true;
        private readonly Dictionary<DT1.Tile, Mesh> meshCache = new Dictionary<DT1.Tile, Mesh>();
        private readonly Queue<RectInt> pending = new Queue<RectInt>();
        private readonly List<Popup> regionPopups = new List<Popup>();
        private Coroutine worker;
        private WorldGrid subscribedGrid;

        public bool Busy => worker != null || pending.Count > 0 || needRebuild;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (subscribedGrid != null)
                subscribedGrid.OnReset -= OnGridReset;
        }

        private void Start()
        {
            subscribedGrid = WorldState.instance.Grid;
            subscribedGrid.OnReset += OnGridReset;
        }

        private void OnGridReset()
        {
            needRebuild = true;
            pending.Clear();
            if (worker != null)
            {
                StopCoroutine(worker);
                worker = null;
            }
        }

        // Called by the level streamer after a level has been written into the grid
        public void AddRegion(RectInt region)
        {
            if (needRebuild)
                return; // the full rebuild will include it
            pending.Enqueue(region);
            if (worker == null)
                worker = StartCoroutine(Work());
        }

        private void LateUpdate()
        {
            if (needRebuild)
            {
                needRebuild = false;
                Rebuild();
            }
        }

        private IEnumerator Work()
        {
            while (pending.Count > 0)
                yield return BuildRegion(pending.Dequeue(), SliceMs);
            worker = null;
        }

        private void Rebuild()
        {
            if (root)
                Destroy(root);
            foreach (var mesh in meshCache.Values)
                Destroy(mesh);
            meshCache.Clear();
            root = new GameObject("Tiles");
            var grid = WorldState.instance.Grid;
            var all = new RectInt(0, 0, grid.Width, grid.Height);
            var it = BuildRegion(all, double.MaxValue);
            while (it.MoveNext()) { }
        }

        private IEnumerator BuildRegion(RectInt region, double sliceMs)
        {
            if (root == null)
                root = new GameObject("Tiles");
            var grid = WorldState.instance.Grid;
            int x0 = Mathf.Max(region.xMin, 0);
            int y0 = Mathf.Max(region.yMin, 0);
            int x1 = Mathf.Min(region.xMax, grid.Width - 1);
            int y1 = Mathf.Min(region.yMax, grid.Height - 1);
            if (x0 >= x1 || y0 >= y1)
                yield break;

            var parent = new GameObject("Region " + x0 + "," + y0).transform;
            parent.SetParent(root.transform, false);

            regionPopups.Clear();
            foreach (var popup in WorldState.instance.Popups)
            {
                var a = popup.revealArea;
                if (a.xMin <= x1 && a.xMax >= x0 && a.yMin <= y1 && a.yMax >= y0)
                    regionPopups.Add(popup);
            }

            int created = 0;
            int width = grid.Width;
            sliceClock.Restart();

            for (int layerIndex = 0; layerIndex < grid.Floors.Length; ++layerIndex)
            {
                var tiles = grid.Floors[layerIndex];
                for (int y = y0; y < y1; ++y)
                {
                    for (int x = x0; x < x1; ++x)
                    {
                        var tile = tiles[y * width + x];
                        if (tile == null)
                            continue;
                        CreateTileRenderer(tile, x, y, parent);
                        if ((++created & 15) == 0 && sliceClock.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; sliceClock.Restart(); }
                    }
                }
            }

            for (int layerIndex = 0; layerIndex < grid.Walls.Length; ++layerIndex)
            {
                var tiles = grid.Walls[layerIndex];
                for (int y = y0; y < y1; ++y)
                {
                    for (int x = x0; x < x1; ++x)
                    {
                        var tile = tiles[y * width + x];
                        if (tile == null)
                            continue;
                        var renderer = CreateTileRenderer(tile, x, y, parent);
                        PutToPopup(tile, renderer, x, y);
                        if ((++created & 15) == 0 && sliceClock.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; sliceClock.Restart(); }
                    }
                }
            }

            {
                var tiles = grid.Shadows;
                for (int y = y0; y < y1; ++y)
                {
                    for (int x = x0; x < x1; ++x)
                    {
                        var tile = tiles[y * width + x];
                        if (tile == null)
                            continue;
                        CreateTileRenderer(tile, x, y, parent);
                        if ((++created & 15) == 0 && sliceClock.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; sliceClock.Restart(); }
                    }
                }
            }

            {
                var tiles = grid.SpecialTiles;
                for (int y = y0; y < y1; ++y)
                {
                    for (int x = x0; x < x1; ++x)
                    {
                        var tile = tiles[y * width + x];
                        if (tile == null)
                            continue;
                        var renderer = CreateTileRenderer(tile, x, y, parent);
                        renderer.gameObject.layer = UnityLayers.SpecialTiles;
                        if ((++created & 15) == 0 && sliceClock.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; sliceClock.Restart(); }
                    }
                }
            }

            regionPopups.Clear();
        }

        private Renderer CreateTileRenderer(DT1.Tile tile, int x, int y, Transform parent)
        {
            var pos = Iso.MapTileToWorld(x, y);

            var gameObject = new GameObject(string.Empty, typeof(MeshFilter), typeof(MeshRenderer));
#if DIABLERIE_TILE_NAMES
            gameObject.name = tile.mainIndex + "_" + tile.subIndex + "_" + tile.orientation;
#endif
            var t = gameObject.transform;
            t.SetParent(parent, false);
            t.position = pos;
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            var meshFilter = gameObject.GetComponent<MeshFilter>();

            if (tile.orientation == 0 || tile.orientation == 15)
            {
                meshRenderer.sortingLayerID = tile.orientation == 0 ? SortingLayers.Floor : SortingLayers.Roof;
            }
            else if (tile.orientation > 15)
            {
                meshRenderer.sortingLayerID = SortingLayers.LowerWall;
            }
            else
            {
                if (tile.orientation == 13) // shadows
                {
                    meshRenderer.sortingLayerID = SortingLayers.Shadow;
                }

                meshRenderer.sortingOrder = Iso.SortingOrder(pos) - 4;
            }

            meshFilter.sharedMesh = GetTileMesh(tile);
            meshRenderer.sharedMaterial = tile.material;
            return meshRenderer;
        }

        private Mesh GetTileMesh(DT1.Tile tile)
        {
            if (meshCache.TryGetValue(tile, out var cached))
                return cached;
            var mesh = CreateTileMesh(tile);
            meshCache.Add(tile, mesh);
            return mesh;
        }

        private static Mesh CreateTileMesh(DT1.Tile tile)
        {
            var texture = tile.texture;
            float x0 = tile.textureX;
            float y0 = tile.textureY;
            float w = tile.width / Iso.pixelsPerUnit;
            float h = -tile.height / Iso.pixelsPerUnit;
            Mesh mesh = new Mesh();
            Vector3 topLeft;

            if (tile.orientation == 0 || tile.orientation == 15)
            {
                topLeft = new Vector3(-1f, 0.5f);
                if (tile.orientation == 15)
                    topLeft.y += tile.roofHeight / Iso.pixelsPerUnit;
            }
            else if (tile.orientation > 15)
            {
                int upperPart = Math.Min(96, -tile.height);
                y0 -= upperPart;
                topLeft = new Vector3(-1f, upperPart / Iso.pixelsPerUnit - 0.5f);
            }
            else
            {
                topLeft = new Vector3(-1f, h - 0.5f);
                y0 += tile.height;
            }

            mesh.vertices = new[]
            {
                topLeft,
                topLeft + new Vector3(0, -h),
                topLeft + new Vector3(w, -h),
                topLeft + new Vector3(w, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(x0 / texture.width, -y0 / texture.height),
                new Vector2(x0 / texture.width, (-y0 + tile.height) / texture.height),
                new Vector2((x0 + tile.width) / texture.width, (-y0 + tile.height) / texture.height),
                new Vector2((x0 + tile.width) / texture.width, -y0 / texture.height)
            };
            mesh.triangles = new[] { 2, 1, 0, 3, 2, 0 };
            return mesh;
        }

        private void PutToPopup(DT1.Tile tile, Renderer renderer, int x, int y)
        {
            for (int i = 0; i < regionPopups.Count; ++i)
            {
                var popup = regionPopups[i];
                if (popup.revealMainIndex == tile.mainIndex && popup.revealArea.Contains(x, y))
                    popup.roofs.Add(renderer);
            }
        }
    }
}
