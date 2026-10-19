using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.Utility;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Game.World
{
    public class Act1 : Act
    {
        private static Act1 instance;
        private LevelStreamer streamer;
        private LevelStreamer.Slot tristramSlot;

        // Tristram is built the first time it is needed (the Cairn Stones portal)
        public static bool HasTristram => instance != null && instance.tristramSlot != null;

        // Where the player arrives in Tristram (map tile)
        public static Vector2i TristramEntry
        {
            get
            {
                var slot = instance.tristramSlot;
                instance.streamer.Load(slot);
                return slot.Builder.FindEntry() + slot.offset;
            }
        }

        private static readonly string[] roomSuffixes =
        {
            "W", "E", "EW", "S", "SW", "SE", "SEW", "N", "NW", "NE", "NEW", "NS", "NSW", "NSE", "NSEW"
        };
        private static readonly string[] dirSuffixes = { "W", "E", "S", "N" };

        // Dungeons are packed into the area right of the wilderness, row by row
        private const int dungeonAreaX = 400;
        private const int dungeonAreaWidth = 590;
        private const int dungeonGap = 8;
        private int shelfX = dungeonAreaX;
        private int shelfY = 0;
        private int shelfHeight = 0;

        public Act1()
        {
            instance = this;
            root = new GameObject("Act 1");
            streamer = root.AddComponent<LevelStreamer>();

            // Levels are registered here and built by the streamer when they are needed.
            // Wilderness: neighbouring maps touch each other and are joined through gaps in their borders.
            var origin = new Vector2i(300, 700);
            var bloodMoorSize = WildernessSize("Act 1 - Wilderness 1");
            var town = new LevelBuilder("Act 1 - Town");
            var townOffset = new Vector2i(
                bloodMoorSize.x - town.gridWidth * town.gridX,
                bloodMoorSize.y) + origin;
            streamer.Add("Act 1 - Town", townOffset, () => town, true);
            streamer.Add("Act 1 - Wilderness 1", origin, CreateBloodMoor, true);

            streamer.Add("Act 1 - Wilderness 2", new Vector2i(300, 620), CreateColdPlains, true);
            streamer.Add("Act 1 - Wilderness 3", new Vector2i(300, 540), CreateStonyField, true);
            streamer.Add("Act 1 - Wilderness 4", new Vector2i(220, 540), CreateDarkWood, true);
            streamer.Add("Act 1 - Wilderness 5", new Vector2i(140, 540), CreateBlackMarsh, true);
            streamer.Add("Act 1 - Wilderness 6", new Vector2i(140, 460), CreateTamoeHighland, true);
            streamer.Add("Act 1 - Graveyard", new Vector2i(260, 630), CreateBurialGrounds, true);
            streamer.Add("Act 1 - Monastery", new Vector2i(148, 442), () => new LevelBuilder("Act 1 - Monastery"), true);

            // Den of Evil stays where it always was
            streamer.Add("Act 1 - Cave 1", new Vector2i(120, 0), CreateDenOfEvil, false);

            // Everything below is only entered through warps, so it is built on demand
            // Caves
            Dungeon("Act 1 - Cave 2", () => CreateCave("Act 1 - Cave 2", true, false, true));
            Dungeon("Act 1 - Cave 3", () => CreateCave("Act 1 - Cave 3", false, true, false));
            Dungeon("Act 1 - Cave 4", () => CreateCave("Act 1 - Cave 4", false, false, true));
            Dungeon("Act 1 - Cave 5", () => CreateCave("Act 1 - Cave 5", false, false, true));
            foreach (var treasure in new[] { "Act 1 - Cave 2 Treasure", "Act 1 - Cave 3 Treasure", "Act 1 - Cave 4 Treasure", "Act 1 - Cave 5 Treasure" })
            {
                var levelName = treasure;
                Dungeon(levelName, () => new LevelBuilder(levelName));
            }

            // Burial Grounds crypts, Forgotten Tower and the Tower Cellar down to the Countess
            Dungeon("Act 1 - Crypt 1 A", () => CreateCrypt("Act 1 - Crypt 1 A", false, true, false));
            Dungeon("Act 1 - Crypt 2 A", () => CreateCrypt("Act 1 - Crypt 2 A", false, false, true));
            Dungeon("Act 1 - Tower 2", () => new LevelBuilder("Act 1 - Tower 2"));
            foreach (var cellar in new[] { "Act 1 - Crypt 3 A", "Act 1 - Crypt 3 B", "Act 1 - Crypt 3 C", "Act 1 - Crypt 3 D" })
            {
                var levelName = cellar;
                Dungeon(levelName, () => CreateCrypt(levelName, true, false, false));
            }
            Dungeon("Act 1 - Crypt 3 E", () => new LevelBuilder("Act 1 - Crypt 3 E"));

            // Monastery: Outer Cloister, Barracks, Jail, Inner Cloister, Cathedral, Catacombs
            Dungeon("Act 1 - Courtyard 1", () => new LevelBuilder("Act 1 - Courtyard 1"));
            Dungeon("Act 1 - Barracks", CreateBarracks);
            Dungeon("Act 1 - Jail 1", () => CreateJail("Act 1 - Jail 1", true, false));
            Dungeon("Act 1 - Jail 2", () => CreateJail("Act 1 - Jail 2", false, false));
            Dungeon("Act 1 - Jail 3", () => CreateJail("Act 1 - Jail 3", false, true));
            Dungeon("Act 1 - Courtyard 2", () => new LevelBuilder("Act 1 - Courtyard 2"));
            Dungeon("Act 1 - Cathedral", () => new LevelBuilder("Act 1 - Cathedral"));
            Dungeon("Act 1 - Catacombs 1", () => CreateCatacombs("Act 1 - Catacombs 1", false));
            Dungeon("Act 1 - Catacombs 2", () => CreateCatacombs("Act 1 - Catacombs 2", true));
            Dungeon("Act 1 - Catacombs 3", () => CreateCatacombs("Act 1 - Catacombs 3", false));
            Dungeon("Act 1 - Catacombs 4", () => new LevelBuilder("Act 1 - Catacombs 4"));

            // Tristram is only reachable through the Cairn Stones portal
            tristramSlot = Dungeon("Act 1 - Tristram", () => new LevelBuilder("Act 1 - Tristram"));

            entry = town.FindEntry() + townOffset;

            // Build what is around the arrival point now, the rest follows the camera
            streamer.LoadNear(new Vector2(entry.x, entry.y));
        }

        private LevelStreamer.Slot Dungeon(string levelName, System.Func<LevelBuilder> create)
        {
            return streamer.Add(levelName, Allocate(levelName), create, false);
        }

        // Size of an outdoor map made of 8x8 tile cells
        private static Vector2i WildernessSize(string levelName)
        {
            var info = LevelInfo.Find(levelName);
            return new Vector2i(info.sizeX / 8 * 8, info.sizeY / 8 * 8);
        }

        private Vector2i Allocate(string levelName)
        {
            var size = LevelBuilder.Footprint(LevelInfo.Find(levelName));
            if (shelfX + size.x > dungeonAreaX + dungeonAreaWidth)
            {
                shelfX = dungeonAreaX;
                shelfY += shelfHeight + dungeonGap;
                shelfHeight = 0;
            }
            var position = new Vector2i(shelfX, shelfY);
            shelfX += size.x + dungeonGap;
            shelfHeight = Mathf.Max(shelfHeight, size.y);
            return position;
        }

        // ---------------------------------------------------------------- maze helpers

        private static LevelPreset Preset(string name)
        {
            var preset = LevelPreset.Find(name);
            if (preset == null)
                Debug.LogWarning("Level preset not found: " + name);
            return preset;
        }

        // "<family> W/E/S/N" presets, in the order Maze expects for special rooms
        private static LevelPreset[] DirectionalSet(string family)
        {
            var set = new LevelPreset[4];
            for (int i = 0; i < 4; ++i)
                set[i] = Preset(family + " " + dirSuffixes[i]);
            return set;
        }

        // A preset reduced to a single one of its files
        private static LevelPreset SingleFile(LevelPreset source, int fileIndex)
        {
            var preset = new LevelPreset { name = source.name, popPad = source.popPad };
            preset.ds1Files.Add(source.ds1Files[fileIndex]);
            return preset;
        }

        private static Maze.Palette MazePalette(string family, params LevelPreset[][] special)
        {
            var palette = new Maze.Palette();
            palette.special = special;
            palette.rooms = new LevelPreset[16];
            palette.themedRooms = new LevelPreset[16];
            for (int i = 0; i < roomSuffixes.Length; ++i)
            {
                palette.rooms[i + 1] = Preset("Act 1 - " + family + " " + roomSuffixes[i]);
                palette.themedRooms[i + 1] = Preset("Act 1 - " + family + " Theme " + roomSuffixes[i]);
            }
            return palette;
        }

        private static LevelBuilder BuildMaze(string levelName, Maze.Palette palette)
        {
            var builder = new LevelBuilder(levelName);
            Maze.Generate(builder, palette);
            return builder;
        }

        // ---------------------------------------------------------------- dungeons

        private LevelBuilder CreateDenOfEvil()
        {
            var builder = new LevelBuilder("Act 1 - Cave 1");
            var mazePalette = new Maze.Palette();
            mazePalette.special = new LevelPreset[][] {
                new LevelPreset[] {
                    LevelPreset.Find("Act 1 - Cave Prev W"),
                    LevelPreset.Find("Act 1 - Cave Prev E"),
                    LevelPreset.Find("Act 1 - Cave Prev S"),
                    LevelPreset.Find("Act 1 - Cave Prev N")
                },
                new LevelPreset[] {
                    LevelPreset.Find("Act 1 - Cave Den Of Evil W"),
                    LevelPreset.Find("Act 1 - Cave Den Of Evil E"),
                    LevelPreset.Find("Act 1 - Cave Den Of Evil S"),
                    LevelPreset.Find("Act 1 - Cave Den Of Evil N")
                }
            };
            mazePalette.rooms = new LevelPreset[16];
            for (int i = 0; i < 15; ++i)
                mazePalette.rooms[i + 1] = LevelPreset.sheet[53 + i];
            mazePalette.themedRooms = new LevelPreset[16];
            for (int i = 0; i < 15; ++i)
                mazePalette.themedRooms[i + 1] = LevelPreset.sheet[68 + i];
            Maze.Generate(builder, mazePalette);
            return builder;
        }

        // Cave levels: an entrance room (Prev), optionally a way on (Next), a hole down to the
        // treasure room (Down) and Coldcrow's lair
        private LevelBuilder CreateCave(string levelName, bool coldcrow, bool next, bool down)
        {
            var special = new List<LevelPreset[]> { DirectionalSet("Act 1 - Cave Prev") };
            if (next)
                special.Add(DirectionalSet("Act 1 - Cave Next"));
            if (down)
                special.Add(DirectionalSet("Act 1 - Cave Down"));
            if (coldcrow)
                special.Add(DirectionalSet("Act 1 - Cave Coldcrow"));
            return BuildMaze(levelName, MazePalette("Cave", special.ToArray()));
        }

        private LevelBuilder CreateCrypt(string levelName, bool hasNext, bool bonebreak, bool chest)
        {
            var special = new List<LevelPreset[]> { DirectionalSet("Act 1 - Crypt Prev") };
            if (hasNext)
                special.Add(DirectionalSet("Act 1 - Crypt Next"));
            if (bonebreak)
                special.Add(DirectionalSet("Act 1 - Crypt Bonebreak"));
            if (chest)
                special.Add(DirectionalSet("Act 1 - Crypt Chest"));
            return BuildMaze(levelName, MazePalette("Crypt", special.ToArray()));
        }

        private LevelBuilder CreateBarracks()
        {
            // The way up is the Outer Cloister connection, which only exists facing west, north and east
            var courtConnect = Preset("Act 1 - Barracks Court Connect");
            var prev = new LevelPreset[]
            {
                SingleFile(courtConnect, 0), // W
                SingleFile(courtConnect, 2), // E
                null,                        // S
                SingleFile(courtConnect, 1)  // N
            };
            var palette = MazePalette("Barracks",
                prev,
                DirectionalSet("Act 1 - Barracks Next"),
                DirectionalSet("Act 1 - Barracks Forge"));
            palette.specialMasks = new[] { 0x1 | 0x2 | 0x8, 0xf, 0xf };
            return BuildMaze("Act 1 - Barracks", palette);
        }

        private LevelBuilder CreateJail(string levelName, bool waypoint, bool toCathedral)
        {
            var special = new List<LevelPreset[]> { DirectionalSet("Act 1 - Jail Prev") };
            special.Add(DirectionalSet(toCathedral ? "Act 1 - Jail Cath" : "Act 1 - Jail Next"));
            if (waypoint)
                special.Add(DirectionalSet("Act 1 - Jail Waypoint"));
            return BuildMaze(levelName, MazePalette("Jail", special.ToArray()));
        }

        private LevelBuilder CreateCatacombs(string levelName, bool waypoint)
        {
            // The stairs up come in east-west and north-south versions only
            var prevEW = Preset("Act 1 - Catacombs Prev EW");
            var prevNS = Preset("Act 1 - Catacombs Prev NS");
            var special = new List<LevelPreset[]>
            {
                new[] { prevEW, prevEW, prevNS, prevNS },
                DirectionalSet("Act 1 - Catacombs Next")
            };
            if (waypoint)
                special.Add(DirectionalSet("Act 1 - Catacombs Waypoint"));
            return BuildMaze(levelName, MazePalette("Catacombs", special.ToArray()));
        }

        // ---------------------------------------------------------------- outdoors

        private LevelBuilder CreateBloodMoor()
        {
            var bloodMoor = new LevelBuilder("Act 1 - Wilderness 1", 8, 8);
            var riverN = DS1.Load(@"data\global\tiles\act1\outdoors\UriverN.ds1");
            var uRiver = DS1.Load(@"data\global\tiles\act1\outdoors\Uriver.ds1");
            var lRiver = DS1.Load(@"data\global\tiles\act1\outdoors\Lriver.ds1");
            var bord1 = LevelPreset.Find("Act 1 - Wild Border 1");
            var bord2 = LevelPreset.Find("Act 1 - Wild Border 2");
            var bord3 = LevelPreset.Find("Act 1 - Wild Border 3");
            var bord5 = LevelPreset.Find("Act 1 - Wild Border 5");
            var bord6 = LevelPreset.Find("Act 1 - Wild Border 6");
            var bord9 = LevelPreset.Find("Act 1 - Wild Border 9");
            var cottage = LevelPreset.Find("Act 1 - Cottages 1");
            var denEntrance = LevelPreset.Find("Act 1 - DOE Entrance");

            for (int i = 0; i < bloodMoor.gridHeight; ++i)
                bloodMoor.Place(lRiver, new Vector2i(bloodMoor.gridWidth - 1, i));
            for (int i = 1; i < bloodMoor.gridHeight; ++i)
                bloodMoor.Place(uRiver, new Vector2i(bloodMoor.gridWidth - 2, i));
            bloodMoor.Place(riverN, new Vector2i(bloodMoor.gridWidth - 2, 0));

            for (int i = 1; i < bloodMoor.gridHeight - 1; ++i)
                bloodMoor.Place(bord2, new Vector2i(0, i), 0, 3);
            bloodMoor.Place(bord5, new Vector2i(0, bloodMoor.gridHeight - 1));

            for (int i = 1; i < 3; ++i)
                bloodMoor.Place(bord1, new Vector2i(i, bloodMoor.gridHeight - 1), 0, 3);
            bloodMoor.Place(bord9, new Vector2i(3, bloodMoor.gridHeight - 1));

            // The north border is left open at x = 3 and 4, that is the way on to the Cold Plains
            for (int i = 1; i < bloodMoor.gridWidth - 2; ++i)
            {
                if (IsPassage(i))
                    continue;
                bloodMoor.Place(bord3, new Vector2i(i, 0), 0, 3);
            }

            bloodMoor.Place(bord6, new Vector2i(0, 0));
            for (int i = 1; i < 5; ++i)
                bloodMoor.Place(cottage, new Vector2i(i, 4 + Random.Range(-1, 1)));
            bloodMoor.Place(denEntrance, new Vector2i(5, 7));

            return bloodMoor;
        }

        // Border cells that stay empty so that two touching maps are joined
        private static bool IsPassage(int index)
        {
            return index == 3 || index == 4;
        }

        [System.Flags]
        private enum Edges
        {
            None = 0,
            North = 1,
            South = 2,
            East = 4,
            West = 8
        }

        // Builds an 8x8 cell wilderness map. Edges listed in 'open' have a gap in the border,
        // everything else is closed by the wild border presets.
        private static LevelBuilder CreateWilderness(string levelName, Edges open, string[] fillPresets, float fillChance)
        {
            var map = new LevelBuilder(levelName, 8, 8);
            int w = map.gridWidth;
            int h = map.gridHeight;
            var north = Preset("Act 1 - Wild Border 3");
            var south = Preset("Act 1 - Wild Border 1");
            var west = Preset("Act 1 - Wild Border 2");
            var east = Preset("Act 1 - Wild Border 4");
            var corners = new[]
            {
                Preset("Act 1 - Wild Border 6"), // north west
                Preset("Act 1 - Wild Border 7"), // north east
                Preset("Act 1 - Wild Border 5"), // south west
                Preset("Act 1 - Wild Border 8")  // south east
            };

            map.Place(corners[0], new Vector2i(0, 0));
            map.Place(corners[1], new Vector2i(w - 1, 0));
            map.Place(corners[2], new Vector2i(0, h - 1));
            map.Place(corners[3], new Vector2i(w - 1, h - 1));

            for (int i = 1; i < w - 1; ++i)
            {
                bool passage = IsPassage(i);
                if (!(passage && (open & Edges.North) != 0))
                    map.Place(north, new Vector2i(i, 0), 0, 3);
                if (!(passage && (open & Edges.South) != 0))
                    map.Place(south, new Vector2i(i, h - 1), 0, 3);
            }
            for (int i = 1; i < h - 1; ++i)
            {
                bool passage = IsPassage(i);
                if (!(passage && (open & Edges.West) != 0))
                    map.Place(west, new Vector2i(0, i), 0, 3);
                if (!(passage && (open & Edges.East) != 0))
                    map.Place(east, new Vector2i(w - 1, i), 0, 3);
            }

            var fills = new List<LevelPreset>();
            foreach (var name in fillPresets)
            {
                var preset = Preset(name);
                if (preset != null)
                    fills.Add(preset);
            }
            if (fills.Count > 0)
            {
                for (int y = 1; y < h - 1; ++y)
                {
                    for (int x = 1; x < w - 1; ++x)
                    {
                        if (Random.value < fillChance)
                            map.Place(fills[Random.Range(0, fills.Count)], new Vector2i(x, y));
                    }
                }
            }
            return map;
        }

        // Quest and entrance presets go in last so that random fill cannot cover them
        private static void PlaceFixed(LevelBuilder map, string presetName, int x, int y)
        {
            var preset = Preset(presetName);
            if (preset != null)
                map.Place(preset, new Vector2i(x, y));
        }

        private static readonly string[] meadowFill =
        {
            "Act 1 - Stone Fill 1", "Act 1 - Stone Fill 2", "Act 1 - Fallen Camp 1", "Act 1 - Pond"
        };

        private LevelBuilder CreateColdPlains()
        {
            var map = CreateWilderness("Act 1 - Wilderness 2", Edges.North | Edges.South | Edges.West, meadowFill, 0.25f);
            PlaceFixed(map, "Act 1 - Cave Entrance", 5, 2);   // Cave 2, Coldcrow's lair
            return map;
        }

        private LevelBuilder CreateStonyField()
        {
            var map = CreateWilderness("Act 1 - Wilderness 3", Edges.South | Edges.West, meadowFill, 0.2f);
            PlaceFixed(map, "Act 1 - Cairn Stones", 4, 3);    // the five stones and the portal stone
            PlaceFixed(map, "Act 1 - Cave Entrance", 2, 5);   // Cave 3
            return map;
        }

        private LevelBuilder CreateDarkWood()
        {
            var fill = new[] { "Act 1 - Stone Fill 1", "Act 1 - Fallen Camp 2", "Act 1 - Pond" };
            var map = CreateWilderness("Act 1 - Wilderness 4", Edges.East | Edges.West, fill, 0.25f);
            PlaceFixed(map, "Act 1 - Inifus", 3, 5);          // Tree of Inifuss
            PlaceFixed(map, "Act 1 - Cave Entrance", 5, 2);   // Cave 3 from the other side
            return map;
        }

        private LevelBuilder CreateBlackMarsh()
        {
            var fill = new[] { "Act 1 - Swamp Fill 1", "Act 1 - Fallen Camp 2", "Act 1 - Pond" };
            var map = CreateWilderness("Act 1 - Wilderness 5", Edges.North | Edges.East, fill, 0.3f);
            PlaceFixed(map, "Act 1 - Tower 1", 4, 3);         // Forgotten Tower
            PlaceFixed(map, "Act 1 - Cave Entrance", 2, 5);   // Cave 4
            return map;
        }

        private LevelBuilder CreateTamoeHighland()
        {
            var fill = new[] { "Act 1 - Stone Fill 2", "Act 1 - Fallen Camp 2", "Act 1 - Swamp Fill 1" };
            var map = CreateWilderness("Act 1 - Wilderness 6", Edges.North | Edges.South, fill, 0.25f);
            PlaceFixed(map, "Act 1 - Cave Entrance", 5, 5);   // Cave 5
            return map;
        }

        private LevelBuilder CreateBurialGrounds()
        {
            // One hand-made 24x32 layout that covers the part of the level players walk in
            var builder = new LevelBuilder("Act 1 - Graveyard", 24, 32);
            PlaceFixed(builder, "Act 1 - Graveyard", 0, 0);
            return builder;
        }
    }
}
