using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CMS5000.Models;

namespace CMS5000.Services;

/// <summary>
/// 앱 활동 로그 서비스. 메모리에 항목을 모아 UI(로그 화면)에 실시간 바인딩하고,
/// 동시에 LocalAppData의 일자별 로그 파일에 누적 기록한다(오프라인 동작).
/// 어디서든 <c>AppLogService.Info(...)</c> 등으로 호출 가능.
/// </summary>
public static class AppLogService
{
    private const int MaxEntries = 1000;
    private static readonly object FileLock = new();

    /// <summary>UI 바인딩용 로그 목록(최신이 위). 로그 화면이 그대로 구독한다.</summary>
    public static ObservableCollection<AppLogEntry> Entries { get; } = [];

    private static string LogDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMS5000", "logs");

    public static string CurrentLogFilePath =>
        Path.Combine(LogDir, $"cms5000-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info   (string category, string message, string? user = null) => Log(LogLevel.Info,    category, message, user);
    public static void Success(string category, string message, string? user = null) => Log(LogLevel.Success, category, message, user);
    public static void Warning(string category, string message, string? user = null) => Log(LogLevel.Warning, category, message, user);
    public static void Error  (string category, string message, string? user = null) => Log(LogLevel.Error,   category, message, user);

    public static void Log(LogLevel level, string category, string message, string? user = null)
    {
        var entry = new AppLogEntry
        {
            Time     = DateTime.Now,
            Level    = level,
            Category = category,
            Message  = message,
            User     = user ?? AuthService.CurrentUser?.DisplayName ?? ""
        };

        AddToCollection(entry);
        AppendToFile(entry);
    }

    private static void AddToCollection(AppLogEntry entry)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => AddToCollection(entry));
            return;
        }

        Entries.Insert(0, entry);
        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(Entries.Count - 1);
    }

    private static void AppendToFile(AppLogEntry entry)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(CurrentLogFilePath, entry.ToLine() + Environment.NewLine);
            }
        }
        catch
        {
            // 파일 기록 실패는 무시 (메모리 로그는 유지) — 로깅이 앱 동작을 막지 않도록.
        }
    }

    public static void Clear()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(Clear);
            return;
        }
        Entries.Clear();
    }
}
