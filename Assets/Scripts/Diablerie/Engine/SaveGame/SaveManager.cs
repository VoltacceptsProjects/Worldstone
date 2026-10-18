using System;
using System.Collections.Generic;
using System.IO;
using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine.SaveGame
{
    public static class SaveManager
    {
        public const int CurrentVersion = 1;
        public const int MaxCharacters = 8;
        private const string Extension = ".d2json";
        private const string BackupExtension = ".bak";

        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public static string GetPath(string characterName)
        {
            return Path.Combine(SaveDirectory, SanitizeFileName(characterName) + Extension);
        }

        public static bool Exists(string characterName)
        {
            return File.Exists(GetPath(characterName));
        }

        // Case-insensitive, since save files live on file systems that may ignore case
        public static bool NameExists(string characterName)
        {
            if (Exists(characterName))
                return true;
            return List().Exists(c => string.Equals(c.name, characterName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Save(Player player, int act)
        {
            if (player == null)
                return false;

            try
            {
                var save = Capture(player, act);
                string json = JsonUtility.ToJson(save, prettyPrint: true);
                WriteAtomically(GetPath(save.name), json);
                Debug.Log("Character saved: " + GetPath(save.name));
                SaveSharedStash(player);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to save character: " + e);
                return false;
            }
        }

        // Returns null if there is no usable save (missing, corrupt, wrong class, newer version).
        public static CharacterSave Load(string characterName)
        {
            string path = GetPath(characterName);
            var save = TryRead(path);
            if (save == null && File.Exists(path + BackupExtension))
            {
                Debug.LogWarning("Falling back to backup save for " + characterName);
                save = TryRead(path + BackupExtension);
            }
            return save;
        }

        // The stash is shared by all characters. ".json" so the character list (".d2json") never picks it up.
        public static string SharedStashPath => Path.Combine(SaveDirectory, "shared_stash.json");

        // Fills the stash of a freshly created player. A character save from before the stash was shared
        // can bring its own stash along, but only while no shared stash file exists yet.
        public static void RestoreSharedStash(Player player, CharacterSave legacy)
        {
            var shared = ReadSharedStash();
            int gold;
            List<InventoryEntrySave> entries;
            if (shared != null)
            {
                gold = shared.gold;
                entries = shared.items;
            }
            else if (legacy != null && (legacy.stash.Count > 0 || legacy.stashGold > 0))
            {
                Debug.Log("Migrating the stash of " + legacy.name + " to the shared stash");
                gold = legacy.stashGold;
                entries = legacy.stash;
            }
            else
            {
                return;
            }

            player.stash.gold = Mathf.Max(0, gold);
            foreach (var entry in entries)
            {
                var item = ItemSerializer.FromSave(entry.item);
                if (item == null)
                    continue;

                if (!player.stash.Put(item, entry.x, entry.y, out var popped, maxPops: 0) &&
                    !player.stash.Put(item))
                {
                    Debug.LogWarning("Stash: no room for item " + item.name + ", item lost");
                }
            }
        }

        public static bool SaveSharedStash(Player player)
        {
            if (player == null || player.stash == null)
                return false;

            try
            {
                var data = new SharedStashSave { gold = player.stash.gold };
                foreach (var entry in player.stash.entries)
                {
                    data.items.Add(new InventoryEntrySave
                    {
                        x = entry.x,
                        y = entry.y,
                        item = ItemSerializer.ToSave(entry.item),
                    });
                }
                WriteAtomically(SharedStashPath, JsonUtility.ToJson(data, prettyPrint: true));
                Debug.Log("Shared stash saved: " + SharedStashPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to save the shared stash: " + e);
                return false;
            }
        }

        private static SharedStashSave ReadSharedStash()
        {
            foreach (var path in new[] { SharedStashPath, SharedStashPath + BackupExtension })
            {
                try
                {
                    if (!File.Exists(path))
                        continue;
                    var data = JsonUtility.FromJson<SharedStashSave>(File.ReadAllText(path));
                    if (data != null && data.items != null)
                        return data;
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to read the shared stash '" + path + "': " + e.Message);
                }
            }
            return null;
        }

        public static List<CharacterSaveInfo> List()
        {
            var result = new List<CharacterSaveInfo>();
            if (!Directory.Exists(SaveDirectory))
                return result;

            foreach (var file in Directory.GetFiles(SaveDirectory, "*" + Extension))
            {
                var save = TryRead(file);
                if (save == null)
                    continue;
                result.Add(new CharacterSaveInfo
                {
                    name = save.name,
                    className = save.className,
                    level = save.level,
                    act = save.act,
                    savedAtUtc = save.savedAtUtc,
                });
            }
            result.Sort((a, b) => string.CompareOrdinal(b.savedAtUtc, a.savedAtUtc));
            return result;
        }

        public static void Delete(string characterName)
        {
            string path = GetPath(characterName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + BackupExtension)) File.Delete(path + BackupExtension);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to delete save: " + e);
            }
        }

        public static CharacterSave Capture(Player player, int act)
        {
            var save = new CharacterSave
            {
                name = player.name,
                className = player.charStatInfo.className,
                act = act,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                level = player.charStat.level,
                experience = player.charStat.experience,
                health = player.unit.health,
                maxHealth = player.unit.maxHealth,
                mana = player.unit.mana,
                maxMana = player.unit.maxMana,
                gold = player.inventory.gold,
            };

            foreach (var entry in player.inventory.entries)
            {
                save.inventory.Add(new InventoryEntrySave
                {
                    x = entry.x,
                    y = entry.y,
                    item = ItemSerializer.ToSave(entry.item),
                });
            }

            for (int loc = 0; loc < player.equip.items.Length; ++loc)
            {
                var item = player.equip.items[loc];
                if (item == null)
                    continue;
                save.equipment.Add(new EquippedItemSave { loc = loc, item = ItemSerializer.ToSave(item) });
            }

            if (player.HandsItem != null)
            {
                save.hasHandsItem = true;
                save.handsItem = ItemSerializer.ToSave(player.HandsItem);
            }

            save.waypoints.AddRange(player.waypoints);
            save.waypoints.Sort();

            if (PlayerController.instance != null)
                PlayerController.instance.WriteSkills(save);

            return save;
        }

        // Applies a save to a freshly created player (no starting items given yet).
        public static void Restore(Player player, CharacterSave save)
        {
            player.charStat.level = Mathf.Max(1, save.level);
            player.charStat.experience = (uint)System.Math.Max(0L, save.experience);
            player.inventory.gold = Mathf.Max(0, save.gold);

            foreach (var equipped in save.equipment)
            {
                var item = ItemSerializer.FromSave(equipped.item);
                if (item == null)
                    continue;

                if (equipped.loc >= 0 && equipped.loc < player.equip.items.Length)
                {
                    player.equip.Equip(item, equipped.loc);
                    if (player.equip.items[equipped.loc] == item)
                        continue;
                }

                // Couldn't equip (e.g. datasheet rules changed), don't lose it
                if (!player.inventory.Put(item))
                    Debug.LogWarning("Save: no room for item " + item.name + ", item lost");
            }

            foreach (var entry in save.inventory)
            {
                var item = ItemSerializer.FromSave(entry.item);
                if (item == null)
                    continue;

                if (!player.inventory.Put(item, entry.x, entry.y, out var popped, maxPops: 0) &&
                    !player.inventory.Put(item))
                {
                    Debug.LogWarning("Save: no room for item " + item.name + ", item lost");
                }
            }

            if (save.hasHandsItem)
                player.HandsItem = ItemSerializer.FromSave(save.handsItem);

            if (save.waypoints != null)
            {
                foreach (int index in save.waypoints)
                    player.waypoints.Add(index);
            }

            player.unit.health = Mathf.Clamp(save.health, 1, player.unit.maxHealth);
            player.unit.mana = Mathf.Clamp(save.mana, 0, player.unit.maxMana);
        }

        private static CharacterSave TryRead(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var save = JsonUtility.FromJson<CharacterSave>(File.ReadAllText(path));
                if (save == null || string.IsNullOrEmpty(save.name))
                    throw new InvalidDataException("empty or malformed save");
                if (save.version > CurrentVersion)
                    throw new InvalidDataException("save version " + save.version + " is newer than supported " + CurrentVersion);
                if (CharStatsInfo.Find(save.className) == null)
                    throw new InvalidDataException("unknown class '" + save.className + "'");

                save.act = Mathf.Clamp(save.act, 1, 5);
                return save;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to read save '" + path + "': " + e.Message);
                return null;
            }
        }

        // Write to a temp file first so a crash mid-write can't destroy the existing save;
        // the previous save is kept as .bak.
        private static void WriteAtomically(string path, string contents)
        {
            Directory.CreateDirectory(SaveDirectory);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, contents);
            if (File.Exists(path))
                File.Copy(path, path + BackupExtension, true);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
