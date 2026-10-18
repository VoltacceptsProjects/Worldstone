using System;
using System.Collections.Generic;

namespace Diablerie.Engine.SaveGame
{
    // Plain serializable data for JsonUtility. Datasheet objects are never stored directly,
    // only their identifiers (item code, unique name, set id, property code, skill name),
    // so saves survive datasheet reloads and stay human-readable.

    [Serializable]
    public class CharacterSave
    {
        public int version = SaveManager.CurrentVersion;
        public string name;
        public string className;
        public int act = 1;
        public string savedAtUtc;

        public int level = 1;
        public long experience;
        public int health;
        public int maxHealth;
        public int mana;
        public int maxMana;

        public int gold;
        public List<InventoryEntrySave> inventory = new List<InventoryEntrySave>();
        // Legacy: the stash used to be stored per character. It is shared by all characters now
        // (see SharedStashSave), these are only read to migrate an old save.
        public int stashGold;
        public List<InventoryEntrySave> stash = new List<InventoryEntrySave>();
        public List<EquippedItemSave> equipment = new List<EquippedItemSave>();
        public bool hasHandsItem;
        public ItemSave handsItem;

        public List<string> hotSkills = new List<string>();
        public string leftSkill;
        public string rightSkill;

        public List<int> waypoints = new List<int>();
    }

    // The stash shared by all characters, stored in its own file next to the character saves
    [Serializable]
    public class SharedStashSave
    {
        public int version = 1;
        public int gold;
        public List<InventoryEntrySave> items = new List<InventoryEntrySave>();
    }

    [Serializable]
    public class InventoryEntrySave
    {
        public int x;
        public int y;
        public ItemSave item;
    }

    [Serializable]
    public class EquippedItemSave
    {
        public int loc;
        public ItemSave item;
    }

    [Serializable]
    public class ItemSave
    {
        public string code;
        public string name;
        public int quality;
        public int level;
        public int levelReq;
        public bool identified = true;
        public int quantity = 1;
        public int invFileIndex;
        public string uniqueName;   // UniqueItem.nameStr
        public string setItemId;    // SetItem.id
        public List<PropertySave> properties = new List<PropertySave>();
        public List<PropertyGroupSave> setItemProperties = new List<PropertyGroupSave>();
    }

    [Serializable]
    public class PropertyGroupSave
    {
        public List<PropertySave> properties = new List<PropertySave>();
    }

    [Serializable]
    public class PropertySave
    {
        public string code;
        public string param;
        public int value;
        public int min;
        public int max;
        public string classSpecific;
    }

    public class CharacterSaveInfo
    {
        public string name;
        public string className;
        public int level;
        public int act;
        public string savedAtUtc;
    }
}
