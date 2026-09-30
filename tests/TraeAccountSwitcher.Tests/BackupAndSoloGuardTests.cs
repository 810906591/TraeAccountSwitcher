using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using TraeAccountSwitcher.Domain.ValueObjects;
using TraeAccountSwitcher.Infrastructure.Storage;
using TraeAccountSwitcher.Infrastructure.Trae;
using Xunit;

namespace TraeAccountSwitcher.Tests;

/// <summary>备份仓储测试。</summary>
public sealed class BackupStoreTests : IDisposable
{
    private readonly string _root;
    private readonly string _soloDb;
    private readonly BackupStore _store;

    public BackupStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tas-backups-" + Guid.NewGuid().ToString("N"));
        _soloDb = Path.Combine(_root, "ai-agent.db");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(_soloDb, RandomNumberGenerator.GetBytes(1024));
        _store = new BackupStore(_root, _soloDb, NullLogger<BackupStore>.Instance);
    }

    [Fact]
    public async Task 备份SOLO库_记录与文件一致且校验通过()
    {
        var record = await _store.BackupSoloDatabaseAsync("测试备份");
        Assert.True(await _store.VerifyAsync(record));
        Assert.Equal(new FileInfo(_soloDb).Length, record.SizeBytes);
        Assert.True(File.Exists(record.FilePath));
    }

    [Fact]
    public async Task 认证快照_往返一致()
    {
        var bundle = new AuthBundle(new Dictionary<string, string> { ["iCubeAuthInfo://a"] = "tok-1" });
        var record = await _store.SnapshotAuthBundleAsync(bundle, "切换前快照");

        var restored = await _store.ReadAuthSnapshotAsync(record);
        Assert.NotNull(restored);
        Assert.Equal("tok-1", restored!.Entries["iCubeAuthInfo://a"]);
        Assert.Equal("切换前快照", record.Description);
    }

    [Fact]
    public async Task 清理超量备份_仅保留最近N条()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(1100);
            await _store.BackupSoloDatabaseAsync($"第{i}次");
        }

        await _store.PruneAsync(BackupKind.SoloDatabase, 2);
        var remaining = (await _store.ListAsync()).Where(r => r.Kind == BackupKind.SoloDatabase).ToList();

        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, r => Assert.True(File.Exists(r.FilePath)));
    }

    [Fact]
    public async Task SOLO库损坏后从备份恢复_内容一致()
    {
        var record = await _store.BackupSoloDatabaseAsync("恢复源");
        var originalBytes = await File.ReadAllBytesAsync(record.FilePath);

        await File.WriteAllBytesAsync(_soloDb, RandomNumberGenerator.GetBytes(512));
        await _store.RestoreSoloDatabaseAsync(record);

        var restoredBytes = await File.ReadAllBytesAsync(_soloDb);
        Assert.Equal(originalBytes, restoredBytes);
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

/// <summary>SOLO 数据守卫测试。</summary>
public sealed class SoloDataGuardServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _soloDb;
    private readonly SoloDataGuardService _guard;

    public SoloDataGuardServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tas-solo-" + Guid.NewGuid().ToString("N"));
        _soloDb = Path.Combine(_root, TraePathLocator.SoloDatabaseRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(_soloDb)!);
        File.WriteAllBytes(_soloDb, new byte[2048]);
        _guard = new SoloDataGuardService(
            new TraePathLocator(NullLogger<TraePathLocator>.Instance, _root, executablePathOverride: null),
            _root,
            NullLogger<SoloDataGuardService>.Instance);
    }

    [Fact]
    public async Task 计算清单_指纹稳定且与基线一致()
    {
        var manifest = await _guard.ComputeManifestAsync();
        await _guard.SaveBaselineAsync(manifest);

        var baseline = await _guard.LoadBaselineAsync();
        var recomputed = await _guard.ComputeManifestAsync();

        Assert.NotNull(baseline);
        Assert.Equal(manifest.Sha256Hex, baseline!.Sha256Hex);
        Assert.Equal(recomputed.Sha256Hex, baseline.Sha256Hex);
    }

    [Fact]
    public async Task 数据变化后_新指纹与基线不一致()
    {
        var manifest = await _guard.ComputeManifestAsync();
        await _guard.SaveBaselineAsync(manifest);

        await File.WriteAllBytesAsync(_soloDb, new byte[4096]);
        var changed = await _guard.ComputeManifestAsync();

        Assert.NotEqual(manifest.Sha256Hex, changed.Sha256Hex);
    }

    [Fact]
    public async Task 任务库不存在时_GetDatabasePath返回null()
    {
        File.Delete(_soloDb);
        Assert.Null(_guard.GetDatabasePath());
        await Assert.ThrowsAsync<TraeAccountSwitcher.Domain.Exceptions.SoloDataIntegrityException>(() => _guard.ComputeManifestAsync());
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
