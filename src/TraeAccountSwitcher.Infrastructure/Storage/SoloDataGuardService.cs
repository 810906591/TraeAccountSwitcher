using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.ValueObjects;
using TraeAccountSwitcher.Infrastructure.Trae;

namespace TraeAccountSwitcher.Infrastructure.Storage;

/// <summary>基线清单持久化 DTO。</summary>
internal sealed class SoloBaselineDto
{
    /// <summary>数据库路径。</summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>大小（字节）。</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256。</summary>
    public string Sha256Hex { get; set; } = string.Empty;

    /// <summary>计算时间（UTC）。</summary>
    public DateTimeOffset VerifiedAtUtc { get; set; }
}

/// <summary>
/// SOLO 数据守卫实现：计算 ai-agent 任务库 SHA-256 指纹并管理基线文件。
/// 基线用于在切换前确认“任务库未被误改”，是本地任务数据完整性的核心防线。
/// </summary>
public sealed class SoloDataGuardService : ISoloDataGuard
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly TraePathLocator _paths;
    private readonly string _baselinePath;
    private readonly ILogger<SoloDataGuardService> _logger;

    /// <summary>创建 SOLO 数据守卫。</summary>
    /// <param name="paths">Trae 路径定位器。</param>
    /// <param name="rootDirectory">工具数据根目录。</param>
    /// <param name="logger">日志。</param>
    public SoloDataGuardService(TraePathLocator paths, string rootDirectory, ILogger<SoloDataGuardService> logger)
    {
        _paths = paths;
        _baselinePath = Path.Combine(rootDirectory, "solo-baseline.json");
        _logger = logger;
    }

    /// <inheritdoc />
    public string? GetDatabasePath()
    {
        var path = _paths.SoloDatabasePath;
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <inheritdoc />
    public async Task<SoloDataManifest> ComputeManifestAsync(CancellationToken cancellationToken = default)
    {
        var path = GetDatabasePath()
            ?? throw new TraeAccountSwitcher.Domain.Exceptions.SoloDataIntegrityException("SOLO 任务库文件不存在，无法计算完整性指纹。");

        var (size, sha) = await Task.Run(
            () => ComputeFingerprint(path, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return new SoloDataManifest(path, size, sha, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<SoloDataManifest?> LoadBaselineAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_baselinePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_baselinePath, cancellationToken).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<SoloBaselineDto>(json, JsonOptions);
            return dto is null ? null : new SoloDataManifest(dto.DatabasePath, dto.SizeBytes, dto.Sha256Hex, dto.VerifiedAtUtc);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "基线清单读取失败");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveBaselineAsync(SoloDataManifest manifest, CancellationToken cancellationToken = default)
    {
        var dto = new SoloBaselineDto
        {
            DatabasePath = manifest.DatabasePath,
            SizeBytes = manifest.SizeBytes,
            Sha256Hex = manifest.Sha256Hex,
            VerifiedAtUtc = manifest.VerifiedAtUtc
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await File.WriteAllTextAsync(_baselinePath, json, cancellationToken).ConfigureAwait(false);
    }

    private static (long Size, string Sha256) ComputeFingerprint(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        ct.ThrowIfCancellationRequested();
        return (stream.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }
}
