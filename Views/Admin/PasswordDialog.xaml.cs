using System.Windows;

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

    private void Fail(string msg)
    {
        StatusText.Text = msg;
        if (_setMode) { if (_revealed) PwConfirmText.Focus(); else PwConfirmBox.Focus(); }
        else          { if (_revealed) PwText.Focus();        else PwBox.Focus(); }
    }
}
