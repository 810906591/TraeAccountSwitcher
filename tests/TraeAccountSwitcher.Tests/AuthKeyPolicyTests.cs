using TraeAccountSwitcher.Domain.Services;
using TraeAccountSwitcher.Domain.ValueObjects;
using Xunit;

namespace TraeAccountSwitcher.Tests;

/// <summary>认证键策略单元测试。</summary>
public class AuthKeyPolicyTests
{
    [Theory]
    [InlineData("iCubeAuthInfo://icube.cloudide", true)]
    [InlineData("iCubeAuthInfo://usertag", true)]
    [InlineData("iCubeAuthInfo://icube-dc:123456", true)]
    [InlineData("iCubeServerData://icube.cloudide", true)]
    [InlineData("iCubeHostInfo", false)]
    [InlineData("theme", false)]
    [InlineData("windowsState", false)]
    [InlineData("icube_gtm", false)]
    public void IsAuthKey_区分登录态键与普通设置键(string key, bool expected)
    {
        Assert.Equal(expected, AuthKeyPolicy.IsAuthKey(key));
    }

    [Fact]
    public void SelectAuthKeys_仅保留登录态键()
    {
        var keys = new[] { "theme", "iCubeAuthInfo://icube.cloudide", "iCubeHostInfo", "iCubeServerData://x" };
        var selected = AuthKeyPolicy.SelectAuthKeys(keys).ToList();

        Assert.Equal(2, selected.Count);
        Assert.Contains("iCubeAuthInfo://icube.cloudide", selected);
        Assert.Contains("iCubeServerData://x", selected);
    }
}

/// <summary>认证束值对象单元测试。</summary>
public class AuthBundleTests
{
    [Fact]
    public void 构造_空集合应抛出参数异常()
    {
        Assert.Throws<ArgumentException>(() => new AuthBundle(new Dictionary<string, string>()));
    }

    [Fact]
    public void 构造_应保留全部键值且不区分大小写键比较器被避免()
    {
        var entries = new Dictionary<string, string> { ["iCubeAuthInfo://a"] = "v1", ["iCubeServerData://b"] = "v2" };
        var bundle = new AuthBundle(entries);

        Assert.Equal(2, bundle.Entries.Count);
        Assert.Equal("v1", bundle.Entries["iCubeAuthInfo://a"]);
        Assert.Equal("v2", bundle.Entries["iCubeServerData://b"]);
    }

    [Fact]
    public void 构造_键名大小写不同的同名键应视为不同键()
    {
        var entries = new Dictionary<string, string> { ["iCubeAuthInfo://Key"] = "v1" };
        var bundle = new AuthBundle(entries);
        Assert.False(bundle.Entries.ContainsKey("iCubeAuthInfo://key"));
    }
}
