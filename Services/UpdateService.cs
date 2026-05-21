using Velopack;
using Velopack.Sources;

namespace CMS5000.Services;

public static class UpdateService
{
    private const string GitHubRepo = "https://github.com/TomRyu/CMS5000";

    public static async Task CheckAndDownloadAsync()
    {
        try
        {
            var source = new GithubSource(GitHubRepo, null, false);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled) return;

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null) return;

            await mgr.DownloadUpdatesAsync(newVersion);
            // 다운로드만 완료 — 다음 재실행 시 자동 적용
        }
        catch
        {
            // 업데이트 실패는 조용히 무시 (앱 동작에 영향 없음)
        }
    }
}
