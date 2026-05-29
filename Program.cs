using System.IO;
using System.Text.Json;
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

        ApplyLightningChartLicense();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void ApplyLightningChartLicense()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("LightningChart", out var lc)) return;
            if (!lc.TryGetProperty("LicenseKey", out var keyProp)) return;
            var key = keyProp.GetString();
            if (string.IsNullOrWhiteSpace(key)) return;

            // 리플렉션으로 LightningChart 라이선스 키 설정 (API 변경에 대응)
            var lcType = typeof(LightningChartLib.WPF.ChartingMVVM.LightningChart);
            var setKey = lcType.GetMethod("SetLicenseKey",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            setKey?.Invoke(null, [key]);

            var prop = lcType.GetProperty("LicenseKey",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            prop?.SetValue(null, key);
        }
        catch
        {
            // 라이선스 키 없이도 평가판 모드로 동작
        }
    }
}
