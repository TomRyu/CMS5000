# CMS-5000

㈜오토시스(소음·진동 측정장비 전문 기업)의 **설비 상태 모니터링 데스크톱 앱**.
진동 분석 · 이상 진단 · 점검 이력 관리.

## 기술 스택
- **WPF / .NET 9** (`net9.0-windows`), MVVM
- **LightningChart** (진동 차트, NuGet net8 빌드)
- **Velopack** (자동 업데이트 + 설치 패키지)
- **Supabase** (인증 · 데이터, `supabase-csharp`)
- 언어: 한국어 (UI · 커밋 메시지 · 주석 모두 한국어)

## 빌드 / 실행
```powershell
dotnet build -c Debug      # 빌드
dotnet run                 # 실행 (로그인 → 메뉴)
```
- 진입점은 WPF 자동생성 Main이 아니라 `Program.cs`의 `Main` (`<StartupObject>CMS5000.Program`). Velopack 후킹 때문.

## ⚠️ 배포 — main에 push하면 즉시 프로덕션 릴리스
`main` 브랜치에 push하면 `.github/workflows/publish.yml`이 자동 실행되어:
1. 자립실행(self-contained win-x64) 빌드
2. Velopack 패키지 생성
3. **GitHub Release 자동 발행** — 버전은 `1.0.{github.run_number}` 자동 증가
4. 설치 안내 페이지(GitHub Pages) 배포

→ **main push = 배포**이므로 커밋 전 사용자 확인 필수. 가벼운 실험은 별도 브랜치에서.

## 아키텍처
- `Program.cs` — 진입점. Velopack 초기화 → Supabase 연결 → `App` 실행.
- `MainWindow.xaml` — 좌측 NavRail(대시보드/설비/분석/진단/점검·이력/보고서/설정)과 상단 바. 메뉴는 사용자 **역할(Operator/Maintenance/Expert/Admin)** 에 따라 표시/숨김.
- `Views/` — 역할별 메인 뷰 + Settings/Shell(Login)/Admin. `ViewModels/`와 1:1, `ViewModelBase`(INotifyPropertyChanged) + `RelayCommand`.
- `Services/` — `UpdateService`(Velopack+GitHub), `SupabaseService`, `AuthService`, `ChangelogService` 등.
- `Themes/` — `Colors.xaml`, `Styles.xaml`. 색/스타일은 항상 리소스 키 사용(`AccentBlueBrush`, `StatusGoodBrush`, `CardStyle`, `AccentButtonStyle` 등). 하드코딩 금지.

## 핵심 주의사항
- **LightningChart는 `UseWindowsForms=true`가 필수.** net8 빌드가 런타임에 `System.Windows.Forms`를 요구하는데, WPF만 켜면 어셈블리가 없어 차트 진입 시 크래시. 정품 net9 어셈블리를 포함시켜 버전 롤포워드로 해결. (과거의 net8 DLL 번들 + AssemblyResolve 핵은 제거됨 — 되살리지 말 것.)
- `UseWindowsForms` 때문에 `System.Windows.Forms`/`System.Drawing` 전역 using이 자동 주입되어 WPF 타입과 CS0104 충돌 → csproj에서 `<Using Remove>`로 제거해 둠.

## 개발이력 (CHANGELOG)
- 설정 → "프로그램 정보" 카드에 버전 이력 표시. 데이터는 번들 `CHANGELOG.json`(오프라인 동작).
- **의미 있는 변경을 배포할 때 `CHANGELOG.json` 맨 위 `entries`에 블록 추가**:
  ```json
  { "version": "1.0.XX", "date": "YYYY-MM-DD",
    "changes": [ { "kind": "New|Improve|Fix", "text": "..." } ] }
  ```
  `kind`: `New`(신규/파랑) · `Improve`(개선/초록) · `Fix`(버그수정/주황).
