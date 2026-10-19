using System;
using System.Collections.Generic;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    public class QuestManager : MonoBehaviour
    {
        private static QuestManager _instance;
        public static QuestManager Instance => _instance;

        public static QuestManager EnsureExists()
        {
            if (_instance != null)
                return _instance;

            _instance = FindObjectOfType<QuestManager>();
            if (_instance == null)
            {
                var go = new GameObject("QuestManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<QuestManager>();
            }
            return _instance;
        }

        private Dictionary<string, QuestData> quests = new Dictionary<string, QuestData>();
        private Dictionary<int, List<QuestData>> questsByFlag = new Dictionary<int, List<QuestData>>();

        // Which one-time NPC lines the player has already heard (saved with the character)
        private readonly HashSet<string> heard = new HashSet<string>();
        public IEnumerable<string> HeardLines => heard;
        // A voice line another system wants the next Talk click to play first
        public string RequestVoice;

        public bool HasHeard(string key) { return heard.Contains(key); }
        public void MarkHeard(string key) { heard.Add(key); }
        public void ForgetHeard(string key) { heard.Remove(key); }
        public void ClearHeard() { heard.Clear(); }

        public static event Action<QuestData> OnQuestStateChanged;
        public static event Action<QuestData> OnQuestInitiated;
        public static event Action<QuestData> OnQuestCompleted;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (quests.Count == 0)
                InitializeAct1Quests();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void InitializeAct1Quests()
        {
            // Quest 1: Den of Evil
            AddQuest(new QuestData
            {
                id = "A1Q1",
                name = "Den of Evil",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 1
            });

            // Quest 2: Blood Raven
            AddQuest(new QuestData
            {
                id = "A1Q2",
                name = "Blood Raven",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 2
            });

            // Quest 3: The Horadric Malus
            AddQuest(new QuestData
            {
                id = "A1Q3",
                name = "The Horadric Malus",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 3
            });

            // Quest 4: The Search for Cain
            AddQuest(new QuestData
            {
                id = "A1Q4",
                name = "The Search for Cain",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 4
            });

            // Quest 5: The Countess
            AddQuest(new QuestData
            {
                id = "A1Q5",
                name = "The Countess",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 5
            });

            // Quest 6: Andariel
            AddQuest(new QuestData
            {
                id = "A1Q6",
                name = "Andariel",
                act = 1,
                state = QuestState.NotStarted,
                questFlag = 6
            });
        }

        private void AddQuest(QuestData quest)
        {
            quests[quest.id] = quest;
            if (!questsByFlag.ContainsKey(quest.questFlag))
                questsByFlag[quest.questFlag] = new List<QuestData>();
            questsByFlag[quest.questFlag].Add(quest);
        }

        public QuestData GetQuest(string id)
        {
            if (quests.ContainsKey(id))
                return quests[id];
            return null;
        }

        public QuestData GetQuestByFlag(int flag)
        {
            if (questsByFlag.ContainsKey(flag) && questsByFlag[flag].Count > 0)
                return questsByFlag[flag][0];
            return null;
        }

        public void InitiateQuest(string id)
        {
            var quest = GetQuest(id);
            if (quest != null && quest.state == QuestState.NotStarted)
            {
                quest.state = QuestState.Initiated;
                quest.initiatedTime = DateTime.Now;
                OnQuestStateChanged?.Invoke(quest);
                OnQuestInitiated?.Invoke(quest);
                Debug.Log($"Quest initiated: {quest.name}");
                
                // Note: Quest data is saved when the character is saved via SaveManager.Save
                // We don't auto-save on every quest state change to avoid excessive I/O
            }
        }

        public void SetQuestInProgress(string id)
        {
            var quest = GetQuest(id);
            if (quest != null && (quest.state == QuestState.Initiated || quest.state == QuestState.NotStarted))
            {
                quest.state = QuestState.InProgress;
                OnQuestStateChanged?.Invoke(quest);
                Debug.Log($"Quest in progress: {quest.name}");
            }
        }

        public void CompleteQuest(string id)
        {
            var quest = GetQuest(id);
            if (quest != null && quest.state != QuestState.Completed)
            {
                quest.state = QuestState.Completed;
                quest.completedTime = DateTime.Now;
                OnQuestStateChanged?.Invoke(quest);
                OnQuestCompleted?.Invoke(quest);
                Debug.Log($"Quest completed: {quest.name}");
                
                // Grant rewards based on quest
                GrantQuestRewards(quest);
                
                // Note: Quest data is saved when the character is saved via SaveManager.Save
                // We don't auto-save on every quest state change to avoid excessive I/O
            }
        }

        public void FailQuest(string id)
        {
            var quest = GetQuest(id);
            if (quest != null && quest.state != QuestState.Completed)
            {
                quest.state = QuestState.Failed;
                OnQuestStateChanged?.Invoke(quest);
                Debug.Log($"Quest failed: {quest.name}");
            }
        }

        private void GrantQuestRewards(QuestData quest)
        {
            switch (quest.id)
            {
                case "A1Q1":
                    // Den of Evil: Free skill point
                    Debug.Log("Reward: 1 Free Skill Point");
                    break;
                case "A1Q2":
                    // Blood Raven: Rogue mercenaries available
                    Debug.Log("Reward: Rogue mercenaries available from Kashya");
                    break;
                case "A1Q3":
                    // Horadric Malus: Imbue item
                    Debug.Log("Reward: Charsi will imbue one item");
                    break;
                case "A1Q4":
                    // Search for Cain: Cain identifies items for free
                    Debug.Log("Reward: Cain identifies items for free");
                    break;
                case "A1Q5":
                    // Countess: Gold and items
                    Debug.Log("Reward: Gold and items from Countess");
                    break;
                case "A1Q6":
                    // Andariel: Can travel to Act 2
                    Debug.Log("Reward: Can travel to Act 2 with Warriv");
                    break;
            }
        }

        public bool IsQuestComplete(string id)
        {
            var quest = GetQuest(id);
            return quest != null && quest.state == QuestState.Completed;
        }

        public bool IsQuestInProgress(string id)
        {
            var quest = GetQuest(id);
            return quest != null && (quest.state == QuestState.Initiated || quest.state == QuestState.InProgress);
        }

        public void OnMonsterKilled(Unit monster)
        {
            if (monster.monStat == null)
                return;

            // Check for quest-related monster kills
            if (monster.monStat.id == "bloodraven" && IsQuestInProgress("A1Q2"))
            {
                CompleteQuest("A1Q2");
            }
        }

        public void OnLevelEntered(LevelInfo level)
        {
            // Quest-specific triggers when entering levels
            if (level != null)
            {
                if (level.name == "Act 1 - Cave 1" && IsQuestInProgress("A1Q1"))
                {
                    SetQuestInProgress("A1Q1");
                }
            }
        }
    }
}
