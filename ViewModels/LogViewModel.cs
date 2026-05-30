using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels;

public class LogViewModel : ViewModelBase
{
    private string _levelFilter = "All";   // All / Info / Success / Warning / Error
    private string _searchText  = "";

    public ICollectionView LogView { get; }

    public string LevelFilter
    {
        get => _levelFilter;
        set
        {
            if (SetProperty(ref _levelFilter, value))
            {
                LogView.Refresh();
                RaiseFilterStates();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) LogView.Refresh(); }
    }

    // 필터 버튼 활성 표시용
    public bool IsAll     => _levelFilter == "All";
    public bool IsInfo    => _levelFilter == "Info";
    public bool IsSuccess => _levelFilter == "Success";
    public bool IsWarning => _levelFilter == "Warning";
    public bool IsError   => _levelFilter == "Error";

    public int TotalCount   => AppLogService.Entries.Count;
    public int WarningCount => AppLogService.Entries.Count(e => e.Level == LogLevel.Warning);
    public int ErrorCount   => AppLogService.Entries.Count(e => e.Level == LogLevel.Error);

    public RelayCommand<string> SetLevelFilterCommand { get; }
    public RelayCommand         ClearCommand          { get; }
    public RelayCommand         OpenLogFolderCommand   { get; }

    public LogViewModel()
    {
        LogView = CollectionViewSource.GetDefaultView(AppLogService.Entries);
        LogView.Filter = FilterPredicate;

        AppLogService.Entries.CollectionChanged += (_, _) => RaiseCounts();

        SetLevelFilterCommand = new RelayCommand<string>(f => LevelFilter = f ?? "All");
        ClearCommand          = new RelayCommand(_ =>
        {
            AppLogService.Clear();
            AppLogService.Info("시스템", "로그 화면을 비웠습니다.");
        });
        OpenLogFolderCommand  = new RelayCommand(_ => OpenLogFolder());
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not AppLogEntry e) return false;

        if (_levelFilter != "All" &&
            !string.Equals(e.Level.ToString(), _levelFilter, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim();
            if (e.Message.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                e.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                e.User.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private static void OpenLogFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppLogService.CurrentLogFilePath)!;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogService.Error("시스템", $"로그 폴더 열기 실패: {ex.Message}");
        }
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ErrorCount));
    }

    private void RaiseFilterStates()
    {
        OnPropertyChanged(nameof(IsAll));
        OnPropertyChanged(nameof(IsInfo));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsError));
    }
}
