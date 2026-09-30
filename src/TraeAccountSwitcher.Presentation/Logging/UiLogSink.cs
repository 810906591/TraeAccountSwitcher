using Serilog.Core;
using Serilog.Events;

namespace TraeAccountSwitcher.Presentation.Logging;

/// <summary>
/// 界面日志接收器：将 Serilog 日志事件转发到事件流，供日志面板显示。
/// </summary>
public sealed class UiLogSink : ILogEventSink
{
    private readonly object _gate = new();

    /// <summary>新日志事件到达（在后台线程触发，订阅方需自行调度到 UI 线程）。</summary>
    public event EventHandler<LogRenderedEventArgs>? LogEmitted;

    /// <summary>写入单条日志事件。</summary>
    /// <param name="logEvent">日志事件。</param>
    public void Emit(LogEvent logEvent)
    {
        var rendered = Render(logEvent);
        lock (_gate)
        {
            LogEmitted?.Invoke(this, new LogRenderedEventArgs(rendered));
        }
    }

    private static string Render(LogEvent logEvent)
    {
        var time = logEvent.Timestamp.LocalDateTime.ToString("HH:mm:ss");
        var level = MapLevel(logEvent.Level);
        return $"[{time}] [{level}] {logEvent.RenderMessage()}";
    }

    private static string MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Error or LogEventLevel.Fatal => "错误",
        LogEventLevel.Warning => "警告",
        LogEventLevel.Debug or LogEventLevel.Verbose => "调试",
        _ => "信息"
    };
}

/// <summary>日志渲染结果事件参数。</summary>
/// <param name="Line">渲染后的日志行。</param>
public sealed record LogRenderedEventArgs(string Line);
