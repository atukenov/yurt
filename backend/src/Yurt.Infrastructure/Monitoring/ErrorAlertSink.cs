using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace Yurt.Infrastructure.Monitoring;

/// <summary>
/// Serilog sink that hands every Error/Fatal log line to <see cref="ErrorAlertService"/>. Never blocks or throws:
/// when the queue is full the oldest entry is dropped.
/// </summary>
public sealed class ErrorAlertSink(AlertOptions options) : ILogEventSink
{
    // Logged by these sources already, or about the alert sender itself (an alert about a failed alert would loop).
    private static readonly string[] IgnoredSources =
    [
        "Serilog.AspNetCore.RequestLoggingMiddleware", // repeats the unhandled-exception log for the same 500
        typeof(ErrorAlertService).FullName!,
    ];

    private readonly Channel<LogEvent> _queue = Channel.CreateBounded<LogEvent>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<LogEvent> Reader => _queue.Reader;

    public void Emit(LogEvent logEvent)
    {
        if (!options.Enabled || logEvent.Level < LogEventLevel.Error) return;
        if (logEvent.Properties.TryGetValue("SourceContext", out var src)
            && src is ScalarValue { Value: string source }
            && IgnoredSources.Contains(source)) return;
        _queue.Writer.TryWrite(logEvent);
    }
}
