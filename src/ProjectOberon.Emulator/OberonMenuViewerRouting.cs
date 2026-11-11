namespace ProjectOberon.Emulator;

public sealed partial class OberonMenuViewer
{
    private void HandleViewerMessage(OberonViewer viewer, OberonViewerMessage message)
    {
        switch (message.Kind)
        {
            case OberonViewerMessageKind.Restore:
                LayoutChildren(OberonFrameMessageKind.Restore);
                break;
            case OberonViewerMessageKind.Modify:
                var kind = Viewer.Height >= _lastViewerHeight
                    ? OberonFrameMessageKind.Extend
                    : OberonFrameMessageKind.Reduce;
                LayoutChildren(kind);
                break;
            case OberonViewerMessageKind.Suspend:
                SuspendChildren();
                break;
            case OberonViewerMessageKind.Input:
                RouteInput(message);
                break;
            case OberonViewerMessageKind.Control:
                RouteControl(message);
                break;
        }
    }

    private void RouteInput(OberonViewerMessage message)
    {
        if (message.Payload is not OberonInputMessage input)
        {
            return;
        }

        var frameMessage = new OberonFrameMessage(OberonFrameMessageKind.Input)
        {
            X = input.X,
            Y = input.Y,
            Payload = input
        };
        if (input.Kind == OberonInputMessageKind.Consume || IsInsideMain(input.X, input.Y))
        {
            Main.Handle(frameMessage);
        }
        else
        {
            Menu.Handle(frameMessage);
        }
    }

    private void RouteControl(OberonViewerMessage message)
    {
        var frameMessage = new OberonFrameMessage(OberonFrameMessageKind.Control)
        {
            X = message.X,
            Y = message.Y,
            Payload = message.Payload
        };
        Menu.Handle(frameMessage);
        Main.Handle(frameMessage);
    }

    private static OberonFrameMessage CreateInputMessage(int x, int y)
    {
        return new OberonFrameMessage(OberonFrameMessageKind.Input)
        {
            X = x,
            Y = y
        };
    }
}
