using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Primitives;
using TraeAccountSwitcher.Application.Models;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Application.UseCases;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.ValueObjects;
using TraeAccountSwitcher.Presentation.Logging;

namespace TraeAccountSwitcher.Presentation.ViewModels;

/// <summary>
/// 主视图模型依赖集合。
/// </summary>
public sealed class MainViewModelDependencies
{
    /// <summary>登录态访问器。</summary>
    public ITraeAuthAccessor AuthAccessor { get; init; } = null!;

    /// <summary>Trae 进程管理器。</summary>
    public ITraeAppManager AppManager { get; init; } = null!;

    /// <summary>备份仓储。</summary>
    public IBackupRepository BackupRepository { get; init; } = null!;

    /// <summary>捕获用例。</summary>
    public CaptureCurrentAccountUseCase CaptureUseCase { get; init; } = null!;

    /// <summary>切换用例。</summary>
    public SwitchAccountUseCase SwitchUseCase { get; init; } = null!;

    /// <summary>完整性校验用例。</summary>
    public VerifySoloIntegrityUseCase VerifySoloUseCase { get; init; } = null!;

    /// <summary>备份用例。</summary>
    public BackupSoloDataUseCase BackupSoloUseCase { get; init; } = null!;

    /// <summary>总览用例。</summary>
    public GetOverviewUseCase OverviewUseCase { get; init; } = null!;

    /// <summary>界面日志接收器。</summary>
    public UiLogSink LogSink { get; init; } = null!;

    /// <summary>日志。</summary>
    public ILogger<MainViewModel> Logger { get; init; } = null!;
}

/// <summary>
/// 主视图模型：账号捕获/切换、SOLO 数据完整性校验与备份、操作日志展示。
/// 所有 IO 在 TaskPool 执行，结果经 MainThreadScheduler 回到 UI 线程；订阅统一纳入 CompositeDisposable。
/// </summary>
public sealed class MainViewModel : ReactiveObject, IDisposable
{
    private readonly MainViewModelDependencies _deps;
    private readonly CompositeDisposable _disposables = new();
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private bool _disposed;

    private string _traeStatus = "检测中…";
    private string _busyText = string.Empty;
    private string _newProfileName = string.Empty;
    private string _soloDbPath = "未找到";
    private string _soloStatus = "未校验";
    private string _soloSize = "—";
    private string _traePath = "未找到";

    /// <summary>创建主视图模型。</summary>
    /// <param name="dependencies">依赖集合。</param>
    public MainViewModel(MainViewModelDependencies dependencies)
    {
        _deps = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        CaptureCommand = ReactiveCommand.CreateFromTask(CaptureAsync);
        LaunchTraeCommand = ReactiveCommand.CreateFromTask(LaunchTraeAsync);
        SwitchCommand = ReactiveCommand.CreateFromTask<Guid>(SwitchAsync);
        VerifySoloCommand = ReactiveCommand.CreateFromTask(VerifySoloAsync);
        RebaselineCommand = ReactiveCommand.CreateFromTask(RebaselineAsync);
        BackupSoloCommand = ReactiveCommand.CreateFromTask(BackupSoloAsync);

        _deps.LogSink.LogEmitted += OnLogEmitted;
        _disposables.Add(Disposable.Create(() => _deps.LogSink.LogEmitted -= OnLogEmitted));

        _deps.Logger.LogInformation("主界面初始化完成");
    }

    /// <summary>账号档案卡片集合。</summary>
    public ObservableCollection<AccountCardViewModel> Profiles { get; } = new();

    /// <summary>日志行集合（倒序）。</summary>
    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>刷新总览命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    /// <summary>捕获当前账号命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> CaptureCommand { get; }

    /// <summary>启动 Trae（引导登录）命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> LaunchTraeCommand { get; }

    /// <summary>切换到指定账号命令（参数为档案标识）。</summary>
    public ReactiveCommand<Guid, RxVoid> SwitchCommand { get; }

    /// <summary>SOLO 完整性校验命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> VerifySoloCommand { get; }

    /// <summary>重建 SOLO 基线命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> RebaselineCommand { get; }

    /// <summary>SOLO 备份命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> BackupSoloCommand { get; }

    /// <summary>Trae 运行状态文本。</summary>
    public string TraeStatus { get => _traeStatus; set => this.RaiseAndSetIfChanged(ref _traeStatus, value); }

    /// <summary>忙碌提示；空表示空闲。</summary>
    public string BusyText { get => _busyText; set => this.RaiseAndSetIfChanged(ref _busyText, value); }

    /// <summary>新档案名称输入。</summary>
    public string NewProfileName { get => _newProfileName; set => this.RaiseAndSetIfChanged(ref _newProfileName, value); }

    /// <summary>SOLO 任务库路径显示。</summary>
    public string SoloDbPath { get => _soloDbPath; set => this.RaiseAndSetIfChanged(ref _soloDbPath, value); }

    /// <summary>SOLO 完整性状态显示。</summary>
    public string SoloStatus { get => _soloStatus; set => this.RaiseAndSetIfChanged(ref _soloStatus, value); }

    /// <summary>SOLO 大小显示。</summary>
    public string SoloSize { get => _soloSize; set => this.RaiseAndSetIfChanged(ref _soloSize, value); }

    /// <summary>Trae 安装路径显示。</summary>
    public string TraePath { get => _traePath; set => this.RaiseAndSetIfChanged(ref _traePath, value); }

    /// <summary>释放订阅资源。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposables.Dispose();
    }

    /// <summary>界面加载后触发的初始刷新（由视图激活调用）。</summary>
    public void Activate()
    {
        var execution = RefreshCommand.Execute();
        System.ObservableExtensions.Subscribe(execution, _ => { });
    }

    private async Task RefreshAsync()
    {
        try
        {
            var overview = await _deps.OverviewUseCase.ExecuteAsync().ConfigureAwait(false);
            RunOnUi(() =>
            {
                Profiles.Clear();
                foreach (var profile in overview.Profiles)
                {
                    Profiles.Add(AccountCardViewModel.FromProfile(profile));
                }

                TraeStatus = overview.TraeRunning ? "Trae 正在运行" : "Trae 未运行";
                TraePath = overview.TraePath ?? "未找到";
                SoloDbPath = overview.Solo.Current?.DatabasePath ?? "未找到";
                SoloSize = overview.Solo.Current?.SizeDisplay ?? "—";
                SoloStatus = DescribeSoloState(overview.Solo);
            });
        }
        catch (Exception ex)
        {
            AppendLog($"刷新失败：{ResolveMessage(ex)}");
        }
    }

    private async Task CaptureAsync()
    {
        SetBusy("正在捕获当前账号…");
        try
        {
            var name = string.IsNullOrWhiteSpace(NewProfileName) ? $"账号 {DateTime.Now:MMdd-HHmm}" : NewProfileName.Trim();
            var profile = await _deps.CaptureUseCase.ExecuteAsync(name).ConfigureAwait(false);
            AppendLog($"已捕获账号档案「{profile.Name}」（{profile.Email ?? "未知邮箱"}）");
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendLog($"捕获失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private async Task LaunchTraeAsync()
    {
        SetBusy("正在启动 Trae…");
        try
        {
            await _deps.AppManager.StartAsync().ConfigureAwait(false);
            AppendLog("Trae 已启动。请在 Trae 中退出当前账号并登录目标账号，完成后回到本工具点击「捕获当前账号」。");
        }
        catch (Exception ex)
        {
            AppendLog($"启动 Trae 失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private async Task SwitchAsync(Guid profileId)
    {
        SetBusy("正在切换账号…");
        try
        {
            var progress = new Progress<OperationProgress>(p => RunOnUi(() => BusyText = p.Message));
            var result = await _deps.SwitchUseCase.ExecuteAsync(profileId, new SwitchOptions(), progress)
                .ConfigureAwait(false);
            AppendLog(result.Success
                ? $"切换成功：{result.TargetProfile?.Name}。SOLO 任务列表保持本地原样。"
                : result.Message);
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendLog($"切换失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private async Task VerifySoloAsync()
    {
        SetBusy("正在校验 SOLO 任务库…");
        try
        {
            var report = await _deps.VerifySoloUseCase.ExecuteAsync().ConfigureAwait(false);
            AppendLog(DescribeSoloReport(report));
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendLog($"校验失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private async Task RebaselineAsync()
    {
        SetBusy("正在重建 SOLO 基线…");
        try
        {
            var manifest = await _deps.VerifySoloUseCase.RebaselineAsync().ConfigureAwait(false);
            AppendLog($"已重建基线：{manifest.ShaPreview}…（{manifest.SizeDisplay}）。后续切换将以该指纹校验任务库未被误改。");
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendLog($"重建基线失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private async Task BackupSoloAsync()
    {
        SetBusy("正在备份 SOLO 任务库（数百 MB，请稍候）…");
        try
        {
            var record = await _deps.BackupSoloUseCase.ExecuteAsync("手动备份").ConfigureAwait(false);
            AppendLog($"备份完成并校验通过：{Path.GetFileName(record.FilePath)}（{record.SizeBytes / 1024.0 / 1024.0:F0} MB）");
        }
        catch (Exception ex)
        {
            AppendLog($"备份失败：{ResolveMessage(ex)}");
        }
        finally
        {
            ClearBusy();
        }
    }

    private static string DescribeSoloState(SoloIntegrityReport report) => report.State switch
    {
        SoloIntegrityState.NotFound => "任务库不存在",
        SoloIntegrityState.NoBaseline => "尚未建立基线（首次使用请点击「重建基线」）",
        SoloIntegrityState.Ok => "与基线一致",
        _ => "与基线不一致（确认无误后可重建基线）"
    };

    private static string DescribeSoloReport(SoloIntegrityReport report) => report.State switch
    {
        SoloIntegrityState.Ok => "SOLO 任务库与基线一致，可以安全切换。",
        SoloIntegrityState.Changed => "SOLO 任务库与基线不一致：若为 Trae 正常使用所致，请点击「重建基线」确认。",
        SoloIntegrityState.NoBaseline => "尚未建立基线，请点击「重建基线」。",
        _ => "未找到 SOLO 任务库。"
    };

    private static string ResolveMessage(Exception ex)
        => ex is DomainException domainEx ? domainEx.Message : $"{ex.GetType().Name}: {ex.Message}";

    private void SetBusy(string text) => RunOnUi(() => BusyText = text);

    private void ClearBusy() => RunOnUi(() => BusyText = string.Empty);

    private void OnLogEmitted(object? sender, LogRenderedEventArgs e) => RunOnUi(() => LogLines.Insert(0, e.Line));

    private void AppendLog(string line) => RunOnUi(() => LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {line}"));

    private void RunOnUi(Action action)
    {
        var context = _uiContext;
        if (context is null || context == SynchronizationContext.Current)
        {
            action();
        }
        else
        {
            context.Post(_ => action(), null);
        }
    }
}
