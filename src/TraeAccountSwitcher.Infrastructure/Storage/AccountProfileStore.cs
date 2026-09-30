using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Infrastructure.Storage;

/// <summary>
/// 账号档案持久化 DTO（认证束以 DPAPI 密文存储，落盘不含明文登录态）。
/// </summary>
internal sealed class AccountProfileDto
{
    /// <summary>档案标识。</summary>
    public Guid Id { get; set; }

    /// <summary>显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>登录邮箱。</summary>
    public string? Email { get; set; }

    /// <summary>认证键密文映射（键名 → DPAPI 密文 Base64）。</summary>
    public Dictionary<string, string> ProtectedEntries { get; set; } = new();

    /// <summary>捕获时间（UTC）。</summary>
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>最近使用时间（UTC）。</summary>
    public DateTimeOffset? LastUsedAtUtc { get; set; }
}

/// <summary>
/// 账号档案仓储实现：每份档案一个 JSON 文件，认证束经 DPAPI（CurrentUser）加密后落盘。
/// </summary>
public sealed class AccountProfileStore : IAccountProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _directory;
    private readonly ILogger<AccountProfileStore> _logger;

    /// <summary>创建账号档案仓储。</summary>
    /// <param name="rootDirectory">工具数据根目录。</param>
    /// <param name="logger">日志。</param>
    public AccountProfileStore(string rootDirectory, ILogger<AccountProfileStore> logger)
    {
        _directory = Path.Combine(rootDirectory, "profiles");
        Directory.CreateDirectory(_directory);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var profiles = new List<AccountProfile>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*.profile.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var profile = await LoadAsync(file, cancellationToken).ConfigureAwait(false);
            if (profile is not null)
            {
                profiles.Add(profile);
            }
        }

        return profiles.OrderBy(p => p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task SaveAsync(AccountProfile profile, CancellationToken cancellationToken = default)
    {
        var dto = new AccountProfileDto
        {
            Id = profile.Id,
            Name = profile.Name,
            Email = profile.Email,
            CapturedAtUtc = profile.Bundle.CapturedAtUtc,
            CreatedAtUtc = profile.CreatedAtUtc,
            LastUsedAtUtc = profile.LastUsedAtUtc,
            ProtectedEntries = profile.Bundle.Entries.ToDictionary(kv => kv.Key, kv => DpapiProtector.ProtectText(kv.Value))
        };

        var path = FilePath(profile.Id);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("账号档案已保存：{Name}（{Path}）", profile.Name, path);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var path = FilePath(profileId);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("账号档案已删除：{Id}", profileId);
        }

        return Task.CompletedTask;
    }

    private string FilePath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".profile.json");

    private async Task<AccountProfile?> LoadAsync(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<AccountProfileDto>(json, JsonOptions);
            if (dto is null)
            {
                return null;
            }

            var entries = dto.ProtectedEntries.ToDictionary(kv => kv.Key, kv => DpapiProtector.UnprotectText(kv.Value));
            var profile = new AccountProfile(
                dto.Name,
                new AuthBundle(entries, dto.CapturedAtUtc),
                dto.Email,
                dto.Id,
                dto.CreatedAtUtc)
            {
                LastUsedAtUtc = dto.LastUsedAtUtc
            };
            return profile;
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or SecurityException or FormatException)
        {
            _logger.LogError(ex, "账号档案加载失败（可能由其他 Windows 用户创建）：{Path}", path);
            return null;
        }
    }
}
