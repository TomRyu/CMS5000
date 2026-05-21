using System.Windows;
using CMS5000.Services;

namespace CMS5000;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _ = UpdateService.CheckAndDownloadAsync();
    }
}

