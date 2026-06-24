using System;
using System.Threading.Tasks;
using System.Windows;
using CMS5000.Services;

namespace CMS5000.Views.Admin;

/// <summary>
/// 중요사항 실행 비밀번호 입력 다이얼로그.
/// setMode=false: 검증용(비밀번호 1개) — Password 를 호출측이 SecurityService로 검증.
/// setMode=true : 설정/변경용(비밀번호 + 확인) — 일치·최소길이 검증 후 Password 반환.
/// </summary>
public partial class PasswordDialog : Window
{
    private readonly bool _setMode;
    private const int MinLength = 4;
    private bool _revealed;

    /// <summary>입력된 비밀번호(확인 클릭 시 유효).</summary>
    public string Password { get; private set; } = "";

    public PasswordDialog(bool setMode, string title, string prompt)
    {
        InitializeComponent();
        _setMode = setMode;
        Title = title;
        PromptText.Text = prompt;
        if (setMode) ConfirmRow.Visibility = Visibility.Visible;
        Loaded += (_, _) => PwBox.Focus();
    }

    // 현재 표시 모드에 따라 올바른 컨트롤에서 값을 읽는다.
    private string CurrentPassword => _revealed ? PwText.Text : PwBox.Password;
    private string CurrentConfirm  => _revealed ? PwConfirmText.Text : PwConfirmBox.Password;

    private void ToggleReveal_Click(object sender, RoutedEventArgs e)
    {
        _revealed = !_revealed;
        if (_revealed)
        {
            PwText.Text = PwBox.Password;
            PwConfirmText.Text = PwConfirmBox.Password;
            PwBox.Visibility = Visibility.Collapsed;        PwText.Visibility = Visibility.Visible;
            PwConfirmBox.Visibility = Visibility.Collapsed; PwConfirmText.Visibility = Visibility.Visible;
            EyeIcon.Opacity = 1.0;
        }
        else
        {
            PwBox.Password = PwText.Text;
            PwConfirmBox.Password = PwConfirmText.Text;
            PwBox.Visibility = Visibility.Visible;          PwText.Visibility = Visibility.Collapsed;
            PwConfirmBox.Visibility = Visibility.Visible;   PwConfirmText.Visibility = Visibility.Collapsed;
            EyeIcon.Opacity = 0.55;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string pw = CurrentPassword;
        if (string.IsNullOrEmpty(pw)) { Fail("비밀번호를 입력하세요."); return; }

        if (_setMode)
        {
            if (pw.Length < MinLength) { Fail($"비밀번호는 {MinLength}자 이상이어야 합니다."); return; }
            if (pw != CurrentConfirm) { Fail("비밀번호 확인이 일치하지 않습니다."); return; }
        }

        Password = pw;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// 중요사항 실행 비밀번호 검증 플로우(공용). 미설정이면 설정받고, 설정돼 있으면 검증한다.
    /// 통과 시 true. DownLoad/UpLoad/DB저장/RACK 내용비우기 등에서 공통 사용.
    /// </summary>
    public static async Task<bool> EnsureCriticalAuthAsync(Window? owner)
    {
        bool isSet;
        try { isSet = await SecurityService.IsSetAsync(); }
        catch (Exception ex)
        {
            MessageBox.Show($"비밀번호 설정 확인 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!isSet)
        {
            var dlg = new PasswordDialog(true, "중요사항 실행 비밀번호 설정 (최초 1회)",
                "DownLoad/UpLoad/DB저장 등 중요사항 실행에 사용할 비밀번호를 설정하세요. 이후 실행 시마다 입력이 필요합니다.")
            { Owner = owner };
            if (dlg.ShowDialog() != true) return false;
            try { await SecurityService.SetAsync(dlg.Password); return true; }
            catch (Exception ex)
            {
                MessageBox.Show($"비밀번호 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        var verify = new PasswordDialog(false, "비밀번호 확인", "중요사항 실행 비밀번호를 입력하세요.") { Owner = owner };
        if (verify.ShowDialog() != true) return false;
        bool ok;
        try { ok = await SecurityService.VerifyAsync(verify.Password); }
        catch (Exception ex)
        {
            MessageBox.Show($"비밀번호 확인 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        if (!ok)
        {
            MessageBox.Show("비밀번호가 일치하지 않습니다.", "확인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        return true;
    }

    private void Fail(string msg)
    {
        StatusText.Text = msg;
        if (_setMode) { if (_revealed) PwConfirmText.Focus(); else PwConfirmBox.Focus(); }
        else          { if (_revealed) PwText.Focus();        else PwBox.Focus(); }
    }
}
