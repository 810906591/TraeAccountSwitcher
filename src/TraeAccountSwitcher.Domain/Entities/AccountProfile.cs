using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Domain.Entities;

/// <summary>
/// 账号档案实体：代表本机保存的一个 TRAE 登录账号（含其认证束快照）。
/// </summary>
public sealed class AccountProfile
{
    /// <summary>档案唯一标识。</summary>
    public Guid Id { get; }

    /// <summary>档案显示名称（如“账号A / 工作号”）。</summary>
    public string Name { get; set; }

    /// <summary>登录邮箱（捕获时从 Trae 登录历史读取，仅用于展示）。</summary>
    public string? Email { get; set; }

    /// <summary>认证束（登录态快照）。</summary>
    public AuthBundle Bundle { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>最近一次切换到该账号的时间（UTC）；null 表示从未切换。</summary>
    public DateTimeOffset? LastUsedAtUtc { get; set; }

    /// <summary>创建账号档案。</summary>
    /// <param name="name">显示名称。</param>
    /// <param name="bundle">认证束快照。</param>
    /// <param name="email">登录邮箱（可选）。</param>
    /// <param name="id">档案标识，默认新生成。</param>
    /// <param name="createdAtUtc">创建时间，默认当前 UTC 时间。</param>
    public AccountProfile(
        string name,
        AuthBundle bundle,
        string? email = null,
        Guid? id = null,
        DateTimeOffset? createdAtUtc = null)
    {
        Id = id ?? Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("档案名称不能为空。", nameof(name))
            : name.Trim();
        Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        Email = email;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>标记该账号已被切换启用。</summary>
    /// <param name="atUtc">启用时间（UTC）。</param>
    public void MarkUsed(DateTimeOffset atUtc) => LastUsedAtUtc = atUtc;
}
