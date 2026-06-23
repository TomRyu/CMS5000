using System.Collections.ObjectModel;

namespace CMS5000.Models.Monitoring;

/// <summary>모니터링 화면 공통 상태(LED·뱃지 색 결정).</summary>
public enum MonStatus { None, Good, Warning, Alert, Alarm }

/// <summary>좌측 머신/디바이스 트리 노드.</summary>
public class MachineNode
{
    public string Name { get; set; } = "";
    public MonStatus Status { get; set; } = MonStatus.None;
    /// <summary>root / group / machine — 아이콘 색 구분용.</summary>
    public string Kind { get; set; } = "machine";
    public bool IsExpanded { get; set; } = true;
    public ObservableCollection<MachineNode> Children { get; } = [];
}

/// <summary>좌측 하단 "Spectrum &amp; Phy Waveforms" / "Trended Variables" 목록 항목.</summary>
public class SidebarItem
{
    public string Text { get; set; } = "";
    public bool Highlighted { get; set; }
}

/// <summary>Status &gt; List 표 행.</summary>
public class StatusPointRow
{
    public MonStatus Status { get; set; }
    public string Point { get; set; } = "";
    public string Machine { get; set; } = "";
    public string TagName { get; set; } = "";
    public string Display { get; set; } = "";
    public string PeakAmplitude { get; set; } = "";
    public string LastRecorded { get; set; } = "";
}

/// <summary>Status &gt; Bar Graph 카드 1개(Horizontal/Vertical/Axial).</summary>
public class BarCard
{
    public string Axis { get; set; } = "";
    public MonStatus Status { get; set; }
    public double Value { get; set; }
    public double AxisMax { get; set; } = 1;
    public string ValueText { get; set; } = "";
    public string Timestamp { get; set; } = "";
}

/// <summary>Bar Graph 머신 그룹(머신명 + 카드들).</summary>
public class BarGroup
{
    public string MachineTitle { get; set; } = "";
    public ObservableCollection<BarCard> Cards { get; } = [];
}

/// <summary>Status &gt; Overview 머신 다이어그램 블록(Motor/Pump 등).</summary>
public class OverviewBlock
{
    public string Name { get; set; } = "";
    public MonStatus Status { get; set; }
    public string Overall { get; set; } = "";
    public string Horizontal { get; set; } = "";
    public string Axial { get; set; } = "";
    public string Vertical { get; set; } = "";
    public bool HasReadings { get; set; } = true;
}

/// <summary>Events &gt; Alarms 표 행.</summary>
public class AlarmRow
{
    /// <summary>Alert / Alarm.</summary>
    public string Level { get; set; } = "";
    public bool IsAlarm { get; set; }
    public string AuditPath { get; set; } = "";
    public string DevicePoint { get; set; } = "";
    public string Machine { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public string Time { get; set; } = "";
}
