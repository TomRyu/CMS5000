# CMS-5000

㈜오토시스(소음·진동 측정장비 전문 기업)의 **설비 상태 모니터링 데스크톱 앱**.
진동 분석 · 이상 진단 · 점검 이력 관리.

## 기술 스택
- **WPF / .NET 9** (`net9.0-windows10.0.19041.0`), MVVM
- **ScottPlot.WPF** (진동 차트)
- **Velopack** (자동 업데이트 + 설치 패키지)
- **PostgreSQL** (인증 · 데이터). `Npgsql`로 로컬 DB에 **직접 연결** — 접속 정보는 `appsettings.json`의 `PostgreSQL` 섹션. (※ Supabase 호스팅/`supabase-csharp`는 더 이상 사용하지 않으며, `supabase/` 폴더는 레거시 마이그레이션 잔재)
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
- `Program.cs` — 진입점. Velopack 초기화 → PostgreSQL 연결(`PostgresService.Initialize`/`EnsureReachableAsync`) → 확장 스키마 멱등 보장(`EnsureSchemaAsync`) → `App` 실행.
- `MainWindow.xaml` — 좌측 NavRail(대시보드/설비/분석/진단/점검·이력/보고서/설정)과 상단 바. 메뉴는 사용자 **역할(Operator/Maintenance/Expert/Admin)** 에 따라 표시/숨김.
- `Views/` — 역할별 메인 뷰 + Settings/Shell(Login)/Admin. `ViewModels/`와 1:1, `ViewModelBase`(INotifyPropertyChanged) + `RelayCommand`.
- `Services/` — `PostgresService`(Npgsql 데이터소스·연결·스키마), `AuthService`(로그인·잠금), `UserService`/`LoginLogService`(`cms_users`/`cms_login_logs`), `UpdateService`(Velopack+GitHub), `ChangelogService`, `AppLogService`(앱 동작 로그) 등.
- `Themes/` — `Colors.xaml`, `Styles.xaml`. 색/스타일은 항상 리소스 키 사용(`AccentBlueBrush`, `StatusGoodBrush`, `CardStyle`, `AccentButtonStyle` 등). 하드코딩 금지.

## 핵심 주의사항
- LightningChart 관련 코드와 패키지 참조는 제거됨. 다시 추가하지 말 것.
- 차트 구현은 `Controls/ScottPlotSampleView.xaml(.cs)`에서 관리한다. Expert 화면은 `ScottPlotSampleView`를 바인딩해 사용한다.
- ScottPlot은 2D 중심 렌더러이므로 `Surface`, `Spectrogram`, Waterfall/Cascade의 3D 토글은 heatmap 기반 표현으로 대체 구현되어 있다.
- LightningChart 제거 후 `UseWindowsForms`, `System.Drawing.Common`, `System.Windows.Forms`/`System.Drawing` 전역 using 제거 설정은 더 이상 사용하지 않는다.

## 개발이력 (CHANGELOG)
- 설정 → "프로그램 정보" 카드에 버전 이력 표시. 데이터는 번들 `CHANGELOG.json`(오프라인 동작).
- **의미 있는 변경을 배포할 때 `CHANGELOG.json` 맨 위 `entries`에 블록 추가**:
  ```json
  { "version": "1.0.XX", "date": "YYYY-MM-DD",
    "changes": [ { "kind": "New|Improve|Fix", "text": "..." } ] }
  ```
  `kind`: `New`(신규/파랑) · `Improve`(개선/초록) · `Fix`(버그수정/주황).
