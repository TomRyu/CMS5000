using System.Threading;
using System.Windows;
using CMS5000.Services;
using Velopack;

namespace CMS5000;

class Program
{
    private const string MutexName    = "CMS5000_SingleInstance_Mutex";
    private const string ActivateName = "CMS5000_Activate_Event";

    private static Mutex? _instanceMutex;

    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // ── 단일 인스턴스 강제 ──────────────────────────────
        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 이미 실행 중 → 기존 창을 깨우고 종료
            if (EventWaitHandle.TryOpenExisting(ActivateName, out var existing))
                existing.Set();
            return;
        }

        AppLogService.Info("시스템", $"CMS-5000 시작 (v{UpdateService.GetCurrentVersionText()})");

        if (!ConnectApiWithRetry())
            return;

        var app = new App();
        app.InitializeComponent();
        StartActivationListener(app);
        app.Run();

        GC.KeepAlive(_instanceMutex);
    }

    /// <summary>API(Edge Functions) 초기화 + 헬스체크. 실패 시 재시도/종료를 사용자에게 묻는다.</summary>
    private static bool ConnectApiWithRetry()
    {
        try { ApiService.Initialize(); }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 로드 실패:\n\n{ex.Message}\n\nappsettings.json을 확인하세요.",
                "CMS-5000 시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        while (true)
        {
            try
            {
                Task.Run(async () => await ApiService.EnsureReachableAsync()).GetAwaiter().GetResult();
                AppLogService.Success("시스템", "서버 연결 완료");
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Error("시스템", $"서버 연결 실패: {ex.Message}");
                var result = MessageBox.Show(
                    $"서버 연결에 실패했습니다.\n\n{ex.Message}\n\n네트워크 상태를 확인 후 다시 시도하시겠습니까?\n" +
                    "([예] 재연결 / [아니요] 종료)",
                    "CMS-5000 연결 오류",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result != MessageBoxResult.Yes)
                    return false;
            }
        }
    }

    /// <summary>두 번째 실행이 보낸 신호를 받아 현재 창을 전면으로 가져온다.</summary>
    private static void StartActivationListener(App app)
    {
        var handle = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateName);
        var thread = new Thread(() =>
        {
            while (handle.WaitOne())
            {
                app.Dispatcher.BeginInvoke(() =>
                {
                    var window = app.MainWindow;
                    if (window == null) return;
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                    window.Focus();
                });
            }
        })
        { IsBackground = true };
        thread.Start();
    }
}
