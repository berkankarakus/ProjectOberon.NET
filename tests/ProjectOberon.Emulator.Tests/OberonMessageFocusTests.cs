using ProjectOberon.Emulator;

internal static partial class OberonMessageTests
{
    private static void PassesFocusAndDefocusesThePreviousViewer()
    {
        var controlMessages = new List<(string Name, OberonControlMessageKind Kind)>();
        var keyboardMessages = new List<(string Name, char Character)>();
        var system = CreateSystem();
        var first = CreateRecordingViewer("first", controlMessages, keyboardMessages);
        var second = CreateRecordingViewer("second", controlMessages, keyboardMessages);
        system.Open(first, 10, 50);
        system.Open(second, 10, 90);

        TestAssertions.True(system.PassFocus(first));
        TestAssertions.True(system.PassFocus(second));
        TestAssertions.Equal(second, system.FocusViewer);
        TestAssertions.Equal(("first", OberonControlMessageKind.Defocus), controlMessages[0]);

        TestAssertions.True(system.DispatchKeyboard(new OberonInputMessage(OberonInputMessageKind.Consume)
        {
            Character = 'A'
        }));
        TestAssertions.Equal(("second", 'A'), keyboardMessages[0]);
        TestAssertions.True(system.Close(second));
        TestAssertions.Equal(null, system.FocusViewer);
    }
}
