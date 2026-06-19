using CMS5000.Models;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

public class RackOpenViewModel : ViewModelBase
{
    private RackSlotItem? _selectedSlot;

    public string            RackTitle { get; }
    public List<RackSlotItem> Slots    { get; } = [];

    public RackSlotItem? SelectedSlot
    {
        get => _selectedSlot;
        set => SetProperty(ref _selectedSlot, value);
    }

    public RelayCommand ModifyCommand { get; }

    public event Action? ModifyRequested;

    public RackOpenViewModel(DeviceTreeNode rackNode)
    {
        RackTitle = $"RACK {rackNode.RackId:D2}";

        var modulesBySlot = rackNode.Children
            .Where(c => c.Kind == NodeKind.Module)
            .ToDictionary(c => c.ModuleId, c => c);

        for (int i = 1; i <= 14; i++)
        {
            if (modulesBySlot.TryGetValue(i, out var mod))
            {
                Slots.Add(new RackSlotItem
                {
                    SlotNumber = i,
                    IsOccupied = true,
                    ModuleName = mod.Name,
                    ModuleType = mod.ModuleType,
                    IsActive   = mod.IsActive
                });
            }
            else
            {
                Slots.Add(new RackSlotItem { SlotNumber = i });
            }
        }

        SelectedSlot  = Slots.FirstOrDefault(s => s.IsOccupied);
        ModifyCommand = new RelayCommand(
            _ => ModifyRequested?.Invoke(),
            _ => SelectedSlot?.IsOccupied == true);
    }
}
