using System.IO;
using System.Windows;
using System.Windows.Threading;
using CMS5000.Services;

namespace CMS5000;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogService.Info("시스템", $"CMS-5000 종료 (코드 {e.ApplicationExitCode})");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogService.Error("시스템", $"처리되지 않은 오류: {e.Exception.Message}");
        WriteCrashLog(e.Exception);
        // 종료 시 메시지박스 표시 임시 주석 처리 (요청)
        // MessageBox.Show(
        //     $"오류가 발생했습니다:\n\n{e.Exception.Message}\n\n로그 파일: {CrashLogPath}",
        //     "CMS-5000 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogService.Error("시스템", $"치명적 오류: {ex.Message}");
            WriteCrashLog(ex);
        }
    }

    private static string CrashLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CMS5000", "crash.log");

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var path = CrashLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }
}
