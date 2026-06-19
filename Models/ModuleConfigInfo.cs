namespace CMS5000.Models;

/// <summary>Module Config(frmModule) 로드 결과: 설정시각 + 채널 목록.</summary>
public class ModuleConfigInfo
{
    public string ConfigDate { get; set; } = "";
    public List<ModuleChannelRow> Channels { get; set; } = [];
}

/// <summary>모듈 내 채널 1건 (Activity + Reference 사용여부/대상).</summary>
public class ModuleChannelRow
{
    public int    ChannelId         { get; set; }
    public string Name              { get; set; } = "";
    public bool   Activity          { get; set; }
    public bool   ReferenceOn       { get; set; }
    public int    ReferenceId       { get; set; }
}
