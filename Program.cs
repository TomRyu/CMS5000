using System.IO;
using System.Reflection;
using CMS5000.Services;
using System.Windows;
using Velopack;

namespace CMS5000;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // LightningChart NuGet(net8)이 요구하는 System.Windows.Forms 8.0.0.0을
        // 현재 런타임의 버전으로 리다이렉트 (강한 이름 버전 불일치 우회)
        AppDomain.CurrentDomain.AssemblyResolve += RedirectWindowsForms;

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

    private static Assembly? RedirectWindowsForms(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name);
        if (name.Name != "System.Windows.Forms") return null;

        // 이미 로드된 버전 재사용
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if (asm.GetName().Name == "System.Windows.Forms") return asm;

        // 앱 디렉터리의 DLL 직접 로드
        var path = Path.Combine(AppContext.BaseDirectory, "System.Windows.Forms.dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
