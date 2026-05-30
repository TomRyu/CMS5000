namespace CMS5000.Models;

/// <summary>CHANGELOG.json의 버전 1건 (배포물에 번들되어 오프라인에서도 표시).</summary>
public class ChangelogEntry
{
    public string Version { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public List<ChangeItem> Changes { get; set; } = new();

    /// <summary>최신 버전이면 기본 펼침 상태로 표시 (서비스가 첫 항목에 설정).</summary>
    public bool IsLatest { get; set; }

    // 표시용
    public string Header => $"v{Version}   ·   {Date}";
}

/// <summary>변경 1건. Kind: New / Improve / Fix.</summary>
public class ChangeItem
{
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public string KindLabel => Kind switch
    {
        "New"     => "신규",
        "Improve" => "개선",
        "Fix"     => "버그수정",
        _         => Kind
    };
}
