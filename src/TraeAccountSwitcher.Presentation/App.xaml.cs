using System.IO;
using System.Windows;
using Autofac;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using TraeAccountSwitcher.Infrastructure.Composition;
using TraeAccountSwitcher.Presentation.Composition;
using TraeAccountSwitcher.Presentation.Logging;
using TraeAccountSwitcher.Presentation.ViewModels;
using TraeAccountSwitcher.Presentation.Views;

namespace TraeAccountSwitcher.Presentation;

/// <summary>
/// 应用入口：初始化 Serilog 与 Autofac 容器，装配各层模块并打开主窗口。
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly UiLogSink UiLogSinkInstance = new();

    private IContainer? _container;

    /// <summary>应用启动。</summary>
    /// <param name="e">启动参数。</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureLogging();

        try
        {
            _container = BuildContainer();
            var mainWindow = _container.Resolve<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            MessageBox.Show($"应用启动失败：{ex.Message}", "TRAE 账号切换器", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>应用退出：释放容器与日志。</summary>
    /// <param name="e">退出参数。</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _container?.Dispose();
        Log.Information("应用退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(InfrastructureModule.DefaultRootDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(UiLogSinkInstance)
            .WriteTo.File(
                Path.Combine(logDirectory, "switcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static IContainer BuildContainer()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: false)));
        var builder = new ContainerBuilder();
        builder.RegisterInstance(UiLogSinkInstance).As<UiLogSink>().SingleInstance();
        builder.RegisterModule(new InfrastructureModule(loggerFactory, InfrastructureModule.DefaultRootDirectory));
        builder.RegisterModule<PresentationModule>();
        builder.Register(c => c.Resolve<MainViewModelDependenciesBuilder>().Build(c)).As<MainViewModelDependencies>();
        return builder.Build();
    }
}

/// <summary>主视图模型依赖装配器。</summary>
public sealed class MainViewModelDependenciesBuilder
{
    /// <summary>从容器解析并构建依赖集合。</summary>
    /// <param name="context">组件上下文。</param>
    public MainViewModelDependencies Build(IComponentContext context) => new()
    {
        AuthAccessor = context.Resolve<TraeAccountSwitcher.Application.Repositories.ITraeAuthAccessor>(),
        AppManager = context.Resolve<TraeAccountSwitcher.Application.Repositories.ITraeAppManager>(),
        BackupRepository = context.Resolve<TraeAccountSwitcher.Application.Repositories.IBackupRepository>(),
        CaptureUseCase = context.Resolve<TraeAccountSwitcher.Application.UseCases.CaptureCurrentAccountUseCase>(),
        SwitchUseCase = context.Resolve<TraeAccountSwitcher.Application.UseCases.SwitchAccountUseCase>(),
        VerifySoloUseCase = context.Resolve<TraeAccountSwitcher.Application.UseCases.VerifySoloIntegrityUseCase>(),
        BackupSoloUseCase = context.Resolve<TraeAccountSwitcher.Application.UseCases.BackupSoloDataUseCase>(),
        OverviewUseCase = context.Resolve<TraeAccountSwitcher.Application.UseCases.GetOverviewUseCase>(),
        LogSink = context.Resolve<UiLogSink>(),
        Logger = context.Resolve<ILogger<MainViewModel>>()
    };
}
