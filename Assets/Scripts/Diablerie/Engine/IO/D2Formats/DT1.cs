using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diablerie.Engine.LibraryExtensions;
using Diablerie.Engine.Utility;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Diablerie.Engine.IO.D2Formats
{
    public class DT1
    {
        public string filename;
        public Tile[] tiles;
        public List<Texture2D> textures = new List<Texture2D>();

        [Flags]
        public enum BlockFlags : byte
        {
            Walk = 1, // both player and mercenary
            Light = 2, // and line of sight
            Jump = 4, // and teleport probably
            PlayerWalk = 8,
            LightOnly = 32, // but not line of sight
        }

        public class Sampler
        {
            Dictionary<int, List<Tile>> tiles = new Dictionary<int, List<Tile>>();
            Dictionary<int, int> rarities = new Dictionary<int, int>();
            int dt1Count = 0;

            public void Clear()
            {
                tiles.Clear();
                rarities.Clear();
                dt1Count = 0;
            }

            public void Add(Tile[] newTiles)
            {
                foreach (var tile in newTiles)
                {
                    List<Tile> list = tiles.GetValueOrDefault(tile.index, null);
                    if (list == null)
                    {
                        list = new List<Tile>();
                        tiles[tile.index] = list;
                    }

                    if (dt1Count == 0)
                        list.Insert(0, tile);
                    else
                        list.Add(tile);

                    if (!rarities.ContainsKey(tile.index))
                        rarities[tile.index] = tile.rarity;
                    else
                        rarities[tile.index] += tile.rarity;
                }
                dt1Count += 1;
            }

            public bool Sample(int index, out Tile tile)
            {
                List<Tile> tileList;
                if (!tiles.TryGetValue(index, out tileList))
                {
                    tile = new Tile();
                    return false;
                }

                int raritySum = rarities[index];
                if (raritySum == 0)
                {
                    tile = tileList[0];
                }
                else
                {
                    int randomValue = Random.Range(0, raritySum);
                    for(int i = 0; i < tileList.Count; ++i)
                    {
                        if (randomValue < tileList[i].rarity)
                        {
                            tile = tileList[i];
                            return true;
                        }
                        randomValue -= tileList[i].rarity;
                    }
                
                    Debug.LogError("Failed to sample tile");
                    tile = tileList[0];
                }

                return true;
            }
        }

        public class Tile
        {
            public int direction;
            public short roofHeight;
            public byte soundIndex;
            public byte animated;
            public int height;
            public int width;
            public int orientation;
            public int mainIndex;
            public int subIndex;
            public int rarity;
            public BlockFlags[] flags;
            public bool blocksMovement;
            public int blockHeaderPointer;
            public int blockDatasLength;
            public int blockCount;

            public Material material;
            public Texture2D texture;
            public int textureX;
            public int textureY;
            public int index;

            public void Read(BinaryReader reader)
            {
                direction = reader.ReadInt32();
                roofHeight = reader.ReadInt16();
                soundIndex = reader.ReadByte();
                animated = reader.ReadByte();
                height = reader.ReadInt32();
                width = reader.ReadInt32();
                reader.ReadInt32();
                orientation = reader.ReadInt32();
                mainIndex = reader.ReadInt32();
                subIndex = reader.ReadInt32();
                rarity = reader.ReadInt32();
                reader.ReadInt32();
                flags = new BlockFlags[25];
                const BlockFlags blockMask = BlockFlags.Walk | BlockFlags.PlayerWalk;
                blocksMovement = false;
                for (int i = 0; i < 25; ++i)
                {
                    var flag = (BlockFlags)reader.ReadByte();
                    flags[i] = flag;
                    if ((flag & blockMask) != 0)
                        blocksMovement = true;
                }
                reader.BaseStream.Seek(7, SeekOrigin.Current);
                blockHeaderPointer = reader.ReadInt32();
                blockDatasLength = reader.ReadInt32();
                blockCount = reader.ReadInt32();
                reader.BaseStream.Seek(12, SeekOrigin.Current);
                index = Index(mainIndex, subIndex, orientation);
            }

            public static int Index(int mainIndex, int subIndex, int orientation)
            {
                return (((mainIndex << 6) + subIndex) << 5) + orientation;
            }
        }

        static Dictionary<string, DT1> cache = new Dictionary<string, DT1>();
        static Dictionary<string, Job> inflight = new Dictionary<string, Job>();

        public static void ResetCache()
        {
            cache.Clear();
            inflight.Clear();
        }

        public static bool IsCached(string filename)
        {
            return cache.ContainsKey(filename.ToLower());
        }

        public class Job
        {
            public readonly DT1 dt1 = new DT1();

            readonly string filename;
            readonly string lowerFilename;
            readonly Color32[] palette;
            readonly bool mpq;

            bool begun;
            bool done;
            byte[] bytes;
            MemoryStream stream;
            BinaryReader reader;
            int tileCount;
            int next;
            int textureSize;
            TexturePacker packer;
            Texture2D texture;
            Material material;
            byte[] pixels;
            Texture paletteTexture;

            public bool Done => done;

            internal Job(string filename, Color32[] palette, bool mpq)
            {
                this.filename = filename;
                this.lowerFilename = filename.ToLower();
                this.palette = palette;
                this.mpq = mpq;
                dt1.filename = filename;
                inflight[lowerFilename] = this;
            }

            public bool Step(double budgetMs)
            {
                if (done)
                    return true;

                UnityEngine.Profiling.Profiler.BeginSample("DT1.Job.Step");
                try
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    if (!begun)
                    {
                        begun = true;
                        bool ok;
                        try
                        {
                            ok = Begin();
                        }
                        catch
                        {
                            Abort();
                            throw;
                        }

                        if (!ok)
                        {
                            Abort();
                            return true;
                        }
                    }

                    while (next < tileCount)
                    {
                        ProcessTile(dt1.tiles[next]);
                        ++next;
                        if ((next & 3) == 0 && clock.Elapsed.TotalMilliseconds >= budgetMs)
                            return false;
                    }

                    Finish();
                    return true;
                }
                finally
                {
                    UnityEngine.Profiling.Profiler.EndSample();
                }
            }

            bool Begin()
            {
                bytes = mpq ? Mpq.ReadAllBytes(filename) : File.ReadAllBytes(filename);
                stream = new MemoryStream(bytes);
                reader = new BinaryReader(stream);

                int version1 = reader.ReadInt32();
                int version2 = reader.ReadInt32();
                if (version1 != 7 || version2 != 6)
                {
                    Debug.Log(string.Format("Can't read dt1 file, bad version ({0}.{1})", version1, version2));
                    dt1.tiles = new Tile[0];
                    return false;
                }

                stream.Seek(260, SeekOrigin.Current);
                tileCount = reader.ReadInt32();
                reader.ReadInt32();
                dt1.tiles = new Tile[tileCount];
                for (int i = 0; i < tileCount; ++i)
                {
                    dt1.tiles[i] = new Tile();
                    dt1.tiles[i].Read(reader);
                }

                paletteTexture = Palette.GetSharedTexture(palette);
                textureSize = CalcTextureSize(dt1.tiles);
                packer = new TexturePacker(textureSize, textureSize);
                return true;
            }

            void ProcessTile(Tile tile)
            {
                if (tile.width == 0 || tile.height == 0)
                    return;

                var pack = packer.put(tile.width, -tile.height);
                if (pack.newTexture)
                {
                    UploadTexture();
                    texture = new Texture2D(textureSize, textureSize, TextureFormat.Alpha8, false);
                    texture.filterMode = FilterMode.Point;
                    dt1.textures.Add(texture);
                    material = new Material(Materials.Indexed);
                    material.mainTexture = texture;
                    material.SetTexture("_Palette", paletteTexture);
                    pixels = new byte[textureSize * textureSize];
                }

                tile.textureX = pack.x;
                tile.textureY = pack.y;
                tile.texture = texture;
                tile.material = material;

                if ((tile.orientation == 0 || tile.orientation == 15) && tile.height != 0)
                {
                    tile.height = -79;
                }
                else if (tile.orientation > 0 && tile.orientation < 15)
                {
                    tile.textureY += (-tile.height);
                }
                else if (tile.orientation > 15)
                {
                    tile.textureY += Math.Min(96, -tile.height);
                }

                stream.Seek(tile.blockHeaderPointer, SeekOrigin.Begin);
                for (int block = 0; block < tile.blockCount; ++block)
                {
                    int x = reader.ReadInt16();
                    int y = reader.ReadInt16();
                    reader.ReadInt16();
                    reader.ReadByte();
                    reader.ReadByte();
                    short format = reader.ReadInt16();
                    int length = reader.ReadInt32();
                    reader.ReadInt16();
                    int fileOffset = reader.ReadInt32();
                    int blockDataPosition = tile.blockHeaderPointer + fileOffset;

                    if (format == 1)
                    {
                        drawBlockIsometric(pixels, textureSize, tile.textureX + x, tile.textureY + y, bytes, blockDataPosition, length);
                    }
                    else
                    {
                        drawBlockNormal(pixels, textureSize, tile.textureX + x, tile.textureY + y, bytes, blockDataPosition, length);
                    }
                }
            }

            void UploadTexture()
            {
                if (texture == null || pixels == null)
                    return;
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, true);
            }

            void Finish()
            {
                UploadTexture();
                cache[lowerFilename] = dt1;
                inflight.Remove(lowerFilename);
                Release();
                done = true;
            }

            void Abort()
            {
                inflight.Remove(lowerFilename);
                Release();
                done = true;
            }

            void Release()
            {
                if (reader != null)
                    reader.Close();
                if (stream != null)
                    stream.Dispose();
                reader = null;
                stream = null;
                bytes = null;
                pixels = null;
                texture = null;
                material = null;
            }
        }

        public static Job BeginLoad(string filename, Color32[] palette, bool mpq = true)
        {
            string lowerFilename = filename.ToLower();
            if (cache.ContainsKey(lowerFilename))
                return null;
            if (inflight.TryGetValue(lowerFilename, out var running))
                return running;
            return new Job(filename, palette, mpq);
        }

        public static DT1 Load(string filename, Color32[] palette, bool mpq = true)
        {
            string lowerFilename = filename.ToLower();
            if (cache.TryGetValue(lowerFilename, out var cached))
                return cached;

            var job = BeginLoad(filename, palette, mpq);
            job.Step(double.MaxValue);
            return job.dt1;
        }

        static void drawBlockNormal(byte[] texturePixels, int textureSize, int x0, int y0, byte[] data, int ptr, int length)
        {
            if(y0 < 0)
            {
                Debug.LogError("DT1 drawBlockNormal y0 < 0");
                return;
            }

            int dst = texturePixels.Length - y0 * textureSize - textureSize + x0;
            int x = 0;
            int y = 0;

            while (length > 0)
            {
                byte b1 = data[ptr];
                byte b2 = data[ptr + 1];
                ptr += 2;
                length -= 2;
                if (b1 != 0 || b2 != 0)
                {
                    x += b1;
                    length -= b2;
                    while (b2 != 0)
                    {
                        texturePixels[dst + x] = data[ptr];
                        ptr++;
                        x++;
                        b2--;
                    }
                }
                else
                {
                    x = 0;
                    y++;
                    dst -= textureSize;
                }
            }
        }

        static int[] xjump = { 14, 12, 10, 8, 6, 4, 2, 0, 2, 4, 6, 8, 10, 12, 14 };
        static int[] nbpix = { 4, 8, 12, 16, 20, 24, 28, 32, 28, 24, 20, 16, 12, 8, 4 };

        static void drawBlockIsometric(byte[] texturePixels, int textureSize, int x0, int y0, byte[] data, int ptr, int length)
        {
            int dst = texturePixels.Length - y0 * textureSize - textureSize + x0;
            int x, y = 0, n;

            // 3d-isometric subtile is 256 bytes, no more, no less 
            Debug.Assert(length == 256);
            if (length != 256)
                return;

            while (length > 0)
            {
                x = xjump[y];
                n = nbpix[y];
                length -= n;
                while (n != 0)
                {
                    texturePixels[dst + x] = data[ptr];
                    ptr++;
                    x++;
                    n--;
                }
                y++;
                dst -= textureSize;
            }
        }

        static int CalcTextureSize(IList<Tile> tiles, int minSize = 128, int maxSize = 2048, int padding = 0)
        {
            int size = minSize;
            while(size < maxSize)
            {
                var packer = new TexturePacker(size, size, padding);
                int textureCount = 0;
                foreach(var tile in tiles)
                {
                    var pack = packer.put(tile.width, -tile.height);
                    if (pack.newTexture)
                        ++textureCount;
                    if (textureCount > 1)
                        break;
                }

                if (textureCount == 1)
                    break;

                size *= 2;
            }
            return size;
        }
    }
}
