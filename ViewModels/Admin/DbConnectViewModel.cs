using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// DB 접속정보 다이얼로그(원본 dDatabase) VM.
/// Server/Database/User/Password 입력 → OK 시 연결 시도, 성공하면 저장·접속.
/// </summary>
public class DbConnectViewModel : ViewModelBase
{
    private string _server;
    private string _database;
    private string _user;
    private string _password;
    private string _status = "";

    public string Server   { get => _server;   set => SetProperty(ref _server, value); }
    public string Database { get => _database; set => SetProperty(ref _database, value); }
    public string User     { get => _user;     set => SetProperty(ref _user, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Status   { get => _status;   set => SetProperty(ref _status, value); }

    /// <summary>연결 성공 시 true.</summary>
    public bool Connected { get; private set; }
    public event Action? CloseRequested;

    public RelayCommand OkCommand     { get; }
    public RelayCommand CancelCommand { get; }

    public DbConnectViewModel()
    {
        _server   = PostgresService.CurrentHost;
        _database = PostgresService.CurrentDatabase;
        _user     = PostgresService.CurrentUsername;
        _password = PostgresService.CurrentPassword;

        OkCommand     = new RelayCommand(_ => _ = ConnectAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    private async Task ConnectAsync()
    {
        // 원본 dDatabase OK: 빈 항목이 있으면 진행하지 않음
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database) ||
            string.IsNullOrWhiteSpace(User)   || string.IsNullOrWhiteSpace(Password))
        {
            Status = "모든 항목을 입력하세요.";
            return;
        }

        Status = $"{Database} Conecting...";
        bool ok = await PostgresService.ReconfigureAsync(Server.Trim(), Database.Trim(), User.Trim(), Password);
        if (ok)
        {
            Connected = true;
            CloseRequested?.Invoke();
        }
        else
        {
            Status = $"{Database} Connection FAIL.";
            System.Windows.MessageBox.Show($"{Database} 연결에 실패했습니다.\n접속정보를 확인하세요.", "Database Connect",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}
