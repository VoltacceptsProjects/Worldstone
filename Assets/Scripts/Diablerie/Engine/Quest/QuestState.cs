using System;

namespace Diablerie.Engine.Quest
{
    public enum QuestState
    {
        NotStarted,
        Initiated,
        InProgress,
        Completed,
        Failed
    }

    [System.Serializable]
    public class QuestData
    {
        public string id;
        public string name;
        public int act;
        public QuestState state;
        public int questFlag;
        public DateTime initiatedTime;
        public DateTime completedTime;
    }
}
