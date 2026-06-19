using System.ComponentModel;

namespace CMS5000.Models;

public class ChannelModifyItem : INotifyPropertyChanged
{
    private bool          _isActive = true;
    private bool          _refOn;
    private bool          _showReference;
    private ChannelOption? _selectedRefChannel;
    private string        _config = "";

    public int    ChannelId     { get; init; }
    public string ChannelName   { get; init; } = "";
    public string SlotLabel     { get; init; } = "";   // "CHANNEL 01" 등

    public bool ShowReference
    {
        get => _showReference;
        set { _showReference = value; Notify(nameof(ShowReference)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; Notify(nameof(IsActive)); }
    }

    public bool RefOn
    {
        get => _refOn;
        set { _refOn = value; Notify(nameof(RefOn)); Notify(nameof(RefOff)); }
    }

    public bool RefOff
    {
        get => !_refOn;
        set { _refOn = !value; Notify(nameof(RefOn)); Notify(nameof(RefOff)); }
    }

    public ChannelOption? SelectedRefChannel
    {
        get => _selectedRefChannel;
        set { _selectedRefChannel = value; Notify(nameof(SelectedRefChannel)); }
    }

    public string Config
    {
        get => _config;
        set { _config = value; Notify(nameof(Config)); }
    }

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
