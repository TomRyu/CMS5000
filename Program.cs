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
            if (EventWaitHandle.TryOpenExisting(ActivateName, out var existing))
                existing.Set();
            return;
        }

        AppLogService.Info("시스템", $"CMS-5000 시작 (v{UpdateService.GetCurrentVersionText()})");

        // App 을 먼저 생성해 Application.Current/리소스를 준비한다(접속 다이얼로그 표시에 필요).
        // 연결 단계에서 다이얼로그가 닫혀도 앱이 자동 종료되지 않도록 ShutdownMode 를 명시 모드로 둔다.
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!ConnectDbWithRetry())
            return;

        app.ShutdownMode = ShutdownMode.OnLastWindowClose;   // 정상 모드 복원(StartupUri 로 MainWindow 표시)
        StartActivationListener(app);
        app.Run();

        GC.KeepAlive(_instanceMutex);
    }

    /// <summary>
    /// PostgreSQL 초기화 + 연결 체크. 최초 실행(사용자 접속정보 없음)이거나 연결 실패 시
    /// 접속 다이얼로그(DbConnectView)로 접속정보를 입력받아 connection.json 을 생성한다.
    /// </summary>
    private static bool ConnectDbWithRetry()
    {
        try { PostgresService.Initialize(); }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 로드 실패:\n\n{ex.Message}\n\nappsettings.json을 확인하세요.",
                "CMS-5000 시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        // 최초 실행: 저장된 접속정보가 없으면 바로 접속 다이얼로그로 입력받는다.
        if (!PostgresService.HasUserConfig)
        {
            if (!ShowConnectDialog())
                return false;   // 사용자가 취소 → 종료
        }

        while (true)
        {
            try
            {
                Task.Run(async () => await PostgresService.EnsureReachableAsync()).GetAwaiter().GetResult();
                AppLogService.Success("시스템", "데이터베이스 연결 완료");

                // 확장 스키마(Rack/Module/Channel Config) 멱등 보장
                try
                {
                    Task.Run(async () => await PostgresService.EnsureSchemaAsync()).GetAwaiter().GetResult();
                    AppLogService.Success("시스템", "스키마 확인 완료");
                }
                catch (Exception sx)
                {
                    AppLogService.Error("시스템", $"스키마 확인 실패(권한 등): {sx.Message}");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Error("시스템", $"데이터베이스 연결 실패: {ex.Message}");
                var result = MessageBox.Show(
                    $"데이터베이스 연결에 실패했습니다.\n\n{ex.Message}\n\n접속정보를 입력하시겠습니까?\n" +
                    "([예] 접속정보 입력 / [아니요] 종료)",
                    "CMS-5000 연결 오류",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result != MessageBoxResult.Yes)
                    return false;

                if (!ShowConnectDialog())
                    return false;   // 다이얼로그에서 취소 → 종료
            }
        }
    }

    /// <summary>접속 다이얼로그를 모달로 띄운다. OK(연결 성공)면 true, 취소면 false.</summary>
    private static bool ShowConnectDialog()
    {
        var dlg = new Views.Admin.DbConnectView();
        dlg.ShowDialog();
        return dlg.Connected;
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
