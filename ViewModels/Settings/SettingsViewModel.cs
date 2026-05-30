using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    private FontSizePreset _fontSizePreset = FontSizePreset.Medium;

    public FontSizePreset FontSizePreset
    {
        get => _fontSizePreset;
        set
        {
            if (!SetProperty(ref _fontSizePreset, value)) return;
            OnPropertyChanged(nameof(IsSmall));
            OnPropertyChanged(nameof(IsMedium));
            OnPropertyChanged(nameof(IsLarge));
            FontSizeManager.Apply(value);
            _ = UserSettingsService.SaveFontSizeAsync(value);
        }
    }

    public bool IsSmall  => _fontSizePreset == FontSizePreset.Small;
    public bool IsMedium => _fontSizePreset == FontSizePreset.Medium;
    public bool IsLarge  => _fontSizePreset == FontSizePreset.Large;

    public RelayCommand<string> SetFontSizeCommand { get; }
    public RelayCommand CheckUpdateCommand { get; }

    // 프로그램 정보 / 버전 이력
    public string CurrentVersion { get; } = UpdateService.GetCurrentVersionText();
    public List<ChangelogEntry> Changelog { get; } = ChangelogService.Load();

    private string _updateStatus = "";
    public string UpdateStatus { get => _updateStatus; set => SetProperty(ref _updateStatus, value); }

    public SettingsViewModel()
    {
        SetFontSizeCommand = new RelayCommand<string>(preset =>
        {
            if (Enum.TryParse<FontSizePreset>(preset, out var p))
                FontSizePreset = p;
        });

        CheckUpdateCommand = new RelayCommand(_ => _ = CheckUpdateAsync());
    }

    private async Task CheckUpdateAsync()
    {
        await UpdateService.CheckDownloadAndApplyAsync(
            status => UpdateStatus = status,
            versionToApply =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"새 버전 v{versionToApply} 업데이트가 준비되었습니다.\n지금 재시작해서 적용하시겠습니까?",
                    "업데이트 준비 완료",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);
                return result == System.Windows.MessageBoxResult.Yes;
            });
    }

    public string DisplayName { get; private set; } = "";
    public string Username    { get; private set; } = "";
    public string RoleName    { get; private set; } = "";

    public void LoadFromCurrentUser()
    {
        var user = AuthService.CurrentUser;
        DisplayName = user?.DisplayName ?? "";
        Username    = user?.Username ?? "";
        RoleName    = user?.Role switch
        {
            "Admin"       => "관리자",
            "Expert"      => "진단전문가",
            "Maintenance" => "정비담당자",
            _             => "운전자"
        };
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(RoleName));

        var preset = FontSizeManager.Current;
        _fontSizePreset = preset;
        OnPropertyChanged(nameof(FontSizePreset));
        OnPropertyChanged(nameof(IsSmall));
        OnPropertyChanged(nameof(IsMedium));
        OnPropertyChanged(nameof(IsLarge));
        FontSizeManager.Apply(preset);
    }
}
