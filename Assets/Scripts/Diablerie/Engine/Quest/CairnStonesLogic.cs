using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using Diablerie.Game.World;
using System.Collections.Generic;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Handles the Cairn Stones - touching the stones in the right order opens the portal to Tristram
    /// </summary>
    public class CairnStonesLogic : MonoBehaviour
    {
        private const int StonyFieldLevelId = 4; // Act 1 - Wilderness 3
        private const int FirstStoneObjectId = 17; // StoneAlpha, Beta, Gamma, Delta, Lambda
        private const int StoneCount = 5;
        private const int PortalStoneObjectId = 22; // StoneTheta

        private readonly List<StaticObject> cairnStones = new List<StaticObject>();
        private StaticObject portalStone;
        private int[] correctSequence = new int[StoneCount]; // Will be set by Akara after translation
        private int[] playerSequence = new int[StoneCount];
        private int sequenceIndex = 0;
        private bool sequenceSet = false;
        private bool portalOpen = false;

        private void Start()
        {
            Events.LevelChanged += OnLevelChanged;
            if (Level.Current != null && Level.Current.id == StonyFieldLevelId)
                FindStones();
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel != null && newLevel.id == StonyFieldLevelId)
                FindStones();
        }

        private void FindStones()
        {
            if (cairnStones.Count > 0)
                return;

            var found = new StaticObject[StoneCount];
            foreach (var obj in FindObjectsOfType<StaticObject>())
            {
                if (obj.info == null)
                    continue;

                int index = obj.info.id - FirstStoneObjectId;
                if (index >= 0 && index < StoneCount)
                    found[index] = obj;
                else if (obj.info.id == PortalStoneObjectId)
                    portalStone = obj;
            }

            // Stone index is the position in Alpha, Beta, Gamma, Delta, Lambda order
            for (int i = 0; i < StoneCount; ++i)
            {
                if (found[i] == null)
                    continue;
                int stoneIndex = i;
                found[i].onOperate += unit => OnStoneOperated(stoneIndex);
                cairnStones.Add(found[i]);
            }

            if (portalStone != null)
                portalStone.onOperate += OnPortalOperated;

            if (cairnStones.Count > 0)
                Debug.Log($"Found {cairnStones.Count} Cairn Stones in Stony Field");
            else
                Debug.LogWarning("No Cairn Stones found in Stony Field");
        }

        private void OnStoneOperated(int stoneIndex)
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            // Only allow stone operation if Search for Cain quest is in progress
            if (!questManager.IsQuestInProgress("A1Q4"))
            {
                Debug.Log("Search for Cain quest not in progress yet");
                return;
            }

            if (portalOpen)
            {
                Debug.Log("Portal already open");
                return;
            }

            if (!sequenceSet)
            {
                Debug.Log("Cairn Stone sequence not yet translated by Akara");
                return;
            }

            TouchStone(stoneIndex);
        }

        private void TouchStone(int stoneIndex)
        {
            if (sequenceIndex >= correctSequence.Length)
            {
                Debug.Log("Sequence already complete");
                return;
            }

            playerSequence[sequenceIndex] = stoneIndex;
            sequenceIndex++;

            Debug.Log($"Touched stone {stoneIndex} in sequence position {sequenceIndex}");

            // Check if the sequence is correct so far
            bool correctSoFar = true;
            for (int i = 0; i < sequenceIndex; i++)
            {
                if (playerSequence[i] != correctSequence[i])
                {
                    correctSoFar = false;
                    break;
                }
            }

            if (!correctSoFar)
            {
                Debug.Log("Wrong sequence! Resetting...");
                ResetSequence();
            }
            else if (sequenceIndex == correctSequence.Length)
            {
                OpenPortal();
            }
        }

        public void SetSequence(int[] sequence)
        {
            if (sequence.Length != StoneCount)
            {
                Debug.LogWarning("Cairn Stone sequence must be 5 numbers");
                return;
            }

            correctSequence = (int[])sequence.Clone();
            sequenceSet = true;
            Debug.Log("Cairn Stone sequence set by Akara");
        }

        private void ResetSequence()
        {
            sequenceIndex = 0;
            playerSequence = new int[StoneCount];
        }

        private void OpenPortal()
        {
            portalOpen = true;
            Debug.Log("Cairn Stones activated correctly! Portal to Tristram opening...");

            // The sixth stone turns into the portal
            if (portalStone != null)
                portalStone.SetMode(StaticObjectMode.Operating);
            else
                Debug.LogWarning("Portal stone not found in Stony Field");
        }

        private void OnPortalOperated(Unit unit)
        {
            if (!portalOpen || unit == null)
                return;

            if (!Act1.HasTristram)
            {
                Debug.LogWarning("Tristram was not built");
                return;
            }

            ScreenFader.SetToBlack();
            ScreenFader.FadeToClear();
            unit.InstantMove(Iso.MapToIso(Iso.MapTileToWorld(Act1.TristramEntry)));
        }

        public bool IsPortalOpen()
        {
            return portalOpen;
        }
    }
}
