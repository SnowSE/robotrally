using Microsoft.JSInterop;
using RobotRally.Game;
using RobotRally.Services;

namespace RobotRally.Components.Shared;

/// <summary>
/// What a single racer's run looks like right now (pose, highlight, message) plus its sounds/effects.
/// Shared by the kid race screen and both projector demos.
/// </summary>
public sealed class RunView(IJSRuntime js)
{
    public RunAnimator Anim { get; } = new();
    public Pose Pose { get; private set; }
    public PlayStatus Status { get; private set; }
    public Guid? ActiveBlock { get; private set; }
    public Guid? Bug { get; set; }
    public string? Message { get; private set; }
    public string MessageKind { get; private set; } = "info";
    public bool Celebrate { get; set; }
    public bool Running => Anim.Run is not null;
    /// <summary>Stop sits where GO was, so ignore it briefly after starting (a double-clicked GO must not cancel the run).</summary>
    public static readonly TimeSpan StopGrace = TimeSpan.FromMilliseconds(800);
    private DateTimeOffset _startedAt;

    public bool CanStop => Anim.Run is { } run && run.Outcome != RunOutcome.Success;
    public bool CanStopAt(DateTimeOffset now) => CanStop && now - _startedAt >= StopGrace;

    public void Begin(RunResult result, DateTimeOffset start, object? tag = null)
    {
        _startedAt = start;
        Anim.Begin(result, start, tag);
        Message = null;
        Bug = null;
        Celebrate = false;
        js.Sfx("run");
    }

    public void Stop(Pose start)
    {
        Anim.Clear();
        Reset(start);
    }

    public void Reset(Pose start)
    {
        Pose = start;
        Status = PlayStatus.Idle;
        ActiveBlock = null;
    }

    public void Say(string? message, string kind = "info")
    {
        Message = message;
        MessageKind = kind;
    }

    public void ClearNotes()
    {
        Message = null;
        Bug = null;
    }

    /// <summary>Advances the animation. Returns true if anything visible changed.</summary>
    /// <param name="autoClear">Local runs (demos) end themselves; server runs are ended by the server.</param>
    public bool Update(DateTimeOffset now, Pose start, bool autoClear)
    {
        if (Anim.Run is null)
        {
            if (Celebrate) return false; // a local win: stay on the flag under the "You made it!" overlay
            var was = (Pose, Status, ActiveBlock);
            Reset(start);
            return was != (Pose, Status, ActiveBlock);
        }

        var cues = new List<Cue>();
        var f = Anim.Update(now, start, cues);
        var changed = f.Pose != Pose || f.Status != Status || f.BlockId != ActiveBlock || cues.Count > 0;
        Pose = f.Pose;
        Status = f.Status;
        ActiveBlock = f.BlockId;

        foreach (var cue in cues)
        {
            switch (cue)
            {
                case Cue.Step: js.Sfx("step"); break;
                case Cue.Turn: js.Sfx("turn"); break;
                case Cue.Bonk:
                    js.Sfx("bonk");
                    Bug = f.BlockId;
                    Say("Bonk! 💥 Your racer hit a hedge. Find the 🐞 block and fix it!", "bad");
                    break;
                case Cue.OutOfBlocks:
                    js.Sfx("fail");
                    Say("Your racer stopped before the flag. Add more blocks!", "bad");
                    break;
                case Cue.Dizzy:
                    js.Sfx("dizzy");
                    Say("Whoa, dizzy! 😵 Your racer went around in circles. Check your loops.", "bad");
                    break;
                case Cue.Goal:
                    Celebrate = true;
                    Message = null;
                    js.Sfx("win");
                    js.Confetti(160, 0.7, 0.45);
                    break;
                case Cue.Done when autoClear:
                    var success = Anim.Run?.Outcome == RunOutcome.Success;
                    Anim.Clear();
                    if (!success) Reset(start);
                    break;
            }
        }
        return changed;
    }
}
