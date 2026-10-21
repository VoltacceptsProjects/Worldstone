using System;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.SaveGame;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Couch co-op: player 1 plays with mouse and keyboard as always, player 2 joins with a gamepad
    // (Start button) or with the keyboard (F9). Everything player 2 related that can be tuned is here.
    public static class CoopSettings
    {
        private const string ClassKey = "coop.player2.class";

        // Set once player 2 joined and kept for the rest of the session, so a new game starts with both players
        public static bool Enabled;

        private static readonly string[] DefaultClassOrder =
        {
            "Barbarian", "Necromancer", "Sorceress", "Paladin", "Amazon", "Druid", "Assassin"
        };

        // The class player 2 plays, empty means "something that differs from player 1"
        public static string Player2Class
        {
            get => PlayerPrefs.GetString(ClassKey, "");
            set
            {
                PlayerPrefs.SetString(ClassKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        private const string CharacterKey = "coop.player2.character";

        // The saved character player 2 plays, empty means "the most recently played one that is not player 1"
        public static string Player2Character
        {
            get => PlayerPrefs.GetString(CharacterKey, "");
            set
            {
                PlayerPrefs.SetString(CharacterKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        // The save player 2 plays. With a name: that character. Without: the remembered one, else the latest
        // one that is not player 1. Null means there is none and a new character has to be made.
        public static CharacterSave ResolvePlayer2Save(string player1Name, string name)
        {
            if (!string.IsNullOrEmpty(name))
                return FindSave(name, player1Name);

            var remembered = FindSave(Player2Character, player1Name);
            if (remembered != null)
                return remembered;

            foreach (var info in SaveManager.List())
            {
                var save = FindSave(info.name, player1Name);
                if (save != null)
                    return save;
            }

            return null;
        }

        private static CharacterSave FindSave(string name, string player1Name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var info in SaveManager.List())
            {
                if (!string.Equals(info.name, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(info.name, player1Name, StringComparison.OrdinalIgnoreCase))
                    return null;
                return SaveManager.Load(info.name);
            }
            return null;
        }

        public static string NewCharacterName(string className)
        {
            string name = className;
            for (int i = 2; SaveManager.NameExists(name); ++i)
                name = className + " " + i;
            return name;
        }

        public static string ResolvePlayer2Class(string player1Class)
        {
            string wanted = Player2Class;
            if (!string.IsNullOrEmpty(wanted) && CharStatsInfo.Find(wanted) != null)
                return wanted;

            foreach (var candidate in DefaultClassOrder)
            {
                if (candidate != player1Class && CharStatsInfo.Find(candidate) != null)
                    return candidate;
            }

            return player1Class;
        }

        // "sorc" is not enough, but "sorceress" in any case is
        public static string FindClass(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var info = CharStatsInfo.sheet.Find(c => string.Equals(c.className, text, StringComparison.OrdinalIgnoreCase));
            return info != null ? info.className : null;
        }

        // Player 2 has no skill panel, so every class comes with four skills for the face buttons: A, X, B, Y.
        // Names are the ones of Skills.txt, the same that the main player picks from.
        public static string[] LoadoutFor(string className)
        {
            switch (className)
            {
                case "Sorceress":
                    return new[] { "Fire Bolt", "Charged Bolt", "Frost Nova", "Frozen Orb" };
                case "Necromancer":
                    return new[] { "Attack", "Raise Skeleton", "Clay Golem", "Raise Skeletal Mage" };
                case "Barbarian":
                    return new[] { "Attack", "War Cry", "Battle Cry", "Battle Orders" };
                case "Druid":
                    return new[] { "Attack", "Summon Spirit Wolf", "Summon Grizzly", "Raven" };
                case "Assassin":
                    return new[] { "Attack", "Charged Bolt Sentry", "Lightning Sentry", "Inferno Sentry" };
                default:
                    // Amazon and Paladin: the main player's skill list has nothing for them yet
                    return new[] { "Attack" };
            }
        }
    }
}
