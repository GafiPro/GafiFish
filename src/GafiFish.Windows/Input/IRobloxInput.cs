using GafiFish.Windows.Capture;

namespace GafiFish.Windows.Input;

public interface IRobloxInput
{
    bool IsArmed { get; }
    void Arm();
    void SetLeftButton(bool isDown);
    void SetKey(ushort virtualKey, bool isDown);
    void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame);

    // Optional cursor preservation hooks. Real Windows input implements both;
    // test adapters can leave the default snapshot unavailable.
    (int X, int Y)? GetCursorClientPosition(ScreenCaptureFrame frame) => null;
    void MoveClient(int clientX, int clientY, ScreenCaptureFrame frame) =>
        throw new NotSupportedException("Cursor repositioning is not supported by this input adapter.");

    void Disarm();
}
