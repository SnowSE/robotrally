namespace RobotRally.Game;

public enum PlayStatus { Idle, Moving, Crashed, Failed, Celebrating }

public readonly record struct PlaybackFrame(Pose Pose, int StepIndex, PlayStatus Status, Guid? BlockId, bool Done, StepAction? Action);

/// <summary>Turns a run result plus elapsed time into what to draw. Shared by kid screens, the projector and demos.</summary>
public static class Playback
{
    public static readonly TimeSpan StepTime = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan FailTime = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan SuccessTime = TimeSpan.FromMilliseconds(2200);

    public static TimeSpan ResultTime(RunResult run) => run.Outcome == RunOutcome.Success ? SuccessTime : FailTime;

    public static TimeSpan Duration(RunResult run) => StepTime * run.Steps.Count + ResultTime(run);

    public static PlaybackFrame Frame(RunResult run, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var i = (int)(elapsed / StepTime);
        if (i < run.Steps.Count)
        {
            var s = run.Steps[i];
            return new PlaybackFrame(s.Pose, i, s.Action == StepAction.Crash ? PlayStatus.Crashed : PlayStatus.Moving, s.BlockId, false, s.Action);
        }

        var last = run.Steps.Count > 0 ? run.Steps[^1].Pose : run.Start;
        if (elapsed < Duration(run))
        {
            var status = run.Outcome switch
            {
                RunOutcome.Success => PlayStatus.Celebrating,
                RunOutcome.Crash => PlayStatus.Crashed,
                _ => PlayStatus.Failed,
            };
            return new PlaybackFrame(last, run.Steps.Count, status, null, false, null);
        }

        return run.Outcome == RunOutcome.Success
            ? new PlaybackFrame(last, run.Steps.Count, PlayStatus.Celebrating, null, true, null)
            : new PlaybackFrame(run.Start, run.Steps.Count, PlayStatus.Idle, null, true, null);
    }
}
