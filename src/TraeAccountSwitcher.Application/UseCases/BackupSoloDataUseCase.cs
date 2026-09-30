using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.UseCases;

/// <summary>
/// SOLO 任务库备份用例：创建带 SHA-256 校验的备份并清理超量旧备份。
/// </summary>
public sealed class BackupSoloDataUseCase
{
    /// <summary>默认保留的 SOLO 备份份数。</summary>
    public const int DefaultKeepCount = 5;

    private readonly IBackupRepository _backupRepository;
    private readonly ISoloDataGuard _soloGuard;
    private readonly ILogger<BackupSoloDataUseCase> _logger;

    /// <summary>创建备份用例。</summary>
    public BackupSoloDataUseCase(
        IBackupRepository backupRepository,
        ISoloDataGuard soloGuard,
        ILogger<BackupSoloDataUseCase> logger)
    {
        _backupRepository = backupRepository;
        _soloGuard = soloGuard;
        _logger = logger;
    }

    /// <summary>创建 SOLO 任务库备份。</summary>
    /// <param name="description">备份说明。</param>
    /// <param name="keep">保留份数，默认 5。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新创建的备份记录。</returns>
    public async Task<BackupRecord> ExecuteAsync(
        string? description = null,
        int keep = DefaultKeepCount,
        CancellationToken cancellationToken = default)
    {
        if (_soloGuard.GetDatabasePath() is null)
        {
            throw new SoloDataIntegrityException("未找到 SOLO 任务库文件，无法备份。");
        }

        var record = await _backupRepository.BackupSoloDatabaseAsync(description, cancellationToken).ConfigureAwait(false);
        var verified = await _backupRepository.VerifyAsync(record, cancellationToken).ConfigureAwait(false);
        if (!verified)
        {
            throw new SoloDataIntegrityException("备份完成后校验失败，该备份不可用于恢复，请重试。");
        }

        await _backupRepository.PruneAsync(BackupKind.SoloDatabase, keep, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("SOLO 任务库备份完成：{Path}", record.FilePath);
        return record;
    }
}
