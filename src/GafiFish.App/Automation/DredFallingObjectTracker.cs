using GafiFish.Vision.Detection;

namespace GafiFish.App.Automation;

/// <summary>
/// Tracks calibrated D.R.E.D. object candidates across distinct frames and
/// reports only candidates that move down consistently. This is still visual
/// evidence of motion, not proof that an object was collected.
/// </summary>
public sealed class DredFallingObjectTracker
{
    private sealed class Track(
        DetectedMarker marker,
        DateTimeOffset capturedAt)
    {
        public DetectedMarker Marker { get; set; } = marker;
        public DateTimeOffset LastSeenAt { get; set; } = capturedAt;
        public int DownwardSteps { get; set; }
        public double DownwardDisplacement { get; set; }
    }

    private readonly List<Track> _tracks = [];
    private readonly int _requiredDownwardSteps;
    private readonly double _minimumDownwardStepPixels;
    private readonly double _minimumTotalDownwardPixels;
    private readonly double _maximumMatchDistancePixels;
    private readonly TimeSpan _maximumFrameGap;
    private DateTimeOffset? _lastProcessedAt;

    public DredFallingObjectTracker(
        int requiredDownwardSteps = 2,
        double minimumDownwardStepPixels = 1.5,
        double minimumTotalDownwardPixels = 5,
        double maximumMatchDistancePixels = 64,
        TimeSpan? maximumFrameGap = null)
    {
        if (requiredDownwardSteps is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredDownwardSteps));
        if (!double.IsFinite(minimumDownwardStepPixels)
            || minimumDownwardStepPixels is <= 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(minimumDownwardStepPixels));
        if (!double.IsFinite(minimumTotalDownwardPixels)
            || minimumTotalDownwardPixels < minimumDownwardStepPixels
            || minimumTotalDownwardPixels > 250)
            throw new ArgumentOutOfRangeException(nameof(minimumTotalDownwardPixels));
        if (!double.IsFinite(maximumMatchDistancePixels)
            || maximumMatchDistancePixels < minimumDownwardStepPixels
            || maximumMatchDistancePixels > 500)
            throw new ArgumentOutOfRangeException(nameof(maximumMatchDistancePixels));

        _maximumFrameGap = maximumFrameGap ?? TimeSpan.FromMilliseconds(500);
        if (_maximumFrameGap < TimeSpan.FromMilliseconds(50)
            || _maximumFrameGap > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(maximumFrameGap));

        _requiredDownwardSteps = requiredDownwardSteps;
        _minimumDownwardStepPixels = minimumDownwardStepPixels;
        _minimumTotalDownwardPixels = minimumTotalDownwardPixels;
        _maximumMatchDistancePixels = maximumMatchDistancePixels;
    }

    public int ActiveTrackCount => _tracks.Count;

    /// <summary>
    /// Returns only candidates that have moved downward over enough distinct
    /// observations. Duplicate/out-of-order frames never advance a track.
    /// </summary>
    public IReadOnlyList<DetectedMarker> ProcessObservation(
        DateTimeOffset capturedAt,
        IReadOnlyList<DetectedMarker> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (_lastProcessedAt.HasValue && capturedAt <= _lastProcessedAt.Value)
            return [];

        _lastProcessedAt = capturedAt;
        _tracks.RemoveAll(track => capturedAt - track.LastSeenAt > _maximumFrameGap);

        var matchedTracks = new HashSet<Track>();
        var falling = new List<DetectedMarker>();

        foreach (var candidate in candidates
            .Where(IsValidCandidate)
            .OrderByDescending(marker => marker.Confidence)
            .ThenByDescending(marker => marker.Area))
        {
            var match = FindClosestTrack(candidate, capturedAt, matchedTracks);
            if (match is null)
            {
                var newTrack = new Track(candidate, capturedAt);
                _tracks.Add(newTrack);
                matchedTracks.Add(newTrack);
                continue;
            }

            matchedTracks.Add(match);
            var verticalDelta = candidate.CenterY - match.Marker.CenterY;

            if (verticalDelta >= _minimumDownwardStepPixels)
            {
                match.DownwardSteps++;
                match.DownwardDisplacement += verticalDelta;
            }
            else if (verticalDelta <= -_minimumDownwardStepPixels)
            {
                // An upward-moving or reappearing marker invalidates the
                // previous fall estimate instead of carrying stale momentum.
                match.DownwardSteps = 0;
                match.DownwardDisplacement = 0;
            }

            match.Marker = candidate;
            match.LastSeenAt = capturedAt;

            if (match.DownwardSteps >= _requiredDownwardSteps
                && match.DownwardDisplacement >= _minimumTotalDownwardPixels)
            {
                falling.Add(candidate);
            }
        }

        return falling;
    }

    public void Reset()
    {
        _tracks.Clear();
        _lastProcessedAt = null;
    }

    private Track? FindClosestTrack(
        DetectedMarker candidate,
        DateTimeOffset capturedAt,
        HashSet<Track> alreadyMatched)
    {
        Track? closest = null;
        var closestDistance = _maximumMatchDistancePixels;

        foreach (var track in _tracks)
        {
            if (alreadyMatched.Contains(track)
                || capturedAt - track.LastSeenAt > _maximumFrameGap)
                continue;

            var dx = candidate.CenterX - track.Marker.CenterX;
            var dy = candidate.CenterY - track.Marker.CenterY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= closestDistance)
            {
                closest = track;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static bool IsValidCandidate(DetectedMarker? marker) =>
        marker is not null
        && double.IsFinite(marker.CenterX)
        && double.IsFinite(marker.CenterY)
        && double.IsFinite(marker.Area)
        && marker.Area > 0
        && double.IsFinite(marker.Confidence)
        && marker.Confidence is >= 0 and <= 1;
}
