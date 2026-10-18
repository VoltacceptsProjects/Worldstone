using Diablerie.Engine;
using Diablerie.Engine.IO.D2Formats;
using UnityEngine;

namespace Diablerie.Game.UI
{
    // A DC6 sheet used for panel art. Shared by panels that are built from MPQ assets in code.
    public class PanelSheet
    {
        public Sprite[] sprites;
        public DC6.Frame[] frames;

        public static PanelSheet Load(string path, PaletteType palette = PaletteType.Act1)
        {
            var dc6 = Spritesheet.Load(path, palette) as DC6;
            if (dc6 == null)
                return null;

            var sprites = dc6.GetSprites(0);
            if (sprites == null || sprites.Length == 0)
                return null;
            // Pixel art drawn at fractional screen positions blends tile edges with bilinear filtering
            foreach (var sprite in sprites)
            {
                if (sprite != null && sprite.texture != null)
                    sprite.texture.filterMode = FilterMode.Point;
            }
            return new PanelSheet { sprites = sprites, frames = dc6.directions[0].frames };
        }

        // Lays the frames out as tiles, left to right and top to bottom, starting a new row when the next
        // frame would not fit in maxWidth. That is how the game assembles its panels from DC6 sheets.
        // Blank edge rows are overlapped so that no seam shows between rows of tiles.
        public Vector2 TileLayout(float maxWidth, int[] topBlank, int[] bottomBlank, out Vector2[] positions)
        {
            positions = new Vector2[frames.Length];
            float x = 0, y = 0, rowHeight = 0, width = 0;
            int rowBottomBlank = 0;
            for (int i = 0; i < frames.Length; ++i)
            {
                var frame = frames[i];
                if (x > 0 && x + frame.width > maxWidth)
                {
                    x = 0;
                    y += rowHeight - rowBottomBlank - topBlank[i];
                    rowHeight = 0;
                }

                if (x == 0)
                    rowBottomBlank = bottomBlank[i];

                positions[i] = new Vector2(x, y);
                x += frame.width;
                width = Mathf.Max(width, x);
                rowHeight = Mathf.Max(rowHeight, frame.height);
            }
            return new Vector2(width, y + rowHeight);
        }

        // Counts fully transparent pixel rows at the top or bottom edge of a sprite (at most 4)
        public static int CountBlankRows(Sprite sprite, bool fromTop)
        {
            var texture = sprite.texture;
            if (texture == null || !texture.isReadable)
                return 0;

            var rect = sprite.textureRect;
            var pixels = texture.GetPixels32();
            int x0 = (int) rect.x, y0 = (int) rect.y, width = (int) rect.width, height = (int) rect.height;
            int count = 0;
            for (int i = 0; i < height; ++i)
            {
                int row = fromTop ? y0 + height - 1 - i : y0 + i;
                for (int x = 0; x < width; ++x)
                {
                    if (pixels[row * texture.width + x0 + x].a != 0)
                        return Mathf.Min(count, 4);
                }
                ++count;
            }
            return 0;
        }

        public string Describe()
        {
            var text = frames.Length + " frames";
            for (int i = 0; i < frames.Length; ++i)
            {
                var f = frames[i];
                text += "\n  [" + i + "] size " + f.width + "x" + f.height + " offset " + f.offsetX + "," + f.offsetY;
            }
            return text;
        }
    }
}