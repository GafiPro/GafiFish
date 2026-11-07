using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Converts freshly observed falling notes into short, ordinary Windows key taps.
/// Keys are only sent while the caller confirms the fishing state is Reeling.
/// Lane order and keys are D, F, J, K. No memory, injection or network access is used.
/// </summary>
public sealed class RhythmNoteInputCoordinator
{
    private sealed class LaneState
    {
        public double? PreviousY { get; set; }
        public DateTimeOffset? PreviousAt { get; set; }
        public int MissingFrames { get; set; }
        public bool Triggered { get; set; }

        public void Reset()
        {
            PreviousY = null;
            PreviousAt = null;
            MissingFrames = 0;
            Triggered = false;
        }
    }

    private static readonly ushort[] LaneKeys = [0x44, 0x46, 0x4A, 0x4B];
    private readonly IRobloxInput _input;
    private readonly RhythmNoteVisionDetector _detector;
    private readonly LaneState[] _lanes = [new(), new(), new(), new()];
    private DateTimeOffset? _lastFrameAt;
    private bool _started;
    private int _hitCount;
    private string _lastStatus = "Tranquility Rhythm is stopped.";

    public RhythmNoteInputCoordinator(IRobloxInput input, RhythmNoteVisionDetector? detector = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _detector = detector ?? new RhythmNoteVisionDetector();
    }

    public bool IsStarted => _started;
    public int HitCount => _hitCount;
    public string LastStatus => _lastStatus;

    public void Start(VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (!profile.IsTranquilityRhythmCalibrated)
            throw new InvalidOperationException("Calibrate the four Tranquility lanes and hit line before starting.");

        if (_started)
            throw new InvalidOperationException("Tranquility Rhythm input is already armed.");

        _input.Arm();
        Reset();
        _started = true;
        _lastStatus = "Tranquility Rhythm armed · learning note movement before the first key tap.";
    }

    /// <summary>
    /// Processes a distinct live frame. Detection runs in all fishing states,
    /// but a key tap is only permitted when allowInput is true (Reeling).
    /// </summary>
    public RhythmNoteVisionResult? ProcessFrame(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        bool allowInput)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        if (!_started)
            return null;

        return ProcessObservation(_detector.Detect(frame, profile), profile, allowInput);
    }

    /// <summary>
    /// Processes an already detected frame so the same observation can be
    /// displayed in offline replay without running the input coordinator.
    /// </summary>
    public RhythmNoteVisionResult? ProcessObservation(
        RhythmNoteVisionResult observation,
        VisionProfile profile,
        bool allowInput)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(profile);
        if (!_started)
            return null;

        if (_lastFrameAt.HasValue && observation.CapturedAt <= _lastFrameAt.Value)
        {
            _lastStatus = "Ignored duplicate or out-of-order rhythm frame; no key was sent.";
            return null;
        }

        _lastFrameAt = observation.CapturedAt;

        if (!observation.IsCalibrated)
        {
            _lastStatus = observation.Status;
            return observation;
        }

        var detected = 0;
        var hitsThisFrame = 0;
        for (var lane = 0; lane < _lanes.Length; lane++)
        {
            var state = _lanes[lane];
            var marker = observation.LaneMarkers[lane];
            if (marker is null)
            {
                state.MissingFrames++;
                if (state.MissingFrames >= 2)
                    state.Reset();
                continue;
            }

            detected++;
            state.MissingFrames = 0;

            if (state.PreviousY.HasValue && state.PreviousAt.HasValue)
            {
                var elapsed = (frame.CapturedAt - state.PreviousAt.Value).TotalSeconds;
                if (elapsed is > 0 and <= 0.5)
                {
                    var displacement = marker.CenterY - state.PreviousY.Value;
                    if (displacement < -Math.Max(4, marker.Bounds.Height * 0.35))
                    {
                        // A new note has entered above the previous note.
                        state.Triggered = false;
                    }
                    else if (displacement > 0 && !state.Triggered)
                    {
                        var velocity = displacement / elapsed;
                        if (velocity is >= 15 and <= 1800)
                        {
                            var predictedY = marker.CenterY
                                + (velocity * profile.RhythmInputLeadMilliseconds / 1000d);
                            var hitWindow = Math.Clamp(marker.Bounds.Height * 0.9, 8, 20);
                            var deltaToHitLine = predictedY - observation.HitLineY;

                            if (allowInput && Math.Abs(deltaToHitLine) <= hitWindow)
                            {
                                TapLane(lane);
                                state.Triggered = true;
                                hitsThisFrame++;
                                _hitCount++;
                            }
                            else if (deltaToHitLine > hitWindow)
                            {
                                // Do not send a late press for a note that has
                                // already passed the calibrated timing window.
                                state.Triggered = true;
                            }
                        }
                    }
                }
            }

            state.PreviousY = marker.CenterY;
            state.PreviousAt = frame.CapturedAt;
        }

        _lastStatus = hitsThisFrame > 0
            ? $"Tranquility Rhythm · hit {hitsThisFrame} lane(s) · {_hitCount} total · {detected}/4 notes visible."
            : allowInput
                ? $"Tranquility Rhythm · tracking {detected}/4 lanes · {_hitCount} key taps."
                : $"Tranquility Rhythm · observing {detected}/4 lanes; waiting for a confirmed Reel state.";

        return observation;
    }

    public void Stop()
    {
        try
        {
            _input.Disarm();
        }
        finally
        {
            _started = false;
            Reset();
            _lastStatus = "Tranquility Rhythm stopped; all held inputs released.";
        }
    }

    private void TapLane(int lane)
    {
        var key = LaneKeys[lane];
        try
        {
            _input.SetKey(key, true);
            _input.SetKey(key, false);
        }
        catch
        {
            // Never leave a key held after a partial SendInput failure.
            try { _input.SetKey(key, false); }
            catch { /* Disarm below remains the final best-effort release. */ }
            try { _input.Disarm(); }
            finally
            {
                _started = false;
                _lastStatus = "Rhythm key input failed; the macro was disarmed.";
            }

            throw;
        }
    }

    private void Reset()
    {
        foreach (var lane in _lanes)
            lane.Reset();
        _lastFrameAt = null;
        _hitCount = 0;
    }
}
