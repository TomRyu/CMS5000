using System.Windows.Input;
using System.Windows.Threading;

namespace CMS5000.Services;

/// <summary>
/// 무활동(마우스·키보드 입력 없음) 시간이 설정값을 넘으면 자동 로그아웃시킨다.
/// 타임아웃 길이는 <see cref="LocalSettingsService"/>에서 매 틱 읽으므로 설정 변경이 즉시 반영된다.
/// </summary>
public static class SessionTimeoutService
{
    private static DispatcherTimer? _timer;
    private static DateTime _lastActivityUtc = DateTime.UtcNow;
    private static Func<bool>? _shouldMonitor;   // 로그인 상태일 때만 true
    private static Action? _onTimeout;
    private static bool _firedForThisIdle;

    /// <summary>앱 시작 시 1회 호출(UI 스레드).</summary>
    public static void Start(Func<bool> shouldMonitor, Action onTimeout)
    {
        _shouldMonitor = shouldMonitor;
        _onTimeout     = onTimeout;

        InputManager.Current.PreProcessInput += OnPreProcessInput;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>로그인 직후 등 타이머 기준점을 지금으로 리셋.</summary>
    public static void ResetActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
        _firedForThisIdle = false;
    }

    private static void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs or TextCompositionEventArgs)
        {
            _lastActivityUtc = DateTime.UtcNow;
            _firedForThisIdle = false;
        }
    }

    private static void OnTick(object? sender, EventArgs e)
    {
        var timeoutMin = LocalSettingsService.Current.SessionTimeoutMinutes;
        if (timeoutMin <= 0) return;                       // 비활성
        if (_shouldMonitor?.Invoke() != true) return;      // 로그인 상태가 아니면 무시
        if (_firedForThisIdle) return;                     // 한 번의 무활동에 한 번만

        if (DateTime.UtcNow - _lastActivityUtc >= TimeSpan.FromMinutes(timeoutMin))
        {
            _firedForThisIdle = true;
            _onTimeout?.Invoke();
        }
    }
}
