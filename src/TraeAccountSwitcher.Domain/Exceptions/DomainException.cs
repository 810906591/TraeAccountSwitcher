namespace TraeAccountSwitcher.Domain.Exceptions;

/// <summary>领域层业务异常基类：所有可向用户呈现的业务错误均应派生自此类。</summary>
public class DomainException : Exception
{
    /// <summary>创建领域业务异常。</summary>
    /// <param name="message">面向用户的中文错误描述。</param>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>创建带内部异常的领域业务异常。</summary>
    /// <param name="message">面向用户的中文错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
