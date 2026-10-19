using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    [System.Serializable]
    public class QuestSaveData
    {
        public List<QuestEntry> quests = new List<QuestEntry>();
        public List<string> heardLines = new List<string>();

        [System.Serializable]
        public class QuestEntry
        {
            public string id;
            public int state; // 0=NotStarted, 1=Initiated, 2=InProgress, 3=Completed, 4=Failed
            public long initiatedTimeTicks;
            public long completedTimeTicks;
        }

        public static QuestSaveData FromRuntime(QuestManager questManager)
        {
            if (questManager == null)
                return null;

            var data = new QuestSaveData();
            var questIds = new List<string> { "A1Q1", "A1Q2", "A1Q3", "A1Q4", "A1Q5", "A1Q6" };

            foreach (var id in questIds)
            {
                var quest = questManager.GetQuest(id);
                if (quest != null)
                {
                    data.quests.Add(new QuestEntry
                    {
                        id = quest.id,
                        state = (int)quest.state,
                        initiatedTimeTicks = quest.initiatedTime.Ticks,
                        completedTimeTicks = quest.completedTime.Ticks
                    });
                }
            }

            foreach (var key in questManager.HeardLines)
                data.heardLines.Add(key);

            return data;
        }

        public void ApplyToRuntime(QuestManager questManager)
        {
            if (questManager == null)
                return;

            questManager.ClearHeard();
            if (heardLines != null)
            {
                foreach (var key in heardLines)
                    questManager.MarkHeard(key);
            }

            foreach (var entry in quests)
            {
                var quest = questManager.GetQuest(entry.id);
                if (quest != null)
                {
                    quest.state = (QuestState)entry.state;
                    quest.initiatedTime = new DateTime(entry.initiatedTimeTicks);
                    quest.completedTime = new DateTime(entry.completedTimeTicks);
                }
            }
        }
    }
}
