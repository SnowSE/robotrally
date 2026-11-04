namespace RobotRally.Game;

/// <summary>Things worth a sound or effect while a run plays back.</summary>
public enum Cue { Step, Turn, Bonk, Goal, OutOfBlocks, Dizzy, Done }

/// <summary>
/// Follows one run over time and reports each cue exactly once. If updates lag, only the latest step is
/// cued (no burst of stacked sounds).
/// </summary>
public sealed class RunAnimator
{
    private RunResult? _run;
    private DateTimeOffset _start;
    private int _lastStep;
    private bool _resultCued, _doneCued;

    public RunResult? Run => _run;
    public object? Tag { get; private set; }

    public void Begin(RunResult run, DateTimeOffset start, object? tag = null)
    {
        _run = run;
        _start = start;
        _lastStep = -1;
        _resultCued = _doneCued = false;
        Tag = tag;
    }

    public void Clear()
    {
        _run = null;
        Tag = null;
    }

    public PlaybackFrame Update(DateTimeOffset now, Pose idlePose, List<Cue> cues)
    {
        if (_run is null) return new PlaybackFrame(idlePose, 0, PlayStatus.Idle, null, true, null);

        var f = Playback.Frame(_run, now - _start);
        var step = Math.Min(f.StepIndex, _run.Steps.Count - 1);
        if (step > _lastStep)
        {
            _lastStep = step;
            cues.Add(_run.Steps[step].Action switch
            {
                StepAction.Move => Cue.Step,
                StepAction.TurnLeft or StepAction.TurnRight => Cue.Turn,
                StepAction.Crash => Cue.Bonk,
                _ => Cue.Goal,
            });
        }
        if (f.StepIndex >= _run.Steps.Count && !_resultCued)
        {
            _resultCued = true;
            if (_run.Outcome == RunOutcome.OutOfBlocks) cues.Add(Cue.OutOfBlocks);
            if (_run.Outcome == RunOutcome.StepLimit) cues.Add(Cue.Dizzy);
        }
        if (f.Done && !_doneCued)
        {
            _doneCued = true;
            cues.Add(Cue.Done);
        }
        return f;
    }
}
