using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.ValueObjects;
using TraeAccountSwitcher.Infrastructure.Storage;
using TraeAccountSwitcher.Infrastructure.Trae;
using Xunit;

namespace TraeAccountSwitcher.Tests;

/// <summary>storage.json 登录态访问器测试（在临时目录中模拟 Trae 数据布局）。</summary>
public sealed class StorageJsonAuthAccessorTests : IDisposable
{
    private readonly string _root;
    private readonly TraePathLocator _paths;
    private readonly StorageJsonAuthAccessor _accessor;

    public StorageJsonAuthAccessorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tas-tests-" + Guid.NewGuid().ToString("N"));
        _paths = new TraePathLocator(NullLogger<TraePathLocator>.Instance, _root, executablePathOverride: null);
        Directory.CreateDirectory(Path.Combine(_root, "User", "globalStorage"));
        _accessor = new StorageJsonAuthAccessor(_paths, new StateVscdbReader(_paths, NullLogger<StateVscdbReader>.Instance), NullLogger<StorageJsonAuthAccessor>.Instance);
    }

    [Fact]
    public async Task 写回_只替换登录态键_其余键保持不变()
    {
        var storagePath = _paths.StorageJsonPath!;
        await File.WriteAllTextAsync(storagePath, """
            {"theme":"dark","iCubeAuthInfo://icube.cloudide":"old-token","iCubeHostInfo":{"apiHost":"a"},"windowsState":"x","iCubeAuthInfo://usertag":"old-tag"}
            """);

        var bundle = new AuthBundle(new Dictionary<string, string>
        {
            ["iCubeAuthInfo://icube.cloudide"] = "new-token",
            ["iCubeAuthInfo://icube-dc:999"] = "new-dc"
        });
        await _accessor.WriteAuthBundleAsync(bundle);

        var json = await File.ReadAllTextAsync(storagePath);
        Assert.Contains("new-token", json);
        Assert.Contains("new-dc", json);
        Assert.DoesNotContain("old-token", json);
        Assert.DoesNotContain("old-tag", json);
        Assert.Contains("\"theme\":\"dark\"", json.Replace(" ", string.Empty));
        Assert.Contains("iCubeHostInfo", json);
        Assert.Contains("windowsState", json);

        var read = await _accessor.ReadAuthBundleAsync();
        Assert.NotNull(read);
        Assert.Equal(2, read!.Entries.Count);
        Assert.Equal("new-token", read.Entries["iCubeAuthInfo://icube.cloudide"]);
    }

    [Fact]
    public async Task 读取_无认证键时返回null()
    {
        var storagePath = _paths.StorageJsonPath!;
        await File.WriteAllTextAsync(storagePath, """{"theme":"dark"}""");
        Assert.Null(await _accessor.ReadAuthBundleAsync());
    }

    [Fact]
    public async Task 读取_文件不存在时返回null且可用性为false()
    {
        Assert.False(_accessor.IsTraeDataAvailable());
        Assert.Null(await _accessor.ReadAuthBundleAsync());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>账号档案存储测试（DPAPI 加密落盘）。</summary>
public sealed class AccountProfileStoreTests : IDisposable
{
    private readonly string _root;
    private readonly AccountProfileStore _store;

    public AccountProfileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tas-profiles-" + Guid.NewGuid().ToString("N"));
        _store = new AccountProfileStore(_root, NullLogger<AccountProfileStore>.Instance);
    }

    [Fact]
    public async Task 保存后加载_档案内容一致且落盘不含明文令牌()
    {
        var profile = new AccountProfile(
            "账号A",
            new AuthBundle(new Dictionary<string, string> { ["iCubeAuthInfo://icube.cloudide"] = "secret-token-xyz" }),
            "a@test.com");

        await _store.SaveAsync(profile);
        var loaded = (await _store.GetAllAsync()).Single(p => p.Id == profile.Id);

        Assert.Equal("账号A", loaded.Name);
        Assert.Equal("a@test.com", loaded.Email);
        Assert.Equal("secret-token-xyz", loaded.Bundle.Entries["iCubeAuthInfo://icube.cloudide"]);

        var raw = await File.ReadAllTextAsync(Path.Combine(_root, "profiles", profile.Id.ToString("N") + ".profile.json"));
        Assert.DoesNotContain("secret-token-xyz", raw);
    }

    [Fact]
    public async Task 更新档案_再次加载应返回新值()
    {
        var profile = new AccountProfile("账号B", new AuthBundle(new Dictionary<string, string> { ["k"] = "v1" }));
        await _store.SaveAsync(profile);
        profile.Bundle = new AuthBundle(new Dictionary<string, string> { ["k"] = "v2" });
        profile.LastUsedAtUtc = DateTimeOffset.UtcNow;
        await _store.SaveAsync(profile);

        var loaded = (await _store.GetAllAsync()).Single(p => p.Id == profile.Id);
        Assert.Equal("v2", loaded.Bundle.Entries["k"]);
        Assert.NotNull(loaded.LastUsedAtUtc);
    }

    [Fact]
    public async Task 删除档案_加载结果为空()
    {
        var profile = new AccountProfile("账号C", new AuthBundle(new Dictionary<string, string> { ["k"] = "v" }));
        await _store.SaveAsync(profile);
        await _store.DeleteAsync(profile.Id);
        Assert.Empty(await _store.GetAllAsync());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
