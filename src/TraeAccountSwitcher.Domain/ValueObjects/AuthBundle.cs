namespace TraeAccountSwitcher.Domain.ValueObjects;

/// <summary>
/// 认证束值对象：从 Trae storage.json 中提取的登录态键值集合。
/// 值为 Trae 自身加密后的不透明字符串，本工具不解析其内容，仅整体快照与恢复。
/// </summary>
public sealed class AuthBundle
{
    /// <summary>认证键值集合（键为 storage.json 中的完整键名，如 iCubeAuthInfo://icube.cloudide）。</summary>
    public IReadOnlyDictionary<string, string> Entries { get; }

    /// <summary>捕获时间（UTC）。</summary>
    public DateTimeOffset CapturedAtUtc { get; }

    /// <summary>使用指定的认证键值集合创建认证束。</summary>
    /// <param name="entries">认证键值集合，不可为空且至少包含一条记录。</param>
    /// <param name="capturedAtUtc">捕获时间（UTC），默认为当前时间。</param>
    public AuthBundle(IReadOnlyDictionary<string, string> entries, DateTimeOffset? capturedAtUtc = null)
    {
        if (entries is null || entries.Count == 0)
        {
            throw new ArgumentException("认证束至少需要包含一条键值记录。", nameof(entries));
        }

        Entries = new Dictionary<string, string>(entries, StringComparer.Ordinal);
        CapturedAtUtc = capturedAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>获取认证束中全部键名。</summary>
    public IEnumerable<string> Keys => Entries.Keys;
}
