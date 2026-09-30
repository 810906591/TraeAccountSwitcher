namespace TraeAccountSwitcher.Domain.Exceptions;

/// <summary>账号档案操作异常：档案缺失、认证束不匹配等情况。</summary>
public sealed class AccountProfileException : DomainException
{
    /// <summary>创建账号档案异常。</summary>
    /// <param name="message">中文错误描述。</param>
    public AccountProfileException(string message) : base(message)
    {
    }

    /// <summary>创建带内部异常的账号档案异常。</summary>
    /// <param name="message">中文错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public AccountProfileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
