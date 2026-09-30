namespace TraeAccountSwitcher.Domain.Exceptions;

/// <summary>SOLO 数据完整性异常：校验失败或备份损坏时抛出。</summary>
public sealed class SoloDataIntegrityException : DomainException
{
    /// <summary>期望的 SHA-256 摘要（若有）。</summary>
    public string? ExpectedSha256 { get; }

    /// <summary>实际的 SHA-256 摘要（若有）。</summary>
    public string? ActualSha256 { get; }

    /// <summary>创建 SOLO 数据完整性异常。</summary>
    /// <param name="message">中文错误描述。</param>
    /// <param name="expectedSha256">期望摘要。</param>
    /// <param name="actualSha256">实际摘要。</param>
    public SoloDataIntegrityException(
        string message,
        string? expectedSha256 = null,
        string? actualSha256 = null) : base(message)
    {
        ExpectedSha256 = expectedSha256;
        ActualSha256 = actualSha256;
    }
}
