namespace CMS5000.Models;
public class DisplayPlotItem
{
    public int    PlotId      { get; set; }
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public int    Dynamic     { get; set; }
    public string DynamicLabel => Dynamic == 1 ? "Dynamic" : "Static";
}
