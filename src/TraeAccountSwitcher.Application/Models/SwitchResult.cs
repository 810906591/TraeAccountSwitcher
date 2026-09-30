using TraeAccountSwitcher.Domain.Entities;

namespace TraeAccountSwitcher.Application.Models;

/// <summary>
/// 账号切换结果。
/// </summary>
/// <param name="Success">是否成功。</param>
/// <param name="TargetProfile">目标账号档案。</param>
/// <param name="RolledBack">失败时是否已自动回滚到切换前登录态。</param>
/// <param name="TraeRelaunched">是否已重新启动 Trae。</param>
/// <param name="Message">结果说明（中文）。</param>
public sealed record SwitchResult(
    bool Success,
    AccountProfile? TargetProfile,
    bool RolledBack,
    bool TraeRelaunched,
    string Message);
