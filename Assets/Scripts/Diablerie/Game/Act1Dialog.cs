using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Engine.Quest;
using UnityEngine;

namespace Diablerie.Game
{
    // Decides which voice line an Act 1 NPC speaks when the player clicks "Talk", the way the original game does:
    //   - the first Talk with an NPC is their introduction
    //   - after that, a pending quest line wins over gossip:
    //       success (quest done, reward speech, once) > init (quest giver offers the quest) >
    //       after (first reaction once the quest is on) > early (quest still open, alternates with gossip)
    //   - otherwise random gossip
    // Every line is an existing Sounds.txt entry: <npc>_act1_q<N>_<init|after|early|success>.
    public static class Act1Dialog
    {
        public class QuestDef
        {
            public int number;
            public string id;
            public string giver;        // voice that offers the quest with its "init" line, null if nobody does
            public string rewardGiver;  // voice that the player returns to for the reward, null if none
            public string requires;     // quest id that has to be completed first
        }

        public static readonly QuestDef[] Quests =
        {
            new QuestDef { number = 1, id = "A1Q1", giver = "akara",  rewardGiver = "akara",  requires = null },    // Den of Evil
            new QuestDef { number = 2, id = "A1Q2", giver = "kashya", rewardGiver = "kashya", requires = "A1Q1" },  // Sisters' Burial Grounds
            new QuestDef { number = 3, id = "A1Q3", giver = "charsi", rewardGiver = "charsi", requires = "A1Q2" },  // Tools of the Trade
            new QuestDef { number = 4, id = "A1Q4", giver = "akara",  rewardGiver = "akara",  requires = "A1Q2" },  // The Search for Cain
            new QuestDef { number = 5, id = "A1Q5", giver = null,     rewardGiver = null,     requires = "A1Q4" },  // The Forgotten Tower
            new QuestDef { number = 6, id = "A1Q6", giver = "cain",   rewardGiver = "warriv", requires = "A1Q4" },  // Sisters to the Slaughter
        };

        // Set to true to let quest offers come before the introduction
        private const bool QuestLinesBeforeIntro = false;

        private enum Kind { Success = 0, Init = 1, After = 2, Early = 3 }

        private struct Candidate
        {
            public Kind kind;
            public int number;
            public string sound;
        }

        public static QuestDef Find(int number)
        {
            foreach (var def in Quests)
            {
                if (def.number == number)
                    return def;
            }
            return null;
        }

        private static string SoundName(string voice, int number, string suffix)
        {
            return voice + "_act1_q" + number + "_" + suffix;
        }

        private static string HeardKey(string voice, int number, string suffix)
        {
            return voice + "|q" + number + "|" + suffix;
        }

        // True once the NPC has spoken the reward line of the quest (the quest log uses this too)
        public static bool RewardCollected(QuestDef def)
        {
            if (def.rewardGiver == null)
                return true;
            var qm = QuestManager.Instance;
            return qm != null && qm.HasHeard(HeardKey(def.rewardGiver, def.number, "success"));
        }

        private static bool Available(QuestManager qm, QuestDef def)
        {
            return def.requires == null || qm.IsQuestComplete(def.requires);
        }

        private static string PreferScroll(string voice, int number, string suffix, bool hasScroll)
        {
            // Quest 4 has extra lines for when the player carries the Scroll of Inifuss
            if (number == 4 && hasScroll)
            {
                string scrollName = SoundName(voice, number, suffix + "_scroll");
                if (NpcVoice.Exists(scrollName))
                    return scrollName;
            }
            string plain = SoundName(voice, number, suffix);
            return NpcVoice.Exists(plain) ? plain : null;
        }

        // Called when the player walks up to an NPC: hands in quest items that the original game takes automatically
        public static void RunHandIns(Unit npc, Player player)
        {
            var qm = QuestManager.Instance;
            if (qm == null || npc == null || npc.monStat == null)
                return;

            switch (NpcVoice.VoiceOf(npc))
            {
                case "akara":
                    if (qm.IsQuestInProgress("A1Q4"))
                    {
                        var tree = Object.FindObjectOfType<TreeOfInifussLogic>();
                        if (tree != null && tree.HasBark())
                        {
                            tree.BarkGivenToAkara();
                            var cairn = Object.FindObjectOfType<CairnStonesLogic>();
                            if (cairn != null)
                                cairn.SetSequence(new[] { 0, 1, 2, 3, 4 });
                            // Akara explains the stone order right away
                            qm.RequestVoice = "akara_act1_q4_instructions";
                        }
                    }
                    break;
                case "charsi":
                    if (qm.IsQuestInProgress("A1Q3"))
                    {
                        var malus = Object.FindObjectOfType<HoradricMalusQuestLogic>();
                        if (malus != null && malus.HasMalus())
                            malus.ReturnToCharsi();
                    }
                    break;
            }
        }

        public static AudioSource Talk(Unit npc, Player player)
        {
            var qm = QuestManager.Instance;
            string voice = NpcVoice.VoiceOf(npc);
            if (qm == null || voice == null || NpcVoice.CurrentAct() != 1)
                return NpcVoice.PlayTalk(npc, player);

            // A line another system asked for (e.g. Akara reading the scroll) goes first
            if (qm.RequestVoice != null)
            {
                string requested = qm.RequestVoice;
                qm.RequestVoice = null;
                var spoken = NpcVoice.PlayNamed(npc, requested);
                if (spoken != null)
                    return spoken;
            }

            if (!QuestLinesBeforeIntro && !NpcVoice.IntroPlayed(npc))
                return NpcVoice.PlayTalk(npc, player);

            var scrollTree = Object.FindObjectOfType<TreeOfInifussLogic>();
            bool hasScroll = scrollTree != null && scrollTree.HasBark();

            var candidates = new List<Candidate>();
            foreach (var def in Quests)
            {
                var quest = qm.GetQuest(def.id);
                if (quest == null)
                    continue;

                switch (quest.state)
                {
                    case QuestState.NotStarted:
                        if (def.giver == voice && Available(qm, def))
                        {
                            string init = PreferScroll(voice, def.number, "init", false);
                            if (init != null)
                                candidates.Add(new Candidate { kind = Kind.Init, number = def.number, sound = init });
                        }
                        break;

                    case QuestState.Initiated:
                    case QuestState.InProgress:
                        string after = PreferScroll(voice, def.number, "after", hasScroll);
                        if (after != null && !qm.HasHeard(HeardKey(voice, def.number, "after")))
                        {
                            candidates.Add(new Candidate { kind = Kind.After, number = def.number, sound = after });
                        }
                        else
                        {
                            string early = PreferScroll(voice, def.number, "early", hasScroll);
                            if (early != null)
                                candidates.Add(new Candidate { kind = Kind.Early, number = def.number, sound = early });
                        }
                        break;

                    case QuestState.Completed:
                        string success = PreferScroll(voice, def.number, "success", hasScroll);
                        if (success != null && !qm.HasHeard(HeardKey(voice, def.number, "success")))
                            candidates.Add(new Candidate { kind = Kind.Success, number = def.number, sound = success });
                        break;
                }
            }

            if (candidates.Count > 0)
            {
                var pick = Choose(candidates);
                bool earlyAgain = pick.kind == Kind.Early && qm.HasHeard(LastWasEarlyKey(voice));
                if (!earlyAgain)
                {
                    var audio = NpcVoice.PlayNamed(npc, pick.sound);
                    if (audio != null)
                    {
                        Apply(qm, voice, pick);
                        return audio;
                    }
                }
            }

            // Nothing pending (or an "early" reminder that was just heard): gossip. A reminder may play again afterwards.
            qm.ForgetHeard(LastWasEarlyKey(voice));
            return NpcVoice.PlayTalk(npc, player);
        }

        private static string LastWasEarlyKey(string voice)
        {
            return voice + "|lastEarly";
        }

        private static Candidate Choose(List<Candidate> candidates)
        {
            // Success, init and after lines go in quest order. The reminder for the most recent quest wins.
            Candidate best = candidates[0];
            for (int i = 1; i < candidates.Count; ++i)
            {
                var c = candidates[i];
                if (c.kind < best.kind)
                    best = c;
                else if (c.kind == best.kind)
                {
                    bool latestWins = c.kind == Kind.Early;
                    if (latestWins ? c.number > best.number : c.number < best.number)
                        best = c;
                }
            }
            return best;
        }

        private static void Apply(QuestManager qm, string voice, Candidate pick)
        {
            switch (pick.kind)
            {
                case Kind.Init:
                    qm.InitiateQuest("A1Q" + pick.number);
                    qm.MarkHeard(HeardKey(voice, pick.number, "init"));
                    qm.ForgetHeard(LastWasEarlyKey(voice));
                    break;
                case Kind.After:
                    qm.MarkHeard(HeardKey(voice, pick.number, "after"));
                    qm.ForgetHeard(LastWasEarlyKey(voice));
                    break;
                case Kind.Early:
                    qm.MarkHeard(LastWasEarlyKey(voice));
                    break;
                case Kind.Success:
                    qm.MarkHeard(HeardKey(voice, pick.number, "success"));
                    qm.ForgetHeard(LastWasEarlyKey(voice));
                    break;
            }
        }
    }
}
