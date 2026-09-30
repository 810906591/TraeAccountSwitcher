using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Models;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.UseCases;

/// <summary>
/// SOLO 任务库完整性校验用例：对比当前任务库指纹与基线，并可重建基线。
/// </summary>
public sealed class VerifySoloIntegrityUseCase
{
    private readonly ISoloDataGuard _soloGuard;
    private readonly ILogger<VerifySoloIntegrityUseCase> _logger;

    /// <summary>创建完整性校验用例。</summary>
    public VerifySoloIntegrityUseCase(ISoloDataGuard soloGuard, ILogger<VerifySoloIntegrityUseCase> logger)
    {
        _soloGuard = soloGuard;
        _logger = logger;
    }

    /// <summary>执行完整性检查。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完整性报告。</returns>
    public async Task<SoloIntegrityReport> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var dbPath = _soloGuard.GetDatabasePath();
        if (dbPath is null)
        {
            return new SoloIntegrityReport(null, null, SoloIntegrityState.NotFound);
        }

        var current = await _soloGuard.ComputeManifestAsync(cancellationToken).ConfigureAwait(false);
        var baseline = await _soloGuard.LoadBaselineAsync(cancellationToken).ConfigureAwait(false);
        var state = baseline is null
            ? SoloIntegrityState.NoBaseline
            : string.Equals(current.Sha256Hex, baseline.Sha256Hex, StringComparison.OrdinalIgnoreCase)
                ? SoloIntegrityState.Ok
                : SoloIntegrityState.Changed;

        _logger.LogInformation("SOLO 完整性检查：状态 {State}，当前 {Current}，基线 {Baseline}", state, current.ShaPreview, baseline?.ShaPreview ?? "无");
        return new SoloIntegrityReport(current, baseline, state);
    }

    /// <summary>以当前任务库状态重建基线（用户确认当前数据无误后执行）。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新基线清单。</returns>
    public async Task<SoloDataManifest> RebaselineAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await _soloGuard.ComputeManifestAsync(cancellationToken).ConfigureAwait(false);
        await _soloGuard.SaveBaselineAsync(manifest, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("已重建 SOLO 任务库基线（{Sha}）", manifest.ShaPreview);
        return manifest;
    }
}
