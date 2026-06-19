namespace CMS5000.Models;
public class ScaleRangeItem
{
    public int    ScaleId     { get; set; }
    public string Name        { get; set; } = "";
    public double Min         { get; set; }
    public double Max         { get; set; }
    public string Description { get; set; } = "";
}
