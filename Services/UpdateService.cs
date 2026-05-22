using Velopack;
using Velopack.Sources;

namespace CMS5000.Services;

public static class UpdateService
{
    private const string GitHubRepo = "https://github.com/TomRyu/CMS5000";
    private static readonly SemaphoreSlim UpdateLock = new(1, 1);

    public static string GetCurrentVersionText()
    {
        try
        {
            var current = CreateManager().CurrentVersion;
            if (current != null)
                return current.ToString();
        }
        catch
        {
            // Ignore Velopack metadata errors when running from Visual Studio or a raw build folder.
        }

        var asm = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return asm != null ? $"{asm.Major}.{asm.Minor}.{asm.Build}" : "";
    }

    public static async Task CheckDownloadAndApplyAsync(
        Action<string>? reportStatus = null,
        Func<string, bool>? confirmRestart = null)
    {
        if (!await UpdateLock.WaitAsync(0))
            return;

        try
        {
            var mgr = CreateManager();
            if (!mgr.IsInstalled)
            {
                reportStatus?.Invoke("");
                return;
            }

            reportStatus?.Invoke("업데이트 확인 중...");
            var update = await mgr.CheckForUpdatesAsync();
            if (update == null)
            {
                reportStatus?.Invoke("최신 버전입니다.");
                return;
            }

            var targetVersion = update.TargetFullRelease.Version.ToString();
            reportStatus?.Invoke($"업데이트 다운로드 중... v{targetVersion}");
            await mgr.DownloadUpdatesAsync(update);
            reportStatus?.Invoke($"v{targetVersion} 업데이트 준비 완료");

            if (confirmRestart?.Invoke(targetVersion) == true)
                mgr.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            reportStatus?.Invoke($"업데이트 오류: {ex.Message}");
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    private static UpdateManager CreateManager() =>
        new(new GithubSource(GitHubRepo, null, false));
}
