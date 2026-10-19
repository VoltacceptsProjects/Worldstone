using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Quest;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    public class QuestObjectManager : MonoBehaviour
    {
        private void Awake()
        {
            Events.UnitDied += OnUnitDied;
        }

        private void OnDestroy()
        {
            Events.UnitDied -= OnUnitDied;
        }

        private void OnUnitDied(Unit unit, Unit killer)
        {
            if (unit.monStat == null)
                return;

            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Check for quest-related monster deaths
            if (unit.monStat.id == "bloodraven" && questManager.IsQuestInProgress("A1Q2"))
            {
                questManager.CompleteQuest("A1Q2");
            }
        }
    }
}
