namespace TraeAccountSwitcher.Domain.ValueObjects;

/// <summary>备份类型。</summary>
public enum BackupKind
{
    /// <summary>切换前的认证快照（自动创建，用于回滚）。</summary>
    PreSwitchAuth,

    /// <summary>SOLO 任务库备份（手动或首次切换前创建）。</summary>
    SoloDatabase
}

/// <summary>
/// 备份记录值对象：描述一次备份产物的元信息。
/// </summary>
public sealed class BackupRecord
{
    /// <summary>备份文件完整路径。</summary>
    public string FilePath { get; }

    /// <summary>备份类型。</summary>
    public BackupKind Kind { get; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>文件大小（字节）。</summary>
    public long SizeBytes { get; }

    /// <summary>SHA-256 摘要（十六进制小写），用于恢复前完整性校验。</summary>
    public string Sha256Hex { get; }

    /// <summary>关联说明（如“切换到账号B 前的认证快照”）。</summary>
    public string? Description { get; }

    /// <summary>创建备份记录。</summary>
    /// <param name="filePath">备份文件路径。</param>
    /// <param name="kind">备份类型。</param>
    /// <param name="createdAtUtc">创建时间（UTC）。</param>
    /// <param name="sizeBytes">文件大小（字节）。</param>
    /// <param name="sha256Hex">SHA-256 摘要。</param>
    /// <param name="description">说明（可选）。</param>
    public BackupRecord(
        string filePath,
        BackupKind kind,
        DateTimeOffset createdAtUtc,
        long sizeBytes,
        string sha256Hex,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("备份文件路径不能为空。", nameof(filePath));
        }

        FilePath = filePath;
        Kind = kind;
        CreatedAtUtc = createdAtUtc;
        SizeBytes = sizeBytes;
        Sha256Hex = sha256Hex;
        Description = description;
    }
}
