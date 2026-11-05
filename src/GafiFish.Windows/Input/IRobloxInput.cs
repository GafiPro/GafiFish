using GafiFish.Windows.Capture;

namespace GafiFish.Windows.Input;

public interface IRobloxInput
{
    bool IsArmed { get; }
    void Arm();
    void SetLeftButton(bool isDown);
    void SetKey(ushort virtualKey, bool isDown);
    void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame);
    void Disarm();
}
