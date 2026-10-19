using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles the Horadric Malus quest - retrieving the smithing hammer from the Barracks
    /// </summary>
    public class HoradricMalusQuestLogic : MonoBehaviour
    {
        private StaticObject malusObject;
        private bool hasMalus = false;
        private const string MALUS_ITEM_CODE = "hdm"; // Horadric Malus hammer code from weapons.txt

        private const int BarracksLevelId = 28;
        private const int MalusObjectId = 108;

        private void Start()
        {
            Events.LevelChanged += OnLevelChanged;
            if (Level.Current != null && Level.Current.id == BarracksLevelId)
                FindMalus();
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
            if (malusObject != null)
                malusObject.onOperate -= OnMalusOperated;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel != null && newLevel.id == BarracksLevelId)
                FindMalus();
        }

        private void FindMalus()
        {
            if (malusObject != null)
                return;

            foreach (var obj in FindObjectsOfType<StaticObject>())
            {
                if (obj.info != null && obj.info.id == MalusObjectId)
                {
                    malusObject = obj;
                    break;
                }
            }

            if (malusObject != null)
            {
                Debug.Log("Found Horadric Malus in Barracks");
                malusObject.onOperate += OnMalusOperated;
            }
            else
            {
                Debug.LogWarning("Horadric Malus object not found in Barracks");
            }
        }

        private void OnMalusOperated(Unit unit)
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Only allow pickup if Horadric Malus quest is in progress
            if (!questManager.IsQuestInProgress("A1Q3"))
            {
                Debug.Log("Horadric Malus quest not in progress yet");
                return;
            }

            if (hasMalus)
            {
                Debug.Log("Already have the Horadric Malus");
                return;
            }

            PickupMalus(unit);
        }

        private void PickupMalus(Unit playerUnit)
        {
            var player = WorldState.instance.Player;
            if (player == null)
            {
                Debug.LogWarning("Player not found");
                return;
            }

            // Create the Horadric Malus item
            var itemInfo = ItemInfo.Find(MALUS_ITEM_CODE);
            if (itemInfo == null)
            {
                Debug.LogWarning("Could not find item info for Horadric Malus: " + MALUS_ITEM_CODE);
                return;
            }

            var item = new Item(itemInfo);
            item.quality = Item.Quality.Normal;
            item.name = "Horadric Malus";

            // Try to add to player's inventory
            if (player.inventory.Put(item))
            {
                hasMalus = true;
                Debug.Log("Horadric Malus added to inventory");
                
                // Remove the object from the world
                if (malusObject != null)
                {
                    malusObject.SetMode(StaticObjectMode.On); // Change visual state
                }
            }
            else
            {
                Debug.LogWarning("No room in inventory for Horadric Malus");
            }
        }

        public bool HasMalus()
        {
            return hasMalus;
        }

        public void ReturnToCharsi()
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            if (hasMalus && questManager.IsQuestInProgress("A1Q3"))
            {
                var player = WorldState.instance.Player;
                if (player != null)
                {
                    // Remove the item from inventory using Take (public method)
                    var malusEntry = FindMalusInInventory(player);
                    if (malusEntry.item != null)
                    {
                        player.inventory.Take(malusEntry.x, malusEntry.y);
                    }
                }

                questManager.CompleteQuest("A1Q3");
                hasMalus = false;
                Debug.Log("Horadric Malus returned to Charsi");
            }
        }

        private Inventory.Entry FindMalusInInventory(Player player)
        {
            if (player == null || player.inventory == null)
                return default(Inventory.Entry);

            foreach (var entry in player.inventory.entries)
            {
                if (entry.item != null && entry.item.info.code == MALUS_ITEM_CODE)
                {
                    return entry;
                }
            }
            return default(Inventory.Entry);
        }
    }
}
