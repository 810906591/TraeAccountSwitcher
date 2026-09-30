using System.IO;
using Autofac;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Infrastructure.Storage;
using TraeAccountSwitcher.Infrastructure.Trae;

namespace TraeAccountSwitcher.Infrastructure.Composition;

/// <summary>
/// Infrastructure 层 Autofac 模块：按功能分组注册仓储实现与 Trae 基础设施服务。
/// </summary>
/// <param name="LoggerFactory">日志工厂（由呈现层注入 Serilog 提供程序）。</param>
/// <param name="RootDirectory">工具数据根目录。</param>
public sealed class InfrastructureModule(ILoggerFactory loggerFactory, string rootDirectory) : Module
{
    /// <summary>日志工厂。</summary>
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;

    /// <summary>工具数据根目录。</summary>
    public string RootDirectory { get; } = rootDirectory;

    /// <summary>工具默认数据根目录（%LOCALAPPDATA%\TraeAccountSwitcher）。</summary>
    public static string DefaultRootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TraeAccountSwitcher");

    /// <summary>加载模块注册。</summary>
    /// <param name="builder">容器构建器。</param>
    protected override void Load(ContainerBuilder builder)
    {
        Directory.CreateDirectory(RootDirectory);

        builder.RegisterInstance(LoggerFactory).As<ILoggerFactory>().SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();

        builder.RegisterType<TraePathLocator>().SingleInstance();
        builder.RegisterType<StateVscdbReader>().SingleInstance();
        builder.RegisterType<StorageJsonAuthAccessor>().As<ITraeAuthAccessor>().SingleInstance();
        builder.RegisterType<TraeAppManager>().As<ITraeAppManager>().SingleInstance();

        builder.Register(c => new AccountProfileStore(
                RootDirectory,
                c.Resolve<ILogger<AccountProfileStore>>()))
            .As<IAccountProfileRepository>().SingleInstance();

        builder.Register(c => new BackupStore(
                RootDirectory,
                c.Resolve<TraePathLocator>().SoloDatabasePath,
                c.Resolve<ILogger<BackupStore>>()))
            .As<IBackupRepository>().SingleInstance();

        builder.Register(c => new SoloDataGuardService(
                c.Resolve<TraePathLocator>(),
                RootDirectory,
                c.Resolve<ILogger<SoloDataGuardService>>()))
            .As<ISoloDataGuard>().SingleInstance();
    }
}
