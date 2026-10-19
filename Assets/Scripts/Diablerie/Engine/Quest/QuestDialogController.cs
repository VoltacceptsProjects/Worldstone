using System.Collections.Generic;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    public class QuestDialogController
    {
        public static string GetDialogForNPC(Unit npc, Player player)
        {
            if (npc.monStat == null)
                return null;

            string npcId = npc.monStat.id.ToLower();
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return null;

            switch (npcId)
            {
                case "akara":
                    return GetAkaraDialog(questManager);
                case "kashya":
                    return GetKashyaDialog(questManager);
                case "charsi":
                    return GetCharsiDialog(questManager);
                case "warriv":
                    return GetWarrivDialog(questManager);
                case "cain":
                case "cain1":
                case "cain2":
                    return GetCainDialog(questManager);
                default:
                    return null;
            }
        }

        private static string GetAkaraDialog(QuestManager questManager)
        {
            var q1 = questManager.GetQuest("A1Q1");
            var q2 = questManager.GetQuest("A1Q2");
            var q4 = questManager.GetQuest("A1Q4");

            // Den of Evil dialog
            if (q1.state == QuestState.NotStarted)
                return "A1Q1InitAkara";
            else if (q1.state == QuestState.Initiated)
                return "A1Q1AfterInitAkara";
            else if (q1.state == QuestState.InProgress)
                return "A1Q1EarlyReturnAkara";
            else if (q1.state == QuestState.Completed)
                return "A1Q1SuccessfulAkara";

            // Blood Raven dialog
            if (q2.state == QuestState.NotStarted && q1.state == QuestState.Completed)
                return "A1Q2AfterInitAkara";
            else if (q2.state == QuestState.InProgress)
                return "A1Q2EarlyReturnAkara";
            else if (q2.state == QuestState.Completed)
                return "A1Q2SuccessfulAkara";

            // Search for Cain dialog
            if (q4.state == QuestState.NotStarted && q2.state == QuestState.Completed)
                return "A1Q4InitAkara";
            else if (q4.state == QuestState.InProgress)
                return "A1Q4EarlyReturnAkara";
            else if (q4.state == QuestState.Completed)
                return "A1Q4QuestSuccessfulAkara";

            // Default gossip
            return "AkaraGossip1";
        }

        private static string GetKashyaDialog(QuestManager questManager)
        {
            var q1 = questManager.GetQuest("A1Q1");
            var q2 = questManager.GetQuest("A1Q2");
            var q4 = questManager.GetQuest("A1Q4");

            // Den of Evil dialog
            if (q1.state == QuestState.Initiated)
                return "A1Q1AfterInitKashya";
            else if (q1.state == QuestState.InProgress)
                return "A1Q1EarlyReturnKashya";
            else if (q1.state == QuestState.Completed)
                return "A1Q1SuccessfulKashya";

            // Blood Raven dialog
            if (q2.state == QuestState.NotStarted && q1.state == QuestState.Completed)
                return "A1Q2InitKashya";
            else if (q2.state == QuestState.Initiated)
                return "A1Q2AfterInitKashya";
            else if (q2.state == QuestState.InProgress)
                return "A1Q2EarlyReturnKashya";
            else if (q2.state == QuestState.Completed)
                return "A1Q2SuccessfulKashya";

            // Search for Cain dialog
            if (q4.state == QuestState.InProgress)
                return "A1Q4EarlyReturnKashya";
            else if (q4.state == QuestState.Completed)
                return "A1Q4QuestSuccessfulKashya";

            // Default gossip
            return "KashyaGossip1";
        }

        private static string GetCharsiDialog(QuestManager questManager)
        {
            var q1 = questManager.GetQuest("A1Q1");
            var q2 = questManager.GetQuest("A1Q2");
            var q3 = questManager.GetQuest("A1Q3");
            var q4 = questManager.GetQuest("A1Q4");

            // Den of Evil dialog
            if (q1.state == QuestState.Initiated)
                return "A1Q1AfterInitCharsiMain";
            else if (q1.state == QuestState.InProgress)
                return "A1Q1EarlyReturnCharsi";
            else if (q1.state == QuestState.Completed)
                return "A1Q1SuccessfulCharsi";

            // Blood Raven dialog
            if (q2.state == QuestState.InProgress)
                return "A1Q2EarlyReturnCharsi";
            else if (q2.state == QuestState.Completed)
                return "A1Q2SuccessfulCharsi";

            // Horadric Malus dialog
            if (q3.state == QuestState.NotStarted && q2.state == QuestState.Completed)
                return "A1Q3InitCharsi";
            else if (q3.state == QuestState.InProgress)
                return "A1Q3EarlyReturnCharsi";
            else if (q3.state == QuestState.Completed)
                return "A1Q3SuccessfulCharsi";

            // Search for Cain dialog
            if (q4.state == QuestState.InProgress)
                return "A1Q4EarlyReturnCharsi";
            else if (q4.state == QuestState.Completed)
                return "A1Q4QuestSuccessfulCharsi";

            // Default gossip
            return "CharsiGossip1";
        }

        private static string GetWarrivDialog(QuestManager questManager)
        {
            var q1 = questManager.GetQuest("A1Q1");
            var q2 = questManager.GetQuest("A1Q2");
            var q4 = questManager.GetQuest("A1Q4");
            var q6 = questManager.GetQuest("A1Q6");

            // Den of Evil dialog
            if (q1.state == QuestState.Initiated)
                return "A1Q1AfterInitWarriv";
            else if (q1.state == QuestState.InProgress)
                return "A1Q1EarlyReturnWarriv";
            else if (q1.state == QuestState.Completed)
                return "A1Q1SuccessfulWarriv";

            // Blood Raven dialog
            if (q2.state == QuestState.InProgress)
                return "A1Q2EarlyReturnWarriv";
            else if (q2.state == QuestState.Completed)
                return "A1Q2SuccessfulWarriv";

            // Search for Cain dialog
            if (q4.state == QuestState.InProgress)
                return "A1Q4EarlyReturnSWarriv";
            else if (q4.state == QuestState.Completed)
                return "A1Q4QuestSuccessfulWarriv";

            // Andariel dialog
            if (q6.state == QuestState.Completed)
                return "A1Q6SuccessfulWarriv";

            // Default gossip
            return "WarrivGossip1";
        }

        private static string GetCainDialog(QuestManager questManager)
        {
            var q3 = questManager.GetQuest("A1Q3");
            var q4 = questManager.GetQuest("A1Q4");
            var q5 = questManager.GetQuest("A1Q5");
            var q6 = questManager.GetQuest("A1Q6");

            // After being rescued
            if (q4.state == QuestState.Completed)
            {
                // Horadric Malus dialog
                if (q3.state == QuestState.InProgress)
                    return "A1Q3EarlyReturnCain";
                else if (q3.state == QuestState.Completed)
                    return "A1Q3SuccessfulCain";

                // Countess dialog
                if (q5.state == QuestState.InProgress)
                    return "A1Q5EarlyReturnCain";
                else if (q5.state == QuestState.Completed)
                    return "A1Q5SuccessfulCain";

                // Andariel dialog
                if (q6.state == QuestState.NotStarted)
                    return "A1Q6InitCain";
                else if (q6.state == QuestState.InProgress)
                    return "A1Q6EarlyReturnCain";

                // Default after rescue
                return "A1Q4QuestSuccessfulCain";
            }

            // Default gossip
            return "CainGossip1";
        }
    }
}
