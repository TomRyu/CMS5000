namespace CMS5000.Models;

/// <summary>릴레이 로직 1행(원본 frmRelay grdLogic). 텍스트로 표시·저장 시 0/1/2로 변환.</summary>
public class RelayLogicRow
{
    public int    Sequence    { get; set; }
    public int    ModuleId    { get; set; }
    public int    ChannelId   { get; set; }
    public string AlertDanger { get; set; } = "Alert";   // Alert / Danger
    public string AndOrEnd    { get; set; } = "And";     // And / Or / End
}

/// <summary>릴레이 채널 설정(원본 cRelay: Mode/AndVoting + 로직 목록).</summary>
public class RelayConfigInfo
{
    public int Mode      { get; set; }   // 0=Latching, 1=Non Latching
    public int AndVoting { get; set; }   // 0=Normal, 1=True
    public List<RelayLogicRow> Logic { get; set; } = [];
}
