using System;
using System.Text;
using Diablerie.Engine.IO.D2Formats;
using UnityEngine;

namespace Diablerie.Engine
{
    // Geometry of the stash panel. The numbers come from the game's own data\global\excel\inventory.txt
    // (rows "Big Bank Page 1" for the expansion and "Bank Page 1" for classic), so the grid always matches
    // the stash artwork that ships in the MPQs. Positions are relative to the top left corner of the panel.
    public class StashLayout
    {
        private const string InventoryPath = @"data\global\excel\inventory.txt";
        private static readonly string[] RowNames = { "Big Bank Page 1", "Bank Page 1" };

        public int columns = 6;
        public int rows = 8;
        public float cellWidth = 29;
        public float cellHeight = 29;
        public float gridLeft = 16;
        public float gridTop = 83;
        public string source = "built-in fallback";

        private static StashLayout _cached;

        public static StashLayout Get()
        {
            if (_cached == null)
                _cached = Read(RowNames, new StashLayout(), 60, 120);
            return _cached;
        }

        private static StashLayout _cachedVendor;

        // The shop panel an NPC shows: a 10x10 grid of 29x29 cells on the buysell artwork.
        // inventory.txt is NOT used here. Its "Trade Page 1" numbers don't describe the grid that is drawn
        // in the artwork, which squashed the item positions and left the items piled up on top of each other.
        // TUNE: VendorGridLeft / VendorGridTop are the pixels from the panel's top left corner to the first cell.
        private const float VendorGridLeft = 15;
        private const float VendorGridTop = 63;

        public static StashLayout GetVendor()
        {
            if (_cachedVendor == null)
            {
                _cachedVendor = new StashLayout
                {
                    columns = 10,
                    rows = 10,
                    cellWidth = 29,
                    cellHeight = 29,
                    gridLeft = VendorGridLeft,
                    gridTop = VendorGridTop,
                    source = "fixed vendor geometry",
                };
                Debug.Log("StashLayout (vendor): " + _cachedVendor);
            }
            return _cachedVendor;
        }

        private static StashLayout Read(string[] rowNames, StashLayout layout, float minTop, float maxTop)
        {
            try
            {
                string text = Encoding.ASCII.GetString(Mpq.ReadAllBytes(InventoryPath));
                var lines = text.Split('\n');
                if (lines.Length < 2)
                    return layout;

                var header = lines[0].TrimEnd('\r').Split('\t');
                int iClass = Column(header, "class", 0);
                int iInvLeft = Column(header, "invLeft", 1);
                int iInvTop = Column(header, "invTop", 3);
                int iInvBottom = Column(header, "invBottom", 4);
                int iGridX = Column(header, "gridX", 5);
                int iGridY = Column(header, "gridY", 6);
                int iGridLeft = Column(header, "gridLeft", 7);
                int iGridRight = Column(header, "gridRight", 8);
                int iGridTop = Column(header, "gridTop", 9);
                int iGridBottom = Column(header, "gridBottom", 10);

                foreach (var rowName in rowNames)
                {
                    for (int i = 1; i < lines.Length; ++i)
                    {
                        var cells = lines[i].TrimEnd('\r').Split('\t');
                        if (cells.Length <= iGridBottom || !cells[iClass].Trim().Equals(rowName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        int gridX = Int(cells, iGridX), gridY = Int(cells, iGridY);
                        int left = Int(cells, iGridLeft), right = Int(cells, iGridRight);
                        int top = Int(cells, iGridTop), bottom = Int(cells, iGridBottom);
                        if (gridX <= 0 || gridY <= 0 || right <= left || bottom <= top)
                            continue;

                        layout.columns = gridX;
                        layout.rows = gridY;
                        layout.cellWidth = (right - left) / (float) gridX;
                        layout.cellHeight = (bottom - top) / (float) gridY;
                        layout.gridLeft = left - Int(cells, iInvLeft);
                        layout.gridTop = ResolveGridTop(top, Int(cells, iInvTop), Int(cells, iInvBottom), layout.gridTop, minTop, maxTop);
                        layout.source = "inventory.txt '" + rowName + "'";
                        Debug.Log("StashLayout: " + layout);
                        return layout;
                    }
                }
                Debug.LogWarning("StashLayout: no row " + string.Join(" / ", rowNames) + " found in inventory.txt, using the built-in fallback");
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashLayout: could not read inventory.txt (" + e.Message + "), using the built-in fallback");
            }
            return layout;
        }

        // inventory.txt is not consistent about which of invTop / invBottom is the upper edge of a panel,
        // so try both and keep the one that puts the grid inside of the panel artwork. The grid starts
        // roughly 60-120 pixels below the top edge of the stash art (below the gold bar and the title).
        private static float ResolveGridTop(int gridTop, int invTop, int invBottom, float fallback, float minTop, float maxTop)
        {
            const float PanelHeight = 432;
            float[] candidates =
            {
                gridTop - invTop,
                gridTop - invBottom,
                gridTop - (invTop - PanelHeight),
                gridTop - (invBottom - PanelHeight),
            };
            foreach (var value in candidates)
            {
                if (value >= minTop && value <= maxTop)
                    return value;
            }
            Debug.LogWarning("StashLayout: gridTop " + gridTop + " (invTop " + invTop + ", invBottom " + invBottom +
                             ") does not fit the panel, using the built-in " + fallback);
            return fallback;
        }

        private static int Column(string[] header, string name, int fallback)
        {
            for (int i = 0; i < header.Length; ++i)
            {
                if (header[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return fallback;
        }

        private static int Int(string[] cells, int index)
        {
            return index < cells.Length && int.TryParse(cells[index].Trim(), out int value) ? value : 0;
        }

        public override string ToString()
        {
            return columns + "x" + rows + " cells of " + cellWidth + "x" + cellHeight + " at " + gridLeft + "," + gridTop + " (" + source + ")";
        }
    }
}