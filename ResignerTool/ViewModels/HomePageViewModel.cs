using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Dispatching;
using ResignerTool.Services;
using System.Collections.ObjectModel;

namespace ResignerTool.ViewModels;

public partial class HomePageViewModel : ObservableObject, IDisposable
{
    private readonly ILogService _logger;
    private readonly IDispatcherTimer? _logUpdateTimer;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<string> recentLogs = new();

    public HomePageViewModel()
    {
        _logger = new FileLoggerService();

        if (Application.Current?.Dispatcher is IDispatcher dispatcher)
        {
            _logUpdateTimer = dispatcher.CreateTimer();
            _logUpdateTimer.Interval = TimeSpan.FromSeconds(5);
            _logUpdateTimer.Tick += OnLogUpdate;
            _logUpdateTimer.Start();
            UpdateLogs();
        }
    }

    private void OnLogUpdate(object? sender, EventArgs e)
    {
        if (!_disposed) UpdateLogs();
    }

    [RelayCommand]
    private async Task NavigateToApk()
    {
        if (!_disposed)
            await Shell.Current.GoToAsync("//ApkResign");
    }

    [RelayCommand]
    private async Task NavigateToIpa()
    {
        if (!_disposed)
            await Shell.Current.GoToAsync("//IpaResign");
    }

    [RelayCommand]
    private void ClearLogs()
    {
        if (!_disposed)
        {
            _logger.Clear();
            RecentLogs.Clear();
        }
    }

    private void UpdateLogs()
    {
        if (_disposed) return;

        try
        {
            var logs = _logger.GetRecentLogs(20);
            var dispatcher = Application.Current?.Dispatcher;
            
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() =>
                {
                    if (!_disposed)
                    {
                        RecentLogs.Clear();
                        foreach (var log in logs.Reverse())
                        {
                            RecentLogs.Add(log);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to update logs", ex);
        }
    }

    public void OnDisappearing()
    {
        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _logUpdateTimer != null)
            {
                _logUpdateTimer.Stop();
                _logUpdateTimer.Tick -= OnLogUpdate;
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}