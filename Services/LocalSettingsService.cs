using System.IO;
using System.Text.Json;

namespace CMS5000.Services;

/// <summary>
/// 머신 단위 로컬 설정(LocalAppData). 계정과 무관하게 이 PC에 저장되는 값:
/// 세션 타임아웃, 마지막 창 위치/크기 등. 오프라인 동작.
/// </summary>
public static class LocalSettingsService
{
    public class LocalSettings
    {
        // 세션 타임아웃(분). 0 이하이면 비활성.
        public int SessionTimeoutMinutes { get; set; } = 15;

        // 마지막 창 배치 (null이면 기본값 사용)
        public double? WindowLeft   { get; set; }
        public double? WindowTop    { get; set; }
        public double? WindowWidth  { get; set; }
        public double? WindowHeight { get; set; }
        public bool    WindowMaximized { get; set; }
    }

    private static readonly object Gate = new();
    private static LocalSettings? _current;

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CMS5000", "settings.json");

    public static LocalSettings Current
    {
        get
        {
            if (_current != null) return _current;
            lock (Gate)
            {
                if (_current != null) return _current;
                _current = Load();
                return _current;
            }
        }
    }

    private static LocalSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new LocalSettings();
    }

    public static void Save()
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }
}
