using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles the logic for rescuing Deckard Cain from his cage in Tristram
    /// </summary>
    public class CainRescueLogic : MonoBehaviour
    {
        private StaticObject cainsCage;
        private Unit cainUnit;

        private const int TristramLevelId = 38;
        private const int GibbetObjectId = 26;

        private void Start()
        {
            // Level.Current is still null when the scene starts, so the level is recognised when the
            // player walks in (same as DenOfEvilQuestLogic)
            Events.LevelChanged += OnLevelChanged;
            if (Level.Current != null && Level.Current.id == TristramLevelId)
                FindCage();
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
            if (cainsCage != null)
                cainsCage.onOperate -= OnCageOperated;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel != null && newLevel.id == TristramLevelId)
                FindCage();
        }

        private void FindCage()
        {
            if (cainsCage != null)
                return;

            foreach (var obj in FindObjectsOfType<StaticObject>())
            {
                if (obj.info != null && obj.info.id == GibbetObjectId)
                {
                    cainsCage = obj;
                    break;
                }
            }

            if (cainsCage != null)
            {
                Debug.Log("Found Cain's cage in Tristram");
                cainsCage.onOperate += OnCageOperated;
            }
            else
            {
                Debug.LogWarning("Cain's cage not found in Tristram");
            }
        }

        private void OnCageOperated(Unit unit)
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Only allow rescue if quest is in progress
            if (!questManager.IsQuestInProgress("A1Q4"))
            {
                Debug.Log("Search for Cain quest not in progress yet");
                return;
            }

            RescueCain();
        }

        public void RescueCain()
        {
            if (cainsCage == null)
            {
                Debug.LogWarning("Cain's cage not found");
                return;
            }

            // Activate the cage to release Cain
            cainsCage.SetMode(StaticObjectMode.On);

            // Spawn Cain if not already spawned
            if (cainUnit == null)
            {
                SpawnCain(cainsCage.transform.position);
            }

            // Complete the quest
            QuestManager.Instance.CompleteQuest("A1Q4");
            Debug.Log("Cain rescued!");
        }

        private void SpawnCain(Vector3 position)
        {
            var monStat = MonStat.Find("cain1");
            if (monStat == null)
            {
                Debug.LogWarning("Could not find MonStat for Cain");
                return;
            }

            var cainObj = new GameObject("DeckardCain");
            cainObj.transform.position = position + Vector3.forward * 2;
            cainObj.transform.SetParent(transform);

            cainUnit = cainObj.AddComponent<Unit>();
            cainUnit.monStat = monStat;
            cainUnit.token = monStat.code;
            cainUnit.basePath = @"data\global\chars\" + monStat.code + @"\";
            cainUnit.weaponClass = "";
            cainUnit.party = Party.Good;
            cainUnit.killable = false;
            cainUnit.size = 2;

            Debug.Log("Cain spawned at: " + position);
        }
    }
}
