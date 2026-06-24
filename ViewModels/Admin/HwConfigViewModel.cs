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

    /// <summary>로그 전체를 한 문자열로(읽기전용 TextBox 바인딩 — 블럭 선택·복사용).</summary>
    public string LogText => string.Join(Environment.NewLine, Log);

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
    public RelayCommand ToggleConnectionCommand { get; }
    public RelayCommand DownloadCommand   { get; }
    public RelayCommand UploadCommand     { get; }
    public RelayCommand ClearCommand      { get; }
    public RelayCommand SaveLogCommand    { get; }
    public RelayCommand SaveToDbCommand   { get; }

    /// <summary>마지막 UpLoad 파싱 결과(DB 저장용). 성공 시에만 채워짐.</summary>
    private HwRackConfigParser.RackResult? _lastUpload;

    /// <summary>인벤토리/활성을 DB에 저장한 직후 발생 — 왼쪽 메인 트리 갱신 트리거.</summary>
    public event Action? InventorySaved;

    /// <summary>수신 패킷 원시 바이트를 hex 로그로 남길지(디버깅용). 기본 켜짐.</summary>
    private bool _logRawHex = true;
    public bool LogRawHex { get => _logRawHex; set => SetProperty(ref _logRawHex, value); }

    /// <summary>RACK LIST 체크박스 표시 여부. 기본 표시, UpLoad 후 숨김 / DownLoad 시 다시 표시.</summary>
    private bool _showCheckBoxes = true;
    public bool ShowCheckBoxes { get => _showCheckBoxes; set => SetProperty(ref _showCheckBoxes, value); }

    public HwConfigViewModel(DeviceTreeNode rackNode)
    {
        _rack = rackNode;
        RackTitle = $"H/W CONFIG  RACK [{rackNode.RackId:D2}]";
        _ip   = string.IsNullOrEmpty(rackNode.LocalIp) ? "192.168.0.190" : rackNode.LocalIp;
        _port = rackNode.LocalPort > 0 ? rackNode.LocalPort.ToString() : "3000";

        BuildTree(rackNode);

        Log.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogText));

        _socket.Connected    += () => UI(() => { Connected = true;  State = $"{Ip} Completing the Connection."; AddLog(State); });
        _socket.Disconnected += () => UI(() => { Connected = false; State = $"{Ip} Terminate the connection Complete."; AddLog(State); });
        _socket.Sent         += n  => UI(() => AddLog($"{n} Bytes Transfer Completed."));
        _socket.Received     += (pk, payload, raw) => UI(() => OnReceived(pk, payload, raw));
        _socket.Error        += m  => UI(() => AddLog($"[오류] {m}"));

        ConnectCommand    = new RelayCommand(_ => Connect(),    _ => !Connected);
        DisconnectCommand = new RelayCommand(_ => _socket.Disconnect(), _ => Connected);
        ToggleConnectionCommand = new RelayCommand(_ => { if (Connected) _socket.Disconnect(); else Connect(); });
        DownloadCommand   = new RelayCommand(_ => { ShowCheckBoxes = true; _ = DownloadAsync(); }, _ => Connected);
        UploadCommand     = new RelayCommand(_ => Upload(),     _ => Connected);
        ClearCommand      = new RelayCommand(_ => Log.Clear());
        SaveLogCommand    = new RelayCommand(_ => SaveLog(), _ => Log.Count > 0);
        SaveToDbCommand   = new RelayCommand(_ => SaveToDb(), _ => _lastUpload != null);
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
        ShowCheckBoxes = false;   // UpLoad(기기→PC 읽기)는 선택이 필요 없으므로 체크박스 숨김

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
    private void OnReceived(HwPacket pk, byte[] payload, byte[] rawFrame)
    {
        AddLog($"수신: {pk.CommandText} {TypeText(pk.Type)} (Len {pk.Length})");
        if (LogRawHex) AddLog($"  raw[{rawFrame.Length}]: {ToHex(rawFrame)}");

        try
        {
            // 실기기는 UpLoad(Config Request)에 대해 ACK + RACK FULL CONFIG 통짜 payload로 응답한다.
            if (pk.Command == HwPacket.CMD_ACK && pk.ReturnType == HwPacket.TYP_RACK_CFG)
            {
                ApplyRackConfig(payload);
                return;
            }

            // (참고) 모듈/채널 단위 CFG 응답이 오는 경우 처리.
            if (pk.Command == HwPacket.CMD_CFG)
            {
                switch (pk.Type)
                {
                    case HwPacket.TYP_COMM_CFG:    ApplyComm(pk, payload);    break;  // 0x01
                    case HwPacket.TYP_RACK_CFG:    ApplyRack(pk, payload);    break;  // 0x02
                    case HwPacket.TYP_MODULE_CFG:  ApplyModule(pk, payload);  break;  // 0x22
                    case HwPacket.TYP_CHANNEL_CFG: ApplyChannel(pk, payload); break;  // 0x32
                    default: break;
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"[오류] 응답 파싱 실패: {ex.Message}");
        }
    }

    // RACK FULL CONFIG 응답(ACK + 통짜 payload)을 파싱해 모듈/채널 전체를 트리에 반영.
    private void ApplyRackConfig(byte[] payload)
    {
        var res = HwRackConfigParser.Parse(payload);
        _lastUpload = res;   // DB 저장 버튼 활성화
        if (!res.Exact)
            AddLog($"  · [경고] 레이아웃 불일치(소비 {res.Consumed}/{payload.Length}B) — 채널 값 신뢰 불가");

        var rackNode = Nodes.FirstOrDefault();
        if (rackNode != null)
        {
            rackNode.FromDevice = true;
            rackNode.DeviceInfo = $"{ActiveText(res.Rack.Active)}, Modules {res.Modules.Count}, WfInterval {res.Rack.WaveFormInterval}";
        }

        int chTotal = 0;
        foreach (var m in res.Modules)
        {
            var modNode = FindModule(m.Info.Id);
            if (modNode == null)
            {
                modNode = new HwTreeNode { Name = $"M{m.Info.Id:D2}  (기기)", Kind = NodeKind.Module,
                                           StationId = _rack.StationId, RackId = _rack.RackId, ModuleId = m.Info.Id };
                rackNode?.Children.Add(modNode);
            }
            modNode.FromDevice = true;
            modNode.DeviceInfo = $"type{m.Info.Type}, {ActiveText(m.Info.Active)}, CH {m.ChannelCount}";

            foreach (var ch in m.Channels)
            {
                chTotal++;
                var chNode = FindChannel(m.Info.Id, ch.Info.Id);
                if (chNode == null)
                {
                    chNode = new HwTreeNode { Name = $"CH{ch.Info.Id:D2}  (기기)", Kind = NodeKind.Channel,
                                              StationId = _rack.StationId, RackId = _rack.RackId, ModuleId = m.Info.Id, ChannelId = ch.Info.Id };
                    modNode.Children.Add(chNode);
                }
                chNode.FromDevice = true;
                chNode.DeviceInfo = SummarizeChannel(ch);
            }
        }
        AddLog($"  · 랙 구성 반영 완료: 모듈 {res.Modules.Count}, 채널 {chTotal}");
    }

    // UpLoad로 읽은 인벤토리/활성을 현재 랙 DB에 저장(확인 후) → 왼쪽 메인 트리 갱신.
    private async void SaveToDb()
    {
        if (_lastUpload is not { } res) return;
        int modCnt = res.Modules.Count;
        int chCnt  = res.Modules.Sum(m => m.Channels.Count);
        int devRackId = res.Rack.Id;   // 기기가 보고한 rack id

        // 기기가 보고한 rack 과 현재 탭의 rack 이 다르면 경고(엉뚱한 rack에 생성/덮어쓰기 방지)
        string mismatch = devRackId != _rack.RackId
            ? $"\n\n⚠️ 기기는 rack {devRackId:D2}를 보고했는데 현재 탭은 rack {_rack.RackId:D2}입니다.\n" +
              $"   대상이 맞는지 반드시 확인하세요(틀리면 엉뚱한 랙에 저장됩니다).\n"
            : "";

        var confirm = MessageBox.Show(
            $"기기에서 읽은 구성을 현재 랙(R{_rack.RackId:D2}) DB에 저장합니다.\n\n" +
            $"• 모듈 {modCnt}개 · 채널 {chCnt}개의 활성 상태를 저장합니다.\n" +
            $"• DB에 없는 모듈/채널은 새로 생성(INSERT)됩니다.\n" +
            $"• 센서/스케일/알람·모듈타입 등 상세값은 저장하지 않습니다." +
            mismatch +
            $"\n진행할까요?",
            "DB 저장 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);   // 디폴트 버튼 = No(실수 방지)
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var (rackUpd, mu, mi, cu, ci) = await DeviceService.SaveUploadedInventoryAsync(_rack.StationId, _rack.RackId, res);
            AddLog($"DB 저장 완료: 랙 {(rackUpd ? "갱신" : "미일치")}, " +
                   $"모듈 갱신 {mu}/신규 {mi}, 채널 갱신 {cu}/신규 {ci}");
            InventorySaved?.Invoke();   // 왼쪽 메인 트리 새로고침
        }
        catch (Exception ex)
        {
            AddLog($"[오류] DB 저장 실패: {ex.Message}");
            MessageBox.Show($"DB 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SummarizeChannel(HwRackConfigParser.ChannelResult ch)
    {
        var io = ch.Io; var s = io.Sensor;
        return $"{ActiveText(ch.Info.Active)}, Sensor t{s.Type} sens{s.Sensitivity:0.###}, " +
               $"Orient {io.Orientation}/{io.OrientationAngle}°, FreqSpan {io.FreqSpan}, Zero {io.ZeroPosition:0.###}";
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

    private static string ToHex(byte[] b) => BitConverter.ToString(b).Replace('-', ' ');

    // COMMUNICATION STATE 로그를 텍스트 파일로 저장(디버깅용 — 실기기 응답 캡처).
    private void SaveLog()
    {
        if (Log.Count == 0) { AddLog("저장할 로그가 없습니다."); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = "통신 로그 저장",
            Filter   = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            FileName = $"HwConfig_R{_rack.RackId:D2}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var header = $"# H/W CONFIG 통신 로그  RACK[{_rack.RackId:D2}]  {Ip}:{Port}  {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            System.IO.File.WriteAllLines(dlg.FileName, new[] { header }.Concat(Log), Encoding.UTF8);
            AddLog($"로그 저장 완료: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            AddLog($"[오류] 로그 저장 실패: {ex.Message}");
        }
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
