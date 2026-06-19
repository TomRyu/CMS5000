namespace CMS5000.Models;

public class DeviceStation
{
    public int    StationId      { get; set; }
    public string Name           { get; set; } = "";
    public string Company        { get; set; } = "";
    public string CompanyAddr    { get; set; } = "";
    public int    RackListenPort { get; set; }

    public override string ToString() => $"[{StationId}] {Name}";
}
