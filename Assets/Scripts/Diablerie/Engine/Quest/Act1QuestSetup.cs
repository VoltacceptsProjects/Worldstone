using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Sets up Act 1 quest-related objects and spawns at runtime
    /// Since MPQ files cannot be modified, all quest objects are spawned dynamically
    /// </summary>
    public class Act1QuestSetup : MonoBehaviour
    {
        private void Start()
        {
            // Ensure QuestManager exists
            if (QuestManager.Instance == null)
            {
                var questManagerObj = new GameObject("QuestManager");
                questManagerObj.AddComponent<QuestManager>();
                DontDestroyOnLoad(questManagerObj);
            }

            // Ensure QuestObjectManager exists
            if (FindObjectOfType<QuestObjectManager>() == null)
            {
                var questObjManagerObj = new GameObject("QuestObjectManager");
                questObjManagerObj.AddComponent<QuestObjectManager>();
                DontDestroyOnLoad(questObjManagerObj);
            }

            // Try to load legacy PlayerPrefs quest data if no character save loaded
            // (Quest data is normally loaded via SaveManager.Restore when loading a character)
            if (QuestManager.Instance.GetQuest("A1Q1").state == QuestState.NotStarted)
            {
                QuestSaveManager.LoadQuests();
            }

            // Add NPC spawner
            if (FindObjectOfType<Act1NPCSpawner>() == null)
            {
                gameObject.AddComponent<Act1NPCSpawner>();
            }

            // Add Boss spawner
            if (FindObjectOfType<BossSpawner>() == null)
            {
                gameObject.AddComponent<BossSpawner>();
            }

            // Add runtime quest object spawner
            if (FindObjectOfType<RuntimeQuestObjectSpawner>() == null)
            {
                gameObject.AddComponent<RuntimeQuestObjectSpawner>();
            }

            // Add Den of Evil quest logic
            if (FindObjectOfType<DenOfEvilQuestLogic>() == null)
            {
                gameObject.AddComponent<DenOfEvilQuestLogic>();
            }

            // Add Cain rescue logic
            if (FindObjectOfType<CainRescueLogic>() == null)
            {
                gameObject.AddComponent<CainRescueLogic>();
            }

            // Add Horadric Malus quest logic
            if (FindObjectOfType<HoradricMalusQuestLogic>() == null)
            {
                gameObject.AddComponent<HoradricMalusQuestLogic>();
            }

            // Add Tree of Inifuss logic
            if (FindObjectOfType<TreeOfInifussLogic>() == null)
            {
                gameObject.AddComponent<TreeOfInifussLogic>();
            }

            // Add Cairn Stones logic
            if (FindObjectOfType<CairnStonesLogic>() == null)
            {
                gameObject.AddComponent<CairnStonesLogic>();
            }

            // Subscribe to level change events
            Events.LevelChanged += OnLevelChanged;

            Debug.Log("Act 1 Quest Setup Complete");
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel == null)
                return;

            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Quest-specific level triggers
            switch (newLevel.id)
            {
                case 8: // Act 1 - Cave 1 (Den of Evil)
                    if (questManager.IsQuestInProgress("A1Q1"))
                    {
                        questManager.SetQuestInProgress("A1Q1");
                        Debug.Log("Den of Evil quest set to InProgress");
                    }
                    break;
            }
        }
    }
}
