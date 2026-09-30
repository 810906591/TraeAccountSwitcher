using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;

namespace TraeAccountSwitcher.Infrastructure.Trae;

/// <summary>
/// Trae CN 进程管理器实现：优雅关闭（关闭主窗口 → 超时强杀）并等待存储文件句柄释放。
/// </summary>
public sealed class TraeAppManager : ITraeAppManager
{
    /// <summary>Trae CN 进程名（不含扩展名）。</summary>
    public const string ProcessName = "Trae CN";

    /// <summary>进程退出后额外等待存储文件落盘的时间。</summary>
    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(2);

    private readonly TraePathLocator _paths;
    private readonly ILogger<TraeAppManager> _logger;

    /// <summary>创建 Trae 进程管理器。</summary>
    public TraeAppManager(TraePathLocator paths, ILogger<TraeAppManager> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? GetExecutablePath() => _paths.ExecutablePath;

    /// <inheritdoc />
    public bool IsRunning() => Process.GetProcessesByName(ProcessName).Length > 0;

    /// <inheritdoc />
    public async Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout.GetValueOrDefault(TimeSpan.FromSeconds(15));
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0)
        {
            return;
        }

        _logger.LogInformation("正在关闭 Trae（{Count} 个进程）…", processes.Length);
        foreach (var process in processes)
        {
            TryCloseMainWindow(process);
        }

        var exitedAll = await WaitForExitAsync(processes, effectiveTimeout, cancellationToken).ConfigureAwait(false);
        if (!exitedAll)
        {
            _logger.LogWarning("Trae 未在超时内退出，执行强制结束");
            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                TryKill(process);
            }

            await WaitForExitAsync(Process.GetProcessesByName(ProcessName), effectiveTimeout, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(FlushDelay, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Trae 已完全退出");
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var exePath = _paths.ExecutablePath
            ?? throw new TraeAccountSwitcher.Domain.Exceptions.TraeAuthException("未找到 Trae CN 安装路径，无法自动启动，请手动打开 Trae。");

        _logger.LogInformation("启动 Trae：{Path}", exePath);
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
    }

    private static void TryCloseMainWindow(Process process)
    {
        try
        {
            _ = process.CloseMainWindow();
        }
        catch (InvalidOperationException)
        {
            // 进程可能已退出
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 进程可能已退出
        }
    }

    private static async Task<bool> WaitForExitAsync(Process[] processes, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            foreach (var process in processes)
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 内部超时触发
            return false;
        }
    }
}
