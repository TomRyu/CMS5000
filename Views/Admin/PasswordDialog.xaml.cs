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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string pw = PwBox.Password;
        if (string.IsNullOrEmpty(pw)) { Fail("비밀번호를 입력하세요."); return; }

        if (_setMode)
        {
            if (pw.Length < MinLength) { Fail($"비밀번호는 {MinLength}자 이상이어야 합니다."); return; }
            if (pw != PwConfirmBox.Password) { Fail("비밀번호 확인이 일치하지 않습니다."); return; }
        }

        Password = pw;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Fail(string msg)
    {
        StatusText.Text = msg;
        (_setMode ? PwConfirmBox : PwBox).Focus();
    }
}
