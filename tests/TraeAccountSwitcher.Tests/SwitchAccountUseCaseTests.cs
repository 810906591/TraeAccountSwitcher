using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Application.UseCases;
using TraeAccountSwitcher.Domain.Entities;
using TraeAccountSwitcher.Domain.ValueObjects;
using Xunit;

namespace TraeAccountSwitcher.Tests;

/// <summary>
/// 切换用例测试（Moq 模拟仓储）：验证完整切换流程中登录态被替换、SOLO 校验参与流程、失败时回滚。
/// </summary>
public sealed class SwitchAccountUseCaseTests
{
    private static AccountProfile MakeProfile(string name, string token)
        => new(name, new AuthBundle(new Dictionary<string, string> { ["iCubeAuthInfo://icube.cloudide"] = token }));

    private static SwitchAccountUseCase BuildUseCase(
        Mock<IAccountProfileRepository> profiles,
        Mock<ITraeAuthAccessor> auth,
        Mock<ISoloDataGuard> solo,
        Mock<IBackupRepository> backups,
        Mock<ITraeAppManager> app)
        => new(profiles.Object, auth.Object, solo.Object, backups.Object, app.Object, NullLogger<SwitchAccountUseCase>.Instance);

    [Fact]
    public async Task 切换成功_写回目标登录态并标记最近使用()
    {
        var target = MakeProfile("账号B", "token-B");
        var current = MakeProfile("账号A", "token-A");

        var profiles = new Mock<IAccountProfileRepository>();
        profiles.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountProfile> { current, target });

        var auth = new Mock<ITraeAuthAccessor>();
        AuthBundle? stored = current.Bundle;
        auth.Setup(a => a.ReadAuthBundleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        auth.Setup(a => a.WriteAuthBundleAsync(It.IsAny<AuthBundle>(), It.IsAny<CancellationToken>()))
            .Callback<AuthBundle, CancellationToken>((b, _) => stored = b)
            .Returns(Task.CompletedTask);

        var solo = new Mock<ISoloDataGuard>();
        solo.Setup(s => s.LoadBaselineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoloDataManifest?)null);
        solo.Setup(s => s.ComputeManifestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SoloDataManifest("C:\\ai-agent.db", 1024, "abc", DateTimeOffset.UtcNow));

        var backups = new Mock<IBackupRepository>();
        backups.Setup(b => b.SnapshotAuthBundleAsync(It.IsAny<AuthBundle>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupRecord("snap", BackupKind.PreSwitchAuth, DateTimeOffset.UtcNow, 10, "sha"));

        var app = new Mock<ITraeAppManager>();
        app.Setup(a => a.IsRunning()).Returns(true);

        var useCase = BuildUseCase(profiles, auth, solo, backups, app);
        var result = await useCase.ExecuteAsync(target.Id);

        Assert.True(result.Success, result.Message);
        Assert.Equal("token-B", stored!.Entries["iCubeAuthInfo://icube.cloudide"]);
        Assert.NotNull(target.LastUsedAtUtc);
        app.Verify(a => a.StopAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
        app.Verify(a => a.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SOLO基线不一致_切换被中止且不触碰登录态()
    {
        var target = MakeProfile("账号B", "token-B");
        var profiles = new Mock<IAccountProfileRepository>();
        profiles.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountProfile> { target });

        var auth = new Mock<ITraeAuthAccessor>();
        auth.Setup(a => a.ReadAuthBundleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(target.Bundle);

        var solo = new Mock<ISoloDataGuard>();
        solo.Setup(s => s.LoadBaselineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SoloDataManifest("C:\\ai-agent.db", 1, "baseline", DateTimeOffset.UtcNow));
        solo.Setup(s => s.ComputeManifestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SoloDataManifest("C:\\ai-agent.db", 2, "changed", DateTimeOffset.UtcNow));

        var backups = new Mock<IBackupRepository>();
        var app = new Mock<ITraeAppManager>();

        var useCase = BuildUseCase(profiles, auth, solo, backups, app);
        var result = await useCase.ExecuteAsync(target.Id);

        Assert.False(result.Success);
        auth.Verify(a => a.WriteAuthBundleAsync(It.IsAny<AuthBundle>(), It.IsAny<CancellationToken>()), Times.Never);
        app.Verify(a => a.StopAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task 写回校验失败_自动回滚到切换前登录态()
    {
        var target = MakeProfile("账号B", "token-B");
        var current = MakeProfile("账号A", "token-A");
        var profiles = new Mock<IAccountProfileRepository>();
        profiles.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountProfile> { current, target });

        var auth = new Mock<ITraeAuthAccessor>();
        // 首次读取返回当前登录态（用于快照），写回后读取故意与目标不一致以触发失败
        var readCount = 0;
        auth.Setup(a => a.ReadAuthBundleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++readCount == 1 ? current.Bundle : MakeProfile("错误态", "wrong").Bundle);

        var solo = new Mock<ISoloDataGuard>();
        solo.Setup(s => s.LoadBaselineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoloDataManifest?)null);
        solo.Setup(s => s.ComputeManifestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SoloDataManifest("C:\\ai-agent.db", 1, "abc", DateTimeOffset.UtcNow));

        var snapshotRecord = new BackupRecord("snap", BackupKind.PreSwitchAuth, DateTimeOffset.UtcNow, 10, "sha");
        var backups = new Mock<IBackupRepository>();
        backups.Setup(b => b.SnapshotAuthBundleAsync(It.IsAny<AuthBundle>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshotRecord);
        backups.Setup(b => b.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BackupRecord> { snapshotRecord });
        backups.Setup(b => b.ReadAuthSnapshotAsync(snapshotRecord, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current.Bundle);

        var app = new Mock<ITraeAppManager>();
        app.Setup(a => a.IsRunning()).Returns(false);

        var useCase = BuildUseCase(profiles, auth, solo, backups, app);
        var result = await useCase.ExecuteAsync(target.Id);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        // 回滚写入了切换前的登录态（最后一次写回调用是回滚）
        auth.Verify(a => a.WriteAuthBundleAsync(
            It.Is<AuthBundle>(b => b.Entries["iCubeAuthInfo://icube.cloudide"] == "token-A"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
