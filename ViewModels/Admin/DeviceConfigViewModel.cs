using System.Collections.ObjectModel;
using System.ComponentModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

public class TypeTab : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Key   { get; init; } = "";
    public string Title { get; init; } = "";
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DeviceConfigViewModel : ViewModelBase
{
    // ── 공통 ────────────────────────────────────────────────
    private bool   _isBusy;
    private string _statusMessage = "";
    public bool   IsBusy        { get => _isBusy;        set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage;  set { SetProperty(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool   HasStatus     => !string.IsNullOrEmpty(_statusMessage);

    // ── 스테이션 목록 ───────────────────────────────────────
    private ObservableCollection<DeviceStation> _stations = [];
    private DeviceStation? _selectedStation;

    public ObservableCollection<DeviceStation> Stations
    {
        get => _stations;
        set => SetProperty(ref _stations, value);
    }
    public DeviceStation? SelectedStation
    {
        get => _selectedStation;
        set { SetProperty(ref _selectedStation, value); _ = LoadTreesAsync(); }
    }

    // ── 스테이션 편집 ────────────────────────────────────────
    private bool   _isStationEditing;
    private bool   _isStationAddingNew;
    private int    _editStationId;
    private string _editStationName    = "";
    private string _editStationCompany = "";
    private string _editStationAddr    = "";
    private int    _editStationPort;

    public bool   IsStationEditing    { get => _isStationEditing;   set => SetProperty(ref _isStationEditing, value); }
    public bool   IsStationAddingNew  { get => _isStationAddingNew; set { SetProperty(ref _isStationAddingNew, value); OnPropertyChanged(nameof(StationEditTitle)); OnPropertyChanged(nameof(IsStationIdReadOnly)); } }
    public int    EditStationId       { get => _editStationId;      set => SetProperty(ref _editStationId, value); }
    public string EditStationName     { get => _editStationName;    set => SetProperty(ref _editStationName, value); }
    public string EditStationCompany  { get => _editStationCompany; set => SetProperty(ref _editStationCompany, value); }
    public string EditStationAddr     { get => _editStationAddr;    set => SetProperty(ref _editStationAddr, value); }
    public int    EditStationPort     { get => _editStationPort;    set => SetProperty(ref _editStationPort, value); }
    public string StationEditTitle    => IsStationAddingNew ? "스테이션 추가" : "스테이션 편집";
    public bool   IsStationIdReadOnly => !IsStationAddingNew;

    // ── RACK 트리 ──────────────────────────────────────────
    private ObservableCollection<DeviceTreeNode> _rackNodes = [];
    private DeviceTreeNode? _selectedRackNode;
    private bool   _isRackEditing;
    private bool   _isRackNameEditing;
    private bool   _isRackAddingNew;
    private bool   _isModuleAddingNew;
    private bool   _isChannelAddingNew;
    private int    _moduleAddRackId;
    private int    _channelAddRackId;
    private int    _channelAddModuleId;
    private string _rackEditName     = "";
    private string _rackEditInfo     = "";
    private bool   _rackEditIsActive = true;
    private string _editRackName     = "";
    private string _editRackLocation = "";

    public ObservableCollection<DeviceTreeNode> RackNodes
    {
        get => _rackNodes;
        set => SetProperty(ref _rackNodes, value);
    }
    public DeviceTreeNode? SelectedRackNode
    {
        get => _selectedRackNode;
        set { SetProperty(ref _selectedRackNode, value); PopulateRackEdit(); OnPropertyChanged(nameof(RackPanelTitle)); }
    }
    public bool   IsRackEditing      { get => _isRackEditing;      set { SetProperty(ref _isRackEditing, value); if (value) SelectedTypeTab = null; } }
    public bool   IsRackNameEditing  { get => _isRackNameEditing;  set { SetProperty(ref _isRackNameEditing, value); OnPropertyChanged(nameof(IsRackNameReadOnly)); OnPropertyChanged(nameof(ShowRackLocation)); } }
    public bool   IsRackAddingNew     { get => _isRackAddingNew;     set { SetProperty(ref _isRackAddingNew, value);     OnPropertyChanged(nameof(RackPanelTitle)); OnPropertyChanged(nameof(ShowRackLocation)); } }
    public bool   IsModuleAddingNew   { get => _isModuleAddingNew;   set { SetProperty(ref _isModuleAddingNew, value);   OnPropertyChanged(nameof(RackPanelTitle)); OnPropertyChanged(nameof(ShowRackLocation)); } }
    public bool   IsChannelAddingNew  { get => _isChannelAddingNew;  set { SetProperty(ref _isChannelAddingNew, value);  OnPropertyChanged(nameof(RackPanelTitle)); OnPropertyChanged(nameof(ShowRackLocation)); } }
    public bool   IsRackNameReadOnly  => !_isRackNameEditing;
    public bool   ShowRackLocation    => _isRackNameEditing && !_isModuleAddingNew && !_isChannelAddingNew && !(_selectedRackNode?.Kind == NodeKind.Channel);
    public bool   ShowEditNameButton  => _selectedRackNode?.Kind is NodeKind.Rack or NodeKind.Module or NodeKind.Channel;
    public string EditNameButtonLabel => _selectedRackNode?.Kind switch
    {
        NodeKind.Rack    => "이름/위치 수정",
        NodeKind.Module  => "이름 수정",
        NodeKind.Channel => "이름 수정",
        _                => "수정"
    };
    public string RackEditName       { get => _rackEditName;       set => SetProperty(ref _rackEditName, value); }
    public string RackEditInfo       { get => _rackEditInfo;       set => SetProperty(ref _rackEditInfo, value); }
    public bool   RackEditIsActive   { get => _rackEditIsActive;   set => SetProperty(ref _rackEditIsActive, value); }
    public string EditRackName       { get => _editRackName;       set => SetProperty(ref _editRackName, value); }
    public string EditRackLocation   { get => _editRackLocation;   set => SetProperty(ref _editRackLocation, value); }
    public string RackPanelTitle     => _isChannelAddingNew ? "새 CHANNEL 추가" :
                                        _isModuleAddingNew  ? "새 MODULE 추가" :
                                        _isRackAddingNew    ? "새 RACK 추가" :
                                        (_selectedRackNode != null ? $"{_selectedRackNode.KindLabel} 정보" : "RACK 정보");

    // ── 채널 목록 (Point Assign 드롭다운) ──────────────────
    private ChannelOption? _selectedChannel;
    public ObservableCollection<ChannelOption> AvailableChannels { get; } = [];
    public ChannelOption? SelectedChannel
    {
        get => _selectedChannel;
        set { SetProperty(ref _selectedChannel, value); if (value != null) TrainEditAssign = value.ChannelId; }
    }

    // ── TRAIN 트리 ─────────────────────────────────────────
    private ObservableCollection<DeviceTreeNode> _trainNodes = [];
    private DeviceTreeNode? _selectedTrainNode;
    private bool     _isTrainEditing;
    private bool     _isTrainAddingNew;
    private string   _trainEditName     = "";
    private bool     _trainEditIsActive = true;
    private int      _trainEditAssign;
    private NodeKind _trainEditKind     = NodeKind.Train;

    public ObservableCollection<DeviceTreeNode> TrainNodes
    {
        get => _trainNodes;
        set => SetProperty(ref _trainNodes, value);
    }
    public DeviceTreeNode? SelectedTrainNode
    {
        get => _selectedTrainNode;
        set { SetProperty(ref _selectedTrainNode, value); PopulateTrainEdit(); }
    }
    public bool     IsTrainEditing    { get => _isTrainEditing;   set { SetProperty(ref _isTrainEditing, value); if (value) SelectedTypeTab = null; } }
    public bool     IsTrainAddingNew  { get => _isTrainAddingNew; set => SetProperty(ref _isTrainAddingNew, value); }
    public string   TrainEditName     { get => _trainEditName;    set => SetProperty(ref _trainEditName, value); }
    public bool     TrainEditIsActive { get => _trainEditIsActive; set => SetProperty(ref _trainEditIsActive, value); }
    public int      TrainEditAssign   { get => _trainEditAssign;  set => SetProperty(ref _trainEditAssign, value); }
    public NodeKind TrainEditKind     { get => _trainEditKind;    set { SetProperty(ref _trainEditKind, value); OnPropertyChanged(nameof(IsEditingPoint)); OnPropertyChanged(nameof(TrainEditTitle)); } }

    public bool   IsEditingPoint => TrainEditKind == NodeKind.Point;
    public string TrainEditTitle => TrainEditKind switch
    {
        NodeKind.Train     => _isTrainAddingNew ? "TRAIN 추가"  : "TRAIN 편집",
        NodeKind.Component => _isTrainAddingNew ? "부품 추가"   : "부품 편집",
        NodeKind.Point     => _isTrainAddingNew ? "POINT 추가"  : "POINT 편집",
        _                  => "편집"
    };

    // ── 타입 탭 관리 ──────────────────────────────────────────
    private ObservableCollection<TypeTab> _typeTabs = [];
    private TypeTab? _selectedTypeTab;

    public ObservableCollection<TypeTab> TypeTabs
    {
        get => _typeTabs;
        set => SetProperty(ref _typeTabs, value);
    }
    public TypeTab? SelectedTypeTab
    {
        get => _selectedTypeTab;
        set
        {
            if (_selectedTypeTab != null) _selectedTypeTab.IsSelected = false;
            SetProperty(ref _selectedTypeTab, value);
            if (_selectedTypeTab != null) _selectedTypeTab.IsSelected = true;
            // 랙 뷰 탭으로 전환 시 해당 랙의 데이터 복원
            if (RackIdFromTabKey(_selectedTypeTab?.Key) is int rid &&
                _rackViewData.TryGetValue(rid, out var d))
            {
                _rackViewNode  = d.Node;
                _rackViewSlots = d.Slots;
                _selectedRackViewSlot = d.Slots.FirstOrDefault(s => s.IsOccupied);
                OnPropertyChanged(nameof(RackViewNode));
                OnPropertyChanged(nameof(RackViewSlots));
                OnPropertyChanged(nameof(SelectedRackViewSlot));
                OnPropertyChanged(nameof(RackViewTitle));
            }
            // 랙 Modify 탭으로 전환 시 해당 VM 복원
            if (RackIdFromModifyKey(_selectedTypeTab?.Key) is int mrid &&
                _rackModifyData.TryGetValue(mrid, out var mvm))
            {
                RackModifyVM = mvm;
            }
            if (RackIdFromHwKey(_selectedTypeTab?.Key) is int hrid &&
                _hwConfigData.TryGetValue(hrid, out var hvm))
            {
                HwConfigVM = hvm;
            }
            if (RackIdFromKeyPrefix(_selectedTypeTab?.Key, "ModuleInsert_") is int mirid &&
                _moduleInsertData.TryGetValue(mirid, out var mivm))
            {
                ModuleInsertVM = mivm;
            }
            if (RackIdFromKeyPrefix(_selectedTypeTab?.Key, "RelayInsert_") is int ririd &&
                _relayInsertData.TryGetValue(ririd, out var rivm))
            {
                RelayInsertVM = rivm;
            }
            OnPropertyChanged(nameof(IsRackModifyVisible));
            OnPropertyChanged(nameof(IsHwConfigVisible));
            OnPropertyChanged(nameof(IsModuleInsertVisible));
            OnPropertyChanged(nameof(IsRelayInsertVisible));
            OnPropertyChanged(nameof(IsRackViewVisible));
            OnPropertyChanged(nameof(IsModuleTypeVisible));
            OnPropertyChanged(nameof(IsChannelTypeVisible));
            OnPropertyChanged(nameof(IsSensorVisible));
            OnPropertyChanged(nameof(IsSensorUnitVisible));
            OnPropertyChanged(nameof(IsDisplayPlotVisible));
            OnPropertyChanged(nameof(IsProportionalVisible));
            OnPropertyChanged(nameof(IsScaleRangeVisible));
            OnPropertyChanged(nameof(IsEventStatusVisible));
            OnPropertyChanged(nameof(IsRackInsertVisible));
            OnPropertyChanged(nameof(IsTrainInsertVisible));
            OnPropertyChanged(nameof(IsAssignVisible));
            OnPropertyChanged(nameof(HasTypeTabs));
        }
    }
    public bool IsModuleTypeVisible  => _selectedTypeTab?.Key == "ModuleType";
    public bool IsChannelTypeVisible => _selectedTypeTab?.Key == "ChannelType";
    public bool IsSensorVisible      => _selectedTypeTab?.Key == "Sensor";
    public bool IsSensorUnitVisible  => _selectedTypeTab?.Key == "SensorUnit";
    public bool IsDisplayPlotVisible => _selectedTypeTab?.Key == "DisplayPlot";
    public bool IsProportionalVisible=> _selectedTypeTab?.Key == "Proportional";
    public bool IsScaleRangeVisible  => _selectedTypeTab?.Key == "ScaleRange";
    public bool IsEventStatusVisible => _selectedTypeTab?.Key == "EventStatus";
    public bool IsRackInsertVisible  => _selectedTypeTab?.Key == "RackInsert";
    public bool IsTrainInsertVisible => _selectedTypeTab?.Key == "TrainInsert";
    public bool IsRackModifyVisible  => _selectedTypeTab?.Key?.StartsWith("RackModify_") == true;
    public bool IsHwConfigVisible    => _selectedTypeTab?.Key?.StartsWith("HwConfig_") == true;

    // 랙 Modify 탭 (폼 호스팅)
    private readonly Dictionary<int, RackModifyViewModel> _rackModifyData = [];
    private RackModifyViewModel? _rackModifyVM;
    public RackModifyViewModel? RackModifyVM { get => _rackModifyVM; private set => SetProperty(ref _rackModifyVM, value); }
    private static int? RackIdFromModifyKey(string? key) =>
        key != null && key.StartsWith("RackModify_") && int.TryParse(key.AsSpan(11), out int r) ? r : null;

    // H/W Config 탭 (폼 호스팅)
    private readonly Dictionary<int, HwConfigViewModel> _hwConfigData = [];
    private HwConfigViewModel? _hwConfigVM;
    public HwConfigViewModel? HwConfigVM { get => _hwConfigVM; private set => SetProperty(ref _hwConfigVM, value); }
    private static int? RackIdFromHwKey(string? key) =>
        key != null && key.StartsWith("HwConfig_") && int.TryParse(key.AsSpan(9), out int r) ? r : null;

    // REFERENCE/IO · RELAY Insert 탭 (폼 호스팅)
    public bool IsModuleInsertVisible => _selectedTypeTab?.Key?.StartsWith("ModuleInsert_") == true;
    public bool IsRelayInsertVisible  => _selectedTypeTab?.Key?.StartsWith("RelayInsert_") == true;
    private readonly Dictionary<int, ModuleInsertViewModel>      _moduleInsertData = [];
    private readonly Dictionary<int, RelayModuleInsertViewModel> _relayInsertData  = [];
    private ModuleInsertViewModel? _moduleInsertVM;
    private RelayModuleInsertViewModel? _relayInsertVM;
    public ModuleInsertViewModel?      ModuleInsertVM { get => _moduleInsertVM; private set => SetProperty(ref _moduleInsertVM, value); }
    public RelayModuleInsertViewModel? RelayInsertVM  { get => _relayInsertVM;  private set => SetProperty(ref _relayInsertVM, value); }
    private static int? RackIdFromKeyPrefix(string? key, string prefix) =>
        key != null && key.StartsWith(prefix) && int.TryParse(key.AsSpan(prefix.Length), out int r) ? r : null;
    public bool HasTypeTabs          => _typeTabs.Count > 0;

    // ── 모듈 타입 관리 ─────────────────────────────────────
    private ObservableCollection<ModuleTypeItem> _moduleTypeRows = [];
    private ModuleTypeItem? _selectedModuleTypeRow;
    private bool   _isMtEditing;
    private bool   _isMtAddingNew;
    private int    _editMtTypeId;
    private string _editMtNicName = "";
    private string _editMtName    = "";
    private string _editMtDesc    = "";

    public ObservableCollection<ModuleTypeItem> ModuleTypeRows     { get => _moduleTypeRows;         set => SetProperty(ref _moduleTypeRows, value); }
    public ModuleTypeItem? SelectedModuleTypeRow                    { get => _selectedModuleTypeRow;  set => SetProperty(ref _selectedModuleTypeRow, value); }
    public bool   IsMtEditing   { get => _isMtEditing;    set => SetProperty(ref _isMtEditing, value); }
    public bool   IsMtAddingNew { get => _isMtAddingNew;  set { SetProperty(ref _isMtAddingNew, value); OnPropertyChanged(nameof(MtEditTitle)); } }
    public int    EditMtTypeId  { get => _editMtTypeId;   set => SetProperty(ref _editMtTypeId, value); }
    public string EditMtNicName { get => _editMtNicName;  set => SetProperty(ref _editMtNicName, value); }
    public string EditMtName    { get => _editMtName;     set => SetProperty(ref _editMtName, value); }
    public string EditMtDesc    { get => _editMtDesc;     set => SetProperty(ref _editMtDesc, value); }
    public string MtEditTitle   => _isMtAddingNew ? "새 모듈 타입 추가" : "모듈 타입 수정";

    // ── 채널 타입 관리 ─────────────────────────────────────
    private ObservableCollection<ChannelTypeItem> _channelTypeRows = [];
    private ChannelTypeItem? _selectedChannelTypeRow;
    private bool   _isCtEditing;
    private bool   _isCtAddingNew;
    private int    _editCtTypeId;
    private string _editCtNicName = "";
    private string _editCtName    = "";
    private string _editCtDesc    = "";

    public ObservableCollection<ChannelTypeItem> ChannelTypeRows     { get => _channelTypeRows;        set => SetProperty(ref _channelTypeRows, value); }
    public ChannelTypeItem? SelectedChannelTypeRow                    { get => _selectedChannelTypeRow; set => SetProperty(ref _selectedChannelTypeRow, value); }
    public bool   IsCtEditing   { get => _isCtEditing;   set => SetProperty(ref _isCtEditing, value); }
    public bool   IsCtAddingNew { get => _isCtAddingNew; set { SetProperty(ref _isCtAddingNew, value); OnPropertyChanged(nameof(CtEditTitle)); } }
    public int    EditCtTypeId  { get => _editCtTypeId;  set => SetProperty(ref _editCtTypeId, value); }
    public string EditCtNicName { get => _editCtNicName; set => SetProperty(ref _editCtNicName, value); }
    public string EditCtName    { get => _editCtName;    set => SetProperty(ref _editCtName, value); }
    public string EditCtDesc    { get => _editCtDesc;    set => SetProperty(ref _editCtDesc, value); }
    public string CtEditTitle   => _isCtAddingNew ? "새 채널 타입 추가" : "채널 타입 수정";

    // ── Module & Channel Type 연결 (내부 탭) ──────────────────
    private int _mtInnerTabIndex = 0;
    private ObservableCollection<ModuleTypeItem>  _mcModuleTypes         = [];
    private ModuleTypeItem?                       _selectedMcModule;
    private ObservableCollection<McChannelItem>   _mcChannelRows         = [];
    private McChannelItem?                        _mcSelectedChannelRow;
    private ObservableCollection<ChannelTypeItem> _allChannelTypesForMc  = [];
    private ChannelTypeItem?                      _mcSelectedCtForAdd;

    public int MtInnerTabIndex
    {
        get => _mtInnerTabIndex;
        set { SetProperty(ref _mtInnerTabIndex, value); OnPropertyChanged(nameof(IsMtTabMt)); OnPropertyChanged(nameof(IsMtTabMct)); }
    }
    public bool IsMtTabMt  => _mtInnerTabIndex == 0;
    public bool IsMtTabMct => _mtInnerTabIndex == 1;
    public ObservableCollection<ModuleTypeItem>  McModuleTypes        { get => _mcModuleTypes;        set => SetProperty(ref _mcModuleTypes, value); }
    public ModuleTypeItem? SelectedMcModule
    {
        get => _selectedMcModule;
        set { SetProperty(ref _selectedMcModule, value); if (value != null) _ = McLoadChannelsAsync(); }
    }
    public ObservableCollection<McChannelItem>   McChannelRows        { get => _mcChannelRows;        set => SetProperty(ref _mcChannelRows, value); }
    public McChannelItem?   McSelectedChannelRow { get => _mcSelectedChannelRow; set => SetProperty(ref _mcSelectedChannelRow, value); }
    public ObservableCollection<ChannelTypeItem> AllChannelTypesForMc { get => _allChannelTypesForMc; set => SetProperty(ref _allChannelTypesForMc, value); }
    public ChannelTypeItem? McSelectedCtForAdd   { get => _mcSelectedCtForAdd;   set => SetProperty(ref _mcSelectedCtForAdd, value); }

    // ── 센서 (Sensor) ────────────────────────────────────────
    private ObservableCollection<SensorItem>     _sensorRows        = [];
    private SensorItem?                          _selectedSensorRow;
    private ObservableCollection<SensorUnitItem> _sensorUnitOptions = [];
    private bool   _isSensorEditing, _isSensorAddingNew;
    private int    _editSensorId, _editSensorType, _editSensorUnitId, _editSensorIcp, _editSensorPower;
    private string _editSensorName = "", _editSensorSensitivity = "";
    private string _editSensorPowerLow = "", _editSensorPowerHigh = "";
    private string _editSensorBrandName = "", _editSensorSpec = "";

    public ObservableCollection<SensorItem>     SensorRows         { get => _sensorRows;        set => SetProperty(ref _sensorRows, value); }
    public SensorItem?                          SelectedSensorRow  { get => _selectedSensorRow;  set => SetProperty(ref _selectedSensorRow, value); }
    public ObservableCollection<SensorUnitItem> SensorUnitOptions  { get => _sensorUnitOptions;  set => SetProperty(ref _sensorUnitOptions, value); }
    public bool   IsSensorEditing   { get => _isSensorEditing;   set => SetProperty(ref _isSensorEditing, value); }
    public bool   IsSensorAddingNew { get => _isSensorAddingNew; set { SetProperty(ref _isSensorAddingNew, value); OnPropertyChanged(nameof(SensorEditTitle)); } }
    public string SensorEditTitle   => _isSensorAddingNew ? "새 센서 추가" : "센서 수정";
    public int    EditSensorId         { get => _editSensorId;         set => SetProperty(ref _editSensorId, value); }
    public string EditSensorName       { get => _editSensorName;       set => SetProperty(ref _editSensorName, value); }
    public int    EditSensorType       { get => _editSensorType;       set => SetProperty(ref _editSensorType, value); }
    public string EditSensorSensitivity{ get => _editSensorSensitivity;set => SetProperty(ref _editSensorSensitivity, value); }
    public int    EditSensorUnitId     { get => _editSensorUnitId;     set => SetProperty(ref _editSensorUnitId, value); }
    public int    EditSensorIcp        { get => _editSensorIcp;        set => SetProperty(ref _editSensorIcp, value); }
    public int    EditSensorPower      { get => _editSensorPower;      set => SetProperty(ref _editSensorPower, value); }
    public string EditSensorPowerLow   { get => _editSensorPowerLow;   set => SetProperty(ref _editSensorPowerLow, value); }
    public string EditSensorPowerHigh  { get => _editSensorPowerHigh;  set => SetProperty(ref _editSensorPowerHigh, value); }
    public string EditSensorBrandName  { get => _editSensorBrandName;  set => SetProperty(ref _editSensorBrandName, value); }
    public string EditSensorSpec       { get => _editSensorSpec;       set => SetProperty(ref _editSensorSpec, value); }

    // ── 센서 단위 (Sensor Unit) ────────────────────────────
    private ObservableCollection<SensorUnitItem> _suRows = [];
    private SensorUnitItem? _selectedSuRow;
    private bool   _isSuEditing, _isSuAddingNew;
    private int    _editSuId;
    private string _editSuName = "", _editSuDesc = "";

    public ObservableCollection<SensorUnitItem> SuRows       { get => _suRows;       set => SetProperty(ref _suRows, value); }
    public SensorUnitItem? SelectedSuRow                      { get => _selectedSuRow; set => SetProperty(ref _selectedSuRow, value); }
    public bool   IsSuEditing   { get => _isSuEditing;   set => SetProperty(ref _isSuEditing, value); }
    public bool   IsSuAddingNew { get => _isSuAddingNew; set { SetProperty(ref _isSuAddingNew, value); OnPropertyChanged(nameof(SuEditTitle)); } }
    public int    EditSuId      { get => _editSuId;      set => SetProperty(ref _editSuId, value); }
    public string EditSuName    { get => _editSuName;    set => SetProperty(ref _editSuName, value); }
    public string EditSuDesc    { get => _editSuDesc;    set => SetProperty(ref _editSuDesc, value); }
    public string SuEditTitle   => _isSuAddingNew ? "새 센서 단위 추가" : "센서 단위 수정";

    // ── Display Plot ───────────────────────────────────────
    private ObservableCollection<DisplayPlotItem> _dpRows = [];
    private DisplayPlotItem? _selectedDpRow;
    private bool   _isDpEditing, _isDpAddingNew;
    private int    _editDpId, _editDpDynamic;
    private string _editDpName = "", _editDpDesc = "";

    public ObservableCollection<DisplayPlotItem> DpRows     { get => _dpRows;       set => SetProperty(ref _dpRows, value); }
    public DisplayPlotItem? SelectedDpRow                    { get => _selectedDpRow; set => SetProperty(ref _selectedDpRow, value); }
    public bool   IsDpEditing   { get => _isDpEditing;   set => SetProperty(ref _isDpEditing, value); }
    public bool   IsDpAddingNew { get => _isDpAddingNew; set { SetProperty(ref _isDpAddingNew, value); OnPropertyChanged(nameof(DpEditTitle)); } }
    public int    EditDpId      { get => _editDpId;      set => SetProperty(ref _editDpId, value); }
    public string EditDpName    { get => _editDpName;    set => SetProperty(ref _editDpName, value); }
    public string EditDpDesc    { get => _editDpDesc;    set => SetProperty(ref _editDpDesc, value); }
    public int    EditDpDynamic { get => _editDpDynamic; set => SetProperty(ref _editDpDynamic, value); }
    public string DpEditTitle   => _isDpAddingNew ? "새 Display Plot 추가" : "Display Plot 수정";

    // ── 비례값 (Proportional) ──────────────────────────────
    private ObservableCollection<ProportionalItem> _propRows = [];
    private ProportionalItem? _selectedPropRow;
    private bool   _isPropEditing, _isPropAddingNew;
    private int    _editPropId;
    private string _editPropNicName = "", _editPropName = "", _editPropDesc = "";

    public ObservableCollection<ProportionalItem> PropRows    { get => _propRows;       set => SetProperty(ref _propRows, value); }
    public ProportionalItem? SelectedPropRow                   { get => _selectedPropRow; set => SetProperty(ref _selectedPropRow, value); }
    public bool   IsPropEditing   { get => _isPropEditing;   set => SetProperty(ref _isPropEditing, value); }
    public bool   IsPropAddingNew { get => _isPropAddingNew; set { SetProperty(ref _isPropAddingNew, value); OnPropertyChanged(nameof(PropEditTitle)); } }
    public int    EditPropId      { get => _editPropId;      set => SetProperty(ref _editPropId, value); }
    public string EditPropNicName { get => _editPropNicName; set => SetProperty(ref _editPropNicName, value); }
    public string EditPropName    { get => _editPropName;    set => SetProperty(ref _editPropName, value); }
    public string EditPropDesc    { get => _editPropDesc;    set => SetProperty(ref _editPropDesc, value); }
    public string PropEditTitle   => _isPropAddingNew ? "새 비례값 추가" : "비례값 수정";

    // ── 스케일 범위 (Scale Range) ──────────────────────────
    private ObservableCollection<ScaleRangeItem> _srRows = [];
    private ScaleRangeItem? _selectedSrRow;
    private bool   _isSrEditing, _isSrAddingNew;
    private int    _editSrId;
    private double _editSrMin, _editSrMax;
    private string _editSrName = "", _editSrDesc = "";

    public ObservableCollection<ScaleRangeItem> SrRows     { get => _srRows;       set => SetProperty(ref _srRows, value); }
    public ScaleRangeItem? SelectedSrRow                    { get => _selectedSrRow; set => SetProperty(ref _selectedSrRow, value); }
    public bool   IsSrEditing   { get => _isSrEditing;   set => SetProperty(ref _isSrEditing, value); }
    public bool   IsSrAddingNew { get => _isSrAddingNew; set { SetProperty(ref _isSrAddingNew, value); OnPropertyChanged(nameof(SrEditTitle)); } }
    public int    EditSrId      { get => _editSrId;      set => SetProperty(ref _editSrId, value); }
    public string EditSrName    { get => _editSrName;    set => SetProperty(ref _editSrName, value); }
    public double EditSrMin     { get => _editSrMin;     set => SetProperty(ref _editSrMin, value); }
    public double EditSrMax     { get => _editSrMax;     set => SetProperty(ref _editSrMax, value); }
    public string EditSrDesc    { get => _editSrDesc;    set => SetProperty(ref _editSrDesc, value); }
    public string SrEditTitle   => _isSrAddingNew ? "새 스케일 범위 추가" : "스케일 범위 수정";

    // ── 이벤트/상태 (Event) ────────────────────────────────
    private ObservableCollection<EventItem> _evRows = [];
    private EventItem? _selectedEvRow;
    private bool   _isEvEditing, _isEvAddingNew;
    private int    _editEvId, _editEvClass;
    private string _editEvName = "", _editEvDesc = "";

    public ObservableCollection<EventItem> EvRows       { get => _evRows;       set => SetProperty(ref _evRows, value); }
    public EventItem? SelectedEvRow                      { get => _selectedEvRow; set => SetProperty(ref _selectedEvRow, value); }
    public bool   IsEvEditing   { get => _isEvEditing;   set => SetProperty(ref _isEvEditing, value); }
    public bool   IsEvAddingNew { get => _isEvAddingNew; set { SetProperty(ref _isEvAddingNew, value); OnPropertyChanged(nameof(EvEditTitle)); } }
    public int    EditEvId      { get => _editEvId;      set => SetProperty(ref _editEvId, value); }
    public string EditEvName    { get => _editEvName;    set => SetProperty(ref _editEvName, value); }
    public int    EditEvClass   { get => _editEvClass;   set => SetProperty(ref _editEvClass, value); }
    public string EditEvDesc    { get => _editEvDesc;    set => SetProperty(ref _editEvDesc, value); }
    public string EvEditTitle   => _isEvAddingNew ? "새 이벤트 추가" : "이벤트 수정";

    // ── Rack 뷰 탭 (랙별 독립 탭) ────────────────────────────
    private DeviceTreeNode?                    _rackViewNode;
    private ObservableCollection<RackSlotItem> _rackViewSlots       = [];
    private RackSlotItem?                      _selectedRackViewSlot;
    private readonly Dictionary<int, (DeviceTreeNode Node, ObservableCollection<RackSlotItem> Slots)> _rackViewData = [];

    public DeviceTreeNode?                    RackViewNode          { get => _rackViewNode;          private set { SetProperty(ref _rackViewNode, value); OnPropertyChanged(nameof(RackViewTitle)); } }
    public ObservableCollection<RackSlotItem> RackViewSlots         { get => _rackViewSlots;         private set => SetProperty(ref _rackViewSlots, value); }
    public RackSlotItem?                      SelectedRackViewSlot  { get => _selectedRackViewSlot;  set => SetProperty(ref _selectedRackViewSlot, value); }
    public string                             RackViewTitle         => _rackViewNode != null ? $"RACK {_rackViewNode.RackId:D2}  {_rackViewNode.Name}" : "";
    public bool                               IsRackViewVisible     => _selectedTypeTab?.Key?.StartsWith("RackView_") == true;

    private static int? RackIdFromTabKey(string? key) =>
        key?.StartsWith("RackView_") == true && int.TryParse(key["RackView_".Length..], out int id) ? id : null;

    // ── RackInsert / TrainInsert / Assign 서브 VM ──────────────
    private RackInsertViewModel?  _rackInsertTab;
    private TrainInsertViewModel? _trainInsertTab;
    private AssignInsertViewModel? _assignTab;
    public RackInsertViewModel?  RackInsertTab  { get => _rackInsertTab;  private set => SetProperty(ref _rackInsertTab, value); }
    public TrainInsertViewModel? TrainInsertTab { get => _trainInsertTab; private set => SetProperty(ref _trainInsertTab, value); }
    public AssignInsertViewModel? AssignTab     { get => _assignTab;      private set => SetProperty(ref _assignTab, value); }
    public bool IsAssignVisible => _selectedTypeTab?.Key == "Assign";

    // ── 스테이션 모달 이벤트 ────────────────────────────────
    public event Action<int>?           InsertStationRequested;
    public event Action<DeviceStation>? ModifyStationRequested;
    public event Action<DeviceTreeNode>? RackCopyRequested;
    public event Action<DeviceTreeNode>? CopyRequested;   // MODULE / CHANNEL COPY 다이얼로그(frmCopy)

    // ── 명령 ───────────────────────────────────────────────
    public RelayCommand RefreshCommand              { get; }
    public RelayCommand AddStationCommand           { get; }
    public RelayCommand EditStationCommand          { get; }
    public RelayCommand DeleteStationCommand        { get; }
    public RelayCommand SaveStationCommand          { get; }
    public RelayCommand CancelStationCommand        { get; }
    public RelayCommand AddRackCommand              { get; }
    public RelayCommand DeleteRackCommand           { get; }
    public RelayCommand AssignInsertCommand         { get; }
    // Rack 컨텍스트 메뉴 전용
    public RelayCommand RackOpenCommand             { get; }
    public RelayCommand RackModifyCommand           { get; }
    public RelayCommand RackHwConfigCommand         { get; }
    public RelayCommand CtxRackDeleteCommand        { get; }
    public RelayCommand RackCopyCommand             { get; }
    public RelayCommand CtxModuleCopyCommand        { get; }
    public RelayCommand CtxChannelCopyCommand       { get; }
    public RelayCommand CtxAddModuleCommand         { get; }
    public RelayCommand CtxAddChannelCommand        { get; }
    public RelayCommand CtxModuleInsertCommand      { get; }
    public RelayCommand CtxRelayInsertCommand       { get; }
    public RelayCommand EditRackNameCommand         { get; }
    public RelayCommand SaveRackNameCommand         { get; }
    public RelayCommand CancelRackNameCommand       { get; }
    public RelayCommand ToggleRackActivityCommand    { get; }
    public RelayCommand ToggleModuleActivityCommand  { get; }
    public RelayCommand ToggleChannelActivityCommand { get; }
    public RelayCommand AddTrainCommand              { get; }
    public RelayCommand AddChildTrainCommand        { get; }
    public RelayCommand EditTrainNodeCommand        { get; }
    public RelayCommand DeleteTrainNodeCommand      { get; }
    public RelayCommand SaveTrainCommand            { get; }
    public RelayCommand CancelTrainCommand          { get; }
    // Station 컨텍스트메뉴 전용 (탭 기반 Insert)
    public RelayCommand CtxRackInsertCommand        { get; }
    public RelayCommand CtxTrainInsertCommand       { get; }

    public RelayCommand OpenModuleTypeCommand      { get; }
    public RelayCommand SwitchToMtTabCommand       { get; }
    public RelayCommand SwitchToMctTabCommand      { get; }
    public RelayCommand McAddCommand               { get; }
    public RelayCommand McDeleteCommand            { get; }
    public RelayCommand OpenChannelTypeCommand     { get; }
    public RelayCommand OpenSensorCommand          { get; }
    public RelayCommand SensorInsertCommand        { get; }
    public RelayCommand SensorModifyCommand        { get; }
    public RelayCommand SensorDeleteCommand        { get; }
    public RelayCommand SensorSaveCommand          { get; }
    public RelayCommand SensorCancelCommand        { get; }
    public RelayCommand OpenSensorUnitCommand      { get; }
    public RelayCommand OpenDisplayPlotCommand     { get; }
    public RelayCommand OpenProportionalCommand    { get; }
    public RelayCommand OpenScaleRangeCommand      { get; }
    public RelayCommand OpenEventStatusCommand     { get; }
    public RelayCommand CloseTypePanelCommand      { get; }
    public RelayCommand<TypeTab> SelectTabCommand  { get; }
    // System 메뉴
    public RelayCommand CheckDataCommand           { get; }
    public RelayCommand SaveSettingsXlsxCommand    { get; }
    public RelayCommand LoadSettingsCommand        { get; }
    public RelayCommand BackupDbXlsxCommand        { get; }
    public RelayCommand BackupRestoreAllDbCommand  { get; }
    public RelayCommand<TypeTab> CloseTabCommand   { get; }

    // 센서 단위
    public RelayCommand SuInsertCommand  { get; }
    public RelayCommand SuModifyCommand  { get; }
    public RelayCommand SuDeleteCommand  { get; }
    public RelayCommand SuSaveCommand    { get; }
    public RelayCommand SuCancelCommand  { get; }
    // Display Plot
    public RelayCommand DpInsertCommand  { get; }
    public RelayCommand DpModifyCommand  { get; }
    public RelayCommand DpDeleteCommand  { get; }
    public RelayCommand DpSaveCommand    { get; }
    public RelayCommand DpCancelCommand  { get; }
    // 비례값
    public RelayCommand PropInsertCommand { get; }
    public RelayCommand PropModifyCommand { get; }
    public RelayCommand PropDeleteCommand { get; }
    public RelayCommand PropSaveCommand   { get; }
    public RelayCommand PropCancelCommand { get; }
    // 스케일 범위
    public RelayCommand SrInsertCommand  { get; }
    public RelayCommand SrModifyCommand  { get; }
    public RelayCommand SrDeleteCommand  { get; }
    public RelayCommand SrSaveCommand    { get; }
    public RelayCommand SrCancelCommand  { get; }
    // 이벤트/상태
    public RelayCommand EvInsertCommand  { get; }
    public RelayCommand EvModifyCommand  { get; }
    public RelayCommand EvDeleteCommand  { get; }
    public RelayCommand EvSaveCommand    { get; }
    public RelayCommand EvCancelCommand  { get; }
    public RelayCommand MtInsertCommand         { get; }
    public RelayCommand MtModifyCommand         { get; }
    public RelayCommand MtDeleteCommand         { get; }
    public RelayCommand MtSaveCommand           { get; }
    public RelayCommand MtCancelCommand         { get; }
    public RelayCommand CtInsertCommand         { get; }
    public RelayCommand CtModifyCommand         { get; }
    public RelayCommand CtDeleteCommand         { get; }
    public RelayCommand CtSaveCommand           { get; }
    public RelayCommand CtCancelCommand         { get; }
    // CT 내부탭 전환 + 연결 항목 CRUD
    public RelayCommand CtSwitchTab0Command     { get; }
    public RelayCommand CtSwitchTab1Command     { get; }
    public RelayCommand CtSwitchTab2Command     { get; }
    public RelayCommand CtSwitchTab3Command     { get; }
    public RelayCommand CtSwitchTab4Command     { get; }
    public RelayCommand CtSwitchTab5Command     { get; }
    public RelayCommand CtSen_AddCommand        { get; }
    public RelayCommand CtSen_DeleteCommand     { get; }
    public RelayCommand CtSu_AddCommand         { get; }
    public RelayCommand CtSu_DeleteCommand      { get; }
    public RelayCommand CtPl_AddCommand         { get; }
    public RelayCommand CtPl_DeleteCommand      { get; }
    public RelayCommand CtPr_AddCommand         { get; }
    public RelayCommand CtPr_DeleteCommand      { get; }
    public RelayCommand CtSc_AddCommand         { get; }
    public RelayCommand CtSc_DeleteCommand      { get; }
    // SU 내부탭
    public RelayCommand SuSwitchTab0Command     { get; }
    public RelayCommand SuSwitchTab1Command     { get; }
    // DP 내부탭 전환 + 연결 항목 CRUD
    public RelayCommand DpSwitchTab0Command     { get; }
    public RelayCommand DpSwitchTab1Command     { get; }
    public RelayCommand DpSwitchTab2Command     { get; }
    public RelayCommand DpSwitchTab3Command     { get; }
    public RelayCommand DpSwitchTab4Command     { get; }
    public RelayCommand DpPr_AddCommand         { get; }
    public RelayCommand DpPr_DeleteCommand      { get; }
    public RelayCommand DpDs_AddCommand         { get; }
    public RelayCommand DpDs_DeleteCommand      { get; }
    public RelayCommand DpCo_AddCommand         { get; }
    public RelayCommand DpCo_DeleteCommand      { get; }
    public RelayCommand DpFr_AddCommand         { get; }
    public RelayCommand DpFr_DeleteCommand      { get; }
    // Prop 내부탭
    public RelayCommand PrSwitchTab0Command     { get; }
    public RelayCommand PrSwitchTab1Command     { get; }
    public RelayCommand PrSc_AddCommand         { get; }
    public RelayCommand PrSc_DeleteCommand      { get; }

    public DeviceConfigViewModel()
    {
        RefreshCommand              = new RelayCommand(_ => _ = RefreshAsync());
        AddStationCommand           = new RelayCommand(_ => StartAddStation());
        EditStationCommand          = new RelayCommand(_ => StartEditStation(),       _ => SelectedStation != null);
        DeleteStationCommand        = new RelayCommand(_ => _ = DeleteStationAsync(), _ => SelectedStation != null);
        SaveStationCommand          = new RelayCommand(_ => _ = SaveStationAsync());
        CancelStationCommand        = new RelayCommand(_ => CancelStationEdit());
        AddRackCommand               = new RelayCommand(_ => StartAddRackOrModule());
        DeleteRackCommand            = new RelayCommand(_ => _ = DeleteRackNodeAsync(),
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack ||
                                                SelectedRackNode?.Kind == NodeKind.Module ||
                                                SelectedRackNode?.Kind == NodeKind.Channel);
        RackOpenCommand              = new RelayCommand(_ => {
                                               if (SelectedRackNode?.Kind == NodeKind.Rack)
                                                   OpenRackViewTab(SelectedRackNode);
                                           }, _ => SelectedRackNode?.Kind == NodeKind.Rack);
        RackModifyCommand            = new RelayCommand(_ => {
                                               if (SelectedRackNode?.Kind == NodeKind.Rack)
                                                   OpenRackModifyTab(SelectedRackNode);
                                               else if (SelectedRackNode != null)
                                                   StartEditRackName();   // Module/Channel 은 인라인 이름수정
                                           }, _ => SelectedRackNode?.Kind is NodeKind.Rack or NodeKind.Module or NodeKind.Channel);
        RackHwConfigCommand          = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Rack) OpenHwConfigTab(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack);
        CtxRackDeleteCommand         = new RelayCommand(_ => _ = CtxRackDeleteAsync(),
                                           _ => SelectedRackNode?.Kind is NodeKind.Rack or NodeKind.Module or NodeKind.Channel);
        RackCopyCommand              = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Rack) RackCopyRequested?.Invoke(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack);
        CtxModuleCopyCommand         = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Module) CopyRequested?.Invoke(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Module);
        CtxChannelCopyCommand        = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Channel) CopyRequested?.Invoke(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Channel);
        CtxModuleInsertCommand       = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Rack) OpenModuleInsertTab(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack);
        CtxRelayInsertCommand        = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Rack) OpenRelayInsertTab(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack);
        CtxAddModuleCommand          = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Rack) StartAddModule(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Rack);
        CtxAddChannelCommand         = new RelayCommand(_ => { if (SelectedRackNode?.Kind == NodeKind.Module) StartAddChannel(SelectedRackNode); },
                                           _ => SelectedRackNode?.Kind == NodeKind.Module);
        EditRackNameCommand          = new RelayCommand(_ => StartEditRackName(),
                                           _ => SelectedRackNode?.Kind is NodeKind.Rack or NodeKind.Module or NodeKind.Channel);
        SaveRackNameCommand          = new RelayCommand(_ => _ = SaveRackNameAsync());
        CancelRackNameCommand        = new RelayCommand(_ => CancelRackEdit());
        ToggleRackActivityCommand    = new RelayCommand(_ => _ = ToggleRackActivityAsync(),    _ => SelectedRackNode?.Kind == NodeKind.Rack);
        ToggleModuleActivityCommand  = new RelayCommand(_ => _ = ToggleModuleActivityAsync(),  _ => SelectedRackNode?.Kind == NodeKind.Module);
        ToggleChannelActivityCommand = new RelayCommand(_ => _ = ToggleChannelActivityAsync(), _ => SelectedRackNode?.Kind == NodeKind.Channel);
        AssignInsertCommand         = new RelayCommand(_ => StartAssignInsert(), _ => SelectedStation != null);
        AddTrainCommand             = new RelayCommand(_ => StartAddTrain());
        AddChildTrainCommand        = new RelayCommand(_ => StartAddChild(),
                                          _ => SelectedTrainNode != null && SelectedTrainNode.Kind != NodeKind.Point);
        EditTrainNodeCommand        = new RelayCommand(_ => StartEditTrainNode(), _ => SelectedTrainNode != null);
        DeleteTrainNodeCommand      = new RelayCommand(_ => _ = DeleteTrainNodeAsync(), _ => SelectedTrainNode != null);
        SaveTrainCommand            = new RelayCommand(_ => _ = SaveTrainAsync());
        CancelTrainCommand          = new RelayCommand(_ => CancelTrainEdit());
        CtxRackInsertCommand        = new RelayCommand(_ => _ = OpenRackInsertTabAsync());
        CtxTrainInsertCommand       = new RelayCommand(_ => _ = OpenTrainInsertTabAsync());

        OpenModuleTypeCommand   = new RelayCommand(_ => _ = OpenModuleTypeAsync());
        SwitchToMtTabCommand    = new RelayCommand(_ => MtInnerTabIndex = 0);
        SwitchToMctTabCommand   = new RelayCommand(_ => _ = SwitchToMctAsync());
        McAddCommand    = new RelayCommand(_ => _ = McAddChannelTypeAsync(),    _ => McSelectedCtForAdd != null && SelectedMcModule != null);
        McDeleteCommand = new RelayCommand(_ => _ = McDeleteChannelTypeAsync(), _ => McSelectedChannelRow != null && SelectedMcModule != null);
        OpenChannelTypeCommand  = new RelayCommand(_ => _ = OpenChannelTypeAsync());
        OpenSensorCommand       = new RelayCommand(_ => _ = OpenSensorAsync());
        SensorInsertCommand     = new RelayCommand(_ => _ = StartSensorInsertAsync());
        SensorModifyCommand     = new RelayCommand(_ => StartSensorModify(), _ => SelectedSensorRow != null);
        SensorDeleteCommand     = new RelayCommand(_ => _ = DeleteSensorItemAsync(), _ => SelectedSensorRow != null);
        SensorSaveCommand       = new RelayCommand(_ => _ = SaveSensorAsync());
        SensorCancelCommand     = new RelayCommand(_ => { IsSensorEditing = false; });
        OpenSensorUnitCommand   = new RelayCommand(_ => _ = OpenSensorUnitAsync());
        OpenDisplayPlotCommand  = new RelayCommand(_ => _ = OpenDisplayPlotAsync());
        OpenProportionalCommand = new RelayCommand(_ => _ = OpenProportionalAsync());
        OpenScaleRangeCommand   = new RelayCommand(_ => _ = OpenScaleRangeAsync());
        OpenEventStatusCommand  = new RelayCommand(_ => _ = OpenEventStatusAsync());
        CloseTypePanelCommand   = new RelayCommand(_ => CloseTab(SelectedTypeTab));
        CheckDataCommand          = new RelayCommand(_ => _ = CheckDataAsync());
        SaveSettingsXlsxCommand   = new RelayCommand(_ => _ = SaveSettingsXlsxAsync());
        LoadSettingsCommand       = new RelayCommand(_ => _ = LoadSettingsAsync());
        BackupDbXlsxCommand       = new RelayCommand(_ => _ = BackupDbXlsxAsync());
        BackupRestoreAllDbCommand = new RelayCommand(_ => _ = BackupRestoreAllDbAsync());
        SelectTabCommand        = new RelayCommand<TypeTab>(t => { if (t != null) SelectedTypeTab = t; });
        CloseTabCommand         = new RelayCommand<TypeTab>(t => CloseTab(t));

        SuInsertCommand   = new RelayCommand(_ => _ = StartSuInsert());
        SuModifyCommand   = new RelayCommand(_ => StartSuModify(),  _ => SelectedSuRow != null);
        SuDeleteCommand   = new RelayCommand(_ => _ = DeleteSuAsync(), _ => SelectedSuRow != null);
        SuSaveCommand     = new RelayCommand(_ => _ = SaveSuAsync());
        SuCancelCommand   = new RelayCommand(_ => { IsSuEditing = false; });

        DpInsertCommand   = new RelayCommand(_ => _ = StartDpInsert());
        DpModifyCommand   = new RelayCommand(_ => StartDpModify(),  _ => SelectedDpRow != null);
        DpDeleteCommand   = new RelayCommand(_ => _ = DeleteDpAsync(), _ => SelectedDpRow != null);
        DpSaveCommand     = new RelayCommand(_ => _ = SaveDpAsync());
        DpCancelCommand   = new RelayCommand(_ => { IsDpEditing = false; });

        PropInsertCommand = new RelayCommand(_ => _ = StartPropInsert());
        PropModifyCommand = new RelayCommand(_ => StartPropModify(), _ => SelectedPropRow != null);
        PropDeleteCommand = new RelayCommand(_ => _ = DeletePropAsync(), _ => SelectedPropRow != null);
        PropSaveCommand   = new RelayCommand(_ => _ = SavePropAsync());
        PropCancelCommand = new RelayCommand(_ => { IsPropEditing = false; });

        SrInsertCommand   = new RelayCommand(_ => _ = StartSrInsert());
        SrModifyCommand   = new RelayCommand(_ => StartSrModify(),  _ => SelectedSrRow != null);
        SrDeleteCommand   = new RelayCommand(_ => _ = DeleteSrAsync(), _ => SelectedSrRow != null);
        SrSaveCommand     = new RelayCommand(_ => _ = SaveSrAsync());
        SrCancelCommand   = new RelayCommand(_ => { IsSrEditing = false; });

        EvInsertCommand   = new RelayCommand(_ => _ = StartEvInsert());
        EvModifyCommand   = new RelayCommand(_ => StartEvModify(),  _ => SelectedEvRow != null);
        EvDeleteCommand   = new RelayCommand(_ => _ = DeleteEvAsync(), _ => SelectedEvRow != null);
        EvSaveCommand     = new RelayCommand(_ => _ = SaveEvAsync());
        EvCancelCommand   = new RelayCommand(_ => { IsEvEditing = false; });
        MtInsertCommand         = new RelayCommand(_ => _ = StartMtInsert());
        MtModifyCommand         = new RelayCommand(_ => StartMtModify(), _ => SelectedModuleTypeRow != null);
        MtDeleteCommand         = new RelayCommand(_ => _ = DeleteMtAsync(), _ => SelectedModuleTypeRow != null);
        MtSaveCommand           = new RelayCommand(_ => _ = SaveMtAsync());
        MtCancelCommand         = new RelayCommand(_ => { IsMtEditing = false; });
        CtInsertCommand         = new RelayCommand(_ => _ = StartCtInsert());
        CtModifyCommand         = new RelayCommand(_ => StartCtModify(), _ => SelectedChannelTypeRow != null);
        CtDeleteCommand         = new RelayCommand(_ => _ = DeleteCtAsync(), _ => SelectedChannelTypeRow != null);
        CtSaveCommand           = new RelayCommand(_ => _ = SaveCtAsync());
        CtCancelCommand         = new RelayCommand(_ => { IsCtEditing = false; });

        CtSwitchTab0Command = new RelayCommand(_ => CtInnerTabIndex = 0);
        CtSwitchTab1Command = new RelayCommand(_ => CtInnerTabIndex = 1);
        CtSwitchTab2Command = new RelayCommand(_ => CtInnerTabIndex = 2);
        CtSwitchTab3Command = new RelayCommand(_ => CtInnerTabIndex = 3);
        CtSwitchTab4Command = new RelayCommand(_ => CtInnerTabIndex = 4);
        CtSwitchTab5Command = new RelayCommand(_ => CtInnerTabIndex = 5);
        CtSen_AddCommand    = new RelayCommand(_ => _ = CtSen_AddAsync(),    _ => CtSen_SelectedAdd != null && CtJunction_SelectedCt != null);
        CtSen_DeleteCommand = new RelayCommand(_ => _ = CtSen_DeleteAsync(), _ => CtSen_SelectedRow != null && CtJunction_SelectedCt != null);
        CtSu_AddCommand     = new RelayCommand(_ => _ = CtSu_AddAsync(),     _ => CtSu_SelectedAdd != null && CtJunction_SelectedCt != null);
        CtSu_DeleteCommand  = new RelayCommand(_ => _ = CtSu_DeleteAsync(),  _ => CtSu_SelectedRow != null && CtJunction_SelectedCt != null);
        CtPl_AddCommand     = new RelayCommand(_ => _ = CtPl_AddAsync(),     _ => CtPl_SelectedAdd != null && CtJunction_SelectedCt != null);
        CtPl_DeleteCommand  = new RelayCommand(_ => _ = CtPl_DeleteAsync(),  _ => CtPl_SelectedRow != null && CtJunction_SelectedCt != null);
        CtPr_AddCommand     = new RelayCommand(_ => _ = CtPr_AddAsync(),     _ => CtPr_SelectedAdd != null && CtJunction_SelectedCt != null);
        CtPr_DeleteCommand  = new RelayCommand(_ => _ = CtPr_DeleteAsync(),  _ => CtPr_SelectedRow != null && CtJunction_SelectedCt != null);
        CtSc_AddCommand     = new RelayCommand(_ => _ = CtSc_AddAsync(),     _ => CtSc_SelectedAdd != null && CtJunction_SelectedCt != null);
        CtSc_DeleteCommand  = new RelayCommand(_ => _ = CtSc_DeleteAsync(),  _ => CtSc_SelectedRow != null && CtJunction_SelectedCt != null);

        SuSwitchTab0Command = new RelayCommand(_ => SuInnerTabIndex = 0);
        SuSwitchTab1Command = new RelayCommand(_ => SuInnerTabIndex = 1);

        DpSwitchTab0Command = new RelayCommand(_ => DpInnerTabIndex = 0);
        DpSwitchTab1Command = new RelayCommand(_ => DpInnerTabIndex = 1);
        DpSwitchTab2Command = new RelayCommand(_ => DpInnerTabIndex = 2);
        DpSwitchTab3Command = new RelayCommand(_ => DpInnerTabIndex = 3);
        DpSwitchTab4Command = new RelayCommand(_ => DpInnerTabIndex = 4);
        DpPr_AddCommand     = new RelayCommand(_ => _ = DpPr_AddAsync(),     _ => DpPr_SelectedAdd != null && DpJunction_SelectedPlot != null);
        DpPr_DeleteCommand  = new RelayCommand(_ => _ = DpPr_DeleteAsync(),  _ => DpPr_SelectedRow != null && DpJunction_SelectedPlot != null);
        DpDs_AddCommand     = new RelayCommand(_ => _ = DpDs_AddAsync(),     _ => DpDs_SelectedAdd != null && DpJunction_SelectedPlot != null);
        DpDs_DeleteCommand  = new RelayCommand(_ => _ = DpDs_DeleteAsync(),  _ => DpDs_SelectedRow != null && DpJunction_SelectedPlot != null);
        DpCo_AddCommand     = new RelayCommand(_ => _ = DpCo_AddAsync(),     _ => DpCo_SelectedAdd != null && DpJunction_SelectedPlot != null);
        DpCo_DeleteCommand  = new RelayCommand(_ => _ = DpCo_DeleteAsync(),  _ => DpCo_SelectedRow != null && DpJunction_SelectedPlot != null);
        DpFr_AddCommand     = new RelayCommand(_ => _ = DpFr_AddAsync(),     _ => DpFr_SelectedAdd != null && DpJunction_SelectedPlot != null);
        DpFr_DeleteCommand  = new RelayCommand(_ => _ = DpFr_DeleteAsync(),  _ => DpFr_SelectedRow != null && DpJunction_SelectedPlot != null);

        PrSwitchTab0Command = new RelayCommand(_ => PrInnerTabIndex = 0);
        PrSwitchTab1Command = new RelayCommand(_ => PrInnerTabIndex = 1);
        PrSc_AddCommand     = new RelayCommand(_ => _ = PrSc_AddAsync(),     _ => PrSc_SelectedAdd != null && PrJunction_SelectedProp != null);
        PrSc_DeleteCommand  = new RelayCommand(_ => _ = PrSc_DeleteAsync(),  _ => PrSc_SelectedRow != null && PrJunction_SelectedProp != null);
    }

    // ────────────────────────────────────────────────────────
    //  초기화 / 새로 고침
    // ────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        IsBusy = true;
        try
        {
            var list = await DeviceService.GetStationsAsync();
            Stations = new ObservableCollection<DeviceStation>(list);
            if (Stations.Count > 0 && _selectedStation == null)
                await SelectStationAsync(Stations[0]);
        }
        catch (Exception ex) { StatusMessage = $"스테이션 로드 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// DB 연결이 변경된 직후 호출: 이전 DB 기준 선택을 무효화하고
    /// 새 DB의 STATION/RACK/TRAIN 을 처음부터 다시 로드한다.
    /// </summary>
    public async Task ReloadForDbChangeAsync()
    {
        _selectedStation = null;   // 이전 DB의 선택은 무효(스테이션ID가 같아도 다른 DB임)
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        StatusMessage = "";
        int? prevSid = _selectedStation?.StationId;
        IsBusy = true;
        try
        {
            var list = await DeviceService.GetStationsAsync();
            Stations = new ObservableCollection<DeviceStation>(list);
            var target = (prevSid.HasValue ? Stations.FirstOrDefault(s => s.StationId == prevSid) : null)
                         ?? (Stations.Count > 0 ? Stations[0] : null);
            await SelectStationAsync(target);
        }
        catch (Exception ex) { StatusMessage = $"새로 고침 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task SelectStationAsync(DeviceStation? station)
    {
        // DataGrid가 ItemsSource 변경을 처리한 뒤 SelectedItem 바인딩이 반영되도록 한 프레임 양보
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => { }, System.Windows.Threading.DispatcherPriority.Background);
        _selectedStation = station;
        OnPropertyChanged(nameof(SelectedStation));
        await LoadTreesAsync();
    }

    private async Task LoadTreesAsync()
    {
        if (SelectedStation == null) { RackNodes = []; TrainNodes = []; AvailableChannels.Clear(); return; }
        IsRackEditing       = false;
        IsRackNameEditing   = false;
        IsRackAddingNew     = false;
        IsModuleAddingNew   = false;
        IsChannelAddingNew  = false;
        IsTrainEditing      = false;
        _selectedRackNode  = null;
        _selectedTrainNode = null;
        IsBusy = true;
        try
        {
            int sid = SelectedStation.StationId;
            var rackTask    = DeviceService.GetRackNodesAsync(sid);
            var trainTask   = DeviceService.GetTrainNodesAsync(sid);
            var channelTask = DeviceService.GetChannelOptionsAsync(sid);
            await Task.WhenAll(rackTask, trainTask, channelTask);
            RackNodes  = new ObservableCollection<DeviceTreeNode>(rackTask.Result);
            TrainNodes = new ObservableCollection<DeviceTreeNode>(trainTask.Result);

            AvailableChannels.Clear();
            AvailableChannels.Add(new ChannelOption { ChannelId = 0, DisplayName = "(미연결)" });
            foreach (var c in channelTask.Result) AvailableChannels.Add(c);
        }
        catch (Exception ex) { StatusMessage = $"트리 로드 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────
    //  스테이션 CRUD
    // ────────────────────────────────────────────────────────

    private void StartAddStation()
    {
        int nextId = Stations.Count > 0 ? Stations.Max(s => s.StationId) + 1 : 1;
        InsertStationRequested?.Invoke(nextId);
    }

    private void StartEditStation()
    {
        var s = SelectedStation;
        if (s == null) return;
        ModifyStationRequested?.Invoke(s);
    }

    private async Task SaveStationAsync()
    {
        if (string.IsNullOrWhiteSpace(EditStationName)) { StatusMessage = "스테이션 이름을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsStationAddingNew)
            {
                await DeviceService.CreateStationAsync(EditStationId, EditStationName.Trim(),
                    EditStationCompany.Trim(), EditStationAddr.Trim(), EditStationPort);
                StatusMessage = $"스테이션 {EditStationId} '{EditStationName}' 추가 완료";
            }
            else
            {
                await DeviceService.UpdateStationAsync(EditStationId, EditStationName.Trim(),
                    EditStationCompany.Trim(), EditStationAddr.Trim(), EditStationPort);
                StatusMessage = $"스테이션 {EditStationId} '{EditStationName}' 수정 완료";
            }
            IsStationEditing = false;
            int savedSid = EditStationId;
            await RefreshAsyncSelectStation(savedSid);
        }
        catch (Exception ex) { StatusMessage = $"스테이션 저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteStationAsync()
    {
        var s = SelectedStation;
        if (s == null) return;
        var confirm = System.Windows.MessageBox.Show(
            $"스테이션 {s.StationId} '{s.Name}'을(를) 삭제하시겠습니까?\n\n⚠ 하위의 모든 RACK, MODULE, CHANNEL, TRAIN 데이터가 함께 삭제됩니다.",
            "스테이션 삭제 확인",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteStationAsync(s.StationId);
            StatusMessage    = $"스테이션 {s.StationId} 삭제 완료";
            _selectedStation = null;
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"스테이션 삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void CancelStationEdit() => IsStationEditing = false;

    private void StartAssignInsert()
    {
        if (SelectedStation == null)
        {
            System.Windows.MessageBox.Show("스테이션을 먼저 선택하세요.", "알림",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        int sid = SelectedStation.StationId;

        // 이미 열린 Assign 탭이 있으면 닫고 새로 연다
        var existing = TypeTabs.FirstOrDefault(t => t.Key == "Assign");
        if (existing != null) CloseTab(existing);

        var vm = new AssignInsertViewModel(sid) { Title = $"ASSIGN  —  Station {sid}" };
        AssignTab = vm;

        var tab = new TypeTab { Key = "Assign", Title = "Assign Insert" };
        TypeTabs.Add(tab);
        OnPropertyChanged(nameof(HasTypeTabs));
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
        StatusMessage   = "";
        _ = vm.LoadAsync();
    }

    public async Task RefreshAsyncSelectStation(int stationId)
    {
        var list = await DeviceService.GetStationsAsync();
        Stations = new ObservableCollection<DeviceStation>(list);
        _selectedStation = Stations.FirstOrDefault(s => s.StationId == stationId) ?? Stations.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedStation));
        await LoadTreesAsync();
    }

    // ────────────────────────────────────────────────────────
    //  RACK 편집
    // ────────────────────────────────────────────────────────

    private void PopulateRackEdit()
    {
        var node = SelectedRackNode;
        if (node == null) { IsRackEditing = false; IsRackNameEditing = false; return; }
        RackEditName       = node.Name;
        RackEditIsActive   = node.IsActive;
        IsRackNameEditing  = false;
        IsRackAddingNew    = false;
        IsModuleAddingNew  = false;
        IsChannelAddingNew = false;
        OnPropertyChanged(nameof(ShowEditNameButton));
        OnPropertyChanged(nameof(EditNameButtonLabel));
        RackEditInfo = node.Kind switch
        {
            NodeKind.Rack    => $"위치: {(string.IsNullOrEmpty(node.Location) ? "(없음)" : node.Location)}  |  IP: {node.LocalIp}:{node.LocalPort}",
            NodeKind.Module  => $"모듈 타입: {(string.IsNullOrEmpty(node.ModuleType) ? "(없음)" : node.ModuleType)}",
            NodeKind.Channel => $"채널 인덱스: {node.ChannelIndex}",
            _                => ""
        };
        // 탭이 열려 있으면 유지(노드 선택만으로 탭이 사라지지 않도록).
        if (SelectedTypeTab == null) IsRackEditing = true;
    }

    public void SelectRackNode(DeviceTreeNode node) => SelectedRackNode = node;

    private void StartAddRackOrModule()
    {
        if (SelectedStation == null) { StatusMessage = "스테이션을 먼저 선택하세요."; return; }
        if (SelectedRackNode?.Kind == NodeKind.Module)
            StartAddChannel(SelectedRackNode);
        else if (SelectedRackNode?.Kind == NodeKind.Rack)
            StartAddModule(SelectedRackNode);
        else
            StartAddRack();
    }

    private void StartAddRack()
    {
        _selectedRackNode = null;
        OnPropertyChanged(nameof(SelectedRackNode));
        EditRackName      = "";
        EditRackLocation  = "";
        IsModuleAddingNew = false;
        IsRackAddingNew   = true;
        IsRackNameEditing = true;
        IsRackEditing     = true;
    }

    private void StartAddModule(DeviceTreeNode rackNode)
    {
        _moduleAddRackId   = rackNode.RackId;
        _selectedRackNode  = null;
        OnPropertyChanged(nameof(SelectedRackNode));
        EditRackName       = "";
        IsRackAddingNew    = false;
        IsChannelAddingNew = false;
        IsModuleAddingNew  = true;
        IsRackNameEditing  = true;
        IsRackEditing      = true;
    }

    private void StartAddChannel(DeviceTreeNode moduleNode)
    {
        _channelAddRackId   = moduleNode.RackId;
        _channelAddModuleId = moduleNode.ModuleId;
        _selectedRackNode   = null;
        OnPropertyChanged(nameof(SelectedRackNode));
        EditRackName        = "";
        IsRackAddingNew     = false;
        IsModuleAddingNew   = false;
        IsChannelAddingNew  = true;
        IsRackNameEditing   = true;
        IsRackEditing       = true;
    }

    private void StartEditRackName()
    {
        var node = SelectedRackNode;
        if (node == null) return;
        EditRackName       = node.Name;
        EditRackLocation   = node.Location;
        IsRackAddingNew    = false;
        IsModuleAddingNew  = false;
        IsChannelAddingNew = false;
        IsRackNameEditing  = true;
    }

    private async Task SaveRackNameAsync()
    {
        if (SelectedStation == null) return;
        if (string.IsNullOrWhiteSpace(EditRackName)) { StatusMessage = "이름을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            int sid = SelectedStation.StationId;
            if (IsChannelAddingNew)
            {
                int newId  = await DeviceService.NextChannelIdAsync(sid, _channelAddRackId, _channelAddModuleId);
                await DeviceService.CreateChannelAsync(sid, _channelAddRackId, _channelAddModuleId, newId, newId, EditRackName.Trim());
                StatusMessage      = $"CHANNEL {newId} '{EditRackName.Trim()}' 추가 완료";
                IsChannelAddingNew = false;
                IsRackNameEditing  = false;
                IsRackEditing      = false;
                await LoadTreesAsync();
            }
            else if (IsModuleAddingNew)
            {
                int newId = await DeviceService.NextModuleIdAsync(sid, _moduleAddRackId);
                await DeviceService.CreateModuleAsync(sid, _moduleAddRackId, newId, EditRackName.Trim());
                StatusMessage     = $"MODULE {newId} '{EditRackName.Trim()}' 추가 완료";
                IsModuleAddingNew = false;
                IsRackNameEditing = false;
                IsRackEditing     = false;
                await LoadTreesAsync();
            }
            else if (IsRackAddingNew)
            {
                int newId = await DeviceService.NextRackIdAsync(sid);
                await DeviceService.CreateRackAsync(sid, newId, EditRackName.Trim(), EditRackLocation.Trim());
                StatusMessage     = $"RACK {newId} '{EditRackName.Trim()}' 추가 완료";
                IsRackAddingNew   = false;
                IsRackNameEditing = false;
                IsRackEditing     = false;
                await LoadTreesAsync();
            }
            else
            {
                var node = SelectedRackNode;
                if (node == null) return;
                switch (node.Kind)
                {
                    case NodeKind.Rack:
                        await DeviceService.UpdateRackInfoAsync(node.StationId, node.RackId, EditRackName.Trim(), EditRackLocation.Trim());
                        node.Name     = EditRackName.Trim();
                        node.Location = EditRackLocation.Trim();
                        RackEditName  = node.Name;
                        RackEditInfo  = $"위치: {(string.IsNullOrEmpty(node.Location) ? "(없음)" : node.Location)}  |  IP: {node.LocalIp}:{node.LocalPort}";
                        StatusMessage = $"RACK {node.RackId} 정보 수정 완료";
                        break;
                    case NodeKind.Module:
                        await DeviceService.UpdateModuleNameAsync(node.StationId, node.RackId, node.ModuleId, EditRackName.Trim());
                        node.Name    = EditRackName.Trim();
                        RackEditName = node.Name;
                        StatusMessage = $"MODULE {node.ModuleId} 이름 수정 완료";
                        break;
                    case NodeKind.Channel:
                        await DeviceService.UpdateChannelAsync(node.StationId, node.RackId, node.ModuleId, node.ChannelId, EditRackName.Trim());
                        node.Name    = EditRackName.Trim();
                        RackEditName = node.Name;
                        StatusMessage = $"CHANNEL {node.ChannelId} 이름 수정 완료";
                        break;
                }
                IsRackNameEditing = false;
            }
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteRackNodeAsync()
    {
        var node = SelectedRackNode;
        if (node == null || SelectedStation == null) return;

        if (node.Kind == NodeKind.Channel)
        {
            // 원본 CMenu_Channel_Del: 확인 → TREND/EVENT/STATUS 잔여 데이터 가드 → P_DEL_GENERAL_CHANNEL
            var confirm = System.Windows.MessageBox.Show(
                $"{node.DisplayText} 채널를 삭제 하시겠습니까?",
                "채널 삭제",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;
            IsBusy = true;
            try
            {
                var (trend, evt, status) = await DeviceService.ChannelDataCountsAsync(node.ChannelIndex);
                if (trend > 0)
                {
                    System.Windows.MessageBox.Show("TREND 데이터를 먼저 삭제해야 채널 삭제가 가능합니다.", "채널 삭제",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (evt > 0)
                {
                    System.Windows.MessageBox.Show("이벤트 데이터를 먼저 삭제해야 채널 삭제가 가능합니다.", "채널 삭제",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (status > 0)
                {
                    System.Windows.MessageBox.Show("STATUS 데이터를 먼저 삭제해야 채널 삭제가 가능합니다.", "채널 삭제",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await DeviceService.DeleteChannelAsync(node.StationId, node.RackId, node.ModuleId, node.ChannelId);
                StatusMessage     = $"CHANNEL {node.ChannelId} '{node.Name}' 삭제 완료";
                IsRackEditing     = false;
                _selectedRackNode = null;
                await LoadTreesAsync();
            }
            catch (Exception ex) { StatusMessage = $"CHANNEL 삭제 실패: {ex.Message}"; }
            finally { IsBusy = false; }
        }
        else if (node.Kind == NodeKind.Module)
        {
            // 원본 CMenu_Module_Del: 확인 → 채널 존재 시 차단 → MODULE 행만 삭제
            var confirm = System.Windows.MessageBox.Show(
                $"{node.DisplayText} 모듈를 삭제 하시겠습니까?",
                "모듈 삭제",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;
            IsBusy = true;
            try
            {
                int channelCount = await DeviceService.ModuleChannelCountAsync(node.StationId, node.RackId, node.ModuleId);
                if (channelCount > 0)
                {
                    System.Windows.MessageBox.Show("채널을 먼저 삭제해야 모듈 삭제가 가능합니다.", "모듈 삭제",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await DeviceService.DeleteModuleAsync(node.StationId, node.RackId, node.ModuleId);
                StatusMessage     = $"MODULE {node.ModuleId} '{node.Name}' 삭제 완료";
                IsRackEditing     = false;
                _selectedRackNode = null;
                await LoadTreesAsync();
            }
            catch (Exception ex) { StatusMessage = $"MODULE 삭제 실패: {ex.Message}"; }
            finally { IsBusy = false; }
        }
        else if (node.Kind == NodeKind.Rack)
        {
            // 원본 Cms_Rack_Delete: 실수 방지를 위해 RACK 삭제는 제공하지 않음
            System.Windows.MessageBox.Show(
                "실수를 방지하기 위해 RACK 삭제기능은 제공하지 않습니다.\n삭제를 원하시면 공급사에 요청하시고, Activity 를 OFF 하세요.",
                "RACK 삭제",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void CancelRackEdit()
    {
        IsRackAddingNew    = false;
        IsModuleAddingNew  = false;
        IsChannelAddingNew = false;
        IsRackNameEditing  = false;
        if (_selectedRackNode == null) IsRackEditing = false;
    }

    private async Task ToggleRackActivityAsync()
    {
        var node = SelectedRackNode;
        if (node?.Kind != NodeKind.Rack) return;
        IsBusy = true;
        try
        {
            byte newAct  = node.IsActive ? (byte)0 : (byte)1;
            await DeviceService.SetRackActivityAsync(node.StationId, node.RackId, newAct);
            node.Activity    = newAct;
            RackEditIsActive = node.IsActive;
            StatusMessage    = $"RACK {node.RackId}: {(node.IsActive ? "활성화" : "비활성화")} 완료";
        }
        catch (Exception ex) { StatusMessage = $"변경 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ToggleModuleActivityAsync()
    {
        var node = SelectedRackNode;
        if (node?.Kind != NodeKind.Module) return;
        IsBusy = true;
        try
        {
            byte newAct  = node.IsActive ? (byte)0 : (byte)1;
            await DeviceService.SetModuleActivityAsync(node.StationId, node.RackId, node.ModuleId, newAct);
            node.Activity    = newAct;
            RackEditIsActive = node.IsActive;
            StatusMessage    = $"MODULE {node.ModuleId}: {(node.IsActive ? "활성화" : "비활성화")} 완료";
        }
        catch (Exception ex) { StatusMessage = $"변경 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ToggleChannelActivityAsync()
    {
        var node = SelectedRackNode;
        if (node?.Kind != NodeKind.Channel) return;
        IsBusy = true;
        try
        {
            byte newAct  = node.IsActive ? (byte)0 : (byte)1;
            await DeviceService.SetChannelActivityAsync(node.StationId, node.RackId, node.ModuleId, node.ChannelId, newAct);
            node.Activity    = newAct;
            RackEditIsActive = node.IsActive;
            StatusMessage    = $"CHANNEL {node.ChannelId}: {(node.IsActive ? "활성화" : "비활성화")} 완료";
        }
        catch (Exception ex) { StatusMessage = $"변경 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────
    //  TRAIN 편집
    // ────────────────────────────────────────────────────────

    private void PopulateTrainEdit()
    {
        var node = SelectedTrainNode;
        if (node == null) { IsTrainEditing = false; return; }
        TrainEditName     = node.Name;
        TrainEditIsActive = node.IsActive;
        TrainEditAssign   = node.Assign;
        TrainEditKind     = node.Kind;
        IsTrainAddingNew  = false;
        SyncSelectedChannel(node.Kind == NodeKind.Point ? node.Assign : 0);
        // 탭이 열려 있으면 유지(노드 선택만으로 탭이 사라지지 않도록). RACK 과 동일 처리.
        if (SelectedTypeTab == null) IsTrainEditing = true;
    }

    public void SelectTrainNode(DeviceTreeNode node) => SelectedTrainNode = node;

    private void StartAddTrain()
    {
        _selectedTrainNode = null;
        TrainEditName      = "";
        TrainEditIsActive  = true;
        TrainEditAssign    = 0;
        TrainEditKind      = NodeKind.Train;
        IsTrainAddingNew   = true;
        SyncSelectedChannel(0);
        IsTrainEditing     = true;
    }

    private void StartAddChild()
    {
        var parent = SelectedTrainNode;
        if (parent == null) return;
        TrainEditName     = "";
        TrainEditIsActive = true;
        TrainEditAssign   = 0;
        IsTrainAddingNew  = true;
        TrainEditKind     = parent.Kind switch
        {
            NodeKind.Train     => NodeKind.Component,
            NodeKind.Component => NodeKind.Point,
            _                  => NodeKind.Point
        };
        SyncSelectedChannel(0);
        IsTrainEditing = true;
    }

    private void StartEditTrainNode() { PopulateTrainEdit(); IsTrainAddingNew = false; }

    private async Task SaveTrainAsync()
    {
        if (string.IsNullOrWhiteSpace(TrainEditName)) { StatusMessage = "이름을 입력하세요."; return; }
        if (SelectedStation == null) return;
        IsBusy = true;
        try
        {
            int    sid  = SelectedStation.StationId;
            byte   act  = TrainEditIsActive ? (byte)1 : (byte)0;
            string name = TrainEditName.Trim();

            if (IsTrainAddingNew)
            {
                switch (TrainEditKind)
                {
                    case NodeKind.Train:
                        int tid = await DeviceService.NextTrainIdAsync(sid);
                        await DeviceService.CreateTrainAsync(sid, tid, name, act);
                        StatusMessage = $"TRAIN {tid} '{name}' 추가 완료";
                        break;
                    case NodeKind.Component:
                        var pTrain = SelectedTrainNode?.Kind == NodeKind.Train ? SelectedTrainNode
                            : FindAncestor(SelectedTrainNode, NodeKind.Train);
                        if (pTrain == null) { StatusMessage = "TRAIN을 먼저 선택하세요."; return; }
                        int cid = await DeviceService.NextComponentIdAsync(sid, pTrain.TrainId);
                        await DeviceService.CreateComponentAsync(sid, pTrain.TrainId, cid, name, act);
                        StatusMessage = $"부품 {cid} '{name}' 추가 완료";
                        break;
                    case NodeKind.Point:
                        var pComp = SelectedTrainNode?.Kind == NodeKind.Component ? SelectedTrainNode
                            : FindAncestor(SelectedTrainNode, NodeKind.Component);
                        if (pComp == null) { StatusMessage = "부품(Component)을 먼저 선택하세요."; return; }
                        int pid = await DeviceService.NextPointIdAsync(sid, pComp.TrainId, pComp.ComponentId);
                        await DeviceService.CreatePointAsync(sid, pComp.TrainId, pComp.ComponentId, pid, name, act, TrainEditAssign);
                        StatusMessage = $"POINT {pid} '{name}' 추가 완료";
                        break;
                }
            }
            else
            {
                var node = SelectedTrainNode;
                if (node == null) return;
                switch (node.Kind)
                {
                    case NodeKind.Train:
                        await DeviceService.UpdateTrainAsync(sid, node.TrainId, name, act); break;
                    case NodeKind.Component:
                        await DeviceService.UpdateComponentAsync(sid, node.TrainId, node.ComponentId, name, act); break;
                    case NodeKind.Point:
                        await DeviceService.UpdatePointAsync(sid, node.TrainId, node.ComponentId, node.PointId, name, act, TrainEditAssign); break;
                }
                StatusMessage = $"'{name}' 수정 완료";
            }
            IsTrainEditing = false;
            await LoadTreesAsync();
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteTrainNodeAsync()
    {
        var node = SelectedTrainNode;
        if (node == null || SelectedStation == null) return;
        // 원본 frmMain: 트레인/컴포넌트/포인터 삭제 확인 문구
        (string msg, string caption) = node.Kind switch
        {
            NodeKind.Train     => ($"{node.DisplayText} 트레인를 삭제 하시겠습니까?",   "트레인 삭제"),
            NodeKind.Component => ($"{node.DisplayText} 컴포넌트를 삭제 하시겠습니까?", "컴포넌트 삭제"),
            NodeKind.Point     => ($"{node.DisplayText} 포인터를 삭제 하시겠습니까?",   "포인터 삭제"),
            _                  => ($"{node.Name} 을(를) 삭제 하시겠습니까?",            "삭제")
        };
        var confirm = System.Windows.MessageBox.Show(
            msg, caption, System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            int sid = SelectedStation.StationId;
            switch (node.Kind)
            {
                case NodeKind.Train:     await DeviceService.DeleteTrainAsync(sid, node.TrainId); break;
                case NodeKind.Component: await DeviceService.DeleteComponentAsync(sid, node.TrainId, node.ComponentId); break;
                case NodeKind.Point:     await DeviceService.DeletePointAsync(sid, node.TrainId, node.ComponentId, node.PointId); break;
            }
            StatusMessage      = $"'{node.Name}' 삭제 완료";
            IsTrainEditing     = false;
            _selectedTrainNode = null;
            await LoadTreesAsync();
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void CancelTrainEdit() { IsTrainEditing = false; IsTrainAddingNew = false; StatusMessage = ""; }

    // ────────────────────────────────────────────────────────
    //  Station 컨텍스트메뉴 RACK / TRAIN Insert (탭)
    // ────────────────────────────────────────────────────────

    private async Task OpenRackInsertTabAsync()
    {
        if (SelectedStation == null)
        {
            System.Windows.MessageBox.Show("스테이션을 먼저 선택하세요.", "알림",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        IsBusy = true;
        try
        {
            int sid    = SelectedStation.StationId;
            int nextId = await DeviceService.NextRackIdAsync(sid);

            // 이미 열린 탭이 있으면 닫고 초기화
            var existing = TypeTabs.FirstOrDefault(t => t.Key == "RackInsert");
            if (existing != null) CloseTab(existing);

            var vm = new RackInsertViewModel(sid, nextId);
            RackInsertTab = vm;
            vm.CloseRequested += async () =>
            {
                bool modified = vm.Modified;
                CloseTab(TypeTabs.FirstOrDefault(t => t.Key == "RackInsert"));
                RackInsertTab = null;
                if (modified) await LoadTreesAsync();
            };

            var tab = new TypeTab { Key = "RackInsert", Title = "RACK Insert" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
            IsRackEditing  = false;
            IsTrainEditing = false;
            SelectedTypeTab = tab;
            StatusMessage   = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"RACK Insert 오류: {ex.Message}";
            System.Windows.MessageBox.Show($"RACK Insert 탭 오류:\n{ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task OpenTrainInsertTabAsync()
    {
        if (SelectedStation == null)
        {
            System.Windows.MessageBox.Show("스테이션을 먼저 선택하세요.", "알림",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        IsBusy = true;
        try
        {
            int sid    = SelectedStation.StationId;
            int nextId = await DeviceService.NextTrainIdAsync(sid);

            // 이미 열린 탭이 있으면 닫고 초기화
            var existing = TypeTabs.FirstOrDefault(t => t.Key == "TrainInsert");
            if (existing != null) CloseTab(existing);

            var vm = new TrainInsertViewModel(sid, nextId);
            TrainInsertTab = vm;
            vm.CloseRequested += async () =>
            {
                bool modified = vm.Modified;
                CloseTab(TypeTabs.FirstOrDefault(t => t.Key == "TrainInsert"));
                TrainInsertTab = null;
                if (modified) await LoadTreesAsync();
            };

            var tab = new TypeTab { Key = "TrainInsert", Title = "TRAIN Insert" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
            IsRackEditing  = false;
            IsTrainEditing = false;
            SelectedTypeTab = tab;
            StatusMessage   = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"TRAIN Insert 오류: {ex.Message}";
            System.Windows.MessageBox.Show($"TRAIN Insert 탭 오류:\n{ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────
    //  모듈 타입 관리
    // ────────────────────────────────────────────────────────

    private async Task OpenModuleTypeAsync()
    {
        var tab = TypeTabs.FirstOrDefault(t => t.Key == "ModuleType");
        if (tab == null)
        {
            tab = new TypeTab { Key = "ModuleType", Title = "모듈 타입" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
        IsMtEditing    = false;
        StatusMessage  = "";
        MtInnerTabIndex = 0;
        var rows = await DeviceService.GetModuleTypesAsync();
        ModuleTypeRows = new ObservableCollection<ModuleTypeItem>(rows);
    }

    private async Task SwitchToMctAsync()
    {
        MtInnerTabIndex = 1;
        IsMtEditing     = false;
        var mts = await DeviceService.GetModuleTypesAsync();
        McModuleTypes = new ObservableCollection<ModuleTypeItem>(mts);
        var cts = await DeviceService.GetChannelTypesAsync();
        AllChannelTypesForMc = new ObservableCollection<ChannelTypeItem>(cts);
        SelectedMcModule = McModuleTypes.FirstOrDefault();
    }

    private async Task McLoadChannelsAsync()
    {
        if (SelectedMcModule == null) return;
        try
        {
            McChannelRows = new ObservableCollection<McChannelItem>(
                await DeviceService.GetModuleChannelTypesAsync(SelectedMcModule.TypeId));
        }
        catch (Exception ex) { StatusMessage = $"채널 타입 로드 오류: {ex.Message}"; }
    }

    private async Task McAddChannelTypeAsync()
    {
        if (SelectedMcModule == null || McSelectedCtForAdd == null) return;
        try
        {
            await DeviceService.AddModuleChannelTypeAsync(SelectedMcModule.TypeId, McSelectedCtForAdd.TypeId);
            await McLoadChannelsAsync();
        }
        catch (Exception ex) { StatusMessage = $"추가 오류: {ex.Message}"; }
    }

    private async Task McDeleteChannelTypeAsync()
    {
        if (SelectedMcModule == null || McSelectedChannelRow == null) return;
        var ok = System.Windows.MessageBox.Show(
            $"'{McSelectedChannelRow.Name}' 관계를 해제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            await DeviceService.RemoveModuleChannelTypeAsync(SelectedMcModule.TypeId, McSelectedChannelRow.ChannelTypeId);
            await McLoadChannelsAsync();
        }
        catch (Exception ex) { StatusMessage = $"삭제 오류: {ex.Message}"; }
    }

    private async Task StartMtInsert()
    {
        IsMtAddingNew = true;
        IsMtEditing   = true;
        EditMtTypeId  = await DeviceService.NextModuleTypeIdAsync();
        EditMtNicName = "";
        EditMtName    = "";
        EditMtDesc    = "";
    }

    private void StartMtModify()
    {
        if (SelectedModuleTypeRow == null) return;
        IsMtAddingNew = false;
        IsMtEditing   = true;
        EditMtTypeId  = SelectedModuleTypeRow.TypeId;
        EditMtNicName = SelectedModuleTypeRow.NicName;
        EditMtName    = SelectedModuleTypeRow.Name;
        EditMtDesc    = SelectedModuleTypeRow.Description;
    }

    private async Task SaveMtAsync()
    {
        if (string.IsNullOrWhiteSpace(EditMtNicName) || string.IsNullOrWhiteSpace(EditMtName))
        {
            StatusMessage = "NicName과 Name을 입력하세요.";
            return;
        }
        IsBusy = true;
        try
        {
            if (IsMtAddingNew)
                await DeviceService.CreateModuleTypeAsync(EditMtTypeId, EditMtNicName.Trim(), EditMtName.Trim(), EditMtDesc.Trim());
            else
                await DeviceService.UpdateModuleTypeAsync(EditMtTypeId, EditMtNicName.Trim(), EditMtName.Trim(), EditMtDesc.Trim());
            bool wasAdding = IsMtAddingNew;
            IsMtEditing = false;
            var rows = await DeviceService.GetModuleTypesAsync();
            ModuleTypeRows = new ObservableCollection<ModuleTypeItem>(rows);
            StatusMessage = wasAdding ? "모듈 타입 추가 완료" : "모듈 타입 수정 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteMtAsync()
    {
        if (SelectedModuleTypeRow == null) return;
        var confirm = System.Windows.MessageBox.Show(
            $"모듈 타입 '{SelectedModuleTypeRow.Name}' ({SelectedModuleTypeRow.TypeId})를 삭제하시겠습니까?",
            "삭제 확인", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteModuleTypeAsync(SelectedModuleTypeRow.TypeId);
            var rows = await DeviceService.GetModuleTypesAsync();
            ModuleTypeRows = new ObservableCollection<ModuleTypeItem>(rows);
            StatusMessage = "모듈 타입 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 관리
    // ────────────────────────────────────────────────────────

    private async Task OpenChannelTypeAsync()
    {
        var tab = TypeTabs.FirstOrDefault(t => t.Key == "ChannelType");
        if (tab == null)
        {
            tab = new TypeTab { Key = "ChannelType", Title = "채널 타입" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
        IsCtEditing    = false;
        StatusMessage  = "";
        var rows = await DeviceService.GetChannelTypesAsync();
        ChannelTypeRows = new ObservableCollection<ChannelTypeItem>(rows);
        await OpenChannelTypeInnerAsync();
    }

    private async Task StartCtInsert()
    {
        IsCtAddingNew = true;
        IsCtEditing   = true;
        EditCtTypeId  = await DeviceService.NextChannelTypeIdAsync();
        EditCtNicName = "";
        EditCtName    = "";
        EditCtDesc    = "";
    }

    private void StartCtModify()
    {
        if (SelectedChannelTypeRow == null) return;
        IsCtAddingNew = false;
        IsCtEditing   = true;
        EditCtTypeId  = SelectedChannelTypeRow.TypeId;
        EditCtNicName = SelectedChannelTypeRow.NicName;
        EditCtName    = SelectedChannelTypeRow.Name;
        EditCtDesc    = SelectedChannelTypeRow.Description;
    }

    private async Task SaveCtAsync()
    {
        if (string.IsNullOrWhiteSpace(EditCtNicName) || string.IsNullOrWhiteSpace(EditCtName))
        {
            StatusMessage = "NicName과 Name을 입력하세요.";
            return;
        }
        IsBusy = true;
        try
        {
            if (IsCtAddingNew)
                await DeviceService.CreateChannelTypeAsync(EditCtTypeId, EditCtNicName.Trim(), EditCtName.Trim(), EditCtDesc.Trim());
            else
                await DeviceService.UpdateChannelTypeAsync(EditCtTypeId, EditCtNicName.Trim(), EditCtName.Trim(), EditCtDesc.Trim());
            bool wasAdding = IsCtAddingNew;
            IsCtEditing = false;
            var rows = await DeviceService.GetChannelTypesAsync();
            ChannelTypeRows = new ObservableCollection<ChannelTypeItem>(rows);
            StatusMessage = wasAdding ? "채널 타입 추가 완료" : "채널 타입 수정 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteCtAsync()
    {
        if (SelectedChannelTypeRow == null) return;
        var confirm = System.Windows.MessageBox.Show(
            $"채널 타입 '{SelectedChannelTypeRow.Name}' ({SelectedChannelTypeRow.TypeId})를 삭제하시겠습니까?",
            "삭제 확인", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteChannelTypeAsync(SelectedChannelTypeRow.TypeId);
            var rows = await DeviceService.GetChannelTypesAsync();
            ChannelTypeRows = new ObservableCollection<ChannelTypeItem>(rows);
            StatusMessage = "채널 타입 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void CloseTab(TypeTab? tab)
    {
        if (tab == null) return;
        int idx = TypeTabs.IndexOf(tab);
        TypeTabs.Remove(tab);
        OnPropertyChanged(nameof(HasTypeTabs));
        if (tab.Key == "ModuleType")  IsMtEditing = false;
        if (tab.Key == "ChannelType") IsCtEditing = false;
        if (tab.Key == "RackInsert")  RackInsertTab  = null;
        if (tab.Key == "TrainInsert") TrainInsertTab = null;
        if (tab.Key == "Assign")      AssignTab      = null;
        if (RackIdFromTabKey(tab.Key) is int closedRid)
        {
            _rackViewData.Remove(closedRid);
            if (_rackViewNode?.RackId == closedRid) { RackViewNode = null; RackViewSlots = []; }
        }
        if (RackIdFromModifyKey(tab.Key) is int closedMrid)
            _rackModifyData.Remove(closedMrid);
        if (RackIdFromHwKey(tab.Key) is int closedHrid && _hwConfigData.Remove(closedHrid, out var closedHwVm))
            closedHwVm.OnClosed();   // 소켓 정리
        if (RackIdFromKeyPrefix(tab.Key, "ModuleInsert_") is int closedMiRid) _moduleInsertData.Remove(closedMiRid);
        if (RackIdFromKeyPrefix(tab.Key, "RelayInsert_")  is int closedRiRid) _relayInsertData.Remove(closedRiRid);
        if (_selectedTypeTab == tab)
            SelectedTypeTab = TypeTabs.Count > 0 ? TypeTabs[Math.Max(0, idx - 1)] : null;
    }

    public async Task RefreshRackViewAsync()
    {
        if (_rackViewNode == null) return;
        int rackId = _rackViewNode.RackId;
        await LoadTreesAsync();
        var rack = RackNodes.FirstOrDefault(n => n.RackId == rackId);
        if (rack != null) OpenRackViewTab(rack);
    }

    /// <summary>RACK Modify 저장 후: 트리 갱신 → 해당 랙의 H/W Config 탭을 새 IP로 연다.</summary>
    private async Task RefreshAndOpenHwConfigAsync(int rackId)
    {
        await LoadTreesAsync();
        var rack = RackNodes.FirstOrDefault(n => n.RackId == rackId);
        if (rack == null) return;
        if (_hwConfigData.Remove(rackId, out var oldVm)) oldVm.OnClosed();   // 새 IP 반영 위해 재생성
        OpenHwConfigTab(rack);
    }

    /// <summary>RACK 컨텍스트 Modify → 3탭 Modify 폼을 탭으로 연다.</summary>
    private void OpenRackModifyTab(DeviceTreeNode rackNode)
    {
        var key = $"RackModify_{rackNode.RackId}";
        if (!_rackModifyData.TryGetValue(rackNode.RackId, out var vm))
        {
            vm = new RackModifyViewModel(rackNode);
            int rid = rackNode.RackId;
            vm.CloseRequested += () =>
            {
                bool modified = vm.Modified;
                CloseTab(TypeTabs.FirstOrDefault(t => t.Key == key));
                // 저장 시: 트리 갱신 후 해당 랙의 H/W Config 탭을 새 IP로 연다(연동 테스트 연계).
                if (modified) _ = RefreshAndOpenHwConfigAsync(rid);
            };
            _rackModifyData[rackNode.RackId] = vm;
        }
        RackModifyVM = vm;

        var tab = TypeTabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
        {
            tab = new TypeTab { Key = key, Title = $"Rack {rackNode.RackId:D2} Modify" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
    }

    /// <summary>REFERENCE/IO Insert → Module Config(insert) 폼을 탭으로 연다.</summary>
    private void OpenModuleInsertTab(DeviceTreeNode rackNode)
    {
        var key = $"ModuleInsert_{rackNode.RackId}";
        if (!_moduleInsertData.TryGetValue(rackNode.RackId, out var vm))
        {
            vm = new ModuleInsertViewModel(rackNode);
            vm.CloseRequested += () =>
            {
                bool modified = vm.Modified;
                CloseTab(TypeTabs.FirstOrDefault(t => t.Key == key));
                if (modified) _ = LoadTreesAsync();
            };
            _moduleInsertData[rackNode.RackId] = vm;
        }
        ModuleInsertVM = vm;
        OpenFormTab(key, $"REF/IO Insert R{rackNode.RackId:D2}");
    }

    /// <summary>RELAY Insert → 릴레이 모듈(insert) 폼을 탭으로 연다.</summary>
    private void OpenRelayInsertTab(DeviceTreeNode rackNode)
    {
        var key = $"RelayInsert_{rackNode.RackId}";
        if (!_relayInsertData.TryGetValue(rackNode.RackId, out var vm))
        {
            vm = new RelayModuleInsertViewModel(rackNode);
            vm.CloseRequested += () =>
            {
                bool modified = vm.Modified;
                CloseTab(TypeTabs.FirstOrDefault(t => t.Key == key));
                if (modified) _ = LoadTreesAsync();
            };
            _relayInsertData[rackNode.RackId] = vm;
        }
        RelayInsertVM = vm;
        OpenFormTab(key, $"RELAY Insert R{rackNode.RackId:D2}");
    }

    private void OpenFormTab(string key, string title)
    {
        var tab = TypeTabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
        {
            tab = new TypeTab { Key = key, Title = title };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
    }

    /// <summary>RACK 컨텍스트 Hw Config → H/W Config 폼을 탭으로 연다.</summary>
    private void OpenHwConfigTab(DeviceTreeNode rackNode)
    {
        var key = $"HwConfig_{rackNode.RackId}";
        if (!_hwConfigData.TryGetValue(rackNode.RackId, out var vm))
        {
            vm = new HwConfigViewModel(rackNode);
            _hwConfigData[rackNode.RackId] = vm;
        }
        HwConfigVM = vm;

        var tab = TypeTabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
        {
            tab = new TypeTab { Key = key, Title = $"H/W Config R{rackNode.RackId:D2}" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
    }

    private void OpenRackViewTab(DeviceTreeNode rackNode)
    {
        var key = $"RackView_{rackNode.RackId}";

        // 슬롯 빌드
        var modules = rackNode.Children
            .Where(c => c.Kind == NodeKind.Module)
            .ToDictionary(c => c.ModuleId, c => c);

        var slots = new List<RackSlotItem>();
        for (int i = 1; i <= 14; i++)
        {
            if (modules.TryGetValue(i, out var mod))
            {
                int chCnt = mod.Children.Count(c => c.Kind == NodeKind.Channel);
                slots.Add(new RackSlotItem
                {
                    SlotNumber   = i,
                    IsOccupied   = true,
                    ModuleName   = mod.Name,
                    ModuleType   = mod.ModuleType,
                    IsActive     = mod.IsActive,
                    ChannelCount = chCnt,
                    ActiveColor  = mod.IsActive ? "#16A34A" : "#9CA3AF",
                    SlotLabel    = i == 1 ? "Interface" : $"M{i:D2}",
                    ModuleNode   = mod,
                    TooltipText  = i == 1 ? "Reference Configuration (더블클릭: 수정)" : "Module Configuration (더블클릭: 수정)"
                });
            }
            else
            {
                slots.Add(new RackSlotItem
                {
                    SlotNumber  = i,
                    SlotLabel   = $"M{i:D2}",
                    TooltipText = "빈 슬롯"
                });
            }
        }

        // 딕셔너리에 저장 (SelectedTypeTab setter가 여기서 읽음)
        var slotsCol = new ObservableCollection<RackSlotItem>(slots);
        _rackViewData[rackNode.RackId] = (rackNode, slotsCol);

        // 기존 탭이 없으면 추가, 있으면 재사용
        var tab = TypeTabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
        {
            tab = new TypeTab { Key = key, Title = $"Rack {rackNode.RackId:D2}" };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }

        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
        StatusMessage   = "";
    }

    private TypeTab OpenTab(string key, string title)
    {
        var tab = TypeTabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
        {
            tab = new TypeTab { Key = key, Title = title };
            TypeTabs.Add(tab);
            OnPropertyChanged(nameof(HasTypeTabs));
        }
        IsRackEditing  = false;
        IsTrainEditing = false;
        SelectedTypeTab = tab;
        StatusMessage = "";
        return tab;
    }

    private async Task OpenTabAsync(string key, string title, Func<Task> load)
    {
        OpenTab(key, title);
        try { await load(); }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    // ─── 센서 CRUD ───────────────────────────────────────
    private async Task OpenSensorAsync()
    {
        OpenTab("Sensor", "센서");
        try
        {
            SensorRows        = new ObservableCollection<SensorItem>(await DeviceService.GetSensorsAsync());
            SensorUnitOptions = new ObservableCollection<SensorUnitItem>(await DeviceService.GetSensorUnitsAsync());
        }
        catch (Exception ex) { StatusMessage = $"센서 로드 오류: {ex.Message}"; }
    }

    private async Task StartSensorInsertAsync()
    {
        IsSensorAddingNew  = true;
        EditSensorId       = await DeviceService.NextSensorIdAsync();
        EditSensorName     = "";
        EditSensorType     = 0;
        EditSensorSensitivity = "";
        EditSensorUnitId   = SensorUnitOptions.FirstOrDefault()?.UnitId ?? 0;
        EditSensorIcp      = 0;
        EditSensorPower    = 0;
        EditSensorPowerLow = "";
        EditSensorPowerHigh= "";
        EditSensorBrandName= "";
        EditSensorSpec     = "";
        IsSensorEditing    = true;
    }

    private void StartSensorModify()
    {
        if (SelectedSensorRow == null) return;
        IsSensorAddingNew   = false;
        EditSensorId        = SelectedSensorRow.SensorId;
        EditSensorName      = SelectedSensorRow.Name;
        EditSensorType      = SelectedSensorRow.Type;
        EditSensorSensitivity = SelectedSensorRow.Sensitivity.ToString("F3");
        EditSensorUnitId    = SensorUnitOptions.FirstOrDefault(u => u.Name == SelectedSensorRow.UnitName)?.UnitId
                              ?? SensorUnitOptions.FirstOrDefault()?.UnitId ?? 0;
        EditSensorIcp       = SelectedSensorRow.Icp;
        EditSensorPower     = SelectedSensorRow.Power;
        EditSensorPowerLow  = SelectedSensorRow.PowerCheckLow.ToString();
        EditSensorPowerHigh = SelectedSensorRow.PowerCheckHigh.ToString();
        EditSensorBrandName = SelectedSensorRow.BrandName;
        EditSensorSpec      = SelectedSensorRow.Spec;
        IsSensorEditing     = true;
    }

    private async Task SaveSensorAsync()
    {
        if (string.IsNullOrWhiteSpace(EditSensorName)) return;
        double.TryParse(EditSensorSensitivity, out var sens);
        double.TryParse(EditSensorPowerLow,    out var pLow);
        double.TryParse(EditSensorPowerHigh,   out var pHigh);
        try
        {
            if (IsSensorAddingNew)
                await DeviceService.CreateSensorAsync(EditSensorId, EditSensorName.Trim(), EditSensorType,
                    sens, EditSensorUnitId, EditSensorIcp, EditSensorPower, pLow, pHigh,
                    EditSensorBrandName.Trim(), EditSensorSpec.Trim());
            else
                await DeviceService.UpdateSensorAsync(EditSensorId, EditSensorName.Trim(), EditSensorType,
                    sens, EditSensorUnitId, EditSensorIcp, EditSensorPower, pLow, pHigh,
                    EditSensorBrandName.Trim(), EditSensorSpec.Trim());
            IsSensorEditing = false;
            SensorRows = new ObservableCollection<SensorItem>(await DeviceService.GetSensorsAsync());
        }
        catch (Exception ex) { StatusMessage = $"센서 저장 오류: {ex.Message}"; }
    }

    private async Task DeleteSensorItemAsync()
    {
        if (SelectedSensorRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedSensorRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            await DeviceService.DeleteSensorAsync(SelectedSensorRow.SensorId);
            SensorRows = new ObservableCollection<SensorItem>(await DeviceService.GetSensorsAsync());
        }
        catch (Exception ex) { StatusMessage = $"센서 삭제 오류: {ex.Message}"; }
    }

    // ─── 센서 단위 CRUD ───────────────────────────────────
    private async Task OpenSensorUnitAsync()
    {
        OpenTab("SensorUnit", "센서 단위");
        IsSuEditing = false;
        SuInnerTabIndex = 0;
        try { SuRows = new ObservableCollection<SensorUnitItem>(await DeviceService.GetSensorUnitsAsync()); }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task StartSuInsert()
    {
        IsSuAddingNew = true;
        IsSuEditing   = true;
        EditSuId      = await DeviceService.NextSensorUnitIdAsync();
        EditSuName    = "";
        EditSuDesc    = "";
    }

    private void StartSuModify()
    {
        if (SelectedSuRow == null) return;
        IsSuAddingNew = false;
        IsSuEditing   = true;
        EditSuId      = SelectedSuRow.UnitId;
        EditSuName    = SelectedSuRow.Name;
        EditSuDesc    = SelectedSuRow.Description;
    }

    private async Task SaveSuAsync()
    {
        if (string.IsNullOrWhiteSpace(EditSuName)) { StatusMessage = "Name을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsSuAddingNew) await DeviceService.CreateSensorUnitAsync(EditSuId, EditSuName.Trim(), EditSuDesc.Trim());
            else               await DeviceService.UpdateSensorUnitAsync(EditSuId, EditSuName.Trim(), EditSuDesc.Trim());
            IsSuEditing = false;
            SuRows = new ObservableCollection<SensorUnitItem>(await DeviceService.GetSensorUnitsAsync());
            StatusMessage = IsSuAddingNew ? "센서 단위 추가 완료" : "센서 단위 수정 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteSuAsync()
    {
        if (SelectedSuRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedSuRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteSensorUnitAsync(SelectedSuRow.UnitId);
            SuRows = new ObservableCollection<SensorUnitItem>(await DeviceService.GetSensorUnitsAsync());
            StatusMessage = "센서 단위 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ─── Display Plot CRUD ────────────────────────────────
    private async Task OpenDisplayPlotAsync()
    {
        OpenTab("DisplayPlot", "Display Plot");
        IsDpEditing = false;
        try
        {
            DpRows = new ObservableCollection<DisplayPlotItem>(await DeviceService.GetDisplayPlotsAsync());
            await OpenDisplayPlotInnerAsync();
        }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task StartDpInsert()
    {
        IsDpAddingNew  = true;
        IsDpEditing    = true;
        EditDpId       = await DeviceService.NextDisplayPlotIdAsync();
        EditDpName     = "";
        EditDpDesc     = "";
        EditDpDynamic  = 0;
    }

    private void StartDpModify()
    {
        if (SelectedDpRow == null) return;
        IsDpAddingNew  = false;
        IsDpEditing    = true;
        EditDpId       = SelectedDpRow.PlotId;
        EditDpName     = SelectedDpRow.Name;
        EditDpDesc     = SelectedDpRow.Description;
        EditDpDynamic  = SelectedDpRow.Dynamic;
    }

    private async Task SaveDpAsync()
    {
        if (string.IsNullOrWhiteSpace(EditDpName)) { StatusMessage = "Name을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsDpAddingNew) await DeviceService.CreateDisplayPlotAsync(EditDpId, EditDpName.Trim(), EditDpDesc.Trim(), EditDpDynamic);
            else               await DeviceService.UpdateDisplayPlotAsync(EditDpId, EditDpName.Trim(), EditDpDesc.Trim(), EditDpDynamic);
            IsDpEditing = false;
            DpRows = new ObservableCollection<DisplayPlotItem>(await DeviceService.GetDisplayPlotsAsync());
            StatusMessage = "Display Plot 저장 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteDpAsync()
    {
        if (SelectedDpRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedDpRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteDisplayPlotAsync(SelectedDpRow.PlotId);
            DpRows = new ObservableCollection<DisplayPlotItem>(await DeviceService.GetDisplayPlotsAsync());
            StatusMessage = "Display Plot 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ─── 비례값 CRUD ──────────────────────────────────────
    private async Task OpenProportionalAsync()
    {
        OpenTab("Proportional", "비례값");
        IsPropEditing = false;
        try
        {
            PropRows = new ObservableCollection<ProportionalItem>(await DeviceService.GetProportionalsAsync());
            await OpenProportionalInnerAsync();
        }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task StartPropInsert()
    {
        IsPropAddingNew  = true;
        IsPropEditing    = true;
        EditPropId       = await DeviceService.NextProportionalIdAsync();
        EditPropNicName  = "";
        EditPropName     = "";
        EditPropDesc     = "";
    }

    private void StartPropModify()
    {
        if (SelectedPropRow == null) return;
        IsPropAddingNew  = false;
        IsPropEditing    = true;
        EditPropId       = SelectedPropRow.VarId;
        EditPropNicName  = SelectedPropRow.NicName;
        EditPropName     = SelectedPropRow.Name;
        EditPropDesc     = SelectedPropRow.Description;
    }

    private async Task SavePropAsync()
    {
        if (string.IsNullOrWhiteSpace(EditPropNicName) || string.IsNullOrWhiteSpace(EditPropName))
        { StatusMessage = "NicName과 Name을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsPropAddingNew) await DeviceService.CreateProportionalAsync(EditPropId, EditPropNicName.Trim(), EditPropName.Trim(), EditPropDesc.Trim());
            else                 await DeviceService.UpdateProportionalAsync(EditPropId, EditPropNicName.Trim(), EditPropName.Trim(), EditPropDesc.Trim());
            IsPropEditing = false;
            PropRows = new ObservableCollection<ProportionalItem>(await DeviceService.GetProportionalsAsync());
            StatusMessage = "비례값 저장 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeletePropAsync()
    {
        if (SelectedPropRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedPropRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteProportionalAsync(SelectedPropRow.VarId);
            PropRows = new ObservableCollection<ProportionalItem>(await DeviceService.GetProportionalsAsync());
            StatusMessage = "비례값 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ─── 스케일 범위 CRUD ─────────────────────────────────
    private async Task OpenScaleRangeAsync()
    {
        OpenTab("ScaleRange", "스케일 범위");
        IsSrEditing = false;
        try { SrRows = new ObservableCollection<ScaleRangeItem>(await DeviceService.GetScaleRangesAsync()); }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task StartSrInsert()
    {
        IsSrAddingNew = true;
        IsSrEditing   = true;
        EditSrId      = await DeviceService.NextScaleRangeIdAsync();
        EditSrName    = "";
        EditSrMin     = 0;
        EditSrMax     = 0;
        EditSrDesc    = "";
    }

    private void StartSrModify()
    {
        if (SelectedSrRow == null) return;
        IsSrAddingNew = false;
        IsSrEditing   = true;
        EditSrId      = SelectedSrRow.ScaleId;
        EditSrName    = SelectedSrRow.Name;
        EditSrMin     = SelectedSrRow.Min;
        EditSrMax     = SelectedSrRow.Max;
        EditSrDesc    = SelectedSrRow.Description;
    }

    private async Task SaveSrAsync()
    {
        if (string.IsNullOrWhiteSpace(EditSrName)) { StatusMessage = "Name을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsSrAddingNew) await DeviceService.CreateScaleRangeAsync(EditSrId, EditSrName.Trim(), EditSrMin, EditSrMax, EditSrDesc.Trim());
            else               await DeviceService.UpdateScaleRangeAsync(EditSrId, EditSrName.Trim(), EditSrMin, EditSrMax, EditSrDesc.Trim());
            IsSrEditing = false;
            SrRows = new ObservableCollection<ScaleRangeItem>(await DeviceService.GetScaleRangesAsync());
            StatusMessage = "스케일 범위 저장 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteSrAsync()
    {
        if (SelectedSrRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedSrRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteScaleRangeAsync(SelectedSrRow.ScaleId);
            SrRows = new ObservableCollection<ScaleRangeItem>(await DeviceService.GetScaleRangesAsync());
            StatusMessage = "스케일 범위 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ─── 이벤트/상태 CRUD ─────────────────────────────────
    private async Task OpenEventStatusAsync()
    {
        OpenTab("EventStatus", "이벤트/상태");
        IsEvEditing = false;
        try { EvRows = new ObservableCollection<EventItem>(await DeviceService.GetEventsAsync()); }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task StartEvInsert()
    {
        IsEvAddingNew = true;
        IsEvEditing   = true;
        EditEvId      = await DeviceService.NextEventIdAsync();
        EditEvName    = "";
        EditEvClass   = 1;
        EditEvDesc    = "";
    }

    private void StartEvModify()
    {
        if (SelectedEvRow == null) return;
        IsEvAddingNew = false;
        IsEvEditing   = true;
        EditEvId      = SelectedEvRow.EventId;
        EditEvName    = SelectedEvRow.Name;
        EditEvClass   = SelectedEvRow.EventClass;
        EditEvDesc    = SelectedEvRow.Description;
    }

    private async Task SaveEvAsync()
    {
        if (string.IsNullOrWhiteSpace(EditEvName)) { StatusMessage = "Name을 입력하세요."; return; }
        IsBusy = true;
        try
        {
            if (IsEvAddingNew) await DeviceService.CreateEventAsync(EditEvId, EditEvName.Trim(), EditEvClass, EditEvDesc.Trim());
            else               await DeviceService.UpdateEventAsync(EditEvId, EditEvName.Trim(), EditEvClass, EditEvDesc.Trim());
            IsEvEditing = false;
            EvRows = new ObservableCollection<EventItem>(await DeviceService.GetEventsAsync());
            StatusMessage = "이벤트 저장 완료";
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteEvAsync()
    {
        if (SelectedEvRow == null) return;
        var ok = System.Windows.MessageBox.Show($"'{SelectedEvRow.Name}' 삭제하시겠습니까?", "삭제 확인",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await DeviceService.DeleteEventAsync(SelectedEvRow.EventId);
            EvRows = new ObservableCollection<EventItem>(await DeviceService.GetEventsAsync());
            StatusMessage = "이벤트 삭제 완료";
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────
    //  내부 헬퍼
    // ────────────────────────────────────────────────────────

    private void SyncSelectedChannel(int channelId)
    {
        _selectedChannel = AvailableChannels.FirstOrDefault(c => c.ChannelId == channelId)
                        ?? (AvailableChannels.Count > 0 ? AvailableChannels[0] : null);
        OnPropertyChanged(nameof(SelectedChannel));
    }

    private DeviceTreeNode? FindAncestor(DeviceTreeNode? node, NodeKind kind)
    {
        if (node == null) return null;
        foreach (var train in TrainNodes)
        {
            if (kind == NodeKind.Train && train.Children.Any(c => c == node || c.Children.Any(p => p == node)))
                return train;
            foreach (var comp in train.Children)
                if (kind == NodeKind.Component && comp.Children.Any(p => p == node))
                    return comp;
        }
        return null;
    }

    // ════════════════════════════════════════════════════════
    //  채널 타입 내부탭 (6-tab)
    // ════════════════════════════════════════════════════════
    private int _ctInnerTabIndex = 0;
    public int CtInnerTabIndex
    {
        get => _ctInnerTabIndex;
        set { SetProperty(ref _ctInnerTabIndex, value); OnPropertyChanged(nameof(IsCtTab0)); OnPropertyChanged(nameof(IsCtTab1)); OnPropertyChanged(nameof(IsCtTab2)); OnPropertyChanged(nameof(IsCtTab3)); OnPropertyChanged(nameof(IsCtTab4)); OnPropertyChanged(nameof(IsCtTab5)); }
    }
    public bool IsCtTab0 => _ctInnerTabIndex == 0;
    public bool IsCtTab1 => _ctInnerTabIndex == 1;
    public bool IsCtTab2 => _ctInnerTabIndex == 2;
    public bool IsCtTab3 => _ctInnerTabIndex == 3;
    public bool IsCtTab4 => _ctInnerTabIndex == 4;
    public bool IsCtTab5 => _ctInnerTabIndex == 5;

    public ObservableCollection<ChannelTypeItem> AllCtForJunction { get; } = new();
    private ChannelTypeItem? _ctJunction_SelectedCt;
    public ChannelTypeItem? CtJunction_SelectedCt
    {
        get => _ctJunction_SelectedCt;
        set { SetProperty(ref _ctJunction_SelectedCt, value); _ = CtJunctionReloadAsync(); }
    }

    // Tab 1: CT & Sensor
    public ObservableCollection<SimpleItem> CtSen_Rows     { get; } = new();
    public ObservableCollection<SimpleItem> AllSensorsForCt{ get; } = new();
    private SimpleItem? _ctSen_SelectedRow; public SimpleItem? CtSen_SelectedRow { get => _ctSen_SelectedRow; set => SetProperty(ref _ctSen_SelectedRow, value); }
    private SimpleItem? _ctSen_SelectedAdd; public SimpleItem? CtSen_SelectedAdd { get => _ctSen_SelectedAdd; set => SetProperty(ref _ctSen_SelectedAdd, value); }

    // Tab 2: CT & SensorUnit
    public ObservableCollection<SimpleItem> CtSu_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllSuForCt   { get; } = new();
    private SimpleItem? _ctSu_SelectedRow; public SimpleItem? CtSu_SelectedRow { get => _ctSu_SelectedRow; set => SetProperty(ref _ctSu_SelectedRow, value); }
    private SimpleItem? _ctSu_SelectedAdd; public SimpleItem? CtSu_SelectedAdd { get => _ctSu_SelectedAdd; set => SetProperty(ref _ctSu_SelectedAdd, value); }

    // Tab 3: CT & Display Plot
    public ObservableCollection<SimpleItem> CtPl_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllPlotsForCt{ get; } = new();
    private SimpleItem? _ctPl_SelectedRow; public SimpleItem? CtPl_SelectedRow { get => _ctPl_SelectedRow; set => SetProperty(ref _ctPl_SelectedRow, value); }
    private SimpleItem? _ctPl_SelectedAdd; public SimpleItem? CtPl_SelectedAdd { get => _ctPl_SelectedAdd; set => SetProperty(ref _ctPl_SelectedAdd, value); }

    // Tab 4: CT & Proportional
    public ObservableCollection<SimpleItem> CtPr_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllPropsForCt{ get; } = new();
    private SimpleItem? _ctPr_SelectedRow; public SimpleItem? CtPr_SelectedRow { get => _ctPr_SelectedRow; set => SetProperty(ref _ctPr_SelectedRow, value); }
    private SimpleItem? _ctPr_SelectedAdd; public SimpleItem? CtPr_SelectedAdd { get => _ctPr_SelectedAdd; set => SetProperty(ref _ctPr_SelectedAdd, value); }

    // Tab 5: CT & Scale Range
    public ObservableCollection<SimpleItem> CtSc_Rows     { get; } = new();
    public ObservableCollection<SimpleItem> AllScalesForCt { get; } = new();
    private SimpleItem? _ctSc_SelectedRow; public SimpleItem? CtSc_SelectedRow { get => _ctSc_SelectedRow; set => SetProperty(ref _ctSc_SelectedRow, value); }
    private SimpleItem? _ctSc_SelectedAdd; public SimpleItem? CtSc_SelectedAdd { get => _ctSc_SelectedAdd; set => SetProperty(ref _ctSc_SelectedAdd, value); }

    private async Task OpenChannelTypeInnerAsync()
    {
        try
        {
            var cts = await DeviceService.GetChannelTypesAsync();
            AllCtForJunction.Clear(); foreach (var x in cts) AllCtForJunction.Add(x);
            var sensors = await DeviceService.GetSensorsAsync();
            AllSensorsForCt.Clear(); foreach (var s in sensors) AllSensorsForCt.Add(new SimpleItem { Id = s.SensorId, Name = s.Name });
            var sus = await DeviceService.GetSensorUnitsAsync();
            AllSuForCt.Clear(); foreach (var u in sus) AllSuForCt.Add(new SimpleItem { Id = u.UnitId, Name = u.Name });
            var plots = await DeviceService.GetDisplayPlotsAsync();
            AllPlotsForCt.Clear(); foreach (var p in plots) AllPlotsForCt.Add(new SimpleItem { Id = p.PlotId, Name = p.Name });
            var props = await DeviceService.GetProportionalsAsync();
            AllPropsForCt.Clear(); foreach (var p in props) AllPropsForCt.Add(new SimpleItem { Id = p.VarId, Name = p.NicName });
            var scales = await DeviceService.GetScaleRangesAsync();
            AllScalesForCt.Clear(); foreach (var s in scales) AllScalesForCt.Add(new SimpleItem { Id = s.ScaleId, Name = s.Name });
            CtInnerTabIndex = 0;
        }
        catch (Exception ex) { StatusMessage = $"채널타입 로드 실패: {ex.Message}"; }
    }

    private async Task CtJunctionReloadAsync()
    {
        if (_ctJunction_SelectedCt == null) return;
        int ct = _ctJunction_SelectedCt.TypeId;
        try
        {
            var sen = await DeviceService.GetCtSensorRowsAsync(ct);
            CtSen_Rows.Clear(); foreach (var x in sen) CtSen_Rows.Add(x);
            var su = await DeviceService.GetCtSensorUnitRowsAsync(ct);
            CtSu_Rows.Clear(); foreach (var x in su) CtSu_Rows.Add(x);
            var pl = await DeviceService.GetCtPlotRowsAsync(ct);
            CtPl_Rows.Clear(); foreach (var x in pl) CtPl_Rows.Add(x);
            var pr = await DeviceService.GetCtPropRowsAsync(ct);
            CtPr_Rows.Clear(); foreach (var x in pr) CtPr_Rows.Add(x);
            var sc = await DeviceService.GetCtScaleRowsAsync(ct);
            CtSc_Rows.Clear(); foreach (var x in sc) CtSc_Rows.Add(x);
        }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task CtSen_AddAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSen_SelectedAdd == null) return;
        try { await DeviceService.AddCtSensorAsync(_ctJunction_SelectedCt.TypeId, _ctSen_SelectedAdd.Id); var rows = await DeviceService.GetCtSensorRowsAsync(_ctJunction_SelectedCt.TypeId); CtSen_Rows.Clear(); foreach (var x in rows) CtSen_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task CtSen_DeleteAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSen_SelectedRow == null) return;
        try { await DeviceService.RemoveCtSensorAsync(_ctJunction_SelectedCt.TypeId, _ctSen_SelectedRow.Id); var rows = await DeviceService.GetCtSensorRowsAsync(_ctJunction_SelectedCt.TypeId); CtSen_Rows.Clear(); foreach (var x in rows) CtSen_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task CtSu_AddAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSu_SelectedAdd == null) return;
        try { await DeviceService.AddCtSensorUnitAsync(_ctJunction_SelectedCt.TypeId, _ctSu_SelectedAdd.Id); var rows = await DeviceService.GetCtSensorUnitRowsAsync(_ctJunction_SelectedCt.TypeId); CtSu_Rows.Clear(); foreach (var x in rows) CtSu_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task CtSu_DeleteAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSu_SelectedRow == null) return;
        try { await DeviceService.RemoveCtSensorUnitAsync(_ctJunction_SelectedCt.TypeId, _ctSu_SelectedRow.Id); var rows = await DeviceService.GetCtSensorUnitRowsAsync(_ctJunction_SelectedCt.TypeId); CtSu_Rows.Clear(); foreach (var x in rows) CtSu_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task CtPl_AddAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctPl_SelectedAdd == null) return;
        try { await DeviceService.AddCtPlotAsync(_ctJunction_SelectedCt.TypeId, _ctPl_SelectedAdd.Id); var rows = await DeviceService.GetCtPlotRowsAsync(_ctJunction_SelectedCt.TypeId); CtPl_Rows.Clear(); foreach (var x in rows) CtPl_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task CtPl_DeleteAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctPl_SelectedRow == null) return;
        try { await DeviceService.RemoveCtPlotAsync(_ctJunction_SelectedCt.TypeId, _ctPl_SelectedRow.Id); var rows = await DeviceService.GetCtPlotRowsAsync(_ctJunction_SelectedCt.TypeId); CtPl_Rows.Clear(); foreach (var x in rows) CtPl_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task CtPr_AddAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctPr_SelectedAdd == null) return;
        try { await DeviceService.AddCtPropAsync(_ctJunction_SelectedCt.TypeId, _ctPr_SelectedAdd.Id); var rows = await DeviceService.GetCtPropRowsAsync(_ctJunction_SelectedCt.TypeId); CtPr_Rows.Clear(); foreach (var x in rows) CtPr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task CtPr_DeleteAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctPr_SelectedRow == null) return;
        try { await DeviceService.RemoveCtPropAsync(_ctJunction_SelectedCt.TypeId, _ctPr_SelectedRow.Id); var rows = await DeviceService.GetCtPropRowsAsync(_ctJunction_SelectedCt.TypeId); CtPr_Rows.Clear(); foreach (var x in rows) CtPr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task CtSc_AddAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSc_SelectedAdd == null) return;
        try { await DeviceService.AddCtScaleAsync(_ctJunction_SelectedCt.TypeId, _ctSc_SelectedAdd.Id); var rows = await DeviceService.GetCtScaleRowsAsync(_ctJunction_SelectedCt.TypeId); CtSc_Rows.Clear(); foreach (var x in rows) CtSc_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task CtSc_DeleteAsync()
    {
        if (_ctJunction_SelectedCt == null || _ctSc_SelectedRow == null) return;
        try { await DeviceService.RemoveCtScaleAsync(_ctJunction_SelectedCt.TypeId, _ctSc_SelectedRow.Id); var rows = await DeviceService.GetCtScaleRowsAsync(_ctJunction_SelectedCt.TypeId); CtSc_Rows.Clear(); foreach (var x in rows) CtSc_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }

    // ════════════════════════════════════════════════════════
    //  센서 단위 내부탭 (2-tab)
    // ════════════════════════════════════════════════════════
    private int _suInnerTabIndex = 0;
    public int SuInnerTabIndex
    {
        get => _suInnerTabIndex;
        set { SetProperty(ref _suInnerTabIndex, value); OnPropertyChanged(nameof(IsSuTab0)); OnPropertyChanged(nameof(IsSuTab1)); }
    }
    public bool IsSuTab0 => _suInnerTabIndex == 0;
    public bool IsSuTab1 => _suInnerTabIndex == 1;

    private SensorUnitItem? _suConv_SelectedUnit;
    public SensorUnitItem? SuConv_SelectedUnit
    {
        get => _suConv_SelectedUnit;
        set { SetProperty(ref _suConv_SelectedUnit, value); _ = LoadSuConvAsync(); }
    }
    public ObservableCollection<SuConvRow> SuConvRows { get; } = new();

    private async Task LoadSuConvAsync()
    {
        SuConvRows.Clear();
        if (_suConv_SelectedUnit == null) return;
        try
        {
            var rows = await DeviceService.GetSuConverterAsync(_suConv_SelectedUnit.UnitId);
            foreach (var r in rows) SuConvRows.Add(r);
        }
        catch (Exception ex) { StatusMessage = $"변환기 로드 실패: {ex.Message}"; }
    }

    // ════════════════════════════════════════════════════════
    //  Display Plot 내부탭 (5-tab)
    // ════════════════════════════════════════════════════════
    private int _dpInnerTabIndex = 0;
    public int DpInnerTabIndex
    {
        get => _dpInnerTabIndex;
        set { SetProperty(ref _dpInnerTabIndex, value); OnPropertyChanged(nameof(IsDpTab0)); OnPropertyChanged(nameof(IsDpTab1)); OnPropertyChanged(nameof(IsDpTab2)); OnPropertyChanged(nameof(IsDpTab3)); OnPropertyChanged(nameof(IsDpTab4)); }
    }
    public bool IsDpTab0 => _dpInnerTabIndex == 0;
    public bool IsDpTab1 => _dpInnerTabIndex == 1;
    public bool IsDpTab2 => _dpInnerTabIndex == 2;
    public bool IsDpTab3 => _dpInnerTabIndex == 3;
    public bool IsDpTab4 => _dpInnerTabIndex == 4;

    public ObservableCollection<DisplayPlotItem> AllDpForJunction { get; } = new();
    private DisplayPlotItem? _dpJunction_SelectedPlot;
    public DisplayPlotItem? DpJunction_SelectedPlot
    {
        get => _dpJunction_SelectedPlot;
        set { SetProperty(ref _dpJunction_SelectedPlot, value); _ = DpJunctionReloadAsync(); }
    }

    // Tab 1: DP & Proportional
    public ObservableCollection<SimpleItem> DpPr_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllPropsForDp{ get; } = new();
    private SimpleItem? _dpPr_SelectedRow; public SimpleItem? DpPr_SelectedRow { get => _dpPr_SelectedRow; set => SetProperty(ref _dpPr_SelectedRow, value); }
    private SimpleItem? _dpPr_SelectedAdd; public SimpleItem? DpPr_SelectedAdd { get => _dpPr_SelectedAdd; set => SetProperty(ref _dpPr_SelectedAdd, value); }

    // Tab 2: DP & DataSource
    public ObservableCollection<SimpleItem> DpDs_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllDsForDp   { get; } = new();
    private SimpleItem? _dpDs_SelectedRow; public SimpleItem? DpDs_SelectedRow { get => _dpDs_SelectedRow; set => SetProperty(ref _dpDs_SelectedRow, value); }
    private SimpleItem? _dpDs_SelectedAdd; public SimpleItem? DpDs_SelectedAdd { get => _dpDs_SelectedAdd; set => SetProperty(ref _dpDs_SelectedAdd, value); }

    // Tab 3: DP & Compensation
    public ObservableCollection<SimpleItem> DpCo_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllCoForDp   { get; } = new();
    private SimpleItem? _dpCo_SelectedRow; public SimpleItem? DpCo_SelectedRow { get => _dpCo_SelectedRow; set => SetProperty(ref _dpCo_SelectedRow, value); }
    private SimpleItem? _dpCo_SelectedAdd; public SimpleItem? DpCo_SelectedAdd { get => _dpCo_SelectedAdd; set => SetProperty(ref _dpCo_SelectedAdd, value); }

    // Tab 4: DP & Freq Analysis
    public ObservableCollection<SimpleItem> DpFr_Rows    { get; } = new();
    public ObservableCollection<SimpleItem> AllFrForDp   { get; } = new();
    private SimpleItem? _dpFr_SelectedRow; public SimpleItem? DpFr_SelectedRow { get => _dpFr_SelectedRow; set => SetProperty(ref _dpFr_SelectedRow, value); }
    private SimpleItem? _dpFr_SelectedAdd; public SimpleItem? DpFr_SelectedAdd { get => _dpFr_SelectedAdd; set => SetProperty(ref _dpFr_SelectedAdd, value); }

    private async Task OpenDisplayPlotInnerAsync()
    {
        try
        {
            var plots = await DeviceService.GetDisplayPlotsAsync();
            AllDpForJunction.Clear(); foreach (var p in plots) AllDpForJunction.Add(p);
            var props = await DeviceService.GetProportionalsAsync();
            AllPropsForDp.Clear(); foreach (var p in props) AllPropsForDp.Add(new SimpleItem { Id = p.VarId, Name = p.NicName });
            var ds = await DeviceService.GetAllDataSourcesAsync();
            AllDsForDp.Clear(); foreach (var x in ds) AllDsForDp.Add(x);
            var co = await DeviceService.GetAllCompensationsAsync();
            AllCoForDp.Clear(); foreach (var x in co) AllCoForDp.Add(x);
            var fr = await DeviceService.GetAllFreqAnalysisAsync();
            AllFrForDp.Clear(); foreach (var x in fr) AllFrForDp.Add(x);
            DpInnerTabIndex = 0;
        }
        catch (Exception ex) { StatusMessage = $"Display Plot 로드 실패: {ex.Message}"; }
    }

    private async Task DpJunctionReloadAsync()
    {
        if (_dpJunction_SelectedPlot == null) return;
        int pid = _dpJunction_SelectedPlot.PlotId;
        try
        {
            var pr = await DeviceService.GetDpPropRowsAsync(pid);
            DpPr_Rows.Clear(); foreach (var x in pr) DpPr_Rows.Add(x);
            var ds = await DeviceService.GetDpDataSourceRowsAsync(pid);
            DpDs_Rows.Clear(); foreach (var x in ds) DpDs_Rows.Add(x);
            var co = await DeviceService.GetDpCompRowsAsync(pid);
            DpCo_Rows.Clear(); foreach (var x in co) DpCo_Rows.Add(x);
            var fr = await DeviceService.GetDpFreqRowsAsync(pid);
            DpFr_Rows.Clear(); foreach (var x in fr) DpFr_Rows.Add(x);
        }
        catch (Exception ex) { StatusMessage = $"로드 실패: {ex.Message}"; }
    }

    private async Task DpPr_AddAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpPr_SelectedAdd == null) return;
        try { await DeviceService.AddDpPropAsync(_dpJunction_SelectedPlot.PlotId, _dpPr_SelectedAdd.Id); var rows = await DeviceService.GetDpPropRowsAsync(_dpJunction_SelectedPlot.PlotId); DpPr_Rows.Clear(); foreach (var x in rows) DpPr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task DpPr_DeleteAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpPr_SelectedRow == null) return;
        try { await DeviceService.RemoveDpPropAsync(_dpJunction_SelectedPlot.PlotId, _dpPr_SelectedRow.Id); var rows = await DeviceService.GetDpPropRowsAsync(_dpJunction_SelectedPlot.PlotId); DpPr_Rows.Clear(); foreach (var x in rows) DpPr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task DpDs_AddAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpDs_SelectedAdd == null) return;
        try { await DeviceService.AddDpDataSourceAsync(_dpJunction_SelectedPlot.PlotId, _dpDs_SelectedAdd.Id); var rows = await DeviceService.GetDpDataSourceRowsAsync(_dpJunction_SelectedPlot.PlotId); DpDs_Rows.Clear(); foreach (var x in rows) DpDs_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task DpDs_DeleteAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpDs_SelectedRow == null) return;
        try { await DeviceService.RemoveDpDataSourceAsync(_dpJunction_SelectedPlot.PlotId, _dpDs_SelectedRow.Id); var rows = await DeviceService.GetDpDataSourceRowsAsync(_dpJunction_SelectedPlot.PlotId); DpDs_Rows.Clear(); foreach (var x in rows) DpDs_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task DpCo_AddAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpCo_SelectedAdd == null) return;
        try { await DeviceService.AddDpCompAsync(_dpJunction_SelectedPlot.PlotId, _dpCo_SelectedAdd.Id); var rows = await DeviceService.GetDpCompRowsAsync(_dpJunction_SelectedPlot.PlotId); DpCo_Rows.Clear(); foreach (var x in rows) DpCo_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task DpCo_DeleteAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpCo_SelectedRow == null) return;
        try { await DeviceService.RemoveDpCompAsync(_dpJunction_SelectedPlot.PlotId, _dpCo_SelectedRow.Id); var rows = await DeviceService.GetDpCompRowsAsync(_dpJunction_SelectedPlot.PlotId); DpCo_Rows.Clear(); foreach (var x in rows) DpCo_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }
    private async Task DpFr_AddAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpFr_SelectedAdd == null) return;
        try { await DeviceService.AddDpFreqAsync(_dpJunction_SelectedPlot.PlotId, _dpFr_SelectedAdd.Id); var rows = await DeviceService.GetDpFreqRowsAsync(_dpJunction_SelectedPlot.PlotId); DpFr_Rows.Clear(); foreach (var x in rows) DpFr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task DpFr_DeleteAsync()
    {
        if (_dpJunction_SelectedPlot == null || _dpFr_SelectedRow == null) return;
        try { await DeviceService.RemoveDpFreqAsync(_dpJunction_SelectedPlot.PlotId, _dpFr_SelectedRow.Id); var rows = await DeviceService.GetDpFreqRowsAsync(_dpJunction_SelectedPlot.PlotId); DpFr_Rows.Clear(); foreach (var x in rows) DpFr_Rows.Add(x); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }

    // ════════════════════════════════════════════════════════
    //  비례값 내부탭 (2-tab)
    // ════════════════════════════════════════════════════════
    private int _prInnerTabIndex = 0;
    public int PrInnerTabIndex
    {
        get => _prInnerTabIndex;
        set { SetProperty(ref _prInnerTabIndex, value); OnPropertyChanged(nameof(IsPrTab0)); OnPropertyChanged(nameof(IsPrTab1)); }
    }
    public bool IsPrTab0 => _prInnerTabIndex == 0;
    public bool IsPrTab1 => _prInnerTabIndex == 1;

    public ObservableCollection<ProportionalItem> AllPropForJunction { get; } = new();
    private ProportionalItem? _prJunction_SelectedProp;
    public ProportionalItem? PrJunction_SelectedProp
    {
        get => _prJunction_SelectedProp;
        set { SetProperty(ref _prJunction_SelectedProp, value); _ = LoadPrScaleRowsAsync(); }
    }
    public ObservableCollection<SimpleItem> PrSc_Rows     { get; } = new();
    public ObservableCollection<SimpleItem> AllScalesForPr{ get; } = new();
    private SimpleItem? _prSc_SelectedRow; public SimpleItem? PrSc_SelectedRow { get => _prSc_SelectedRow; set => SetProperty(ref _prSc_SelectedRow, value); }
    private SimpleItem? _prSc_SelectedAdd; public SimpleItem? PrSc_SelectedAdd { get => _prSc_SelectedAdd; set => SetProperty(ref _prSc_SelectedAdd, value); }

    private async Task OpenProportionalInnerAsync()
    {
        try
        {
            var props = await DeviceService.GetProportionalsAsync();
            AllPropForJunction.Clear(); foreach (var p in props) AllPropForJunction.Add(p);
            var scales = await DeviceService.GetScaleRangesAsync();
            AllScalesForPr.Clear(); foreach (var s in scales) AllScalesForPr.Add(new SimpleItem { Id = s.ScaleId, Name = s.Name });
            PrInnerTabIndex = 0;
        }
        catch (Exception ex) { StatusMessage = $"비례값 로드 실패: {ex.Message}"; }
    }

    private async Task LoadPrScaleRowsAsync()
    {
        PrSc_Rows.Clear();
        if (_prJunction_SelectedProp == null) return;
        try
        {
            var rows = await DeviceService.GetPropScaleRowsAsync(_prJunction_SelectedProp.VarId);
            foreach (var r in rows) PrSc_Rows.Add(r);
        }
        catch (Exception ex) { StatusMessage = $"스케일 로드 실패: {ex.Message}"; }
    }
    private async Task PrSc_AddAsync()
    {
        if (_prJunction_SelectedProp == null || _prSc_SelectedAdd == null) return;
        try { await DeviceService.AddPropScaleAsync(_prJunction_SelectedProp.VarId, _prSc_SelectedAdd.Id); await LoadPrScaleRowsAsync(); }
        catch (Exception ex) { StatusMessage = $"추가 실패: {ex.Message}"; }
    }
    private async Task PrSc_DeleteAsync()
    {
        if (_prJunction_SelectedProp == null || _prSc_SelectedRow == null) return;
        try { await DeviceService.RemovePropScaleAsync(_prJunction_SelectedProp.VarId, _prSc_SelectedRow.Id); await LoadPrScaleRowsAsync(); }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
    }

    // ── System 메뉴 기능 ──────────────────────────────────────────────────

    private async Task CheckDataAsync()
    {
        IsBusy = true;
        StatusMessage = "데이터 무결성 확인 중...";
        try
        {
            var (ok, msgs) = await ExcelService.CheckDataAsync();
            StatusMessage = ok ? "데이터 이상 없음" : $"이상 항목 발견";
            System.Windows.MessageBox.Show(
                string.Join("\n", msgs),
                "Check Data",
                System.Windows.MessageBoxButton.OK,
                ok ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = $"확인 실패: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task SaveSettingsXlsxAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "설정 저장 (XLSX)",
            Filter           = "Excel 파일 (*.xlsx)|*.xlsx",
            FileName         = $"CMS5000_Settings_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = "xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        StatusMessage = "설정 저장 중...";
        try
        {
            await ExcelService.ExportSettingsAsync(dlg.FileName);
            StatusMessage = "설정 저장 완료";
            System.Windows.MessageBox.Show(
                $"설정이 저장되었습니다.\n{dlg.FileName}",
                "설정 저장 (XLSX)",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    private async Task LoadSettingsAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "설정 로딩 및 수정",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        var confirm = System.Windows.MessageBox.Show(
            "선택한 파일의 설정을 DB에 적용합니다.\n기존 데이터와 충돌 시 기존 데이터가 유지됩니다 (ON CONFLICT DO NOTHING).\n\n계속하시겠습니까?",
            "설정 로딩 및 수정",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        StatusMessage = "설정 로딩 중...";
        try
        {
            var (imported, skipped, errors) = await ExcelService.ImportSettingsAsync(dlg.FileName);
            StatusMessage = $"로딩 완료 — {imported}개 시트 처리";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"처리 완료: {imported}개 시트");
            if (skipped > 0) sb.AppendLine($"건너뜀: {skipped}개 시트");
            if (errors.Count > 0)
            {
                sb.AppendLine("\n오류 목록:");
                foreach (var e in errors) sb.AppendLine($"  • {e}");
            }

            System.Windows.MessageBox.Show(
                sb.ToString(),
                "설정 로딩 완료",
                System.Windows.MessageBoxButton.OK,
                errors.Count > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);

            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"로딩 실패: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    private async Task BackupDbXlsxAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "설정 DB 백업 (XLSX)",
            Filter           = "Excel 파일 (*.xlsx)|*.xlsx",
            FileName         = $"CMS5000_ConfigBackup_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = "xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        StatusMessage = "설정 DB 백업 중...";
        try
        {
            await ExcelService.BackupConfigDbAsync(dlg.FileName);
            StatusMessage = "설정 DB 백업 완료";
            System.Windows.MessageBox.Show(
                $"설정 DB 백업이 완료되었습니다.\n{dlg.FileName}",
                "설정 DB 백업 (XLSX)",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"백업 실패: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    // ── Rack 컨텍스트 메뉴 ──────────────────────────────────────────────────

    private async Task CtxRackDeleteAsync()
    {
        if (SelectedRackNode == null) return;
        if (SelectedRackNode.Kind == NodeKind.Rack)
        {
            System.Windows.MessageBox.Show(
                "실수를 방지하기 위해 RACK 삭제기능은 제공하지 않습니다.\n삭제를 원하시면 공급사에 요청하시고, Activity 를 OFF 하세요.",
                "RACK 삭제",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }
        await DeleteRackNodeAsync();
    }

    private async Task BackupRestoreAllDbAsync()
    {
        // "설정 DB 백업(XLSX)"으로 저장한 파일을 선택해 DB를 복원합니다.
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "설정 DB 백업복원 — 백업 파일 선택",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        var confirm = System.Windows.MessageBox.Show(
            $"선택한 설정 DB 백업 파일을 복원합니다.\n기존 데이터와 충돌 시 기존 데이터가 유지됩니다.\n\n파일: {System.IO.Path.GetFileName(dlg.FileName)}\n\n계속하시겠습니까?",
            "설정 DB 백업복원",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        StatusMessage = "설정 DB 복원 중...";
        try
        {
            var (imported, skipped, errors) = await ExcelService.RestoreAllDbAsync(dlg.FileName);
            StatusMessage = $"복원 완료 — {imported}개 시트 처리";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"복원 완료: {imported}개 시트 처리됨");
            if (skipped > 0) sb.AppendLine($"건너뜀: {skipped}개 시트");
            if (errors.Count > 0)
            {
                sb.AppendLine("\n오류 목록:");
                foreach (var e in errors) sb.AppendLine($"  • {e}");
            }

            System.Windows.MessageBox.Show(
                sb.ToString(),
                "설정 DB 백업복원 완료",
                System.Windows.MessageBoxButton.OK,
                errors.Count > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);

            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"복원 실패: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; System.Windows.Input.Mouse.OverrideCursor = null; }
    }
}
