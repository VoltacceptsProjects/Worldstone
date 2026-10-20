using System.Collections.Generic;
using Diablerie.Engine.Datasheets;

namespace Diablerie.Engine
{
    // The sum of the stat bonuses of everything the character wears, keyed by the ItemStatCost.txt code
    // ("maxhp", "vitality", "item_fastermovevelocity", ...). Plain data, no Unity objects, so it can be tested.
    public class StatTotals
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();

        public IEnumerable<KeyValuePair<string, int>> All => _values;

        public int Get(string code)
        {
            int value;
            return _values.TryGetValue(code, out value) ? value : 0;
        }

        public void Add(string code, int value)
        {
            if (string.IsNullOrEmpty(code) || value == 0)
                return;
            _values[code] = Get(code) + value;
        }
    }

    // Reads the properties of the equipped items (magic affixes, unique and set item properties, and the
    // bonuses for wearing several pieces of a set) and adds them up into StatTotals.
    public static class ItemStats
    {
        // Property functions that do not change a plain stat (skills, charges, events, sockets, indestructible)
        private static bool IsSpecialFunc(int func)
        {
            switch (func)
            {
                case 10: case 11: case 12: case 13: case 14: case 15: case 16: case 19: case 20:
                    return true;
                default:
                    return false;
            }
        }

        public static StatTotals Collect(Equipment equip, int characterLevel)
        {
            var totals = new StatTotals();
            if (equip == null || equip.items == null)
                return totals;

            var setCounts = new Dictionary<ItemSet, int>();
            foreach (var item in equip.items)
            {
                if (item == null || !item.identified)
                    continue;
                if (item.setItem != null && item.setItem.set != null)
                {
                    int count;
                    setCounts.TryGetValue(item.setItem.set, out count);
                    setCounts[item.setItem.set] = count + 1;
                }
            }

            foreach (var item in equip.items)
            {
                if (item == null || !item.identified)
                    continue;

                foreach (var prop in item.properties)
                    AddProperty(totals, prop, characterLevel);

                // Bonuses of a set item that need other pieces of its set
                if (item.setItem != null && item.setItem.set != null)
                {
                    int pieces = setCounts[item.setItem.set];
                    for (int i = 0; i < pieces - 1 && i < item.setItemProperties.Count; ++i)
                    {
                        foreach (var prop in item.setItemProperties[i])
                            AddProperty(totals, prop, characterLevel);
                    }
                }
            }

            // Bonuses of the set itself, once per set no matter how many of its items are worn
            foreach (var entry in setCounts)
            {
                var set = entry.Key;
                int pieces = entry.Value;
                for (int i = 0; i < pieces - 1 && i * 2 + 1 < set.props.Length; ++i)
                {
                    for (int j = i * 2; j < i * 2 + 2; ++j)
                    {
                        if (set.props[j].prop != null)
                            AddProperty(totals, set.props[j].GetItemProperty(), characterLevel);
                    }
                }
                if (pieces >= set.items.Count)
                {
                    foreach (var mod in set.fullProps)
                    {
                        if (mod.prop != null)
                            AddProperty(totals, mod.GetItemProperty(), characterLevel);
                    }
                }
            }

            return totals;
        }

        private static void AddProperty(StatTotals totals, Item.Property prop, int characterLevel)
        {
            if (prop.info == null)
                return;

            // "+x% enhanced damage" lists the minimum and the maximum percent as two blocks with the same
            // value, count it only once
            bool enhancedDamageCounted = false;

            foreach (var block in prop.info.blocks)
            {
                if (block.stat == null)
                    continue;

                string code = block.statId;
                int value = prop.value;

                if (code == "item_mindamage_percent")
                    continue;
                if (code == "item_maxdamage_percent")
                {
                    if (enhancedDamageCounted)
                        continue;
                    enhancedDamageCounted = true;
                }
                else if (code.EndsWith("_perlevel"))
                {
                    // Stored in eighths of a point per character level
                    totals.Add(code, value * characterLevel / 8);
                    continue;
                }
                else if (IsSpecialFunc(block.func))
                {
                    continue;
                }

                totals.Add(code, value);
            }
        }
    }

    // The formulas that turn totals into the numbers of the character, kept apart from Unity for testing.
    public static class StatFormulas
    {
        public static int MaxLife(int baseLife, StatTotals totals, int lifePerVitality)
        {
            long life = baseLife;
            life += totals.Get("maxhp") + totals.Get("hitpoints") + totals.Get("item_hp_perlevel");
            life += (long)totals.Get("vitality") * lifePerVitality;
            life = life * (100 + totals.Get("item_maxhp_percent")) / 100;
            return (int)System.Math.Max(1L, System.Math.Min(life, int.MaxValue));
        }

        public static int MaxMana(int baseMana, StatTotals totals, float manaPerEnergy)
        {
            float mana = baseMana;
            mana += totals.Get("maxmana") + totals.Get("mana") + totals.Get("item_mana_perlevel");
            mana += totals.Get("energy") * manaPerEnergy;
            mana = mana * (100 + totals.Get("item_maxmana_percent")) / 100f;
            return (int)System.Math.Max(0f, System.Math.Min(mana, int.MaxValue));
        }

        // Faster run/walk is a percent of the base speed
        public static float Speed(float baseSpeed, StatTotals totals)
        {
            int percent = System.Math.Max(-90, System.Math.Min(totals.Get("item_fastermovevelocity"), 300));
            return baseSpeed * (100 + percent) / 100f;
        }
    }
}
