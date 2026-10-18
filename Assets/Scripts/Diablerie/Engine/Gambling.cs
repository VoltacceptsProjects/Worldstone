using System;
using System.Collections.Generic;
using System.Text;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.IO.D2Formats;
using UnityEngine;

namespace Diablerie.Engine
{
    // Gambling: the vendor offers unknown items of base types listed in gamble.txt. The price depends on the
    // base item only. Quality and properties are rolled when the stock is made, but nothing is shown until the
    // item is bought, at which point it is identified.
    public static class Gambling
    {
        public enum BuyResult
        {
            Bought,
            NothingThere,
            HandsNotEmpty,
            NotEnoughGold,
        }

        private const string GamblePath = @"data\global\excel\gamble.txt";
        public const int StockColumns = 10;
        public const int StockRows = 10;
        private const int StockTarget = 20;

        // Monster ids (monstats.txt) of the NPCs that gamble. Anya is "drehya" internally.
        // TUNE: edit this list to change who offers gambling.
        private static readonly HashSet<string> Gamblers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gheed", "elzix", "alkor", "jamella", "drehya"
        };

        // Used only if gamble.txt can't be read
        private static readonly string[] FallbackCodes =
        {
            "rin", "amu", "cap", "skp", "hlm", "lgl", "vgl", "mgl", "tgl", "hgl",
            "lbt", "vbt", "mbt", "tbt", "hbt", "lbl", "vbl", "mbl", "tbl", "hbl",
        };

        private static List<string> _codes;

        private class Stock
        {
            public int playerLevel;
            public Inventory inventory;
        }

        private static readonly Dictionary<string, Stock> Stocks = new Dictionary<string, Stock>();

        public static bool IsGambler(MonStat monStat)
        {
            return monStat != null && monStat.id != null && Gamblers.Contains(monStat.id);
        }

        public static int Price(Item item)
        {
            int price = item.info.gambleCost > 0 ? item.info.gambleCost : item.info.cost;
            return Mathf.Max(1, price);
        }

        // The vendor's stock. It is kept while the player stays at the same level, then made again.
        public static Inventory GetStock(string vendorId, Player player)
        {
            int level = player.charStat != null ? player.charStat.level : 1;
            if (Stocks.TryGetValue(vendorId, out var existing) && existing.inventory != null && existing.playerLevel == level)
                return existing.inventory;

            if (existing != null && existing.inventory != null)
                UnityEngine.Object.Destroy(existing.inventory.gameObject);

            var go = new GameObject("Gamble Stock " + vendorId);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var inventory = Inventory.Create(go, StockColumns, StockRows);
            Fill(inventory, level);
            Stocks[vendorId] = new Stock { playerLevel = level, inventory = inventory };
            return inventory;
        }

        // Forgets all stock, for starting a new game
        public static void Reset()
        {
            foreach (var stock in Stocks.Values)
            {
                if (stock.inventory != null)
                    UnityEngine.Object.Destroy(stock.inventory.gameObject);
            }
            Stocks.Clear();
        }

        private static void Fill(Inventory inventory, int playerLevel)
        {
            var candidates = new List<string>();
            foreach (var code in Codes())
            {
                var info = ItemInfo.Find(code);
                // Items of a much higher level than the player never show up
                if (info != null && info.level <= Mathf.Max(playerLevel, 1))
                    candidates.Add(code);
            }
            if (candidates.Count == 0)
                candidates.AddRange(Codes());

            int placed = 0;
            for (int attempt = 0; attempt < StockTarget * 4 && placed < StockTarget; ++attempt)
            {
                var item = ItemDrop.GenerateForGamble(candidates[UnityEngine.Random.Range(0, candidates.Count)], playerLevel);
                if (item != null && inventory.PutFromTop(item))
                    ++placed;
            }
            Debug.Log("Gambling: stocked " + placed + " items for level " + playerLevel);
        }

        private static List<string> Codes()
        {
            if (_codes != null)
                return _codes;

            _codes = new List<string>();
            try
            {
                string text = Encoding.ASCII.GetString(Mpq.ReadAllBytes(GamblePath));
                var lines = text.Split('\n');
                for (int i = 1; i < lines.Length; ++i)
                {
                    // name, code: take the first cell that is an item code
                    foreach (var cell in lines[i].TrimEnd('\r').Split('\t'))
                    {
                        string code = cell.Trim();
                        if (code.Length > 0 && ItemInfo.Find(code) != null)
                        {
                            if (!_codes.Contains(code))
                                _codes.Add(code);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Gambling: could not read gamble.txt (" + e.Message + "), using the built-in list");
            }

            if (_codes.Count == 0)
                _codes.AddRange(FallbackCodes);
            return _codes;
        }

        // Buys the item at the cell of the stock. It ends up on the cursor, like in the original.
        public static BuyResult Buy(Player player, Inventory stock, int x, int y)
        {
            var item = stock.ItemAt(x, y);
            if (item == null)
                return BuyResult.NothingThere;
            if (player.HandsItem != null)
                return BuyResult.HandsNotEmpty;

            int price = Price(item);
            if (player.inventory.gold < price)
                return BuyResult.NotEnoughGold;

            stock.Take(x, y);
            player.inventory.gold -= price;
            item.identified = true;
            player.HandsItem = item;
            return BuyResult.Bought;
        }
    }
}
