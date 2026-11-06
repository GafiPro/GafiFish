namespace GafiFish.Core.Scheduling;

public enum TimedHotbarActionKind { Totem, Potion }

public sealed record TimedHotbarAction(TimedHotbarActionKind Kind, int HotbarSlot, TimeSpan Interval)
{
    public void Validate()
    {
        if (HotbarSlot is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(HotbarSlot));
        if (Interval < TimeSpan.FromMinutes(1) || Interval > TimeSpan.FromMinutes(720))
            throw new ArgumentOutOfRangeException(nameof(Interval));
    }
}

public sealed class TimedHotbarActionSchedule
{
    private sealed record Entry(TimedHotbarAction Action, DateTimeOffset NextUseAt);
    private readonly Dictionary<TimedHotbarActionKind, Entry> _entries = [];
    private readonly TimeSpan _minimumActionGap;
    private DateTimeOffset _nextActionAllowedAt;

    public TimedHotbarActionSchedule(IEnumerable<TimedHotbarAction> actions, DateTimeOffset startedAt, TimeSpan? minimumActionGap = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _minimumActionGap = minimumActionGap ?? TimeSpan.FromSeconds(2);
        if (_minimumActionGap < TimeSpan.Zero || _minimumActionGap > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(minimumActionGap));
        foreach (var action in actions)
        {
            ArgumentNullException.ThrowIfNull(action);
            action.Validate();
            if (!_entries.TryAdd(action.Kind, new Entry(action, startedAt + action.Interval)))
                throw new ArgumentException($"Only one action of kind {action.Kind} can be configured.", nameof(actions));
        }
        if (_entries.Count == 0) throw new ArgumentException("At least one action is required.", nameof(actions));
    }

    public TimedHotbarAction? TryGetDue(DateTimeOffset now, bool isSafeState)
    {
        if (!isSafeState || now < _nextActionAllowedAt) return null;
        return _entries.Values.Where(entry => now >= entry.NextUseAt)
            .OrderBy(entry => entry.NextUseAt)
            .ThenBy(entry => entry.Action.Kind)
            .Select(entry => entry.Action)
            .FirstOrDefault();
    }

    public void MarkExecuted(TimedHotbarAction action, DateTimeOffset executedAt)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_entries.TryGetValue(action.Kind, out var entry) || entry.Action != action)
            throw new ArgumentException("The action does not belong to this schedule.", nameof(action));
        if (executedAt < entry.NextUseAt)
            throw new InvalidOperationException("Action cannot be marked executed before it is due.");
        _entries[action.Kind] = entry with { NextUseAt = executedAt + action.Interval };
        _nextActionAllowedAt = executedAt + _minimumActionGap;
    }
}
