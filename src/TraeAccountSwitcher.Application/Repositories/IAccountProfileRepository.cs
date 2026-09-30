using TraeAccountSwitcher.Domain.Entities;

namespace TraeAccountSwitcher.Application.Repositories;

/// <summary>
/// 账号档案仓储：负责账号档案（含认证束）的持久化（认证束以 DPAPI 加密存储）。
/// </summary>
public interface IAccountProfileRepository
{
    /// <summary>获取全部账号档案（按创建时间排序）。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<AccountProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>保存（新增或更新）账号档案。</summary>
    /// <param name="profile">账号档案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveAsync(AccountProfile profile, CancellationToken cancellationToken = default);

    /// <summary>删除账号档案。</summary>
    /// <param name="profileId">档案标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
