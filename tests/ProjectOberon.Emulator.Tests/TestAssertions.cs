internal static class TestAssertions
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"expected {expected}, got {actual}");
        }
    }

    public static void True(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("expected true");
        }
    }

    public static void False(bool value)
    {
        if (value)
        {
            throw new InvalidOperationException("expected false");
        }
    }

    public static void ThrowsInvalidData(Action action)
    {
        Throws<InvalidDataException>(action);
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException error)
        {
            return error;
        }

        throw new InvalidOperationException($"expected {typeof(TException).Name}");
    }
}
