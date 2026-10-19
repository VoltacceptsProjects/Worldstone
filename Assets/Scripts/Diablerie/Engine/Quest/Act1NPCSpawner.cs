using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.World;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Safety net for the Rogue Encampment. The town DS1 places the NPCs through monpreset.txt, so normally
    /// nothing is missing; if a town NPC did not spawn it is created here through the regular monster
    /// pipeline (renderer, collision, NPC controller), next to where the player arrived.
    /// Deckard Cain only stands in the camp once he has been rescued.
    /// </summary>
    public class Act1NPCSpawner : MonoBehaviour
    {
        private const int RogueEncampmentLevelId = 1;

        // monstats id and offset from the arrival point, in tiles
        private static readonly KeyValuePair<string, Vector2>[] Townsfolk =
        {
            new KeyValuePair<string, Vector2>("akara", new Vector2(-7, 5)),
            new KeyValuePair<string, Vector2>("kashya", new Vector2(7, 5)),
            new KeyValuePair<string, Vector2>("charsi", new Vector2(-7, -5)),
            new KeyValuePair<string, Vector2>("warriv1", new Vector2(8, -5)),
            new KeyValuePair<string, Vector2>("gheed", new Vector2(0, 9)),
        };

        private void Start()
        {
            Events.LevelChanged += OnLevelChanged;
            QuestManager.OnQuestCompleted += OnQuestCompleted;
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
            QuestManager.OnQuestCompleted -= OnQuestCompleted;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel == null || newLevel.id != RogueEncampmentLevelId)
                return;
            SpawnMissing();
        }

        private void OnQuestCompleted(QuestData quest)
        {
            // Cain moves into the camp when he is rescued
            if (quest.id == "A1Q4" && Level.Current != null && Level.Current.id == RogueEncampmentLevelId)
                SpawnMissing();
        }

        private static bool Present(string monStatId)
        {
            foreach (var entity in WorldState.instance.Entities)
            {
                if (entity is Unit unit && unit.monStat != null && unit.monStat.id == monStatId)
                    return true;
            }
            return false;
        }

        private void SpawnMissing()
        {
            var player = WorldState.instance != null ? WorldState.instance.Player : null;
            if (player == null || player.unit == null)
                return;
            Vector3 origin = player.unit.iso.pos;

            foreach (var entry in Townsfolk)
            {
                if (!Present(entry.Key))
                    Spawn(entry.Key, origin + new Vector3(entry.Value.x, entry.Value.y, 0));
            }

            var qm = QuestManager.Instance;
            if (qm != null && qm.IsQuestComplete("A1Q4") && !Present("cain1"))
                Spawn("cain1", origin + new Vector3(-3, 3, 0));
        }

        private void Spawn(string monStatId, Vector3 isoPos)
        {
            var unit = WorldBuilder.SpawnMonster(monStatId, Iso.MapToWorld(isoPos), transform);
            if (unit != null)
                Debug.Log("Spawned missing Rogue Encampment NPC " + monStatId);
        }
    }
}
