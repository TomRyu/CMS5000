using System.Windows;
using System.Windows.Controls;

namespace CMS5000.Services;

public static class InputBox
{
    public static bool Show(string prompt, string title, out string result, string defaultValue = "")
    {
        result = defaultValue;

        var txt = new TextBox
        {
            Text   = defaultValue,
            Margin = new Thickness(0, 4, 0, 10),
            Height = 26,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 0, 4, 0)
        };

        var btnOk     = new Button { Content = "확인", Width = 75, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
        var btnCancel = new Button { Content = "취소", Width = 75, IsCancel = true };

        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnRow.Children.Add(btnOk);
        btnRow.Children.Add(btnCancel);

        var panel = new StackPanel { Margin = new Thickness(14, 12, 14, 10) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(txt);
        panel.Children.Add(btnRow);

        var win = new Window
        {
            Title                 = title,
            Content               = panel,
            Width                 = 340,
            SizeToContent         = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode            = ResizeMode.NoResize,
            Owner                 = Application.Current?.MainWindow,
            ShowInTaskbar         = false
        };

        bool? dlgResult = null;
        btnOk.Click     += (_, _) => { dlgResult = true;  win.Close(); };
        btnCancel.Click += (_, _) => { dlgResult = false; win.Close(); };

        win.ShowDialog();

        result = txt.Text.Trim();
        return dlgResult == true;
    }
}
