using CMS5000.Services;
using System.Windows;
using Velopack;

namespace CMS5000;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        AppLogService.Info("시스템", $"CMS-5000 시작 (v{UpdateService.GetCurrentVersionText()})");

        try
        {
            Task.Run(async () => await SupabaseService.InitializeAsync()).GetAwaiter().GetResult();
            AppLogService.Success("시스템", "Supabase 연결 완료");
        }
        catch (Exception ex)
        {
            AppLogService.Error("시스템", $"Supabase 연결 실패: {ex.Message}");
            MessageBox.Show(
                $"Supabase 연결 실패:\n\n{ex.Message}\n\nappsettings.json의 URL과 ServiceRoleKey를 확인하세요.",
                "CMS-5000 시작 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
