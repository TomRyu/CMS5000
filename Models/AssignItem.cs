using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CMS5000.Models;

public class AssignableChannel : INotifyPropertyChanged
{
    private bool _isSelected;

    public int    RackId           { get; init; }
    public int    ModuleId         { get; init; }
    public int    ChannelId        { get; init; }
    public string ChannelName      { get; set; } = "";
    public bool   ChannelActive    { get; set; }
    public int    AssignedTrainId  { get; set; }
    public int    AssignedCompId   { get; set; }
    public int    AssignedPointId  { get; set; }
    public string AssignedPointName { get; set; } = "";

    public bool IsAssigned => AssignedPointId > 0;

    public string ChannelDisplay =>
        $"R{RackId:D2}-M{ModuleId:D2}-CH{ChannelId:D2}  {ChannelName}";

    public string AssignDisplay => IsAssigned
        ? $"T{AssignedTrainId:D2}-C{AssignedCompId:D2}-P{AssignedPointId:D2}  {AssignedPointName}"
        : "";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AssignablePoint : INotifyPropertyChanged
{
    private bool _isSelected;

    public int    TrainId            { get; init; }
    public int    ComponentId        { get; init; }
    public int    PointId            { get; init; }
    public string PointName          { get; set; } = "";
    public bool   PointActive        { get; set; }
    public int    AssignedChannelId  { get; set; }

    public bool IsAssigned => AssignedChannelId > 0;

    public string PointDisplay =>
        $"T{TrainId:D2}-C{ComponentId:D2}-P{PointId:D2}  {PointName}";

    public string AssignDisplay => IsAssigned
        ? $"CH{AssignedChannelId:D2}"
        : "";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
