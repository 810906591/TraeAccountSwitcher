using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.Repositories;

/// <summary>
/// 备份仓储：负责认证快照与 SOLO 任务库备份的创建、列出、校验与恢复。
/// </summary>
public interface IBackupRepository
{
    /// <summary>创建 SOLO 任务库备份（含 SHA-256 边车校验文件）。</summary>
    /// <param name="description">备份说明。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<BackupRecord> BackupSoloDatabaseAsync(string? description = null, CancellationToken cancellationToken = default);

    /// <summary>创建认证束快照（DPAPI 加密存储，用于切换失败回滚）。</summary>
    /// <param name="bundle">认证束。</param>
    /// <param name="description">快照说明。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<BackupRecord> SnapshotAuthBundleAsync(AuthBundle bundle, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>读取认证快照内容（解密还原为认证束）；文件缺失或校验失败返回 null。</summary>
    /// <param name="record">备份记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AuthBundle?> ReadAuthSnapshotAsync(BackupRecord record, CancellationToken cancellationToken = default);

    /// <summary>列出全部备份记录（按创建时间倒序）。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>校验备份文件完整性（SHA-256 与边车记录一致）。</summary>
    /// <param name="record">备份记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<bool> VerifyAsync(BackupRecord record, CancellationToken cancellationToken = default);

    /// <summary>从 SOLO 任务库备份恢复（恢复前先校验完整性）。</summary>
    /// <param name="record">备份记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RestoreSoloDatabaseAsync(BackupRecord record, CancellationToken cancellationToken = default);

    /// <summary>删除超量旧备份，仅保留最近 <paramref name="keep"/> 条（按类型分别保留）。</summary>
    /// <param name="kind">备份类型。</param>
    /// <param name="keep">保留数量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task PruneAsync(BackupKind kind, int keep, CancellationToken cancellationToken = default);
}
