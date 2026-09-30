using Autofac;
using TraeAccountSwitcher.Application.UseCases;
using TraeAccountSwitcher.Presentation.Logging;
using TraeAccountSwitcher.Presentation.ViewModels;
using TraeAccountSwitcher.Presentation.Views;

namespace TraeAccountSwitcher.Presentation.Composition;

/// <summary>
/// Presentation 层 Autofac 模块：注册用例、主视图模型与主窗口。
/// </summary>
public sealed class PresentationModule : Module
{
    /// <summary>加载模块注册。</summary>
    /// <param name="builder">容器构建器。</param>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CaptureCurrentAccountUseCase>().InstancePerDependency();
        builder.RegisterType<SwitchAccountUseCase>().InstancePerDependency();
        builder.RegisterType<VerifySoloIntegrityUseCase>().InstancePerDependency();
        builder.RegisterType<BackupSoloDataUseCase>().InstancePerDependency();
        builder.RegisterType<GetOverviewUseCase>().InstancePerDependency();

        builder.RegisterType<MainViewModelDependenciesBuilder>().SingleInstance();
        builder.RegisterType<MainViewModel>().SingleInstance();
        builder.RegisterType<MainWindow>().SingleInstance();
    }
}
