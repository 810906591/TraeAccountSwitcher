using ReactiveUI;
using TraeAccountSwitcher.Domain.Entities;

namespace TraeAccountSwitcher.Presentation.ViewModels;

/// <summary>
/// 账号卡片视图模型：展示单个账号档案并承载“切换到此账号”交互。
/// </summary>
public sealed class AccountCardViewModel : ReactiveObject
{
    private string _name = string.Empty;
    private string _email = "未知";
    private string _capturedAt = "—";
    private string _lastUsedAt = "从未使用";
    private bool _isActive;

    /// <summary>档案标识。</summary>
    public Guid ProfileId { get; init; }

    /// <summary>显示名称。</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>登录邮箱。</summary>
    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    /// <summary>捕获时间显示。</summary>
    public string CapturedAt
    {
        get => _capturedAt;
        set => this.RaiseAndSetIfChanged(ref _capturedAt, value);
    }

    /// <summary>最近使用时间显示。</summary>
    public string LastUsedAt
    {
        get => _lastUsedAt;
        set => this.RaiseAndSetIfChanged(ref _lastUsedAt, value);
    }

    /// <summary>是否为当前 Trae 登录账号（展示高亮）。</summary>
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>从账号档案实体构建卡片视图模型。</summary>
    /// <param name="profile">账号档案。</param>
    public static AccountCardViewModel FromProfile(AccountProfile profile) => new()
    {
        ProfileId = profile.Id,
        Name = profile.Name,
        Email = string.IsNullOrWhiteSpace(profile.Email) ? "未知" : profile.Email,
        CapturedAt = profile.Bundle.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
        LastUsedAt = profile.LastUsedAtUtc.HasValue
            ? profile.LastUsedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "从未使用"
    };
}
