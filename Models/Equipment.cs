namespace CMS5000.Models;

public enum EquipmentStatus { Normal, Warning, Danger, Offline }

public class Equipment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Normal;
    public double CurrentValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
