using System;
using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Game
{
    // NPC speech, driven entirely by Sounds.txt entries that point at the voice files in d2speech/d2xtalk
    // (data\local\sfx\ActN\<npc>\*.wav). Sound names follow the pattern <voice>_act<N>_<kind>,
    // e.g. akara_act1_intro, akara_act1_gossip_3, akara_act1_q1_init.
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

        // The sound-name prefix of an NPC. Several monstats rows share one voice (warriv1/warriv2, cain1..cain6)
        public static string VoiceOf(Unit npc)
        {
            if (npc == null || npc.monStat == null || npc.monStat.id == null)
                return null;
            return VoiceName(npc.monStat);
        }

        private static string VoiceName(MonStat monStat)
        {
            string id = monStat.id.ToLowerInvariant();
            if (id.StartsWith("cain"))
                return "cain";
            if (id.StartsWith("warriv"))
                return "warriv";
            if (id == "drehya")
                return "anya";
            return id;
        }

        private static string[] Prefixes(MonStat monStat)
        {
            string id = monStat.id.ToLowerInvariant();
            if (id == "jamella")
                return new[] { "jamella_old", "jamella_young", "jamella" };
            return new[] { VoiceName(monStat) };
        }

        public static int CurrentAct()
        {
            var level = Level.Current;
            return level != null ? level.act + 1 : 1;
        }

        // Class specific lines carry a three letter suffix: _ama _sor _nec _pal _bar (and _dru _ass in the expansion)
        public static string ClassSuffix(Player player)
        {
            if (player == null || player.charStatInfo == null || player.charStatInfo.code == null)
                return null;
            switch (player.charStatInfo.code.ToLowerInvariant())
            {
                case "am": return "ama";
                case "so": return "sor";
                case "ne": return "nec";
                case "pa": return "pal";
                case "ba": return "bar";
                case "dr": return "dru";
                case "as": return "ass";
                default: return null;
            }
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

        public static bool Exists(string soundName)
        {
            return SoundInfo.Find(soundName) != null;
        }

        private static TalkState GetState(string voice, int act)
        {
            string key = voice + "|" + act;
            if (!States.TryGetValue(key, out var state))
            {
                state = new TalkState();
                States[key] = state;
            }
            return state;
        }

        public static bool IntroPlayed(Unit npc)
        {
            string voice = VoiceOf(npc);
            return voice != null && GetState(voice, CurrentAct()).introPlayed;
        }

        // Greeting: played when the NPC menu opens
        public static AudioSource PlayGreeting(Unit npc)
        {
            if (npc == null || npc.monStat == null || npc.monStat.id == null)
                return null;

            var candidates = new List<SoundInfo>();
            int act = CurrentAct();
            string voice = VoiceName(npc.monStat);
            if (voice == "cain")
            {
                var actGreeting = SoundInfo.Find("cain_act" + act + "_greeting");
                if (actGreeting != null)
                    candidates.Add(actGreeting);
            }

            foreach (var prefix in Prefixes(npc.monStat))
                CollectNumbered(candidates, prefix + "_greeting_");

            // Not every NPC has plain greetings (Warriv only has the inactive and time of day ones)
            if (candidates.Count == 0)
            {
                foreach (var prefix in Prefixes(npc.monStat))
                    CollectNumbered(candidates, prefix + "_greeting_inactive_");
            }

            // Time of day greetings: 1 morning, 2 day, 3 evening
            int hour = DateTime.Now.Hour;
            int timeIndex = hour < 12 ? 1 : (hour < 18 ? 2 : 3);
            var timeGreeting = SoundInfo.Find(voice + "_greeting_time_" + timeIndex);
            if (timeGreeting != null)
                candidates.Add(timeGreeting);

            // Someone we already talked to this session welcomes us back
            if (GetState(voice, act).introPlayed)
            {
                var back = SoundInfo.Find(voice + "_greeting_return");
                if (back != null)
                    candidates.Add(back);
            }

            if (candidates.Count == 0)
                return null;
            return Say(npc, candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }

        // Plays one specific Sounds.txt entry through the NPC, null if there is no such sound
        public static AudioSource PlayNamed(Unit npc, string soundName)
        {
            var sound = SoundInfo.Find(soundName);
            if (sound == null)
                return null;
            return Say(npc, sound);
        }

        // Talk without a quest line pending: the introduction first (class specific when there is one),
        // then the gossip lines in random order without repeats
        public static AudioSource PlayTalk(Unit npc, Player player)
        {
            if (npc == null || npc.monStat == null || npc.monStat.id == null)
                return null;

            string voice = VoiceName(npc.monStat);
            int act = CurrentAct();
            var state = GetState(voice, act);
            string suffix = ClassSuffix(player);
            string actStem = voice + "_act" + act + "_";

            if (!state.introPlayed)
            {
                state.introPlayed = true;
                SoundInfo intro = null;
                if (suffix != null)
                    intro = SoundInfo.Find(actStem + "intro_" + suffix);
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
                if (suffix != null)
                {
                    var classGossip = SoundInfo.Find(actStem + "gossip_" + suffix);
                    if (classGossip != null)
                        state.bag.Add(classGossip);
                }

                if (state.bag.Count > 1 && state.last != null)
                    state.bag.Remove(state.last);
            }

            if (state.bag.Count == 0)
                return PlayGreeting(npc);

            int index = UnityEngine.Random.Range(0, state.bag.Count);
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
