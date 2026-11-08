namespace ProjectOberon.Emulator;

public sealed class OberonModuleLoadException : Exception
{
    public OberonModuleLoadException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
