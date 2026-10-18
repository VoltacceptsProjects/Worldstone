namespace Diablerie.Engine.SaveGame
{
    // Naming rules follow Diablo II: 2-15 characters, letters only, plus at most one
    // dash or underscore which can't be the first or last character.
    public static class CharacterNames
    {
        public const int MinLength = 2;
        public const int MaxLength = 15;

        public static bool IsAllowedChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-' || c == '_';
        }

        // Returns an error message, or null if the name is valid.
        public static string Validate(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < MinLength)
                return "Name must be at least " + MinLength + " characters.";
            if (name.Length > MaxLength)
                return "Name must be at most " + MaxLength + " characters.";

            int separators = 0;
            foreach (char c in name)
            {
                if (!IsAllowedChar(c))
                    return "Only letters, one dash or one underscore.";
                if (c == '-' || c == '_')
                    separators++;
            }

            if (separators > 1)
                return "Only one dash or underscore is allowed.";
            if (name[0] == '-' || name[0] == '_' || name[name.Length - 1] == '-' || name[name.Length - 1] == '_')
                return "Name can't start or end with a dash or underscore.";

            return null;
        }
    }
}
