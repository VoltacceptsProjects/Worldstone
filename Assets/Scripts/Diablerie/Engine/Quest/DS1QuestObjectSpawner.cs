using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.World;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Spawns quest-related objects in levels using DS1 data patterns
    /// This serves as a reference for where quest objects should be placed in DS1 files
    /// </summary>
    public class DS1QuestObjectSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class QuestObjectPlacement
        {
            public int levelId;
            public int objectId;
            public Vector3 position;
            public string description;
        }

        public QuestObjectPlacement[] placements;

        private void Start()
        {
            var currentLevel = Level.Current;
            if (currentLevel == null)
                return;

            SpawnQuestObjects(currentLevel);
        }

        private void SpawnQuestObjects(LevelInfo level)
        {
            foreach (var placement in placements)
            {
                if (placement.levelId == level.id)
                {
                    SpawnObject(placement);
                }
            }
        }

        private void SpawnObject(QuestObjectPlacement placement)
        {
            var objectInfo = ObjectInfo.sheet[placement.objectId];
            if (objectInfo == null)
            {
                Debug.LogWarning("Object info not found for id: " + placement.objectId);
                return;
            }

            var staticObject = WorldBuilder.SpawnObject(objectInfo, placement.position, parent: transform);
            staticObject.gameObject.name = objectInfo.name + " (Quest Object)";
            
            Debug.Log($"Spawned quest object: {objectInfo.name} at {placement.position}");
        }

        /// <summary>
        /// Returns the recommended DS1 object placements for Act 1 quest objects
        /// This is a reference for configuring DS1 files
        /// </summary>
        public static QuestObjectPlacement[] GetAct1QuestPlacements()
        {
            return new QuestObjectPlacement[]
            {
                // Cain's Cage (Gibbet, id 26) in Tristram (level id 38)
                new QuestObjectPlacement
                {
                    levelId = 38,
                    objectId = 26,
                    position = new Vector3(0, 0, 0),
                    description = "Cain's cage in Tristram"
                },
                
                // Tree of Inifuss (object id to be determined) in Dark Wood (level id 35)
                new QuestObjectPlacement
                {
                    levelId = 35,
                    objectId = 0, // Need to find correct object id
                    position = new Vector3(0, 0, 0),
                    description = "Tree of Inifuss in Dark Wood"
                },
                
                // Cairn Stones (stone objects) in Stony Field (level id 29)
                new QuestObjectPlacement
                {
                    levelId = 29,
                    objectId = 0, // Need to find correct object id
                    position = new Vector3(0, 0, 0),
                    description = "Cairn Stones in Stony Field"
                },
                
                // Horadric Malus (id 108) in Barracks (level id 28)
                new QuestObjectPlacement
                {
                    levelId = 28,
                    objectId = 108,
                    position = new Vector3(0, 0, 0),
                    description = "Horadric Malus in Barracks"
                }
            };
        }
    }
}
