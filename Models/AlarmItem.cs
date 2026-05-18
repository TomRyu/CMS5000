namespace CMS5000.Models;

public enum AlarmLevel { Info, Warning, Danger, Critical }

public class AlarmItem
{
    public string Id { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public bool IsAcknowledged { get; set; }
    public string Channel { get; set; } = string.Empty;
}
