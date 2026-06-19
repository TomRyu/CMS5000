namespace CMS5000.Models;
public class SensorItem
{
    public int    SensorId      { get; set; }
    public string Name          { get; set; } = "";
    public int    Type          { get; set; }
    public string TypeLabel     => Type == 0 ? "Proximitor" : "Magnetic";
    public double Sensitivity   { get; set; }
    public string UnitName      { get; set; } = "";
    public int    Icp           { get; set; }
    public int    Power         { get; set; }
    public double PowerCheckLow { get; set; }
    public double PowerCheckHigh{ get; set; }
    public string BrandName     { get; set; } = "";
    public string Spec          { get; set; } = "";
}
