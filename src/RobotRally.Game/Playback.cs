namespace RobotRally.Game;

public enum PlayStatus { Idle, Moving, Crashed, Failed, Celebrating }

public readonly record struct PlaybackFrame(Pose Pose, int StepIndex, PlayStatus Status, Guid? BlockId, bool Done, StepAction? Action);

/// <summary>Turns a run result plus elapsed time into what to draw. Shared by kid screens, the projector and demos.</summary>
public static class Playback
{
    public static readonly TimeSpan StepTime = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan FailTime = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan SuccessTime = TimeSpan.FromMilliseconds(2200);

    /// <summary>How much faster the 🐇 button plays a run.</summary>
    public const double FastSpeed = 2.5;

    public static TimeSpan ResultTime(RunResult run) => run.Outcome == RunOutcome.Success ? SuccessTime : FailTime;

    private static TimeSpan StepsTime(RunResult run) => StepTime * run.Steps.Count;

    /// <summary>Real time a run takes at a speed. Speed only shortens the driving, not the crash or celebration.</summary>
    public static TimeSpan Duration(RunResult run, double speed = 1) => StepsTime(run) / speed + ResultTime(run);

    /// <summary>Real elapsed time → position on the normal-speed timeline.</summary>
    public static TimeSpan Position(RunResult run, TimeSpan elapsed, double speed)
    {
        var driving = StepsTime(run) / speed;
        return elapsed < driving ? elapsed * speed : StepsTime(run) + (elapsed - driving);
    }

    /// <summary>Inverse of <see cref="Position"/>: how long it takes at a speed to reach a timeline position.</summary>
    public static TimeSpan ElapsedFor(RunResult run, TimeSpan position, double speed) =>
        position < StepsTime(run) ? position / speed : StepsTime(run) / speed + (position - StepsTime(run));

    public static PlaybackFrame Frame(RunResult run, TimeSpan elapsed, double speed) => Frame(run, Position(run, elapsed, speed));

    /// <summary>Stars already picked up when the playback is at <paramref name="frame"/>.</summary>
    public static IReadOnlySet<(int X, int Y)> StarsTaken(RunResult run, PlaybackFrame frame)
    {
        if (frame.Done && run.Outcome != RunOutcome.Success) return EmptyStars;
        HashSet<(int, int)>? taken = null;
        var last = Math.Min(frame.StepIndex, run.Steps.Count - 1);
        for (var i = 0; i <= last; i++)
            if (run.Steps[i].Star) (taken ??= []).Add((run.Steps[i].Pose.X, run.Steps[i].Pose.Y));
        return taken ?? EmptyStars;
    }

    public static readonly IReadOnlySet<(int X, int Y)> EmptyStars = new HashSet<(int, int)>();

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
