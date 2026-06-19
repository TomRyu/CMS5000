using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CMS5000.Models;

public enum NodeKind { Rack, Module, Channel, Train, Component, Point }

public class DeviceTreeNode : INotifyPropertyChanged
{
    private string _name     = "";
    private byte   _activity = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public NodeKind Kind        { get; init; }
    public int      StationId   { get; init; }
    public int      RackId      { get; init; }
    public int      ModuleId    { get; init; }
    public int      ChannelId   { get; init; }
    public int      ChannelIndex { get; init; }
    public int      TrainId     { get; init; }
    public int      ComponentId { get; init; }
    public int      PointId     { get; init; }
    public int      Assign      { get; set; }
    public string   Info        { get; set; } = "";
    public string   Location    { get; set; } = "";
    public string   LocalIp     { get; set; } = "";
    public int      LocalPort   { get; set; }
    public string   ModuleType  { get; set; } = "";

    public string Name
    {
        get => _name;
        set { _name = value; Notify(nameof(Name)); Notify(nameof(DisplayText)); }
    }

    public byte Activity
    {
        get => _activity;
        set { _activity = value; Notify(nameof(Activity)); Notify(nameof(IsActive)); Notify(nameof(StatusColor)); Notify(nameof(DisplayText)); }
    }

    public bool   IsActive    => Activity == 1;
    public string StatusColor => IsActive ? "#16A34A" : "#9CA3AF";

    public string KindLabel => Kind switch
    {
        NodeKind.Rack      => "RACK",
        NodeKind.Module    => "MODULE",
        NodeKind.Channel   => "CH",
        NodeKind.Train     => "TRAIN",
        NodeKind.Component => "부품",
        NodeKind.Point     => "POINT",
        _                  => ""
    };

    public string DisplayText => Kind switch
    {
        NodeKind.Rack      => $"R{RackId:D2}  {Name}",
        NodeKind.Module    => $"M{ModuleId:D2}  {Name}",
        NodeKind.Channel   => $"CH{ChannelId:D2}  {Name}",
        NodeKind.Train     => $"T{TrainId:D2}  {Name}",
        NodeKind.Component => $"C{ComponentId:D2}  {Name}",
        NodeKind.Point     => $"P{PointId:D2}  {Name}",
        _                  => Name
    };

    public ObservableCollection<DeviceTreeNode> Children { get; } = [];
}
