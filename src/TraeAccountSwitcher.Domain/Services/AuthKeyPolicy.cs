namespace TraeAccountSwitcher.Domain.Services;

/// <summary>
/// 认证键策略（纯领域逻辑）：定义 storage.json 中哪些键属于“账号登录态”，切换账号时仅对这些键进行替换。
/// 其余键（窗口布局、主题、各账号共存的会话草稿等）一律不动，从而保证 SOLO 任务列表与本地设置不随切换改变。
/// </summary>
public static class AuthKeyPolicy
{
    /// <summary>认证信息键前缀（登录令牌）。</summary>
    public const string AuthInfoPrefix = "iCubeAuthInfo://";

    /// <summary>服务端同步数据键前缀（与登录用户绑定的云端数据缓存）。</summary>
    public const string ServerDataPrefix = "iCubeServerData://";

    /// <summary>主机配置键名：与账号无关，切换时保持不变。</summary>
    public const string HostInfoKey = "iCubeHostInfo";

    /// <summary>判断指定键是否属于登录态键。</summary>
    /// <param name="key">storage.json 中的键名。</param>
    /// <returns>属于登录态键返回 true。</returns>
    public static bool IsAuthKey(string key)
    {
        return key.StartsWith(AuthInfoPrefix, StringComparison.Ordinal)
               || key.StartsWith(ServerDataPrefix, StringComparison.Ordinal);
    }

    /// <summary>从全部键中筛选出登录态键。</summary>
    /// <param name="keys">storage.json 全部键名。</param>
    /// <returns>登录态键序列。</returns>
    public static IEnumerable<string> SelectAuthKeys(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.Where(IsAuthKey);
    }
}
