namespace CMS5000.Models;

// 원본 frmAssign 의 7개 스프레드시트에 대응하는 행 모델들.

public class AssignRackRow
{
    public int    RackId   { get; set; }
    public string Name     { get; set; } = "";
    public int    Activity { get; set; }
}

public class AssignModuleRow
{
    public int    ModuleId   { get; set; }
    public string Name       { get; set; } = "";
    public int    Activity   { get; set; }
    public string ModuleType { get; set; } = "";
}

public class AssignChannelRow
{
    public int    ChannelId    { get; set; }
    public string Name         { get; set; } = "";
    public int    Activity     { get; set; }
    public string ChannelType  { get; set; } = "";
    public int    ChannelIndex { get; set; }
    public bool   IsAssigned   { get; set; }
}

public class AssignTrainRow
{
    public int    TrainId  { get; set; }
    public string Name     { get; set; } = "";
    public int    Activity { get; set; }
}

public class AssignComponentRow
{
    public int    ComponentId { get; set; }
    public string Name        { get; set; } = "";
    public int    Activity    { get; set; }
}

public class AssignPointRow
{
    public int    PointId    { get; set; }
    public string Name       { get; set; } = "";
    public int    Activity   { get; set; }
    public bool   IsAssigned { get; set; }
}

/// <summary>ASSIGN 목록(원본 FpSprAssign) 한 행.</summary>
public class AssignListRow
{
    public int    StationId   { get; set; }
    public int    RackId      { get; set; }
    public int    ModuleId    { get; set; }
    public int    ChannelId   { get; set; }
    public string ChannelName { get; set; } = "";
    public int    TrainId     { get; set; }
    public int    ComponentId { get; set; }
    public int    PointId     { get; set; }
    public string PointName   { get; set; } = "";
}
