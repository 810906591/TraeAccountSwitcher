using System.Windows;
using TraeAccountSwitcher.Presentation.ViewModels;

namespace TraeAccountSwitcher.Presentation.Views;

/// <summary>
/// 主窗口：承载账号切换与 SOLO 数据守护界面。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>创建主窗口。</summary>
    /// <param name="viewModel">主视图模型（容器注入）。</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _viewModel.Activate();
}
