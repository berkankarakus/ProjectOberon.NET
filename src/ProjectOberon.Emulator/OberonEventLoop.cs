namespace ProjectOberon.Emulator;

public sealed class OberonEventLoop
{
    public const char Escape = '\u001b';
    public const char SetStar = '\u001a';

    private readonly OberonViewerSystem _viewers;
    private bool _hasMousePosition;
    private int _lastMouseX;
    private int _lastMouseY;

    public OberonEventLoop(OberonViewerSystem viewers)
    {
        _viewers = viewers ?? throw new ArgumentNullException(nameof(viewers));
    }

    public bool ProcessCharacter(char character, int mouseX, int mouseY)
    {
        if (character == Escape)
        {
            _viewers.Broadcast(new OberonControlMessage(OberonControlMessageKind.Neutralize));
            return true;
        }

        if (character == SetStar)
        {
            return _viewers.DispatchControl(
                mouseX,
                mouseY,
                new OberonControlMessage(OberonControlMessageKind.Mark)
                {
                    X = mouseX,
                    Y = mouseY
                });
        }

        return ProcessConsumedCharacter(character);
    }

    public bool ProcessConsumedCharacter(char character)
    {
        return _viewers.DispatchKeyboard(new OberonInputMessage(OberonInputMessageKind.Consume)
        {
            Character = character
        });
    }

    public bool ProcessConsumedKey(int keyCode, ushort modifiers)
    {
        var input = new OberonInputMessage(OberonInputMessageKind.Consume)
        {
            KeyCode = keyCode,
            Modifiers = modifiers
        };
        _viewers.DispatchKeyboard(input);
        return input.Handled;
    }

    public bool ProcessMouse(uint keys, int x, int y, bool extendSelection = false)
    {
        _lastMouseX = x;
        _lastMouseY = y;
        _hasMousePosition = true;
        if (keys != 0)
        {
            var viewer = _viewers.HitTest(x, y);
            if (viewer is not null)
            {
                _viewers.PassFocus(viewer);
            }
        }

        return _viewers.DispatchInput(x, y, new OberonInputMessage(OberonInputMessageKind.Track)
        {
            Keys = keys,
            X = x,
            Y = y,
            ExtendSelection = extendSelection
        });
    }

    public bool ProcessIdleMouse(int x, int y)
    {
        if (_hasMousePosition && _lastMouseX == x && _lastMouseY == y)
        {
            return false;
        }

        return ProcessMouse(keys: 0, x, y);
    }
}
