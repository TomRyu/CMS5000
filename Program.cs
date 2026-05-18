using System;
using Velopack;

namespace CMS5000;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Velopack 초기화 — 업데이트 적용 및 설치/제거 훅 처리
        // 반드시 앱의 가장 첫 번째 라인에 위치해야 합니다
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
