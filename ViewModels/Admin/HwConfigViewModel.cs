using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

    // ── UpLoad(기기→PC) 응답 반영용 ──
    private string? _deviceInfo;
    private bool _fromDevice;

    /// <summary>기기에서 UpLoad 받은 설정 요약(노드 옆에 회색으로 표시).</summary>
    public string? DeviceInfo
    {
        get => _deviceInfo;
        set
        {
            _deviceInfo = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceInfo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDeviceInfo)));
        }
    }
    public bool HasDeviceInfo => !string.IsNullOrEmpty(_deviceInfo);

    /// <summary>기기 응답으로 확인된 노드면 true(이름 강조).</summary>
    public bool FromDevice
    {
        get => _fromDevice;
        set { _fromDevice = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FromDevice))); }
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
        _socket.Received     += (pk, payload) => UI(() => OnReceived(pk, payload));
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
        // 기존 기기 응답 표시 초기화(이번 UpLoad 결과로 다시 채움)
        foreach (var n in Flatten(Nodes)) { n.FromDevice = false; n.DeviceInfo = null; }

        var pk = new HwPacket(HwPacket.CMD_CFG_REQ, HwPacket.TYP_RACK_CFG);
        pk.SetInfo(_rack.StationId, _rack.RackId, 0, 0);
        _socket.Send(pk.BuildHeader());
        AddLog("UpLoad ▶ Config Request (RACK_CFG)");
    }

    // ── UpLoad 응답 수신 처리 (기기→PC). UI 스레드에서 호출됨. ──
    // 기기는 Config Request 에 대해 CMD_CFG 패킷(COMM/RACK/MODULE/CHANNEL)으로 응답하며,
    // 각 payload 를 송신과 동일한 구조체로 역직렬화해 RACK LIST 트리에 반영한다.
    private void OnReceived(HwPacket pk, byte[] payload)
    {
        AddLog($"수신: {pk.CommandText} {TypeText(pk.Type)} (Len {pk.Length})");

        if (pk.Command != HwPacket.CMD_CFG) return;   // ACK/NAK/요청에코 등은 로그만

        try
        {
            switch (pk.Type)
            {
                case HwPacket.TYP_COMM_CFG:    ApplyComm(pk, payload);    break;  // 0x01
                case HwPacket.TYP_RACK_CFG:    ApplyRack(pk, payload);    break;  // 0x02
                case HwPacket.TYP_MODULE_CFG:  ApplyModule(pk, payload);  break;  // 0x22
                case HwPacket.TYP_CHANNEL_CFG: ApplyChannel(pk, payload); break;  // 0x32
                default: break;                                                   // 미지원 타입은 로그만
            }
        }
        catch (Exception ex)
        {
            AddLog($"[오류] 응답 파싱 실패({TypeText(pk.Type)}): {ex.Message}");
        }
    }

    private void ApplyComm(HwPacket pk, byte[] payload)
    {
        if (!HwMarshal.TryFromBytes<PkCommunication>(payload, out var c)) { AddLog("  · COMM 응답 길이 부족"); return; }
        string srvIp = AsciiZ(c.Server.IpAddr);
        string mbIp  = AsciiZ(c.Modbus.IpAddr);

        var rack = Nodes.FirstOrDefault();
        if (rack != null)
        {
            rack.FromDevice = true;
            rack.DeviceInfo = $"Server {srvIp}:{c.Server.Port}  Modbus {mbIp}:{c.Modbus.Port}  COM{c.Serial.Port} {c.Serial.BaudRate}bps";
        }
        AddLog($"  · COMM ◀ Server {srvIp}:{c.Server.Port}, Modbus {mbIp}:{c.Modbus.Port}, Serial COM{c.Serial.Port} {c.Serial.BaudRate}bps");
    }

    private void ApplyRack(HwPacket pk, byte[] payload)
    {
        if (!HwMarshal.TryFromBytes<PkRackInfo>(payload, out var r)) { AddLog("  · RACK 응답 길이 부족"); return; }
        int infoLen = HwMarshal.SizeOf<PkRackInfo>();
        byte moduleCount = payload.Length > infoLen ? payload[infoLen] : (byte)0;

        var rack = Nodes.FirstOrDefault();
        if (rack != null)
        {
            rack.FromDevice = true;
            rack.DeviceInfo = $"{ActiveText(r.Active)}, Modules {moduleCount}, WfInterval {r.WaveFormInterval}";
        }
        AddLog($"  · RACK ◀ Active {r.Active}, ModuleCount {moduleCount}, WaveFormInterval {r.WaveFormInterval}");
    }

    private void ApplyModule(HwPacket pk, byte[] payload)
    {
        if (!HwMarshal.TryFromBytes<PkModuleInfo>(payload, out var m)) { AddLog("  · MODULE 응답 길이 부족"); return; }
        int infoLen = HwMarshal.SizeOf<PkModuleInfo>();
        byte channelCount = payload.Length > infoLen ? payload[infoLen] : (byte)0;

        var mod = FindModule(m.Id);
        if (mod == null)   // 기기에 있으나 DB 트리엔 없던 모듈 → 추가
        {
            mod = new HwTreeNode { Name = $"M{m.Id:D2}  (기기)", Kind = NodeKind.Module,
                                   StationId = pk.StationId, RackId = pk.RackId, ModuleId = m.Id };
            Nodes.FirstOrDefault()?.Children.Add(mod);
            AddLog($"  · MODULE {m.Id:D2} 트리에 추가(기기에만 존재)");
        }
        mod.FromDevice = true;
        mod.DeviceInfo = $"Type {m.Type}, {ActiveText(m.Active)}, CH {channelCount}";
        AddLog($"  · MODULE {m.Id:D2} ◀ Type {m.Type}, Active {m.Active}, ChannelCount {channelCount}");
    }

    private void ApplyChannel(HwPacket pk, byte[] payload)
    {
        if (!HwMarshal.TryFromBytes<PkChannelReference>(payload, out var cr)) { AddLog("  · CHANNEL 응답 길이 부족"); return; }
        var info = cr.Info;
        var refr = cr.Refer;

        var ch = FindChannel(pk.ModuleId, info.Id);
        if (ch == null)    // 기기에 있으나 DB 트리엔 없던 채널 → 추가
        {
            ch = new HwTreeNode { Name = $"CH{info.Id:D2}  (기기)", Kind = NodeKind.Channel,
                                  StationId = pk.StationId, RackId = pk.RackId, ModuleId = pk.ModuleId, ChannelId = info.Id };
            FindModule(pk.ModuleId)?.Children.Add(ch);
            AddLog($"  · M{pk.ModuleId:D2} CH {info.Id:D2} 트리에 추가(기기에만 존재)");
        }
        ch.FromDevice = true;
        ch.DeviceInfo = $"{ActiveText(info.Active)}, Sensor {refr.Sensor.Type}, Sens {refr.Sensor.Sensitivity:0.###}, Thr {refr.ThresholdLevel:0.###}";
        AddLog($"  · M{pk.ModuleId:D2} CH {info.Id:D2} ◀ Active {info.Active}, SensorType {refr.Sensor.Type}, Sensitivity {refr.Sensor.Sensitivity}");
    }

    private HwTreeNode? FindModule(int moduleId)
        => Flatten(Nodes).FirstOrDefault(n => n.Kind == NodeKind.Module && n.ModuleId == moduleId);

    private HwTreeNode? FindChannel(int moduleId, int channelId)
        => Flatten(Nodes).FirstOrDefault(n => n.Kind == NodeKind.Channel && n.ModuleId == moduleId && n.ChannelId == channelId);

    private static string ActiveText(int active) => active != 0 ? "Active" : "Inactive";

    private static string AsciiZ(byte[]? b)
    {
        if (b == null || b.Length == 0) return "";
        int len = Array.IndexOf(b, (byte)0);
        if (len < 0) len = b.Length;
        return Encoding.ASCII.GetString(b, 0, len).Trim();
    }

    private static string TypeText(byte t) => t switch
    {
        HwPacket.TYP_COMM_CFG     => "COMM",      // 0x01
        HwPacket.TYP_RACK_CFG     => "RACK",      // 0x02
        HwPacket.TYP_MODULES_CFG  => "MODULES",   // 0x21
        HwPacket.TYP_MODULE_CFG   => "MODULE",    // 0x22
        HwPacket.TYP_CHANNELS_CFG => "CHANNELS",  // 0x31
        HwPacket.TYP_CHANNEL_CFG  => "CHANNEL",   // 0x32
        _ => $"0x{t:X2}",
    };

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
