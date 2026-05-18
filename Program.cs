using System.Windows;
using CMS5000.Services;
using Velopack;

namespace CMS5000;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        try
        {
            Task.Run(async () => await SupabaseService.InitializeAsync()).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
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
