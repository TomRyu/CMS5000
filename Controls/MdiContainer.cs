using System.Windows;
using System.Windows.Controls;

namespace CMS5000.Controls;

public class MdiContainer : Canvas
{
    private int _zCounter = 1;

    public MdiContainer()
    {
        ClipToBounds = true;
    }

    public MdiChild AddChild(string title, UIElement content, double left = 20, double top = 20, double width = 600, double height = 400)
    {
        var child = new MdiChild
        {
            Title = title,
            Content = content,
            Width = width,
            Height = height
        };

        Canvas.SetLeft(child, left + (Children.Count * 24));
        Canvas.SetTop(child, top + (Children.Count * 24));
        Panel.SetZIndex(child, _zCounter++);

        child.BringToFrontRequested += () => Panel.SetZIndex(child, _zCounter++);
        child.CloseRequested += () => Children.Remove(child);

        Children.Add(child);
        return child;
    }
}
