internal static partial class OberonMessageTests
{
    public static void Run()
    {
        DispatchesTypedInputToTheHitViewer();
        PassesFocusAndDefocusesThePreviousViewer();
        RoutesAndBroadcastsControlMessages();
    }
}
