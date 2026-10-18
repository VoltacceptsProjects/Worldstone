using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Game
{
    public static class NpcVoice
    {
        private const int MaxLines = 20;

        private class TalkState
        {
            public bool introPlayed;
            public readonly List<SoundInfo> bag = new List<SoundInfo>();
            public SoundInfo last;
        }

        private static readonly Dictionary<string, TalkState> States = new Dictionary<string, TalkState>();

        // Voice names
        private static string[] Prefixes(MonStat monStat)
        {
            string id = monStat.id.ToLowerInvariant();
            if (id.StartsWith("cain"))
                return new[] { "cain" };
            if (id == "drehya")
                return new[] { "anya" };
            if (id == "jamella")
                return new[] { "jamella_old", "jamella_young", "jamella" };
            return new[] { id };
        }

        private static string VoiceName(MonStat monStat)
        {
            string id = monStat.id.ToLowerInvariant();
            if (id.StartsWith("cain"))
                return "cain";
            if (id == "drehya")
                return "anya";
            return id;
        }

        private static int CurrentAct()
        {
            var level = Level.Current;
            return level != null ? level.act + 1 : 1;
        }

        private static bool IsNecromancer(Player player)
        {
            return player != null && player.charStatInfo != null && player.charStatInfo.code != null &&
                   player.charStatInfo.code.Equals("ne", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectNumbered(List<SoundInfo> result, string stem)
        {
            for (int i = 1; i <= MaxLines; ++i)
            {
                var sound = SoundInfo.Find(stem + i);
                if (sound == null)
                    break;
                result.Add(sound);
            }
        }

        // Greeting
        public static AudioSource PlayGreeting(Unit npc)
        {
            if (npc == null || npc.monStat == null || npc.monStat.id == null)
                return null;

            var candidates = new List<SoundInfo>();
            int act = CurrentAct();
            if (VoiceName(npc.monStat) == "cain")
            {
                var actGreeting = SoundInfo.Find("cain_act" + act + "_greeting");
                if (actGreeting != null)
                    candidates.Add(actGreeting);
            }

            foreach (var prefix in Prefixes(npc.monStat))
                CollectNumbered(candidates, prefix + "_greeting_");

            if (candidates.Count == 0)
                return null;
            return Say(npc, candidates[Random.Range(0, candidates.Count)]);
        }

        // Talk: the introduction first, then the gossip lines in random order without repeats
        public static AudioSource PlayTalk(Unit npc, Player player)
        {
            if (npc == null || npc.monStat == null || npc.monStat.id == null)
                return null;

            string voice = VoiceName(npc.monStat);
            int act = CurrentAct();
            string key = voice + "|" + act;
            if (!States.TryGetValue(key, out var state))
            {
                state = new TalkState();
                States[key] = state;
            }

            bool necromancer = IsNecromancer(player);
            string actStem = voice + "_act" + act + "_";

            if (!state.introPlayed)
            {
                state.introPlayed = true;
                SoundInfo intro = null;
                if (necromancer)
                    intro = SoundInfo.Find(actStem + "intro_nec");
                if (intro == null)
                    intro = SoundInfo.Find(actStem + "intro");
                if (intro != null)
                {
                    state.last = intro;
                    return Say(npc, intro);
                }
            }

            if (state.bag.Count == 0)
            {
                CollectNumbered(state.bag, actStem + "gossip_");
                if (necromancer)
                {
                    var necroGossip = SoundInfo.Find(actStem + "gossip_nec");
                    if (necroGossip != null)
                        state.bag.Add(necroGossip);
                }

                if (state.bag.Count > 1 && state.last != null)
                    state.bag.Remove(state.last);
            }

            if (state.bag.Count == 0)
                return PlayGreeting(npc);

            int index = Random.Range(0, state.bag.Count);
            var line = state.bag[index];
            state.bag.RemoveAt(index);
            state.last = line;
            return Say(npc, line);
        }

        private static AudioSource Say(Unit npc, SoundInfo sound)
        {
            if (AudioManager.instance == null)
                return null;
            return AudioManager.instance.Play(sound, npc.transform);
        }

        public static void Reset()
        {
            States.Clear();
        }
    }
}
