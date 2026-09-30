using TraeAccountSwitcher.Application.Models;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.UseCases;

/// <summary>
/// 主界面总览查询用例：聚合账号档案、SOLO 完整性状态与 Trae 运行状态。
/// </summary>
public sealed class GetOverviewUseCase
{
    private readonly IAccountProfileRepository _profileRepository;
    private readonly ITraeAuthAccessor _authAccessor;
    private readonly ITraeAppManager _appManager;
    private readonly IBackupRepository _backupRepository;
    private readonly VerifySoloIntegrityUseCase _verifySolo;

    /// <summary>创建总览查询用例。</summary>
    public GetOverviewUseCase(
        IAccountProfileRepository profileRepository,
        ITraeAuthAccessor authAccessor,
        ITraeAppManager appManager,
        IBackupRepository backupRepository,
        VerifySoloIntegrityUseCase verifySolo)
    {
        _profileRepository = profileRepository;
        _authAccessor = authAccessor;
        _appManager = appManager;
        _backupRepository = backupRepository;
        _verifySolo = verifySolo;
    }

    /// <summary>获取主界面总览数据。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<Overview> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _profileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var solo = await _verifySolo.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var backups = await _backupRepository.ListAsync(cancellationToken).ConfigureAwait(false);

        return new Overview(
            Profiles: profiles,
            HasCurrentLogin: (await _authAccessor.ReadAuthBundleAsync(cancellationToken).ConfigureAwait(false)) is not null,
            TraeRunning: _appManager.IsRunning(),
            TraePath: _appManager.GetExecutablePath(),
            Solo: solo,
            Backups: backups);
    }
}

/// <summary>
/// 主界面总览数据。
/// </summary>
/// <param name="Profiles">账号档案列表。</param>
/// <param name="HasCurrentLogin">Trae 当前是否存在登录态。</param>
/// <param name="TraeRunning">Trae 是否正在运行。</param>
/// <param name="TraePath">Trae 可执行文件路径。</param>
/// <param name="Solo">SOLO 完整性报告。</param>
/// <param name="Backups">备份记录列表（倒序）。</param>
public sealed record Overview(
    IReadOnlyList<AccountProfile> Profiles,
    bool HasCurrentLogin,
    bool TraeRunning,
    string? TraePath,
    SoloIntegrityReport Solo,
    IReadOnlyList<BackupRecord> Backups);
