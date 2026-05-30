using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels;

namespace CMS5000;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        AddHandler(TreeView.SelectedItemChangedEvent, new RoutedPropertyChangedEventHandler<object>(NavTree_SelectedItemChanged));

        // 무활동 자동 로그아웃: 로그인 상태일 때만 감시
        SessionTimeoutService.Start(
            shouldMonitor: () => !_viewModel.IsLoginVisible,
            onTimeout:     () => _viewModel.TriggerAutoLogout());
    }

    private void NavTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is NavNode node)
            viewModel.SelectNavNode(node);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RestoreWindowPlacement();
    }

    private void RestoreWindowPlacement()
    {
        var s = LocalSettingsService.Current;
        if (s.WindowWidth is not > 0 || s.WindowHeight is not > 0) return;

        var width  = s.WindowWidth.Value;
        var height = s.WindowHeight.Value;
        var left   = s.WindowLeft  ?? Left;
        var top    = s.WindowTop   ?? Top;

        // 멀티모니터: 가상 화면 밖이면 보이도록 클램프
        var vsLeft   = SystemParameters.VirtualScreenLeft;
        var vsTop    = SystemParameters.VirtualScreenTop;
        var vsRight  = vsLeft + SystemParameters.VirtualScreenWidth;
        var vsBottom = vsTop  + SystemParameters.VirtualScreenHeight;

        width  = Math.Min(width,  SystemParameters.VirtualScreenWidth);
        height = Math.Min(height, SystemParameters.VirtualScreenHeight);
        left   = Math.Max(vsLeft, Math.Min(left, vsRight  - width));
        top    = Math.Max(vsTop,  Math.Min(top,  vsBottom - height));

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left; Top = top; Width = width; Height = height;
        WindowState = s.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void SaveWindowPlacement()
    {
        var s = LocalSettingsService.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;

        // 최대화 상태면 복원 위치(RestoreBounds)를 저장해 다음 실행 시 정상 크기 보존
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        s.WindowLeft   = bounds.Left;
        s.WindowTop    = bounds.Top;
        s.WindowWidth  = bounds.Width;
        s.WindowHeight = bounds.Height;
        LocalSettingsService.Save();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var result = MessageBox.Show(
            "프로그램을 종료하시겠습니까?",
            "CMS-5000 종료",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            base.OnClosing(e);
            return;
        }

        SaveWindowPlacement();
        base.OnClosing(e);
    }
}
