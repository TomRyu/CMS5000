namespace CMS5000.Models;

public class ChannelOption
{
    public int    ChannelId   { get; init; }
    public string DisplayName { get; init; } = "";
    public override string ToString() => DisplayName;
}
