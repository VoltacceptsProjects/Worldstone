using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles the Tree of Inifuss quest - collecting the bark to translate Cairn Stone runes
    /// </summary>
    public class TreeOfInifussLogic : MonoBehaviour
    {
        private StaticObject treeOfInifuss;
        private bool hasBark = false;

        private const int DarkWoodLevelId = 5; // Act 1 - Wilderness 4
        private const int InifussObjectId = 30;

        private void Start()
        {
            Events.LevelChanged += OnLevelChanged;
            if (Level.Current != null && Level.Current.id == DarkWoodLevelId)
                FindTree();
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
            if (treeOfInifuss != null)
                treeOfInifuss.onOperate -= OnTreeOperated;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel != null && newLevel.id == DarkWoodLevelId)
                FindTree();
        }

        private void FindTree()
        {
            if (treeOfInifuss != null)
                return;

            foreach (var obj in FindObjectsOfType<StaticObject>())
            {
                if (obj.info != null && obj.info.id == InifussObjectId)
                {
                    treeOfInifuss = obj;
                    break;
                }
            }

            if (treeOfInifuss != null)
            {
                Debug.Log("Found Tree of Inifuss in Dark Wood");
                treeOfInifuss.onOperate += OnTreeOperated;
            }
            else
            {
                Debug.LogWarning("Tree of Inifuss not found in Dark Wood");
            }
        }

        private void OnTreeOperated(Unit unit)
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Only allow bark collection if Search for Cain quest is in progress
            if (!questManager.IsQuestInProgress("A1Q4"))
            {
                Debug.Log("Search for Cain quest not in progress yet");
                return;
            }

            if (hasBark)
            {
                Debug.Log("Already have the bark from the Tree of Inifuss");
                return;
            }

            CollectBark();
        }

        private void CollectBark()
        {
            hasBark = true;
            Debug.Log("Bark collected from Tree of Inifuss");
            
            // Give the bark item to the player
            // This would normally create an item in the player's inventory
            // For now, we just track the state
        }

        public bool HasBark()
        {
            return hasBark;
        }

        public void BarkGivenToAkara()
        {
            if (hasBark)
            {
                hasBark = false;
                Debug.Log("Bark given to Akara for translation");
            }
        }
    }
}
