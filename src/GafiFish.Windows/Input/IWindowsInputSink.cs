namespace GafiFish.Windows.Input;

/// <summary>
/// Injectable boundary around Windows input APIs.
/// </summary>
public interface IWindowsInputSink
{
    void SetLeftButton(bool isDown);
    void MoveCursor(int screenX, int screenY);
    void SetKey(ushort virtualKey, bool isDown);
}
