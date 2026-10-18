using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.IO.D2Formats;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Diablerie.Engine
{
    public static class VendorStock
    {
        public enum BuyResult
        {
            Bought,
            NothingThere,
            HandsNotEmpty,
            NotEnoughGold,
        }

        public const int StockColumns = 10;
        public const int StockRows = 10;

        // TUNE: what the vendor pays, as a fraction of the buying price (1 / SellDivisor)
        private const int SellDivisor = 4;
        private const int PriceUnit = 1024;

        private class Row
        {
            public string code;
            public int min, max, magicMin, magicMax, magicLvl;
        }

        private static Dictionary<string, List<Row>> _rows;
        private static readonly Dictionary<string, Inventory> Stocks = new Dictionary<string, Inventory>();

        // Vendors are the NPCs that have a column set in armor.txt, weapons.txt and misc.txt
        public static bool IsVendor(MonStat monStat)
        {
            if (monStat == null || monStat.id == null)
                return false;
            Load();
            return _rows.ContainsKey(monStat.id.ToLowerInvariant());
        }

        public static Inventory GetStock(string vendorId, Player player)
        {
            Load();
            string key = vendorId.ToLowerInvariant();
            if (Stocks.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var go = new GameObject("Vendor Stock " + key);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var inventory = Inventory.Create(go, StockColumns, StockRows);
            Fill(inventory, key, player);
            Stocks[key] = inventory;
            return inventory;
        }

        public static void Reset()
        {
            foreach (var stock in Stocks.Values)
            {
                if (stock != null)
                    UnityEngine.Object.Destroy(stock.gameObject);
            }
            Stocks.Clear();
        }

        // Prices
        public static int BuyPrice(Item item)
        {
            long price = item.info.cost;
            if (item.affixes.Count > 0)
            {
                long multiply = 0, add = 0;
                foreach (var affix in item.affixes)
                {
                    multiply += affix.multiply;
                    add += affix.add;
                }
                price = price * (PriceUnit + multiply) / PriceUnit + add;
            }
            return (int)Math.Min(Math.Max(price, 1L), (long)int.MaxValue);
        }

        public static int SellPrice(Item item)
        {
            return Mathf.Max(1, BuyPrice(item) / SellDivisor);
        }

        // Buying and selling
        public static BuyResult Buy(Player player, Inventory stock, int x, int y)
        {
            var item = stock.ItemAt(x, y);
            if (item == null)
                return BuyResult.NothingThere;
            if (player.HandsItem != null)
                return BuyResult.HandsNotEmpty;

            int price = BuyPrice(item);
            if (player.inventory.gold < price)
                return BuyResult.NotEnoughGold;

            stock.Take(x, y);
            player.inventory.gold -= price;
            player.HandsItem = item;
            return BuyResult.Bought;
        }

        public static bool Sell(Player player, Inventory stock)
        {
            var item = player.HandsItem;
            if (item == null)
                return false;

            int price = SellPrice(item);
            player.HandsItem = null;
            player.inventory.gold += price;
            stock.PutFromTop(item);
            return true;
        }

        // Stock
        private static void Fill(Inventory inventory, string key, Player player)
        {
            int level = player != null && player.charStat != null ? player.charStat.level : 1;
            int placed = 0;
            if (_rows.TryGetValue(key, out var rows))
            {
                foreach (var row in rows)
                {
                    int normal = Random.Range(row.min, Mathf.Max(row.min, row.max) + 1);
                    int magic = Random.Range(row.magicMin, Mathf.Max(row.magicMin, row.magicMax) + 1);
                    for (int i = 0; i < normal; ++i)
                        placed += Spawn(inventory, row.code, level, magic: false) ? 1 : 0;
                    for (int i = 0; i < magic; ++i)
                        placed += Spawn(inventory, row.code, level + row.magicLvl, magic: true) ? 1 : 0;
                }
            }
            Debug.Log("VendorStock: " + key + " stocked " + placed + " items");
        }

        private static bool Spawn(Inventory inventory, string code, int itemLevel, bool magic)
        {
            var item = ItemDrop.GenerateForVendor(code, itemLevel, magic);
            return item != null && inventory.PutFromTop(item);
        }

        // Data
        private static void Load()
        {
            if (_rows != null)
                return;

            _rows = new Dictionary<string, List<Row>>();
            foreach (var sheet in new[] { "weapons", "armor", "misc" })
                ReadSheet(sheet);
        }

        private static string ReadText(string sheet)
        {
            try
            {
                return Encoding.ASCII.GetString(Mpq.ReadAllBytes(@"data\global\excel\" + sheet + ".txt"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("VendorStock: could not read " + sheet + ".txt from the archives (" + e.Message + ")");
            }

            string path = Path.Combine(Application.streamingAssetsPath, "data/global/excel/" + sheet + ".txt");
            return File.Exists(path) ? Encoding.ASCII.GetString(File.ReadAllBytes(path)) : null;
        }

        private static int Column(string[] header, string name)
        {
            for (int i = 0; i < header.Length; ++i)
            {
                if (header[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static int Number(string[] cells, int column)
        {
            if (column < 0 || column >= cells.Length)
                return 0;
            return int.TryParse(cells[column].Trim(), out int value) ? value : 0;
        }

        private static void ReadSheet(string sheet)
        {
            string text = ReadText(sheet);
            if (text == null)
                return;

            var lines = text.Split('\n');
            if (lines.Length < 2)
                return;

            var header = lines[0].TrimEnd('\r').Split('\t');
            int codeColumn = Column(header, "code");
            int questColumn = Column(header, "quest");
            if (codeColumn < 0)
                return;

            var vendorColumns = new Dictionary<string, int[]>();
            foreach (var cell in header)
            {
                string name = cell.Trim();
                if (!name.EndsWith("MagicLvl", StringComparison.OrdinalIgnoreCase))
                    continue;

                string vendor = name.Substring(0, name.Length - "MagicLvl".Length);
                int min = Column(header, vendor + "Min");
                int max = Column(header, vendor + "Max");
                if (min < 0 || max < 0)
                    continue;

                vendorColumns[vendor.ToLowerInvariant()] = new[]
                {
                    min, max, Column(header, vendor + "MagicMin"), Column(header, vendor + "MagicMax"), Column(header, vendor + "MagicLvl")
                };
            }

            foreach (var vendor in vendorColumns.Keys)
            {
                if (!_rows.ContainsKey(vendor))
                    _rows[vendor] = new List<Row>();
            }

            for (int i = 1; i < lines.Length; ++i)
            {
                var cells = lines[i].TrimEnd('\r').Split('\t');
                if (codeColumn >= cells.Length)
                    continue;

                string code = cells[codeColumn].Trim();
                var info = code.Length > 0 ? ItemInfo.Find(code) : null;
                if (info == null || info.invWidth <= 0 || info.invHeight <= 0)
                    continue;
                if (Number(cells, questColumn) > 0)
                    continue;

                foreach (var pair in vendorColumns)
                {
                    var columns = pair.Value;
                    var row = new Row
                    {
                        code = code,
                        min = Number(cells, columns[0]),
                        max = Number(cells, columns[1]),
                        magicMin = Number(cells, columns[2]),
                        magicMax = Number(cells, columns[3]),
                        magicLvl = Number(cells, columns[4]),
                    };
                    if (row.max <= 0 && row.magicMax <= 0)
                        continue;
                    _rows[pair.Key].Add(row);
                }
            }
        }
    }
}
