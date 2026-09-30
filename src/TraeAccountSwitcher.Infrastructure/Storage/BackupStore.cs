using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Infrastructure.Storage;

/// <summary>备份索引条目 DTO。</summary>
internal sealed class BackupEntryDto
{
    /// <summary>备份文件相对路径。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>备份类型。</summary>
    public BackupKind Kind { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>文件大小（字节）。</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 摘要。</summary>
    public string Sha256Hex { get; set; } = string.Empty;

    /// <summary>说明。</summary>
    public string? Description { get; set; }
}

/// <summary>
/// 备份仓储实现：备份目录内以 JSON 索引 + 数据文件方式管理；
/// 认证快照经 DPAPI 加密，SOLO 库为原始副本 + SHA-256 边车校验。
/// </summary>
public sealed class BackupStore : IBackupRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _indexPath;
    private readonly string? _soloDatabasePath;
    private readonly ILogger<BackupStore> _logger;

    /// <summary>创建备份仓储。</summary>
    /// <param name="rootDirectory">工具数据根目录。</param>
    /// <param name="soloDatabasePath">SOLO 任务库路径（可为 null，调用时抛业务异常）。</param>
    /// <param name="logger">日志。</param>
    public BackupStore(string rootDirectory, string? soloDatabasePath, ILogger<BackupStore> logger)
    {
        _directory = Path.Combine(rootDirectory, "backups");
        Directory.CreateDirectory(_directory);
        _indexPath = Path.Combine(_directory, "index.json");
        _soloDatabasePath = soloDatabasePath;
        _logger = logger;
    }

    /// <summary>获取 SOLO 任务库路径；缺失时抛出业务异常。</summary>
    private string RequireSoloDatabasePath()
        => _soloDatabasePath ?? throw new SoloDataIntegrityException("未找到 SOLO 任务库，备份功能不可用。");

    /// <inheritdoc />
    public async Task<BackupRecord> BackupSoloDatabaseAsync(string? description = null, CancellationToken cancellationToken = default)
    {
        var sourcePath = RequireSoloDatabasePath();
        if (!File.Exists(sourcePath))
        {
            throw new SoloDataIntegrityException("SOLO 任务库文件不存在，无法备份。");
        }

        var fileName = $"solo-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.db";
        var targetPath = Path.Combine(_directory, fileName);

        await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true), cancellationToken).ConfigureAwait(false);
        var record = await BuildRecordAsync(targetPath, BackupKind.SoloDatabase, description, cancellationToken).ConfigureAwait(false);
        await AddToIndexAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc />
    public async Task<BackupRecord> SnapshotAuthBundleAsync(AuthBundle bundle, string? description = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(bundle.Entries);
        var protectedPayload = DpapiProtector.ProtectText(payload);
        var fileName = $"auth-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.snap";
        var targetPath = Path.Combine(_directory, fileName);

        await File.WriteAllTextAsync(targetPath, protectedPayload, cancellationToken).ConfigureAwait(false);
        var record = await BuildRecordAsync(targetPath, BackupKind.PreSwitchAuth, description, cancellationToken).ConfigureAwait(false);
        await AddToIndexAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc />
    public async Task<AuthBundle?> ReadAuthSnapshotAsync(BackupRecord record, CancellationToken cancellationToken = default)
    {
        if (record.Kind != BackupKind.PreSwitchAuth || !File.Exists(record.FilePath))
        {
            return null;
        }

        try
        {
            var protectedPayload = await File.ReadAllTextAsync(record.FilePath, cancellationToken).ConfigureAwait(false);
            var json = DpapiProtector.UnprotectText(protectedPayload);
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return entries is { Count: > 0 } ? new AuthBundle(entries, record.CreatedAtUtc) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or SecurityException or FormatException)
        {
            _logger.LogError(ex, "认证快照读取失败：{Path}", record.FilePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_indexPath))
        {
            return Array.Empty<BackupRecord>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_indexPath, cancellationToken).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<BackupEntryDto>>(json, JsonOptions) ?? new List<BackupEntryDto>();
            return entries
                .Where(e => File.Exists(Path.Combine(_directory, e.FileName)))
                .Select(e => new BackupRecord(
                    Path.Combine(_directory, e.FileName),
                    e.Kind,
                    e.CreatedAtUtc,
                    e.SizeBytes,
                    e.Sha256Hex,
                    e.Description))
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "备份索引解析失败");
            return Array.Empty<BackupRecord>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(BackupRecord record, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(record.FilePath))
        {
            return false;
        }

        var actual = await Task.Run(() => ComputeSha256Async(record.FilePath, cancellationToken), cancellationToken).ConfigureAwait(false);
        return string.Equals(actual, record.Sha256Hex, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task RestoreSoloDatabaseAsync(BackupRecord record, CancellationToken cancellationToken = default)
    {
        if (record.Kind != BackupKind.SoloDatabase)
        {
            throw new SoloDataIntegrityException("该记录不是 SOLO 任务库备份，无法恢复。");
        }

        if (!await VerifyAsync(record, cancellationToken).ConfigureAwait(false))
        {
            throw new SoloDataIntegrityException("备份完整性校验失败，已阻止恢复以保护本地任务数据。");
        }

        var targetPath = RequireSoloDatabasePath();
        await Task.Run(() => File.Copy(record.FilePath, targetPath, overwrite: true), cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("SOLO 任务库已从备份恢复：{Path}", record.FilePath);
    }

    /// <inheritdoc />
    public async Task PruneAsync(BackupKind kind, int keep, CancellationToken cancellationToken = default)
    {
        var records = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(r => r.Kind == kind)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();

        foreach (var stale in records.Skip(Math.Max(0, keep)))
        {
            try
            {
                if (File.Exists(stale.FilePath))
                {
                    File.Delete(stale.FilePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "删除旧备份失败：{Path}", stale.FilePath);
            }
        }

        await RewriteIndexAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<BackupRecord> BuildRecordAsync(string path, BackupKind kind, string? description, CancellationToken ct)
    {
        var size = new FileInfo(path).Length;
        var sha = await Task.Run(() => ComputeSha256Async(path, ct), ct).ConfigureAwait(false);
        return new BackupRecord(path, kind, DateTimeOffset.UtcNow, size, sha, description);
    }

    private static string ComputeSha256Async(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        ct.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task AddToIndexAsync(BackupRecord record, CancellationToken ct)
    {
        var entries = (await LoadIndexRawAsync(ct).ConfigureAwait(false));
        entries.Add(new BackupEntryDto
        {
            FileName = Path.GetFileName(record.FilePath),
            Kind = record.Kind,
            CreatedAtUtc = record.CreatedAtUtc,
            SizeBytes = record.SizeBytes,
            Sha256Hex = record.Sha256Hex,
            Description = record.Description
        });
        await SaveIndexAsync(entries, ct).ConfigureAwait(false);
    }

    private async Task<List<BackupEntryDto>> LoadIndexRawAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexPath))
        {
            return new List<BackupEntryDto>();
        }

        var json = await File.ReadAllTextAsync(_indexPath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<BackupEntryDto>>(json, JsonOptions) ?? new List<BackupEntryDto>();
    }

    private async Task SaveIndexAsync(List<BackupEntryDto> entries, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(_indexPath, json, ct).ConfigureAwait(false);
    }

    private async Task RewriteIndexAsync(CancellationToken ct)
    {
        var existing = await LoadIndexRawAsync(ct).ConfigureAwait(false);
        var kept = existing.Where(e => File.Exists(Path.Combine(_directory, e.FileName))).ToList();
        if (kept.Count != existing.Count)
        {
            await SaveIndexAsync(kept, ct).ConfigureAwait(false);
        }
    }
}
