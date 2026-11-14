namespace ProjectOberon.SdlUi;

internal sealed class UiLog : IDisposable
{
    private readonly StreamWriter? _writer;

    private UiLog(string? path)
    {
        if (path is not null)
        {
            _writer = new StreamWriter(path, append: false)
            {
                AutoFlush = true
            };
        }
    }

    public static UiLog Open(string? path) => new(path);

    public void Write(string message)
    {
        _writer?.WriteLine($"{DateTimeOffset.UtcNow:O} {message}");
    }

    public void WriteError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Write($"error type={error.GetType().FullName} message={error.Message}");
    }

    public void Dispose() => _writer?.Dispose();
}
