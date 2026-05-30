using System.IO;
using System.Text.Json;
using CMS5000.Models;

namespace CMS5000.Services;

/// <summary>배포물에 번들된 CHANGELOG.json을 읽어 버전 이력을 제공.</summary>
public static class ChangelogService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<ChangelogEntry> Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.json");
            if (!File.Exists(path)) return new();

            var file = JsonSerializer.Deserialize<ChangelogFile>(File.ReadAllText(path), Options);
            var entries = file?.Entries ?? new();

            if (entries.Count > 0)
                entries[0].IsLatest = true;   // 최신 버전은 기본 펼침

            return entries;
        }
        catch
        {
            // 파일 누락·파싱 오류 시 빈 목록 (카드는 표시되되 이력만 비어 있음)
            return new();
        }
    }

    private class ChangelogFile
    {
        public List<ChangelogEntry> Entries { get; set; } = new();
    }
}
