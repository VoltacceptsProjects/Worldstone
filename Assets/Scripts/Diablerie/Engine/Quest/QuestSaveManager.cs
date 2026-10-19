using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles saving and loading of quest data
    /// Note: Quest data is now saved as part of the character save system via SaveManager
    /// This class is kept for backward compatibility and potential standalone quest saves
    /// </summary>
    public class QuestSaveManager
    {
        private const string QuestSaveKey = "QuestData";

        /// <summary>
        /// Save quests to PlayerPrefs (legacy method, use SaveManager.Save instead)
        /// </summary>
        public static void SaveQuests()
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
            {
                Debug.LogWarning("QuestManager not available for saving");
                return;
            }

            var saveData = QuestSaveData.FromRuntime(questManager);
            if (saveData != null)
            {
                string json = JsonUtility.ToJson(saveData);
                PlayerPrefs.SetString(QuestSaveKey, json);
                PlayerPrefs.Save();
                Debug.Log("Quest data saved to PlayerPrefs (legacy)");
            }
        }

        /// <summary>
        /// Load quests from PlayerPrefs (legacy method, loaded automatically via SaveManager.Restore)
        /// </summary>
        public static void LoadQuests()
        {
            if (!PlayerPrefs.HasKey(QuestSaveKey))
            {
                Debug.Log("No saved quest data found in PlayerPrefs");
                return;
            }

            string json = PlayerPrefs.GetString(QuestSaveKey);
            var saveData = JsonUtility.FromJson<QuestSaveData>(json);
            if (saveData != null)
            {
                var questManager = QuestManager.Instance;
                if (questManager != null)
                {
                    saveData.ApplyToRuntime(questManager);
                    Debug.Log("Quest data loaded from PlayerPrefs (legacy)");
                }
                else
                {
                    Debug.LogWarning("QuestManager not available for loading");
                }
            }
        }

        public static void ClearQuests()
        {
            PlayerPrefs.DeleteKey(QuestSaveKey);
            PlayerPrefs.Save();
            Debug.Log("Quest data cleared from PlayerPrefs");
        }
    }
}
