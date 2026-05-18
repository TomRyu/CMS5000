using System.Collections.ObjectModel;

namespace CMS5000.Models;

public class NavNode
{
    public string Name { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Normal;
    public bool IsExpanded { get; set; } = true;
    public ObservableCollection<NavNode> Children { get; set; } = [];
    public bool IsLeaf => Children.Count == 0;
}
