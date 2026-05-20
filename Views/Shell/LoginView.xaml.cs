using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CMS5000.ViewModels;

namespace CMS5000.Views.Shell;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel old)
            old.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is MainViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            PwdBox.Password = vm.LoginPassword;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm) return;
        if (e.PropertyName == nameof(MainViewModel.LoginPassword))
            PwdBox.Password = vm.LoginPassword;
        else if (e.PropertyName == nameof(MainViewModel.IsPasswordVisible) && !vm.IsPasswordVisible)
            PwdBox.Password = vm.LoginPassword;
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.LoginPassword = ((PasswordBox)sender).Password;
    }

    // ── 아이디 드롭다운 ──────────────────────────────────────────────

    private bool _selectingUsername  = false;
    private bool _keyboardNavigating = false;

    private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectingUsername || DataContext is not MainViewModel vm) return;

        var text = UsernameBox.Text;
        if (string.IsNullOrEmpty(text))
        {
            UsernamePopup.IsOpen = false;
            return;
        }

        var filtered = vm.LoginUsernames
            .Where(u => u.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _keyboardNavigating = true;
        UsernameList.ItemsSource   = filtered;
        UsernameList.SelectedIndex = -1;
        _keyboardNavigating = false;

        UsernamePopup.Width  = UsernameBorder.ActualWidth;
        UsernamePopup.IsOpen = filtered.Count > 0;
    }

    private void UsernameDropdownBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _keyboardNavigating = true;
            UsernameList.ItemsSource   = vm.LoginUsernames;
            UsernameList.SelectedIndex = -1;
            _keyboardNavigating = false;
        }
        UsernamePopup.Width  = UsernameBorder.ActualWidth;
        UsernamePopup.IsOpen = !UsernamePopup.IsOpen;
    }

    // 마우스 호버 → SelectedIndex 동기화 (Enter 커밋 가능하게)
    private void UsernameList_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox lb) return;
        var dep = VisualTreeHelper.HitTest(lb, e.GetPosition(lb))?.VisualHit as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);
        if (dep is ListBoxItem item)
        {
            _keyboardNavigating = true;
            lb.SelectedItem = item.DataContext;
            _keyboardNavigating = false;
        }
    }

    // 마우스 클릭으로 선택 (PreviewMouseDown: 이미 SelectedIndex가 설정된 항목 클릭 시도 대응)
    private void UsernameList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox lb) return;
        var dep = VisualTreeHelper.HitTest(lb, e.GetPosition(lb))?.VisualHit as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);
        if (dep is ListBoxItem item && item.DataContext is string username)
        {
            CommitUsername(username);
            e.Handled = true;
        }
    }

    private void UsernameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_keyboardNavigating) return;
        if (sender is ListBox lb && lb.SelectedItem is string username)
            CommitUsername(username);
    }

    private void CommitUsername(string username)
    {
        if (DataContext is not MainViewModel vm) return;
        _selectingUsername = true;
        vm.LoginUsername   = username;
        _selectingUsername = false;
        vm.OnUsernameSelected(username);
        UsernamePopup.IsOpen = false;
        _keyboardNavigating  = true;
        UsernameList.SelectedIndex = -1;
        _keyboardNavigating  = false;
        PwdBox.Focus();
    }

    // ── 키보드 핸들러 ────────────────────────────────────────────────

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        // Popup이 열려 있을 때 UsernameBox에서의 키 처리
        if (UsernamePopup.IsOpen && sender == UsernameBox)
        {
            if (e.Key == Key.Down)
            {
                _keyboardNavigating    = true;
                UsernameList.SelectedIndex = Math.Min(
                    UsernameList.SelectedIndex + 1, UsernameList.Items.Count - 1);
                _keyboardNavigating    = false;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                _keyboardNavigating    = true;
                UsernameList.SelectedIndex = Math.Max(UsernameList.SelectedIndex - 1, -1);
                _keyboardNavigating    = false;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && UsernameList.SelectedIndex >= 0 &&
                UsernameList.SelectedItem is string selected)
            {
                CommitUsername(selected);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                UsernamePopup.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            vm.LoginCommand.Execute(null);
    }
}
