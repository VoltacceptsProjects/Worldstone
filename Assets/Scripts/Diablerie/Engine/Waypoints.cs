using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Engine
{
    public static class Waypoints
    {
        public const int NoWaypoint = 255;
        public const int OperateFn = 23;

        public class Entry
        {
            public int index;
            public LevelInfo level;
            public bool isTown;

            public int act => level.act;
            public string name => level.levelName;
        }

        private static List<Entry> _all;
        private static readonly Dictionary<int, StaticObject> _objects = new Dictionary<int, StaticObject>();
        private static readonly Dictionary<StaticObject, Entry> _entries = new Dictionary<StaticObject, Entry>();

        // Catalog
        public static IReadOnlyList<Entry> All
        {
            get
            {
                EnsureCatalog();
                return _all;
            }
        }

        public static List<Entry> ForAct(int act)
        {
            var result = new List<Entry>();
            foreach (var entry in All)
            {
                if (entry.act == act)
                    result.Add(entry);
            }
            return result;
        }

        public static Entry Find(int index)
        {
            foreach (var entry in All)
            {
                if (entry.index == index)
                    return entry;
            }
            return null;
        }

        public static Entry Find(LevelInfo level)
        {
            if (level == null)
                return null;
            foreach (var entry in All)
            {
                if (entry.level.id == level.id)
                    return entry;
            }
            return null;
        }

        private static void EnsureCatalog()
        {
            if (_all != null)
                return;

            _all = new List<Entry>();
            var seen = new HashSet<int>();
            foreach (var level in LevelInfo.sheet)
            {
                if (level.id == 0 || level.waypoint < 0 || level.waypoint >= NoWaypoint)
                    continue;

                if (!seen.Add(level.waypoint))
                {
                    Debug.LogWarning("Waypoint index " + level.waypoint + " is claimed by more than one level, ignoring " + level.name);
                    continue;
                }

                _all.Add(new Entry
                {
                    index = level.waypoint,
                    level = level,
                    isTown = level.name.EndsWith(" - Town"),
                });
            }

            _all.Sort((a, b) => a.index.CompareTo(b.index));
            if (_all.Count == 0)
                Debug.LogWarning("No waypoints found in Levels.txt");
        }

        // World registry
        public static void Register(StaticObject staticObject)
        {
            var level = staticObject.GetComponentInParent<Level>();
            if (level == null || level.info == null)
            {
                Debug.LogWarning("Waypoint object " + staticObject.name + " is not inside a level");
                return;
            }

            var entry = Find(level.info);
            if (entry == null)
            {
                Debug.LogWarning("Level " + level.info.name + " has a waypoint object but no waypoint index");
                return;
            }

            _objects[entry.index] = staticObject;
            _entries[staticObject] = entry;
        }

        public static void ClearWorld()
        {
            _objects.Clear();
            _entries.Clear();
        }

        public static Entry OfObject(StaticObject staticObject)
        {
            return _entries.TryGetValue(staticObject, out var entry) ? entry : null;
        }

        public static StaticObject GetObject(Entry entry)
        {
            if (_objects.TryGetValue(entry.index, out var staticObject) && staticObject != null)
                return staticObject;
            return null;
        }

        // Player state
        public static bool IsActivated(Player player, Entry entry)
        {
            return player.waypoints.Contains(entry.index);
        }

        public static bool IsUsable(Player player, Entry entry)
        {
            return entry.isTown || IsActivated(player, entry);
        }

        public static bool Activate(Player player, Entry entry)
        {
            return player.waypoints.Add(entry.index);
        }

        public static bool CanTravel(Player player, Entry entry, int currentAct)
        {
            if (!IsUsable(player, entry))
                return false;
            if (entry.act == currentAct)
                return GetObject(entry) != null;
            return entry.isTown;
        }

        public static void SyncVisuals(Player player)
        {
            foreach (var pair in _objects)
            {
                var staticObject = pair.Value;
                if (staticObject == null)
                    continue;
                if (player.waypoints.Contains(pair.Key) && staticObject.Mode == StaticObjectMode.Neutral)
                    staticObject.SetMode(StaticObjectMode.On);
            }
        }

        public static bool TryGetArrival(Entry entry, int unitSize, out Vector3 isoPosition)
        {
            isoPosition = Vector3.zero;
            var staticObject = GetObject(entry);
            if (staticObject == null)
                return false;

            isoPosition = Iso.MapToIso(staticObject.transform.position);
            if (CollisionMap.Fit(isoPosition, out var fitted, unitSize))
                isoPosition = fitted;
            return true;
        }
    }
}
