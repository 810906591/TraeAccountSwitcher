using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace TraeAccountSwitcher.Infrastructure.Storage;

/// <summary>
/// DPAPI 数据保护器：以当前 Windows 用户作用域加解密敏感数据（登录态快照、账号档案）。
/// 仅本机当前用户可解密，满足“本地任务列表与登录数据安全性”要求。
/// </summary>
[SupportedOSPlatform("windows")]
public static class DpapiProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TraeAccountSwitcher.v1.AuthEntropy");

    /// <summary>加密任意数据（CurrentUser 作用域）。</summary>
    /// <param name="plain">明文字节。</param>
    /// <returns>密文字节。</returns>
    public static byte[] Protect(byte[] plain)
        => ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

    /// <summary>解密数据。</summary>
    /// <param name="cipher">密文字节。</param>
    /// <returns>明文字节。</returns>
    public static byte[] Unprotect(byte[] cipher)
        => ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);

    /// <summary>加密 UTF-8 文本并返回 Base64。</summary>
    /// <param name="plainText">明文。</param>
    public static string ProtectText(string plainText) => Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(plainText)));

    /// <summary>解密 Base64 密文为 UTF-8 文本。</summary>
    /// <param name="cipherText">Base64 密文。</param>
    public static string UnprotectText(string cipherText) => Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(cipherText)));

    /// <summary>计算数据的 SHA-256 十六进制摘要（小写）。</summary>
    /// <param name="data">数据。</param>
    public static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
