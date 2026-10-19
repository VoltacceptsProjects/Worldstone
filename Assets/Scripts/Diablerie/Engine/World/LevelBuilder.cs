using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.Utility;
using Diablerie.Game.World;
using UnityEngine;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

namespace Diablerie.Engine.World
{
    public class LevelBuilder
    {
        public readonly int gridX;
        public readonly int gridY;
        public readonly int gridWidth;
        public readonly int gridHeight;

        public LevelInfo info;
        string name;
        DS1[] grid;
        private int popPad;
        DT1.Sampler _tileSampler;
        DT1.Sampler tileSampler
        {
            get
            {
                if (_tileSampler == null)
                    InitTileSampler();
                return _tileSampler;
            }
        }
        MonStat[] monStats;
        Color32[] palette;

        static readonly int mapEntryIndex = DT1.Tile.Index(30, 11, 10);
        static readonly int townEntryIndex = DT1.Tile.Index(30, 0, 10);
        static readonly int townEntry2Index = DT1.Tile.Index(31, 0, 10);
        static readonly int corpseLocationIndex = DT1.Tile.Index(32, 0, 10);
        static readonly int portalLocationIndex = DT1.Tile.Index(33, 0, 10);

        static DT1.Sampler specialTiles = new DT1.Sampler();
        static LevelBuilder()
        {
            var palette = Palette.GetPalette(PaletteType.Act1);
            var dt1 = DT1.Load(Application.streamingAssetsPath + "/ds1edit.dt1", palette, mpq: false);
            specialTiles.Add(dt1.tiles);
        }

        public LevelBuilder(string name, int gridX = -1, int gridY = -1)
        {
            info = LevelInfo.Find(name);
            this.name = info.levelName;
            palette = Palette.GetPalette(info.act);

            if (info.preset != null)
            {
                var ds1 = DS1.Load(info.preset.ds1Files[0]);
                this.gridX = ds1.width - 1;
                this.gridY = ds1.height - 1;
                gridWidth = 1;
                gridHeight = 1;
                grid = new DS1[1] { ds1 };
                popPad = info.preset.popPad;
            }
            else
            {
                if (info.maze != null)
                {
                    this.gridX = info.maze.sizeX;
                    this.gridY = info.maze.sizeY;
                }
                else
                {
                    this.gridX = gridX;
                    this.gridY = gridY;
                }
                gridWidth = info.sizeX / this.gridX;
                gridHeight = info.sizeY / this.gridY;
                grid = new DS1[gridWidth * gridHeight];
            }
        }

        public LevelBuilder(DS1 ds1)
        {
            name = Path.GetFileName(ds1.filename);
            grid = new DS1[1] { ds1 };
            gridWidth = 1;
            gridHeight = 1;
            gridX = ds1.width - 1;
            gridY = ds1.height - 1;
        }

        private void InitTileSampler()
        {
            var sampler = new DT1.Sampler();
            if (info != null)
            {
                foreach (var dt1Filename in info.type.dt1Files)
                {
                    var dt1 = DT1.Load(dt1Filename, palette);
                    sampler.Add(dt1.tiles);
                }
            }
            _tileSampler = sampler;
        }

        // Loads all tile sets this level needs in small steps, so that building it later does not stall a frame
        public IEnumerator PrefetchTiles()
        {
            if (info != null && _tileSampler == null)
            {
                foreach (var dt1Filename in info.type.dt1Files)
                {
                    var job = DT1.BeginLoad(dt1Filename, palette);
                    if (job == null)
                        continue;
                    while (!job.Step(Math.Max(0.5, TimeSlice.RemainingMs)))
                        yield return null;
                }
                InitTileSampler();
                yield return null;
            }

            foreach (var ds1 in grid)
            {
                if (ds1 == null || ds1.TilesReady)
                    continue;
                var prefetch = ds1.PrefetchTiles();
                while (prefetch.MoveNext())
                    yield return null;
                yield return null;
            }
        }

        public void Place(LevelPreset preset, Vector2i pos, int minIndex = 0, int maxIndex = -1)
        {
            if (maxIndex == -1)
                maxIndex = preset.ds1Files.Count;
            var ds1Filename = preset.ds1Files[Random.Range(minIndex, maxIndex)];
            var ds1 = DS1.Load(ds1Filename);
            Place(ds1, pos);
        }

        public void Place(DS1 ds1, Vector2i pos)
        {
            Debug.Assert(ds1.width - 1 == gridX);
            Debug.Assert(ds1.height - 1 == gridY);
            grid[pos.y * gridWidth + pos.x] = ds1;
        }

        void InstantiatePopups(DS1 ds1, int offsetX, int offsetY, Transform parent = null)
        {
            var startPos = new Vector2i[7];
            var firstFound = new bool[7];

            for (int layerIndex = 0; layerIndex < ds1.walls.Length; ++layerIndex)
            {
                var walls = ds1.walls[layerIndex];
                int i = 0;
                for (int y = 0; y < ds1.height; ++y)
                {
                    for (int x = 0; x < ds1.width; ++x, ++i)
                    {
                        if (walls[i].orientation != 10)
                            continue;
                        int mainIndex = walls[i].mainIndex;
                        int group;
                        // TODO merge 8, 9, 10
                        // TODO merge 12, 13
                        if (mainIndex == 8)
                            group = 0;
                        else if (mainIndex == 9)
                            group = 1;
                        else if (mainIndex == 10)
                            group = 2;
                        else if (mainIndex == 12)
                            group = 3;
                        else if (mainIndex == 13)
                            group = 4;
                        else if (mainIndex == 16)
                            group = 5;
                        else if (mainIndex == 20)
                            group = 6;
                        else
                            continue;
                    
                        if (firstFound[group])
                        {
                            int x1 = startPos[group].x;
                            int y1 = startPos[group].y;
                            int width = x - x1;
                            int height = y - y1;
                            var triggerArea = new IntRect(x1 + offsetX, y1 + offsetY, width, height);
                            var revealArea = new IntRect(
                                x1 + offsetX - 1, 
                                y1 + offsetY - 1, 
                                width + 3, 
                                height + 3
                            );
                            var popup = Popup.Create(triggerArea, revealArea, walls[i].subIndex);
                            popup.gameObject.name += "_pad" + popPad;  // TODO use popPad to adjust triggerArea
                            popup.transform.SetParent(parent);
                            WorldState.instance.Add(popup);
                        }
                        else
                        {
                            startPos[group] = new Vector2i(x, y);
                            firstFound[group] = true;
                        }
                    }
                }
            }
        }

        public GameObject Instantiate(Vector2i offset)
        {
            var root = BeginInstantiate(offset);
            var content = InstantiateContent(offset, root.transform);
            while (content.MoveNext()) { }
            return root;
        }

        public GameObject BeginInstantiate(Vector2i offset)
        {
            var root = new GameObject(name);

            if (info != null)
            {
                var collider = root.AddComponent<PolygonCollider2D>();
                collider.offset = Iso.MapTileToWorld(offset);
                var footprint = Footprint();
                collider.points = Iso.CreateTileRectPoints(footprint.x, footprint.y);
                collider.isTrigger = true;

                var level = root.AddComponent<Level>();
                level.info = info;

                SelectMonsterTypes();
            }

            return root;
        }

        public IEnumerator InstantiateContent(Vector2i offset, Transform root)
        {
            int i = 0;
            for (int y = 0; y < gridHeight; ++y)
            {
                for (int x = 0; x < gridWidth; ++x, ++i)
                {
                    var ds1 = grid[i];
                    int offsetX = offset.x + x * gridX;
                    int offsetY = offset.y + y * gridY;
                    if (ds1 != null)
                    {
                        InstantiatePopups(ds1, offsetX, offsetY, root);
                        InstantiateFloors(ds1, offsetX, offsetY, root);
                        yield return null;
                        InstantiateShadows(ds1, offsetX, offsetY, root);
                        InstantiateSpecialTiles(ds1, offsetX, offsetY, root);
                        yield return null;
                        InstantiateWalls(ds1, offsetX, offsetY, root);
                        yield return null;

                        var objects = InstantiateObjects(ds1, offsetX, offsetY, root);
                        while (objects.MoveNext())
                            yield return null;

                        var monsters = InstantiateMonsters(offsetX, offsetY, root);
                        while (monsters.MoveNext())
                            yield return null;
                    }
                    else if (info != null && info.drlgType == 3)
                    {
                        FillGap(offset, x, y, root);
                        yield return null;

                        var monsters = InstantiateMonsters(offsetX, offsetY, root);
                        while (monsters.MoveNext())
                            yield return null;
                    }
                }
            }
        }

        private IEnumerator InstantiateMonsters(int offsetX, int offsetY, Transform root)
        {
            if (info == null || monStats == null || monStats.Length == 0)
                yield break;

            int density = info.monDen[0];

            for (int x = offsetX; x < offsetX + gridX; ++x)
            {
                for (int y = offsetY; y < offsetY + gridY; ++y)
                {
                    int sample = Random.Range(0, 100000);
                    if (sample >= density)
                        continue;

                    var monStat = monStats[Random.Range(0, monStats.Length)];
                    if (Spawn(monStat, x, y, info.id, root))
                        yield return null;
                }
            }
        }

        private static bool Spawn(MonStat monStat, int x, int y, int level, Transform root)
        {
            if (monStat == null)
                return false;

            CollisionLayers collisionMask = CollisionLayers.Walk;
            if (!CollisionMap.Passable(new Vector2i(x, y) * Iso.SubTileCount, collisionMask, monStat.ext.sizeX))
                return false;

            int count = Random.Range(monStat.minGrp, monStat.maxGrp + 1);
            for (int i = 0; i < count; ++i)
            {
                var mon = WorldBuilder.SpawnMonster(monStat, Iso.MapTileToWorld(x, y), root);
                if (mon != null)
                    mon.level = level;
            }

            if (monStat.minion1 != null)
            {
                int minionCount = Random.Range(monStat.partyMin, monStat.partyMax);
                for (int i = 0; i < minionCount; ++i)
                {
                    var mon = WorldBuilder.SpawnMonster(monStat.minion1, Iso.MapTileToWorld(x, y), root);
                    if (mon != null)
                        mon.level = level;
                }
            }

            return true;
        }

        private void SelectMonsterTypes()
        {
            if (info == null)
                return;

            var candidates = new List<MonStat>(info.monsters.Count);
            foreach (var monsterId in info.monsters)
            {
                var candidate = MonStat.Find(monsterId);
                if (candidate != null)
                    candidates.Add(candidate);
            }

            int count = Mathf.Min(info.numMon, candidates.Count);
            monStats = new MonStat[Mathf.Max(count, 0)];
            for (int i = 0; i < monStats.Length; ++i)
            {
                int pick = Random.Range(i, candidates.Count);
                var chosen = candidates[pick];
                candidates[pick] = candidates[i];
                candidates[i] = chosen;
                monStats[i] = chosen;
            }
        }

        private void InstantiateDebugGrid(Vector2i offset, Transform root)
        {
            Profiler.BeginSample("LevelBuilder.InstantiateDebugGrid");
            var grid = new GameObject("debug grid");
            grid.transform.SetParent(root);
            grid.layer = UnityLayers.SpecialTiles;

            for (int y = 0; y < gridHeight; ++y)
            {
                for (int x = 0; x < gridWidth; ++x)
                {
                    var cellObject = new GameObject(x + ", " + y);
                    cellObject.transform.position = Iso.MapToWorld(
                        (x * gridX + offset.x) * Iso.SubTileCount - 2,
                        (y * gridY + offset.y) * Iso.SubTileCount - 2);
                    cellObject.transform.SetParent(grid.transform);
                    cellObject.layer = UnityLayers.SpecialTiles;
                    var line = cellObject.AddComponent<LineRenderer>();
                    line.startWidth = 0.1f;
                    line.endWidth = 0.1f;
                    line.material = Materials.Normal;
                    line.useWorldSpace = false;
                    var corners = new[] {
                        Iso.MapTileToWorld(0, 0),
                        Iso.MapTileToWorld(0 + gridX, 0),
                        Iso.MapTileToWorld(0 + gridX, gridY),
                        Iso.MapTileToWorld(0, gridY),
                        Iso.MapTileToWorld(0, 0)
                    };
                    line.positionCount = corners.Length;
                    line.SetPositions(corners);
                }
            }
            Profiler.EndSample();
        }

        private void InstantiateFloors(DS1 ds1, int offsetX, int offsetY, Transform root)
        {

            for (int f = 0; f < ds1.floors.Length; ++f)
            {
                var floors = ds1.floors[f];
                int i = 0;
                for (int y = 0; y < ds1.height - 1; ++y)
                {
                    for (int x = 0; x < ds1.width - 1; ++x)
                    {
                        var cell = floors[i + x];
                        if (cell.prop1 == 0) // no tile here
                            continue;

                        if ((cell.prop4 & 0x80) != 0)
                            continue;

                        if (ds1.tileSampler.Sample(cell.tileIndex, out var tile))
                        {
                            WorldState.instance.Grid.PutFloor(tile, offsetX + x, offsetY + y, f);
                        }
                    }
                    i += ds1.width;
                }
            }
        }

        private void InstantiateShadows(DS1 ds1, int offsetX, int offsetY, Transform root)
        {

            int i = 0;
            for (int y = 0; y < ds1.height - 1; ++y)
            {
                for (int x = 0; x < ds1.width - 1; ++x)
                {
                    var cell = ds1.shadows[i + x];
                    if (cell.prop1 == 0) // no tile here
                        continue;

                    if ((cell.prop4 & 0x80) != 0)
                        continue;

                    DT1.Tile tile;
                    if (ds1.tileSampler.Sample(cell.tileIndex, out tile))
                    {
                        WorldState.instance.Grid.PutShadow(tile, offsetX + x, offsetY + y);
                    }
                }
                i += ds1.width;
            }
        }

        private void InstantiateSpecialTiles(DS1 ds1, int offsetX, int offsetY, Transform root)
        {
            for (int w = 0; w < ds1.walls.Length; ++w)
            {

                var cells = ds1.walls[w];
                int i = 0;
                for (int y = 0; y < ds1.height - 1; ++y)
                {
                    for (int x = 0; x < ds1.width - 1; ++x)
                    {
                        var cell = cells[i + x];
                        if (cell.prop1 == 0) // no tile here
                            continue;

                        if (cell.orientation == 10 || cell.orientation == 11)
                        {
                            CreateSpecialTile(cell, offsetX + x, offsetY + y, parent: root);
                        }
                    }
                    i += ds1.width;
                }
            }
        }
    
        private void InstantiateWalls(DS1 ds1, int offsetX, int offsetY, Transform root)
        {
            for (int w = 0; w < ds1.walls.Length; ++w)
            {
                var sampler = ds1.tileSampler;

                var cells = ds1.walls[w];
                int i = 0;
                for (int y = 0; y < ds1.height - 1; ++y)
                {
                    for (int x = 0; x < ds1.width - 1; ++x)
                    {
                        var cell = cells[i + x];
                        if (cell.prop1 == 0) // no tile here
                            continue;
                        
                        if (cell.orientation == 10 || cell.orientation == 11)  // special tiles
                            continue;

                        if (sampler.Sample(cell.tileIndex, out var tile))
                        {
                            WorldState.instance.Grid.PutWall(tile, offsetX + x, offsetY + y, w);
                        }

                        if (cell.orientation == 3)
                        {
                            int orientation = 4;
                            int index = DT1.Tile.Index(cell.mainIndex, cell.subIndex, orientation);
                            if (sampler.Sample(index, out tile))
                            {
                                WorldState.instance.Grid.PutWall(tile, offsetX + x, offsetY + y, w + 1);
                            }
                        }
                    }
                    i += ds1.width;
                }
            }
        }
    
        private void CreateSpecialTile(DS1.Cell cell, int x, int y, Transform parent)
        {
            // debug visualization
            if (specialTiles.Sample(cell.tileIndex, out var tile))
            {
                WorldState.instance.Grid.PutSpecialTile(tile, x, y);
            }

            if (info == null)
                return;

            if (cell.mainIndex < 8)
            {
                int targetLevelId = info.vis[cell.mainIndex];
                int warpId = info.warp[cell.mainIndex];
                var targetLevel = LevelInfo.Find(targetLevelId);
                if (targetLevel == null)
                    return;
                var levelWarpInfo = LevelWarpInfo.Find(warpId);
                if (levelWarpInfo == null)
                {
                    Debug.LogWarning("Warp info wasn't found");
                    return;
                }
                Warp.Create(x, y, levelWarpInfo, info, targetLevel, parent);
            }
        }

        private static readonly HashSet<string> warnedPresets = new HashSet<string>();

        private IEnumerator InstantiateObjects(DS1 ds1, int offsetX, int offsetY, Transform root)
        {
            offsetX *= Iso.SubTileCount;
            offsetY *= Iso.SubTileCount;
            int monsterLevel = info != null ? info.id : 1;
            var objects = ds1.objects;
            if (objects == null)
                yield break;

            for (int i = 0; i < objects.Length; ++i)
            {
                var spawnInfo = objects[i];
                var preset = spawnInfo.preset;
                if (preset == null)
                    continue;

                bool created = CreateObject(preset, offsetX + spawnInfo.x, offsetY + spawnInfo.y, monsterLevel, root);
                if (!created)
                {
                    string key = preset.act + "/" + preset.type + "/" + preset.id;
                    if (warnedPresets.Add(key))
                        Debug.LogWarning("Failed to instantiate objects from \"" + ds1.filename + "\" act " + preset.act + ", type " + preset.type + ", id " + preset.id + " (further occurrences suppressed)");
                }
                yield return null;
            }
        }

        private void FillGap(Vector2i offset, int x, int y, Transform root)
        {
            Profiler.BeginSample("LevelBuilder.FillGap");
            int offsetX = x * gridX;
            int offsetY = y * gridY;

            for (y = offsetY; y < offsetY + gridY; ++y)
            {
                for (x = offsetX; x < offsetX + gridX; ++x)
                {
                    if (tileSampler.Sample(0, out var tile))
                    {
                        WorldState.instance.Grid.PutFloor(tile, offset.x + x, offset.y + y, 0);
                    }
                }
            }
            Profiler.EndSample();
        }

        static bool CreateObject(SpawnPreset obj, int x, int y, int level, Transform root)
        {
            var pos = Iso.MapToWorld(x - 2, y - 2);
            if (obj.type == 2)
            {
                if (obj.objectId >= ObjectInfo.sheet.Count)
                    return false;
                ObjectInfo objectInfo = ObjectInfo.sheet[obj.objectId];
                var staticObject = WorldBuilder.SpawnObject(objectInfo, pos, parent: root);
                staticObject.SetMode(obj.mode);
                return true;
            }

            string monPreset = MonPreset.Find(obj.act, obj.id);
            MonStat monStat = null;
            SuperUnique superUnique = null;

            if (monPreset != null)
            {
                monStat = MonStat.Find(monPreset);
                if (monStat == null)
                    superUnique = SuperUnique.Find(monPreset);
            }
            else
            {
                monStat = MonStat.sheet[obj.id];
            }

            if (monStat != null)
            {
                WorldBuilder.SpawnMonster(monStat, pos, root);
                return true;
            }

            if (superUnique != null)
            {
                var monster = WorldBuilder.SpawnMonster(superUnique.monStat, pos, root);
                if (monster != null)
                {
                    monster.gameObject.name = superUnique.nameStr;
                    monster.title = superUnique.name;
                    monster.level = level;
                }
                int minionCount = Random.Range(superUnique.minGrp, superUnique.maxGrp + 1);
                for (int i = 0; i < minionCount; ++i)
                {
                    var minion = WorldBuilder.SpawnMonster(superUnique.monStat, pos, root);
                    if (minion != null)
                        minion.level = level;
                }
                return true;
            }

            if (obj.id == 10)
            {
                // Fallens
                for (int i = 0; i < 4; ++i)
                    WorldBuilder.SpawnMonster("fallen1", pos, root);
                return true;
            }

            if (obj.id == 11)
            {
                // Fallen shaman + fallens
                Spawn(MonStat.Find("fallenshaman1"), x, y, level, root);
                for (int i = 0; i < 4; ++i)
                {
                    var fallen = WorldBuilder.SpawnMonster("fallen1", pos, root);
                    if (fallen != null)
                        fallen.level = level;
                }
                return true;
            }

            if (obj.id == 27)
            {
                // Fallen shaman
                Spawn(MonStat.Find("fallenshaman1"), x, y, level, root);
                return true;
            }

            return false;
        }

        // Maze levels only use an 8x8 room grid even though Levels.txt reserves 200x200 tiles for them.
        // The trigger area has to match what was built, otherwise neighbouring levels would overlap.
        public static Vector2i Footprint(LevelInfo info)
        {
            if (info.maze != null)
                return new Vector2i(Mathf.Min(info.sizeX, info.maze.sizeX * 8), Mathf.Min(info.sizeY, info.maze.sizeY * 8));
            return new Vector2i(info.sizeX, info.sizeY);
        }

        public Vector2i Footprint()
        {
            return Footprint(info);
        }

        public Vector2i FindEntry()
        {
            foreach (var ds1 in grid)
            {
                if (ds1 == null)
                    continue;

                foreach (var cells in ds1.walls)
                {
                    int i = 0;
                    for (int y = 0; y < ds1.height; ++y)
                    {
                        for (int x = 0; x < ds1.width; ++x, ++i)
                        {
                            var cell = cells[i];
                            if (cell.tileIndex == mapEntryIndex)
                            {
                                return new Vector2i(x, y);
                            }

                            if (cell.tileIndex == townEntryIndex)
                            {
                                return new Vector2i(x, y);
                            }

                            if (cell.tileIndex == townEntry2Index)
                            {
                                return new Vector2i(x, y);
                            }
                        }
                    }
                }
            }

            return new Vector2i(info.sizeX, info.sizeY) / 2;
        }
    }
}
