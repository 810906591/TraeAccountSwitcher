using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.Repositories;

/// <summary>
/// Trae 登录态访问器：负责从 storage.json 读取与写回认证键（仓储接口，定义于应用层）。
/// </summary>
public interface ITraeAuthAccessor
{
    /// <summary>读取当前 storage.json 中的登录态认证束；若不存在认证键则返回 null。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AuthBundle?> ReadAuthBundleAsync(CancellationToken cancellationToken = default);

    /// <summary>将认证束写回 storage.json（仅替换登录态键，其余键保持不变；原子写入）。</summary>
    /// <param name="bundle">待写回的认证束。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task WriteAuthBundleAsync(AuthBundle bundle, CancellationToken cancellationToken = default);

    /// <summary>读取当前登录账号邮箱（来自 state.vscdb 登录历史，读取失败返回 null）。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<string?> ReadCurrentEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>校验 Trae 数据目录与 storage.json 是否存在且可访问。</summary>
    /// <returns>可访问返回 true。</returns>
    bool IsTraeDataAvailable();
}
