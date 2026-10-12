#nullable enable
using System;
using System.Threading.Tasks;

namespace tweens.gd;

/// <summary>Awaitable completion and controls for one layout update.</summary>
public sealed class LayoutTransition
{
    private readonly TaskCompletionSource<Reason> completion = new();
    private Group? group;
    private bool paused;
    private bool cancelRequested;

    public Task<Reason> End => completion.Task;
    public bool IsTerminal => completion.Task.IsCompleted;
    public bool IsAnimating => group is { IsTerminal: false };
    public bool IsPaused => group?.IsPaused ?? paused;
    public Reason? CompletionReason { get; private set; }
    public Exception? Error { get; private set; }

    public void Pause()
    {
        if (IsTerminal) return;
        paused = true;
        group?.Pause();
    }

    public void Resume()
    {
        if (IsTerminal) return;
        paused = false;
        group?.Resume();
    }

    public void Cancel()
    {
        if (IsTerminal) return;
        cancelRequested = true;
        group?.Cancel();
    }

    internal bool IsCancellationRequested => cancelRequested;

    internal void Attach(Group value)
    {
        group = value;
        if (paused) group.Pause();
        if (cancelRequested) group.Cancel();
    }

    internal void Complete(Reason reason)
    {
        if (IsTerminal) return;
        CompletionReason = reason;
        group = null;
        completion.TrySetResult(reason);
    }

    internal void Fail(Exception error)
    {
        if (IsTerminal) return;
        Error = error;
        group = null;
        completion.TrySetException(error);
    }
}
