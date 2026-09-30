namespace TraeAccountSwitcher.Domain.Exceptions;

/// <summary>Trae 登录态访问异常：读取或写回 storage.json 认证键失败等情况。</summary>
public sealed class TraeAuthException : DomainException
{
    /// <summary>创建 Trae 登录态访问异常。</summary>
    /// <param name="message">中文错误描述。</param>
    public TraeAuthException(string message) : base(message)
    {
    }

    /// <summary>创建带内部异常的 Trae 登录态访问异常。</summary>
    /// <param name="message">中文错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public TraeAuthException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
