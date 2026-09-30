using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.Repositories;

/// <summary>
/// SOLO 数据守卫：负责本地 ai-agent 任务库的完整性指纹计算、校验与基线管理。
/// </summary>
public interface ISoloDataGuard
{
    /// <summary>获取任务库主数据库文件路径；不存在时返回 null。</summary>
    string? GetDatabasePath();

    /// <summary>计算任务库当前完整性清单。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<SoloDataManifest> ComputeManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>加载上一次保存的基线清单；尚未建立基线时返回 null。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<SoloDataManifest?> LoadBaselineAsync(CancellationToken cancellationToken = default);

    /// <summary>保存基线清单。</summary>
    /// <param name="manifest">清单。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveBaselineAsync(SoloDataManifest manifest, CancellationToken cancellationToken = default);
}
