using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Utility;
using UnityEngine;

namespace Diablerie.Engine.World
{
    public class Maze
    {
        public struct Palette
        {
            public LevelPreset[] rooms;
            public LevelPreset[] themedRooms;
            public LevelPreset[][] special;
            // Optional: directions (bit 0 W, 1 E, 2 S, 3 N) each special room may open towards, 0xf when null
            public int[] specialMasks;
        }

        struct Room
        {
            public bool fill;
            public bool special;
            public int specialIndex;
            public int dirMask;
        }
    
        static Room[,] grid = new Room[8, 8];
        static Vector2i[] dirs = new Vector2i[] {
            new Vector2i(-1, 0),
            new Vector2i(1, 0),
            new Vector2i(0, 1),
            new Vector2i(0, -1),
        };

        public static void Generate(LevelBuilder builder, Palette palette)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Maze.Generate");
            // A layout can leave no valid place for a special room, retry with a new one instead of spinning forever
            for (int attempt = 0; attempt < 16; ++attempt)
            {
                Clear();
                GenerateRooms(builder.info.maze.rooms[0]);
                if (GenerateSpecialRooms(palette))
                    break;
                if (attempt == 15)
                    Debug.LogWarning("Maze: failed to place all special rooms for " + builder.info.levelName);
            }
            Build(builder, palette);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private static void Clear()
        {
            for (int x = 0; x < grid.GetLength(0); ++x)
            {
                for (int y = 0; y < grid.GetLength(1); ++y)
                {
                    grid[x, y] = new Room()
                    {
                        fill = false,
                        special = false,
                        specialIndex = -1,
                        dirMask = 0
                    };
                }
            }
        }

        private static void GenerateRooms(int roomCount)
        {
            Vector2i c = new Vector2i(3, 3);
            for (int i = 0; i < roomCount;)
            {
                int dirIndex = Random.Range(0, 4);
                Vector2i newPos = c + dirs[dirIndex];
                while (!Empty(newPos.x, newPos.y))
                {
                    ++dirIndex;
                    newPos = c + dirs[dirIndex % 4];
                    if (dirIndex > 8)
                    {
                        Debug.LogWarning("Failed to find an empty place for a room");
                        return;
                    }
                }

                c = newPos;
                grid[c.x, c.y].fill = true;
                grid[c.x, c.y].dirMask = 0xf;
                ++i;
            }
        }

        private static bool GenerateSpecialRooms(Palette palette)
        {
            bool allPlaced = true;
            for (int specialIndex = 0; specialIndex < palette.special.Length; ++specialIndex)
            {
                Vector2i pos = new Vector2i(0, 0);
                int dirMask = 0;
                bool found = false;

                // Random tries first, then a full scan, so that this always terminates
                for (int attempt = 0; attempt < 64 && !found; ++attempt)
                {
                    pos = new Vector2i(
                        Random.Range(0, grid.GetLength(0)),
                        Random.Range(0, grid.GetLength(1)));
                    found = TrySpecialMask(palette, specialIndex, pos, out dirMask);
                }

                for (int x = 0; x < grid.GetLength(0) && !found; ++x)
                {
                    for (int y = 0; y < grid.GetLength(1) && !found; ++y)
                    {
                        pos = new Vector2i(x, y);
                        found = TrySpecialMask(palette, specialIndex, pos, out dirMask);
                    }
                }

                if (!found)
                {
                    allPlaced = false;
                    continue;
                }

                int dirIndex = Random.Range(0, 4);
                while ((dirMask & (1 << dirIndex)) == 0)
                    dirIndex = (dirIndex + 1) % 4;
                dirMask = 1 << dirIndex;
                grid[pos.x, pos.y] = new Room()
                {
                    fill = true,
                    special = true,
                    specialIndex = specialIndex,
                    dirMask = dirMask
                };
            }
            return allPlaced;
        }

        private static bool TrySpecialMask(Palette palette, int specialIndex, Vector2i pos, out int dirMask)
        {
            dirMask = 0;
            if (grid[pos.x, pos.y].fill)
                return false;
            dirMask = GenerateDirMask(pos.x, pos.y);
            if (palette.specialMasks != null && specialIndex < palette.specialMasks.Length)
                dirMask &= palette.specialMasks[specialIndex];
            return dirMask != 0;
        }

        private static void Build(LevelBuilder builder, Palette palette)
        {
            for (int x = 0; x < grid.GetLength(0); ++x)
            {
                for (int y = 0; y < grid.GetLength(1); ++y)
                {
                    PlaceRoom(builder, palette, x, y);
                }
            }
        }

        private static void PlaceRoom(LevelBuilder builder, Palette palette, int x, int y)
        {
            Room room = grid[x, y];
            if (!room.fill)
                return;

            if (room.special)
            {
                int index = SpecialIndex(room.dirMask);
                var specialPreset = index >= 0 ? palette.special[room.specialIndex][index] : null;
                if (specialPreset != null)
                    builder.Place(specialPreset, new Vector2i(x, y));
            }
            else
            {
                int index = GenerateDirMask(x, y);
                LevelPreset[] set = Random.Range(0, 2) == 0 ? palette.rooms : palette.themedRooms;
                if (set[index] != null)
                    builder.Place(set[index], new Vector2i(x, y));
            }
        }

        private static int GenerateDirMask(int x, int y)
        {
            int index = 0;
            index |= HasConnection(1, x - 1, y);
            index |= HasConnection(0, x + 1, y) << 1;
            index |= HasConnection(3, x, y + 1) << 2;
            index |= HasConnection(2, x, y - 1) << 3;
            return index;
        }

        private static int HasConnection(int dirIndex, int x, int y)
        {
            if (x < 0 || x >= grid.GetLength(0) || y < 0 || y >= grid.GetLength(1))
                return 0;

            if ((grid[x, y].dirMask & (1 << dirIndex)) == 0)
                return 0;

            return 1;
        }

        private static bool Empty(int x, int y)
        {
            if (x < 0 || x >= grid.GetLength(0) || y < 0 || y >= grid.GetLength(1))
                return false;

            return !grid[x, y].fill;
        }

        private static int SpecialIndex(int dirMask)
        {
            switch (dirMask)
            {
                case 1: return 0;
                case 2: return 1;
                case 4: return 2;
                case 8: return 3;
                default: return -1;
            }
        }
    }
}