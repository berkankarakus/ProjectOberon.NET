namespace ProjectOberon.Emulator;

internal static class RscModuleValidation
{
    public static void ValidateCodeLayout(
        int codeWords,
        IReadOnlyList<RscCommand> commands,
        int bodyOffset)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var codeBytes = checked(codeWords * sizeof(uint));
        if (bodyOffset < 0 || bodyOffset > codeBytes || bodyOffset % sizeof(uint) != 0)
        {
            throw new InvalidDataException($"invalid RSC body offset: {bodyOffset}");
        }

        foreach (var command in commands)
        {
            if (command.CodeOffset < 0 || command.CodeOffset >= codeBytes
                || command.CodeOffset % sizeof(uint) != 0)
            {
                throw new InvalidDataException(
                    $"invalid RSC command offset: {command.Name}={command.CodeOffset}");
            }
        }
    }
}
