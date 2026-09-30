namespace TraeAccountSwitcher.Domain.ValueObjects;

/// <summary>
/// SOLO 任务库数据清单值对象：描述本地 ai-agent 任务库在某一时刻的完整性指纹。
/// </summary>
public sealed class SoloDataManifest
{
    /// <summary>任务库主数据库文件完整路径。</summary>
    public string DatabasePath { get; }

    /// <summary>文件大小（字节）。</summary>
    public long SizeBytes { get; }

    /// <summary>SHA-256 摘要（十六进制小写）。</summary>
    public string Sha256Hex { get; }

    /// <summary>计算时间（UTC）。</summary>
    public DateTimeOffset VerifiedAtUtc { get; }

    /// <summary>创建数据清单。</summary>
    /// <param name="databasePath">数据库文件路径。</param>
    /// <param name="sizeBytes">文件大小（字节）。</param>
    /// <param name="sha256Hex">SHA-256 十六进制摘要。</param>
    /// <param name="verifiedAtUtc">计算时间（UTC）。</param>
    public SoloDataManifest(string databasePath, long sizeBytes, string sha256Hex, DateTimeOffset verifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径不能为空。", nameof(databasePath));
        }

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "文件大小不能为负数。");
        }

        if (string.IsNullOrWhiteSpace(sha256Hex))
        {
            throw new ArgumentException("SHA-256 摘要不能为空。", nameof(sha256Hex));
        }

        DatabasePath = databasePath;
        SizeBytes = sizeBytes;
        Sha256Hex = sha256Hex;
        VerifiedAtUtc = verifiedAtUtc;
    }

    /// <summary>以人类可读格式返回文件大小。</summary>
    public string SizeDisplay => SizeBytes switch
    {
        >= 1L << 30 => $"{SizeBytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{SizeBytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{SizeBytes / (double)(1L << 10):F1} KB",
        _ => $"{SizeBytes} B"
    };

    /// <summary>返回短摘要预览（前 12 位，不足则返回原值）。</summary>
    public string ShaPreview => Sha256Hex.Length > 12 ? Sha256Hex[..12] : Sha256Hex;
}
