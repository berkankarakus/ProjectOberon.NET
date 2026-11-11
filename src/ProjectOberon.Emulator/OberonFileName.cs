namespace ProjectOberon.Emulator;

internal static class OberonFileNameRules
{
    public static void Validate(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length >= 32 || !char.IsAsciiLetter(name[0]) ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
        {
            throw new ArgumentException($"invalid Oberon file name: {name}", nameof(name));
        }
    }
}
