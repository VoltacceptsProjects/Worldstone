using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.SaveGame;
using Diablerie.Engine.Utility;
using Diablerie.Game.World;
using UnityEngine;

namespace Diablerie.Engine
{
    public class Player
    {
        public string name;
        public GameObject gameObject;
        public Transform transform;
        public Unit unit;
        public Equipment equip;
        public Inventory inventory;
        public Inventory stash;
        public CharStat charStat;
        public PlayerStats stats;
        public CharStatsInfo charStatInfo;
        public readonly HashSet<int> waypoints = new HashSet<int>();
        
        private Item _handsItem;

        public Item HandsItem
        {
            get => _handsItem;
            set
            {
                if (_handsItem == value)
                    return;
                // Picking something up ends identify mode
                if (value != null)
                    CancelIdentify();
                _handsItem = value;
                HandsItemChanged?.Invoke(_handsItem);
            }
        }

        // The identify scroll that is waiting for an item to be clicked, or null
        public Item IdentifyScroll { get; private set; }
        public event System.Action<Item> IdentifyScrollChanged;

        public bool BeginIdentify(Item scroll)
        {
            if (!ItemIdentification.IsIdentifyItem(scroll) || HandsItem != null)
                return false;
            if (IdentifyScroll == scroll)
                return true;
            IdentifyScroll = scroll;
            IdentifyScrollChanged?.Invoke(scroll);
            return true;
        }

        public void CancelIdentify()
        {
            if (IdentifyScroll == null)
                return;
            IdentifyScroll = null;
            IdentifyScrollChanged?.Invoke(null);
        }

        // Spends the pending scroll on the item. Identify mode ends when it works.
        public bool IdentifyWithScroll(Item target)
        {
            if (IdentifyScroll == null)
                return false;
            if (!ItemIdentification.IdentifyWithScroll(this, IdentifyScroll, target))
                return false;
            CancelIdentify();
            return true;
        }

        public delegate void OnHandsItemChangedHandler(Item item);
        public event OnHandsItemChangedHandler HandsItemChanged;

        public Player(string className, Vector2i pos) : this(className, className, pos, null)
        {
        }

        public Player(string className, string characterName, Vector2i pos) : this(className, characterName, pos, null)
        {
        }

        public Player(CharacterSave save, Vector2i pos) : this(save.className, save.name, pos, save)
        {
        }

        private Player(string className, string characterName, Vector2i pos, CharacterSave save)
        {
            name = characterName;
            charStatInfo = CharStatsInfo.Find(className);
            gameObject = new GameObject("Player");
            transform = gameObject.transform;
            transform.position = Iso.MapTileToWorld(pos);
            gameObject.tag = "Player";
            unit = gameObject.AddComponent<Unit>();
            unit.basePath = @"data\global\chars";
            unit.token = charStatInfo.token;
            unit.weaponClass = charStatInfo.baseWClass;
            unit.run = true;
            unit.walkSpeed = 7;
            unit.runSpeed = 15;
            unit.maxHealth = 1000;
            unit.health = 1000;
            unit.size = 2;
            unit.party = Party.Good;

            gameObject.AddComponent<COFRenderer>();
            equip = gameObject.AddComponent<Equipment>();
            equip.charInfo = charStatInfo;
            unit.equip = equip;
            inventory = Inventory.Create(gameObject, 10, 4);
            var stashLayout = StashLayout.Get();
            stash = Inventory.Create(gameObject, stashLayout.columns, stashLayout.rows);
            SaveManager.RestoreSharedStash(this, save);
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = Iso.tileSizeY;
            var listenerObject = new GameObject("Audio Listener");
            listenerObject.AddComponent<AudioListener>();
            listenerObject.transform.SetParent(gameObject.transform, true);
            listenerObject.transform.localPosition = new Vector3(0, 0, -1);
            charStat = gameObject.AddComponent<CharStat>();
            charStat.unit = unit;
            stats = PlayerStats.Create(this);
            gameObject.AddComponent<UnitRenderer>();

            if (save != null)
            {
                SaveManager.Restore(this, save);
            }
            else
            {
                GiveStartingItems();
            }
        }

        private void GiveStartingItems()
        {
            foreach (var startingItem in charStatInfo.startingItems)
            {
                if (startingItem.code == null)
                    continue;

                var itemInfo = ItemInfo.Find(startingItem.code);
                for (int i = 0; i < startingItem.count; ++i)
                {
                    var item = new Item(itemInfo);
                    if (startingItem.loc != null)
                    {
                        int loc = BodyLoc.GetIndex(startingItem.loc);
                        equip.Equip(item, loc);
                    }
                    else
                    {
                        Take(item);
                    }
                }
            }
        }
        
        public bool Take(Item item, bool preferHands = false)
        {
            if (item.info.code == "gld")
            {
                inventory.gold += stats != null ? stats.GoldWithBonus(item.quantity) : item.quantity;
                return true;
            }

            if (preferHands)
            {
                HandsItem = item;
                return true;
            }

            if (equip.Equip(item))
                return true;

            return inventory.Put(item);
        }

        public void Use(MiscInfo itemInfo)
        {
            Debug.Log("Use item " + itemInfo.name + ", function: " + itemInfo.useFunction);
            switch (itemInfo.useFunction)
            {
                case MiscInfo.UseFunction.None:
                    break;
                case MiscInfo.UseFunction.IdentifyItem:
                    break;
                case MiscInfo.UseFunction.TownPortal:
                    var pos = Iso.MapToWorld(unit.iso.pos);
                    var teleport = WorldBuilder.SpawnObject("TP", pos, fit: true);
                    teleport.SetMode(StaticObjectMode.Operating);
                    var sound = SoundInfo.Find("player_townportal_cast");
                    AudioManager.instance.Play(sound, pos);
                    break;
                case MiscInfo.UseFunction.Potion:
                    if (itemInfo.stat1 == "hpregen")
                        unit.health += itemInfo.calc1;
                    if (itemInfo.stat1 == "manarecovery")
                        unit.mana += itemInfo.calc1;
                    break;
                case MiscInfo.UseFunction.RejuvPotion:
                    if (itemInfo.stat1 == "hitpoints")
                        unit.health += (int)(itemInfo.calc1 / 100.0f * unit.maxHealth);
                    if (itemInfo.stat1 == "mana")
                        unit.mana += (int)(itemInfo.calc1 / 100.0f * unit.maxMana);
                    if (itemInfo.stat2 == "hitpoints")
                        unit.health += (int)(itemInfo.calc2 / 100.0f * unit.maxHealth);
                    if (itemInfo.stat2 == "mana")
                        unit.mana += (int)(itemInfo.calc2 / 100.0f * unit.maxMana);
                    break;
                case MiscInfo.UseFunction.TemporaryPotion:
                    break;
                case MiscInfo.UseFunction.HoradricCube:
                    break;
                case MiscInfo.UseFunction.Elixir:
                    break;
                case MiscInfo.UseFunction.StaminaPotion:
                    break;
            }
        }
    }
}