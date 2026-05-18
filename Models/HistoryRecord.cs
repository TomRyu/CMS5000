namespace CMS5000.Models;

public class HistoryRecord
{
    public string EquipmentName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string HandledBy { get; set; } = string.Empty;
}
