namespace GafiFish.App.Settings;

public sealed record AppHotkeySettings
{
    public string CaptureKey { get; init; } = "F6";
    public string ObserveKey { get; init; } = "F7";
    public string StartKey { get; init; } = "F8";
    public string StopKey { get; init; } = "F9";
    public string ToggleUiKey { get; init; } = "F10";
    public bool HideUiWhileRunning { get; init; } = true;

    public void Validate()
    {
        var keys = new[] { CaptureKey, ObserveKey, StartKey, StopKey, ToggleUiKey };
        var allowed = Enumerable.Range(2, 11).Select(number => $"F{number}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (keys.Any(key => string.IsNullOrWhiteSpace(key) || !allowed.Contains(key)))
            throw new ArgumentException("Hotkeys must be function keys F2 through F12.");
        if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            throw new ArgumentException("Each GafiFish action must use a different hotkey.");
    }
}
