namespace CMS5000.Models;

/// <summary>Reference Config(원본 frmReference) 채널 신호설정 전체 값.</summary>
public class ReferenceConfigInfo
{
    // 헤더
    public string Name         { get; set; } = "";
    public int    ChannelType  { get; set; }
    public int    ActivityMode { get; set; }   // 0 Inactivity / 1 Activity / 2 Simulated
    public bool   Assign       { get; set; }

    // INFO
    public int ReassignMode     { get; set; }  // 0 Alternate Reference / 1 Simulated Speed
    public int Speed            { get; set; }
    public int AlternateId      { get; set; }  // 콤보 인덱스 (1~4 → 0~3)
    public int RotationDir      { get; set; }  // 0 CW / 1 CCW
    public int SignalPolarity   { get; set; }  // 0 Projection / 1 Notch
    public int    ThresholdType    { get; set; }  // 0 Auto / 1 Manual
    public double ThresholdLevel   { get; set; }
    public int    ClampValue       { get; set; }
    public int    UpperLimit       { get; set; }
    public double HysteresisLevel  { get; set; }
    public int FluctuationRange { get; set; }
    public int UnalteredTime    { get; set; }
    public int OrientationAngle { get; set; }
    public int Orientation      { get; set; }  // 0 Left / 1 Right
    public int WaveFormInterval { get; set; }
    public int EpRevolution     { get; set; }

    // Sensor
    public string SensorName      { get; set; } = "";
    public int    Sensitivity     { get; set; }
    public string SensorUnit      { get; set; } = "";
    public int    Icp             { get; set; }  // 0 OFF / 1 ON
    public double PowerLow        { get; set; }
    public double PowerHigh       { get; set; }
    public int    ProximitorPower { get; set; }  // 0 -18V / 1 -24V
    public int    SignalType      { get; set; }  // 0 Magnetic / 1 Proximitor

    // Auto Upload
    public int UploadTime      { get; set; }
    public int UploadCondition { get; set; }  // 0 None / 1 Time / 2 RPM / 3 Both
    public int StartUpRpm      { get; set; }
    public int ShutDownRpm     { get; set; }

    // Min/Max/Delta
    public int SrBegin { get; set; }
    public int SrEnd   { get; set; }
    public int SrDelta { get; set; }
    public int SdMax   { get; set; }
    public int SdMin   { get; set; }
    public int SdDelta { get; set; }
    public int SuMax   { get; set; }
    public int SuMin   { get; set; }
    public int SuDelta { get; set; }
}
