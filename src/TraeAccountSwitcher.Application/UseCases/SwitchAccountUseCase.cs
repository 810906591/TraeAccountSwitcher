using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Models;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.UseCases;

/// <summary>
/// 账号切换选项。
/// </summary>
/// <param name="RelaunchAfterSwitch">切换完成后自动重启 Trae，默认 true。</param>
/// <param name="EnforceSoloIntegrity">切换前强制校验 SOLO 任务库完整性，不一致则中止，默认 true。</param>
public sealed record SwitchOptions(bool RelaunchAfterSwitch = true, bool EnforceSoloIntegrity = true);

/// <summary>
/// 账号切换用例（核心流程）：
/// 完整性校验 → 认证快照兜底 → 优雅关闭 Trae → 仅替换登录态键 → 校验写回 → 重启 Trae。
/// 全程不触碰 SOLO 任务库（ModularData\ai-agent），保证任务列表在本机持久化且不随切换改变。
/// </summary>
public sealed class SwitchAccountUseCase
{
    private readonly IAccountProfileRepository _profileRepository;
    private readonly ITraeAuthAccessor _authAccessor;
    private readonly ISoloDataGuard _soloGuard;
    private readonly IBackupRepository _backupRepository;
    private readonly ITraeAppManager _appManager;
    private readonly ILogger<SwitchAccountUseCase> _logger;

    /// <summary>创建账号切换用例。</summary>
    public SwitchAccountUseCase(
        IAccountProfileRepository profileRepository,
        ITraeAuthAccessor authAccessor,
        ISoloDataGuard soloGuard,
        IBackupRepository backupRepository,
        ITraeAppManager appManager,
        ILogger<SwitchAccountUseCase> logger)
    {
        _profileRepository = profileRepository;
        _authAccessor = authAccessor;
        _soloGuard = soloGuard;
        _backupRepository = backupRepository;
        _appManager = appManager;
        _logger = logger;
    }

    /// <summary>切换到指定账号。</summary>
    /// <param name="targetProfileId">目标账号档案标识。</param>
    /// <param name="options">切换选项。</param>
    /// <param name="progress">进度回调（可为 null）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>切换结果。</returns>
    public async Task<SwitchResult> ExecuteAsync(
        Guid targetProfileId,
        SwitchOptions? options = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SwitchOptions();
        var target = await LoadTargetAsync(targetProfileId, cancellationToken).ConfigureAwait(false);
        Report(progress, $"准备切换到「{target.Name}」…", 0);

        try
        {
            await EnsureSoloIntegrityAsync(options, progress, cancellationToken).ConfigureAwait(false);

            var rollback = await SnapshotCurrentAuthAsync(progress, cancellationToken).ConfigureAwait(false);

            Report(progress, "正在关闭 Trae…", 30);
            var wasRunning = _appManager.IsRunning();
            if (wasRunning)
            {
                await _appManager.StopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await ApplyAuthBundleAsync(target, progress, cancellationToken).ConfigureAwait(false);

            if (options.RelaunchAfterSwitch && wasRunning)
            {
                Report(progress, "正在重新启动 Trae…", 85);
                await _appManager.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            await MarkUsedAsync(target, cancellationToken).ConfigureAwait(false);
            Report(progress, "切换完成。", 100);
            _logger.LogInformation("账号切换成功：{Name}", target.Name);
            return new SwitchResult(true, target, RolledBack: false, TraeRelaunched: wasRunning, "切换完成，SOLO 任务列表保持本地原样。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await FailWithRollbackAsync(target, ex, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AccountProfile> LoadTargetAsync(Guid id, CancellationToken ct)
    {
        var all = await _profileRepository.GetAllAsync(ct).ConfigureAwait(false);
        return all.FirstOrDefault(p => p.Id == id)
            ?? throw new AccountProfileException("目标账号档案不存在，请先捕获该账号。");
    }

    private async Task EnsureSoloIntegrityAsync(
        SwitchOptions options,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        if (!options.EnforceSoloIntegrity)
        {
            return;
        }

        Report(progress, "校验 SOLO 任务库完整性…", 10);
        var baseline = await _soloGuard.LoadBaselineAsync(ct).ConfigureAwait(false);
        if (baseline is null)
        {
            var manifest = await _soloGuard.ComputeManifestAsync(ct).ConfigureAwait(false);
            await _soloGuard.SaveBaselineAsync(manifest, ct).ConfigureAwait(false);
            _logger.LogInformation("首次切换：建立 SOLO 任务库基线（{Sha}）", manifest.ShaPreview);
            return;
        }

        var current = await _soloGuard.ComputeManifestAsync(ct).ConfigureAwait(false);
        if (!string.Equals(current.Sha256Hex, baseline.Sha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            throw new SoloDataIntegrityException(
                "SOLO 任务库与上次基线不一致（任务库在 Trae 使用中会正常变化）。请先在主界面点击「重建基线」确认当前数据无误后，再执行切换。",
                baseline.Sha256Hex,
                current.Sha256Hex);
        }
    }

    private async Task<BackupRecord?> SnapshotCurrentAuthAsync(IProgress<OperationProgress>? progress, CancellationToken ct)
    {
        Report(progress, "备份当前登录态（用于失败回滚）…", 20);
        var current = await _authAccessor.ReadAuthBundleAsync(ct).ConfigureAwait(false);
        if (current is null)
        {
            _logger.LogWarning("当前 storage.json 无登录态，跳过回滚快照");
            return null;
        }

        return await _backupRepository
            .SnapshotAuthBundleAsync(current, "切换前自动快照", ct)
            .ConfigureAwait(false);
    }

    private async Task ApplyAuthBundleAsync(
        AccountProfile target,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        Report(progress, "写入目标账号登录态…", 55);
        await _authAccessor.WriteAuthBundleAsync(target.Bundle, ct).ConfigureAwait(false);

        Report(progress, "校验登录态写回结果…", 75);
        var written = await _authAccessor.ReadAuthBundleAsync(ct).ConfigureAwait(false)
            ?? throw new TraeAuthException("写回后读取登录态失败，已触发回滚。");
        if (!BundleEquals(written, target.Bundle))
        {
            throw new TraeAuthException("写回后的登录态与目标账号不一致，已触发回滚。");
        }
    }

    private async Task MarkUsedAsync(AccountProfile target, CancellationToken ct)
    {
        target.MarkUsed(DateTimeOffset.UtcNow);
        await _profileRepository.SaveAsync(target, ct).ConfigureAwait(false);
    }

    private async Task<SwitchResult> FailWithRollbackAsync(
        AccountProfile target,
        Exception ex,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        _logger.LogError(ex, "切换到 {Name} 失败，开始回滚", target.Name);
        var rolledBack = false;
        try
        {
            if (_appManager.IsRunning())
            {
                await _appManager.StopAsync(cancellationToken: ct).ConfigureAwait(false);
            }
        }
        catch (Exception stopEx)
        {
            _logger.LogWarning(stopEx, "回滚阶段关闭 Trae 失败");
        }

        try
        {
            var backup = (await _backupRepository.ListAsync(ct).ConfigureAwait(false))
                .FirstOrDefault(b => b.Kind == BackupKind.PreSwitchAuth);
            if (backup is not null)
            {
                var bundle = await _backupRepository.ReadAuthSnapshotAsync(backup, ct).ConfigureAwait(false);
                if (bundle is not null)
                {
                    await _authAccessor.WriteAuthBundleAsync(bundle, ct).ConfigureAwait(false);
                    rolledBack = true;
                }
            }
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError(rollbackEx, "回滚失败，请使用主界面「从快照恢复」功能");
        }

        return new SwitchResult(false, target, rolledBack, TraeRelaunched: false, $"切换失败：{ex.Message}（{(rolledBack ? "已回滚到切换前登录态" : "回滚未完成，请手动恢复")}）");
    }

    private static bool BundleEquals(AuthBundle left, AuthBundle right)
    {
        if (left.Entries.Count != right.Entries.Count)
        {
            return false;
        }

        foreach (var (key, value) in left.Entries)
        {
            if (!right.Entries.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void Report(IProgress<OperationProgress>? progress, string message, int? percent)
        => progress?.Report(new OperationProgress(message, percent));
}
