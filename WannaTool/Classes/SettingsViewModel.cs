using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;

namespace WannaTool
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private bool _autoStart;
        private string _appName = "WannaTool";
        private string _appVersion = "v0.2.0-alpha";
        private string _author = "Kevin GREGOIRE";
        private string _repoUrl = "https://github.com/Laxyny/WannaTool";
        
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
                    SettingsManager.Save();
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

        public SettingsViewModel()
        {
            LoadSettings();
            NavCommand = new RelayCommand(param => CurrentSection = param as string ?? "General");
            AddRedirectCommand = new RelayCommand(AddRedirect);
            RemoveRedirectCommand = new RelayCommand(RemoveRedirect);
            ReindexCommand = new RelayCommand(Reindex);
            OpenRepoCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }));
        }

        private void LoadSettings()
        {
            _autoStart = CheckAutoStart();
            
            Redirects.Clear();
            foreach (var r in SettingsManager.Current.Redirects)
            {
                Redirects.Add(r);
            }
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
            SettingsManager.Current.Redirects.Add(newRedirect);
            Redirects.Add(newRedirect);
            SettingsManager.Save();
        }

        private void RemoveRedirect(object? parameter)
        {
            if (parameter is WebRedirect r)
            {
                SettingsManager.Current.Redirects.Remove(r);
                Redirects.Remove(r);
                SettingsManager.Save();
            }
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
