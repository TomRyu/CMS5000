using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.Services.Hw;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>H/W Config 트리 노드(체크박스).</summary>
public class HwTreeNode : INotifyPropertyChanged
{
    private bool _isChecked;
    public string   Name      { get; init; } = "";
    public NodeKind Kind      { get; init; }
    public int      StationId { get; init; }
    public int      RackId    { get; init; }
    public int      ModuleId  { get; init; }
    public int      ChannelId { get; init; }
    public ObservableCollection<HwTreeNode> Children { get; } = [];

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            foreach (var c in Children) c.IsChecked = value;   // 하위 동기화(원본 AfterCheck)
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 원본 frmRackConfig("RACK COMMUNICATION" / H/W CONFIG) VM.
/// RACK LIST(체크 트리) + 통신상태 로그 + Connect/Disconnect/Download/Upload.
/// 실제 페이로드(cConfigPK)는 하드웨어 의존이라 헤더 프레이밍 + 로그까지 구현.
/// </summary>
public class HwConfigViewModel : ViewModelBase
{
    private readonly DeviceTreeNode _rack;
    private readonly HwSocket _socket = new();

    public string RackTitle { get; }
    public ObservableCollection<HwTreeNode> Nodes { get; } = [];
    public ObservableCollection<string>     Log   { get; } = [];

    private string _ip;
    private string _port;
    private bool   _connected;
    private string _state = "Communication...";

    public string Ip        { get => _ip;        set => SetProperty(ref _ip, value); }
    public string Port      { get => _port;      set => SetProperty(ref _port, value); }
    public bool   Connected { get => _connected; set { SetProperty(ref _connected, value); OnPropertyChanged(nameof(Disconnected)); } }
    public bool   Disconnected => !_connected;
    public string State     { get => _state;     set => SetProperty(ref _state, value); }

    public RelayCommand ConnectCommand    { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand DownloadCommand   { get; }
    public RelayCommand UploadCommand     { get; }
    public RelayCommand ClearCommand      { get; }

    public HwConfigViewModel(DeviceTreeNode rackNode)
    {
        _rack = rackNode;
        RackTitle = $"H/W CONFIG  RACK [{rackNode.RackId:D2}]";
        _ip   = string.IsNullOrEmpty(rackNode.LocalIp) ? "192.168.0.190" : rackNode.LocalIp;
        _port = rackNode.LocalPort > 0 ? rackNode.LocalPort.ToString() : "3000";

        BuildTree(rackNode);

        _socket.Connected    += () => UI(() => { Connected = true;  State = $"{Ip} Completing the Connection."; AddLog(State); });
        _socket.Disconnected += () => UI(() => { Connected = false; State = $"{Ip} Terminate the connection Complete."; AddLog(State); });
        _socket.Sent         += n  => UI(() => AddLog($"{n} Bytes Transfer Completed."));
        _socket.Received     += (pk, _) => UI(() => AddLog($"수신: {pk.CommandText} (Len {pk.Length})"));
        _socket.Error        += m  => UI(() => AddLog($"[오류] {m}"));

        ConnectCommand    = new RelayCommand(_ => Connect(),    _ => !Connected);
        DisconnectCommand = new RelayCommand(_ => _socket.Disconnect(), _ => Connected);
        DownloadCommand   = new RelayCommand(_ => _ = DownloadAsync(), _ => Connected);
        UploadCommand     = new RelayCommand(_ => Upload(),     _ => Connected);
        ClearCommand      = new RelayCommand(_ => Log.Clear());
    }

    private void BuildTree(DeviceTreeNode rack)
    {
        var root = new HwTreeNode { Name = $"R{rack.RackId:D2}  {rack.Name}", Kind = NodeKind.Rack, StationId = rack.StationId, RackId = rack.RackId };
        foreach (var mod in rack.Children)
        {
            var mNode = new HwTreeNode { Name = $"M{mod.ModuleId:D2}  {mod.Name}", Kind = NodeKind.Module,
                                         StationId = rack.StationId, RackId = rack.RackId, ModuleId = mod.ModuleId };
            foreach (var ch in mod.Children)
                mNode.Children.Add(new HwTreeNode { Name = $"CH{ch.ChannelId:D2}  {ch.Name}", Kind = NodeKind.Channel,
                                                    StationId = rack.StationId, RackId = rack.RackId, ModuleId = mod.ModuleId, ChannelId = ch.ChannelId });
            root.Children.Add(mNode);
        }
        Nodes.Add(root);
    }

    private void Connect()
    {
        if (!int.TryParse(Port, out int p)) { AddLog("[오류] 포트 형식이 올바르지 않습니다."); return; }
        AddLog($"{Ip}:{p} Try to Connecting....");
        _socket.Connect(Ip.Trim(), p);
    }

    // 원본 SyncListUp + ActorConfigSender: 체크된 노드에 포팅 구조체(byte 단위) CONFIG 패킷 전송
    private async Task DownloadAsync()
    {
        try
        {
            int sent = 0;
            RackFullInfo? rackFull = null;

            foreach (var n in Flatten(Nodes))
            {
                if (!n.IsChecked) continue;
                byte[]? buf = null;
                string label;

                switch (n.Kind)
                {
                    case NodeKind.Rack:
                        rackFull ??= await DeviceService.GetRackFullAsync(n.StationId, n.RackId);
                        buf   = HwConfigPacker.BuildRackComm(n.StationId, n.RackId, rackFull);
                        label = "RACK COMMUNICATION";
                        break;
                    case NodeKind.Module:
                        buf   = HwConfigPacker.BuildModule(n.StationId, n.RackId, n.ModuleId);
                        label = $"MODULE {n.ModuleId:D2}";
                        break;
                    default: // Channel — reference 채널이면 REFERENCE 구조체
                        var refInfo = await DeviceService.GetReferenceConfigAsync(n.StationId, n.RackId, n.ModuleId, n.ChannelId);
                        buf   = HwConfigPacker.BuildChannelReference(n.StationId, n.RackId, n.ModuleId, n.ChannelId, refInfo);
                        label = $"CH {n.ChannelId:D2} REFERENCE";
                        break;
                }

                if (buf != null)
                {
                    _socket.Send(buf);
                    AddLog($"DownLoad ▶ {n.Name} [{label}] {buf.Length} bytes");
                    sent++;
                }
            }
            if (sent == 0) AddLog("선택된 항목이 없습니다.");
        }
        catch (Exception ex)
        {
            AddLog($"[오류] DownLoad 실패: {ex.Message}");
        }
    }

    private void Upload()
    {
        var pk = new HwPacket(HwPacket.CMD_CFG_REQ, HwPacket.TYP_RACK_CFG);
        pk.SetInfo(_rack.StationId, _rack.RackId, 0, 0);
        _socket.Send(pk.BuildHeader());
        AddLog("UpLoad ▶ Config Request (RACK_CFG)");
    }

    private static IEnumerable<HwTreeNode> Flatten(IEnumerable<HwTreeNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n;
            foreach (var c in Flatten(n.Children)) yield return c;
        }
    }

    private void AddLog(string msg) => Log.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");

    private static void UI(Action a) => Application.Current?.Dispatcher.Invoke(a);

    public void OnClosed() => _socket.Disconnect();
}
