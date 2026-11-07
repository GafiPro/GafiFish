namespace GafiFish.Core.Automation;

/// <summary>Persistable Angler request state, including the hand-in cooldown.</summary>
public sealed record AnglerQuestSnapshot(
    string? TargetSpecies,
    DateTimeOffset? TargetStartedAt,
    DateTimeOffset? TargetCaughtAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? NextRequestAvailableAt)
{
    public static AnglerQuestSnapshot Empty { get; } = new(null, null, null, null, null);

    public void Validate()
    {
        if (TargetSpecies is null)
        {
            if (TargetStartedAt.HasValue || TargetCaughtAt.HasValue || SubmittedAt.HasValue || NextRequestAvailableAt.HasValue)
                throw new ArgumentException("An empty Angler quest cannot contain timestamps.");
            return;
        }
        if (string.IsNullOrWhiteSpace(TargetSpecies) || TargetSpecies.Trim().Length > 100)
            throw new ArgumentException("The saved Angler target must contain 1–100 non-whitespace characters.");
        // TargetStartedAt is optional only for compatibility with state files
        // written before start timestamps were persisted. Restore maps that
        // legacy state to DateTimeOffset.MinValue, which still rejects no valid
        // historical OCR frame from a new request.
        if (!TargetCaughtAt.HasValue)
        {
            if (SubmittedAt.HasValue || NextRequestAvailableAt.HasValue)
                throw new ArgumentException("A quest cannot be submitted before its target fish is caught.");
            return;
        }
        if (!SubmittedAt.HasValue)
        {
            if (NextRequestAvailableAt.HasValue)
                throw new ArgumentException("A cooldown cannot exist before hand-in is confirmed.");
            return;
        }
        if (SubmittedAt.Value < TargetCaughtAt.Value)
            throw new ArgumentException("Saved hand-in time cannot be earlier than the target catch.");
        var expectedReadyAt = SubmittedAt.Value + AnglerQuestTracker.RequestCooldown;
        if (NextRequestAvailableAt != expectedReadyAt)
            throw new ArgumentException("Saved Angler cooldown timestamp is inconsistent.");
    }
}

/// <summary>
/// Tracks one Angler request. The fish must be caught and handed in manually;
/// the 120-second request cooldown begins only when the user confirms hand-in.
/// </summary>
public sealed class AnglerQuestTracker
{
    public static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(120);

    private string? _targetSpecies;
    private DateTimeOffset? _targetStartedAt;
    private DateTimeOffset? _targetCaughtAt;
    private DateTimeOffset? _submittedAt;
    private DateTimeOffset? _nextRequestAvailableAt;

    public string? TargetSpecies => _targetSpecies;
    public DateTimeOffset? TargetStartedAt => _targetStartedAt;
    public bool IsTracking => !string.IsNullOrWhiteSpace(_targetSpecies)
        && !_targetCaughtAt.HasValue
        && !_submittedAt.HasValue;
    public bool HasCatchToSubmit => _targetCaughtAt.HasValue && !_submittedAt.HasValue;
    public bool IsInCooldown => _submittedAt.HasValue && _nextRequestAvailableAt.HasValue;
    public DateTimeOffset? NextRequestAvailableAt => _nextRequestAvailableAt;

    public bool CanStartNewQuest(DateTimeOffset now) =>
        !HasCatchToSubmit && (!IsInCooldown || now >= _nextRequestAvailableAt);

    public void StartTarget(string targetSpecies, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(targetSpecies) || targetSpecies.Trim().Length > 100)
            throw new ArgumentException("Enter a valid Angler target fish name (1–100 characters).", nameof(targetSpecies));
        if (!CanStartNewQuest(now))
            throw new InvalidOperationException(GetStatus(now));

        _targetSpecies = targetSpecies.Trim();
        _targetStartedAt = now;
        _targetCaughtAt = null;
        _submittedAt = null;
        _nextRequestAvailableAt = null;
    }

    public bool MarkTargetFishCaught(string species, DateTimeOffset capturedAt)
    {
        if (!IsTracking || !MatchesSpecies(species, _targetSpecies!)
            || (_targetStartedAt.HasValue && capturedAt < _targetStartedAt.Value))
            return false;
        _targetCaughtAt = capturedAt;
        return true;
    }

    public void ConfirmQuestSubmitted(DateTimeOffset submittedAt)
    {
        if (!HasCatchToSubmit)
            throw new InvalidOperationException("Catch the requested fish first. The cooldown only starts after you hand it to the Angler.");
        if (_targetCaughtAt.HasValue && submittedAt < _targetCaughtAt.Value)
            throw new ArgumentOutOfRangeException(nameof(submittedAt), "Hand-in time cannot be earlier than the target catch.");

        _submittedAt = submittedAt;
        _nextRequestAvailableAt = submittedAt + RequestCooldown;
    }

    public bool IsReadyForNextQuest(DateTimeOffset now) =>
        IsInCooldown && _nextRequestAvailableAt.HasValue && now >= _nextRequestAvailableAt.Value;

    public TimeSpan CooldownRemaining(DateTimeOffset now)
    {
        if (!IsInCooldown || !_nextRequestAvailableAt.HasValue)
            return TimeSpan.Zero;
        var remaining = _nextRequestAvailableAt.Value - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public string GetStatus(DateTimeOffset now)
    {
        if (HasCatchToSubmit)
            return $"Target caught: {_targetSpecies}. Hand it in to the Angler manually, then confirm below to start the 2-minute cooldown.";
        if (IsInCooldown)
        {
            var remaining = CooldownRemaining(now);
            return remaining > TimeSpan.Zero
                ? $"Angler request cooldown: {remaining.Minutes:D2}:{remaining.Seconds:D2} remaining."
                : "Cooldown complete. You can request another Angler quest manually.";
        }
        if (IsTracking)
            return $"Fishing for Angler target: {_targetSpecies}. Waiting for a high-confidence OCR match.";
        return "Angler Quest assist is ready. Accept a quest manually and enter its requested fish.";
    }

    public AnglerQuestSnapshot CreateSnapshot() => new(
        _targetSpecies, _targetStartedAt, _targetCaughtAt, _submittedAt, _nextRequestAvailableAt);

    public void Restore(AnglerQuestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        _targetSpecies = string.IsNullOrWhiteSpace(snapshot.TargetSpecies) ? null : snapshot.TargetSpecies.Trim();
        _targetStartedAt = snapshot.TargetStartedAt ?? (snapshot.TargetSpecies is null ? null : DateTimeOffset.MinValue);
        _targetCaughtAt = snapshot.TargetCaughtAt;
        _submittedAt = snapshot.SubmittedAt;
        _nextRequestAvailableAt = snapshot.NextRequestAvailableAt;
    }

    public void Reset()
    {
        _targetSpecies = null;
        _targetStartedAt = null;
        _targetCaughtAt = null;
        _submittedAt = null;
        _nextRequestAvailableAt = null;
    }

    private static bool MatchesSpecies(string actual, string expected) =>
        Normalize(actual) == Normalize(expected);

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
