using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace WannaTool
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private bool _autoStart;
        private bool _enableSystemMonitoring;
        private int _systemMonitorInterval = 2;
        private string _appName = "WannaTool";
        private string _appVersion = "v0.3.0-alpha";
        private string _author = "Kevin GREGOIRE - Nodasys";
        private string _repoUrl = "https://github.com/Laxyny/WannaTool";
        private bool _isDirty;
        
        private string _currentSection = "General";
        public string CurrentSection
        {
            get => _currentSection;
            set
            {
                if (_currentSection != value)
                {
                    _currentSection = value;
                    OnPropertyChanged(nameof(CurrentSection));
                    OnPropertyChanged(nameof(IsGeneralVisible));
                    OnPropertyChanged(nameof(IsSearchVisible));
                    OnPropertyChanged(nameof(IsMaintenanceVisible));
                    OnPropertyChanged(nameof(IsAboutVisible));
                }
            }
        }

        public bool IsGeneralVisible => CurrentSection == "General";
        public bool IsSearchVisible => CurrentSection == "Search";
        public bool IsMaintenanceVisible => CurrentSection == "Maintenance";
        public bool IsAboutVisible => CurrentSection == "About";

        public ObservableCollection<WebRedirect> Redirects { get; } = new ObservableCollection<WebRedirect>();

        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                if (_autoStart != value)
                {
                    _autoStart = value;
                    OnPropertyChanged(nameof(AutoStart));
                    SetAutoStart(value);
                    SettingsManager.Current.AutoStart = value;
                    IsDirty = true;
                }
            }
        }

        public bool EnableSystemMonitoring
        {
            get => _enableSystemMonitoring;
            set
            {
                if (_enableSystemMonitoring != value)
                {
                    _enableSystemMonitoring = value;
                    OnPropertyChanged(nameof(EnableSystemMonitoring));
                    IsDirty = true;
                }
            }
        }

        public int SystemMonitorInterval
        {
            get => _systemMonitorInterval;
            set
            {
                if (_systemMonitorInterval != value)
                {
                    _systemMonitorInterval = value;
                    OnPropertyChanged(nameof(SystemMonitorInterval));
                    IsDirty = true;
                }
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        public string AppName => _appName;
        public string AppVersion => _appVersion;
        public string Author => _author;
        public string RepoUrl => _repoUrl;

        public ICommand NavCommand { get; }
        public ICommand AddRedirectCommand { get; }
        public ICommand RemoveRedirectCommand { get; }
        public ICommand ReindexCommand { get; }
        public ICommand OpenRepoCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand SaveCommand { get; }

        public SettingsViewModel()
        {
            LoadSettings();
            NavCommand = new RelayCommand(param => CurrentSection = param as string ?? "General");
            AddRedirectCommand = new RelayCommand(AddRedirect);
            RemoveRedirectCommand = new RelayCommand(RemoveRedirect);
            ReindexCommand = new RelayCommand(Reindex);
            OpenRepoCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }));
            OpenHelpCommand = new RelayCommand(_ => OpenHelp());
            SaveCommand = new RelayCommand(_ => SaveSettings(), _ => IsDirty);
            
            Redirects.CollectionChanged += (s, e) => IsDirty = true;
        }

        private void OpenHelp()
        {
            try
            {
                string helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "index.html");
                if (File.Exists(helpPath))
                {
                    Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("Help file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open help: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSettings()
        {
            _autoStart = SettingsManager.Current.AutoStart;
            _enableSystemMonitoring = SettingsManager.Current.EnableSystemMonitoring;
            _systemMonitorInterval = SettingsManager.Current.SystemMonitorInterval;
            
            Redirects.Clear();
            foreach (var r in SettingsManager.Current.Redirects)
            {
                Redirects.Add(r);
            }
            IsDirty = false;
        }

        private bool CheckAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("WannaTool") != null;
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (File.Exists(exePath))
                    {
                        key.SetValue("WannaTool", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("WannaTool", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change startup settings: {ex.Message}");
            }
        }

        private void AddRedirect(object? parameter)
        {
            var newRedirect = new WebRedirect { Name = "New Search", Trigger = "new", UrlTemplate = "https://example.com?q={0}" };
            Redirects.Add(newRedirect);
            IsDirty = true;
        }

        private void RemoveRedirect(object? parameter)
        {
            if (parameter is WebRedirect r)
            {
                Redirects.Remove(r);
                IsDirty = true;
            }
        }

        private void SaveSettings()
        {
            SettingsManager.Current.AutoStart = AutoStart;
            SettingsManager.Current.EnableSystemMonitoring = EnableSystemMonitoring;
            SettingsManager.Current.SystemMonitorInterval = SystemMonitorInterval;
            
            SettingsManager.Current.Redirects.Clear();
            SettingsManager.Current.Redirects.AddRange(Redirects);
            SettingsManager.Save();
            IsDirty = false;
            
            SystemMetricsService.Instance.SetInterval(SystemMonitorInterval);
            if (!EnableSystemMonitoring)
            {
                SystemMetricsService.Instance.Stop();
            }

            MessageBox.Show("Settings saved successfully.", "WannaTool", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Reindex(object? parameter)
        {
            await Indexer.BuildIndexAsync();
            MessageBox.Show("Index rebuilt successfully.", "WannaTool", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
