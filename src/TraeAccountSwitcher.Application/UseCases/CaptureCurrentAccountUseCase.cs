using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Models;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.Exceptions;

namespace TraeAccountSwitcher.Application.UseCases;

/// <summary>
/// 捕获当前账号用例：引导用户在 Trae 中完成登录后，读取当前登录态并保存为账号档案。
/// </summary>
public sealed class CaptureCurrentAccountUseCase
{
    private readonly ITraeAuthAccessor _authAccessor;
    private readonly IAccountProfileRepository _profileRepository;
    private readonly ILogger<CaptureCurrentAccountUseCase> _logger;

    /// <summary>创建捕获当前账号用例。</summary>
    /// <param name="authAccessor">登录态访问器。</param>
    /// <param name="profileRepository">账号档案仓储。</param>
    /// <param name="logger">日志。</param>
    public CaptureCurrentAccountUseCase(
        ITraeAuthAccessor authAccessor,
        IAccountProfileRepository profileRepository,
        ILogger<CaptureCurrentAccountUseCase> logger)
    {
        _authAccessor = authAccessor ?? throw new ArgumentNullException(nameof(authAccessor));
        _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>将 Trae 当前登录态捕获为账号档案；<paramref name="profileId"/> 非空时更新既有档案。</summary>
    /// <param name="profileName">档案显示名称。</param>
    /// <param name="profileId">既有档案标识（更新场景），null 表示新建。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存后的账号档案。</returns>
    public async Task<AccountProfile> ExecuteAsync(
        string profileName,
        Guid? profileId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_authAccessor.IsTraeDataAvailable())
        {
            throw new TraeAuthException("未找到 Trae 数据目录，请确认本机已安装并登录过 Trae CN。");
        }

        var bundle = await _authAccessor.ReadAuthBundleAsync(cancellationToken).ConfigureAwait(false);
        if (bundle is null)
        {
            throw new TraeAuthException("storage.json 中未发现登录态（iCubeAuthInfo）键，请先在 Trae 中完成登录后再捕获。");
        }

        var email = await _authAccessor.ReadCurrentEmailAsync(cancellationToken).ConfigureAwait(false);

        AccountProfile profile;
        if (profileId.HasValue)
        {
            var existing = await FindProfileAsync(profileId.Value, cancellationToken).ConfigureAwait(false);
            existing.Name = profileName;
            existing.Bundle = bundle;
            existing.Email = email;
            profile = existing;
            _logger.LogInformation("更新账号档案 {Name}（{Email}）", profileName, email ?? "未知邮箱");
        }
        else
        {
            profile = new AccountProfile(profileName, bundle, email);
            _logger.LogInformation("捕获新账号档案 {Name}（{Email}），共 {Count} 个认证键", profileName, email ?? "未知邮箱", bundle.Entries.Count);
        }

        await _profileRepository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    private async Task<AccountProfile> FindProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var all = await _profileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var found = all.FirstOrDefault(p => p.Id == id)
            ?? throw new AccountProfileException($"未找到标识为 {id} 的账号档案。");
        return found;
    }
}
