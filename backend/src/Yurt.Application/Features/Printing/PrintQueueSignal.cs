namespace Yurt.Application.Features.Printing;

/// <summary>Wakes the print sender as soon as a job is queued, so a ticket does not wait for the next timer tick.</summary>
public sealed class PrintQueueSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { } // Already woken.
    }

    /// <summary>Waits until a job is queued or the time runs out.</summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}
