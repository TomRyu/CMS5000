namespace CMS5000.Models;

/// <summary>로그 심각도 수준.</summary>
public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 애플리케이션 활동 로그 항목 1건.
/// 로그인 이력(<see cref="LoginLog"/>, Supabase)과 달리 앱 동작 전반(탐색·업데이트·오류 등)을
/// 로컬에서 기록·표시하기 위한 모델이다.
/// </summary>
public class AppLogEntry
{
    public DateTime Time     { get; init; } = DateTime.Now;
    public LogLevel Level    { get; init; } = LogLevel.Info;
    public string   Category { get; init; } = "";
    public string   Message  { get; init; } = "";
    public string   User     { get; init; } = "";

    public string TimeDisplay => Time.ToString("yyyy-MM-dd HH:mm:ss");

    public string LevelLabel => Level switch
    {
        LogLevel.Success => "성공",
        LogLevel.Warning => "경고",
        LogLevel.Error   => "오류",
        _                => "정보"
    };

    /// <summary>파일 기록용 한 줄 표현.</summary>
    public string ToLine() =>
        $"{TimeDisplay} [{LevelLabel}] {Category} | {Message}" +
        (string.IsNullOrEmpty(User) ? "" : $" (사용자: {User})");
}
