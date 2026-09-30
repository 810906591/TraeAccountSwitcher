namespace TraeAccountSwitcher.Application.Repositories;

/// <summary>
/// Trae 应用进程管理器：负责 Trae CN 进程的探测、优雅关闭与启动。
/// </summary>
public interface ITraeAppManager
{
    /// <summary>获取 Trae CN 可执行文件路径；未找到时返回 null。</summary>
    string? GetExecutablePath();

    /// <summary>判断 Trae 是否正在运行。</summary>
    bool IsRunning();

    /// <summary>优雅关闭 Trae（先尝试关闭主窗口，超时后强制结束），并等待进程完全退出与文件句柄释放。</summary>
    /// <param name="timeout">超时时间，默认 15 秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>启动 Trae。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task StartAsync(CancellationToken cancellationToken = default);
}
