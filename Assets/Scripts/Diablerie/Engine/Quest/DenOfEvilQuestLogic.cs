using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles Den of Evil quest logic - tracking monster kills in the cave
    /// </summary>
    public class DenOfEvilQuestLogic : MonoBehaviour
    {
        private int monstersRemaining = 0;
        private int initialMonsterCount = 0;
        private bool questActive = false;

        public int MonstersRemaining => questActive ? Mathf.Max(0, monstersRemaining) : -1;

        private void Start()
        {
            // Subscribe unconditionally: Level.Current is still null when the scene starts, the cave is
            // recognised in OnLevelChanged when the player walks in
            Events.UnitDied += OnMonsterDied;
            Events.LevelChanged += OnLevelChanged;
        }

        private void OnDestroy()
        {
            Events.UnitDied -= OnMonsterDied;
            Events.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel == null)
                return;

            // Reset when entering Den of Evil
            if (newLevel.id == 8)
            {
                questActive = QuestManager.Instance.IsQuestInProgress("A1Q1");
                if (questActive)
                {
                    CountMonsters();
                }
            }
            else
            {
                questActive = false;
            }
        }

        private void CountMonsters()
        {
            // Count the enemy units of this level. Far away monsters are inactive and invisible to
            // FindObjectsOfType, so go through the activity registry instead.
            monstersRemaining = 0;
            initialMonsterCount = 0;

            foreach (var unit in UnitActivity.All)
            {
                if (unit == null)
                    continue;
                var level = unit.transform.parent != null ? unit.transform.parent.GetComponent<Level>() : null;
                if (level == null || level.info == null || level.info.id != 8)
                    continue;
                if (unit.monStat != null && !unit.monStat.npc && unit.party == Party.Evil)
                {
                    monstersRemaining++;
                    initialMonsterCount++;
                }
            }

        }

        private void OnMonsterDied(Unit unit, Unit killer)
        {
            if (!questActive)
                return;

            if (unit.monStat != null && !unit.monStat.npc && unit.party == Party.Evil)
            {
                monstersRemaining--;

                if (monstersRemaining <= 0)
                {
                    QuestManager.Instance.CompleteQuest("A1Q1");
                    questActive = false;
                }
            }
        }
    }
}
