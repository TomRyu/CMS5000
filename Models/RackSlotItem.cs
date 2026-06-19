namespace CMS5000.Models;

public class RackSlotItem
{
    public int             SlotNumber   { get; init; }
    public bool            IsOccupied   { get; init; }
    public string          ModuleName   { get; init; } = "";
    public string          ModuleType   { get; init; } = "";
    public bool            IsActive     { get; init; } = true;
    public int             ChannelCount { get; init; }
    public string          ActiveColor  { get; init; } = "#9CA3AF";
    public string          SlotLabel    { get; init; } = "";
    public DeviceTreeNode? ModuleNode   { get; init; }
    public string          TooltipText  { get; init; } = "";
}
