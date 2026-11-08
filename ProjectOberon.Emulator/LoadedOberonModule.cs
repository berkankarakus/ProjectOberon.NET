namespace ProjectOberon.Emulator;

public sealed class LoadedOberonModule
{
    internal LoadedOberonModule(
        string name,
        int number,
        RscModule definition,
        IReadOnlyList<LoadedOberonModule> imports,
        bool isExternal = false)
    {
        Name = name;
        Number = number;
        Definition = definition;
        Imports = imports;
        IsExternal = isExternal;
    }

    public string Name { get; }

    public int Number { get; }

    public RscModule Definition { get; }

    public IReadOnlyList<LoadedOberonModule> Imports { get; }

    public int ReferenceCount { get; internal set; }

    public bool IsExternal { get; }

    public RscCommand? FindCommand(string name)
    {
        return Definition.Commands.FirstOrDefault(command => command.Name == name);
    }
}
