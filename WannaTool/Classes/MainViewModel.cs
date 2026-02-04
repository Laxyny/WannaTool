using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace WannaTool
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _searchText = "";
        private SearchResult? _selectedResult;
        private bool _isLoading;
        private bool _hasResults;
        private CancellationTokenSource? _searchCts;

        public ObservableCollection<SearchResult> Results { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    DebounceSearch();
                }
            }
        }

        public SearchResult? SelectedResult
        {
            get => _selectedResult;
            set
            {
                _selectedResult = value;
                OnPropertyChanged(nameof(SelectedResult));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public bool HasResults
        {
            get => _hasResults;
            set
            {
                if (_hasResults != value)
                {
                    _hasResults = value;
                    OnPropertyChanged(nameof(HasResults));
                }
            }
        }

        public ICommand ExecuteCommand { get; }
        public ICommand NextResultCommand { get; }
        public ICommand PreviousResultCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand OpenLocationCommand { get; }
        public ICommand ExitCommand { get; }

        public MainViewModel()
        {
            ExecuteCommand = new RelayCommand(Execute);
            NextResultCommand = new RelayCommand(_ => MoveSelection(1));
            PreviousResultCommand = new RelayCommand(_ => MoveSelection(-1));
            CopyPathCommand = new RelayCommand(CopyPath);
            OpenLocationCommand = new RelayCommand(OpenLocation);
            ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
            
            _ = Indexer.InitializeAsync();
        }

        private async void DebounceSearch()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                PerformSearch();
            }
            catch (TaskCanceledException) { }
        }

        private void PerformSearch()
        {
            var query = SearchText.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                UpdateResults(new List<SearchResult>());
                return;
            }

            var redirect = SearchRedirects.TryParseRedirect(query);
            if (redirect != null)
            {
                UpdateResults(new List<SearchResult> { redirect });
                return;
            }

            var colon = query.IndexOf(':');
            List<SearchResult> results;

            if (colon > 0 && colon < query.Length - 1)
            {
                var prefix = query[..colon].ToLowerInvariant();
                var term = query[(colon + 1)..].Trim();
                
                string[]? exts = prefix switch
                {
                    "pdf" => new[] { ".pdf" },
                    "img" => new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" },
                    "code" => new[] { ".cs", ".js", ".ts", ".html", ".css", ".json", ".xml", ".cpp", ".java" },
                    "doc" => new[] { ".docx", ".doc", ".txt", ".rtf" },
                    "exe" => new[] { ".exe", ".lnk" },
                    _ => null
                };

                if (exts != null && !string.IsNullOrEmpty(term))
                {
                    results = Indexer.Search(term, exts);
                }
                else
                {
                    results = Indexer.Search(query);
                }
            }
            else
            {
                results = Indexer.Search(query);
            }

            if (!results.Any())
            {
                results.Add(new SearchResult
                {
                    DisplayName = $"Search Google for \"{query}\"",
                    FullPath = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                    IsFolder = false
                });
            }

            UpdateResults(results);
        }

        private void UpdateResults(List<SearchResult> newResults)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Results.Clear();
                foreach (var item in newResults)
                {
                    Results.Add(item);
                }

                HasResults = Results.Any();

                if (Results.Any())
                {
                    SelectedResult = Results.First();
                }
            });
        }

        private void MoveSelection(int direction)
        {
            if (!Results.Any()) return;

            int index = SelectedResult != null ? Results.IndexOf(SelectedResult) : -1;
            index += direction;

            if (index < 0) index = Results.Count - 1;
            if (index >= Results.Count) index = 0;

            SelectedResult = Results[index];
        }

        private void Execute(object? parameter)
        {
            var result = parameter as SearchResult ?? SelectedResult;
            if (result == null) return;

            try
            {
                if (result.FullPath == "!settings")
                {
                    var settingsWindow = new SettingsWindow();
                    settingsWindow.Show();
                }
                else if (result.FullPath == "!help")
                {
                     string helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "index.html");
                     if (File.Exists(helpPath))
                     {
                         Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
                     }
                }
                else if (result.FullPath == "!exit")
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    Process.Start(new ProcessStartInfo(result.FullPath) { UseShellExecute = true });
                }
                
                Application.Current.MainWindow.Hide();
                SearchText = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening: {ex.Message}");
            }
        }

        private void CopyPath(object? parameter)
        {
            if (parameter is SearchResult res)
            {
                try { Clipboard.SetText(res.FullPath); } catch { }
                Application.Current.MainWindow.Hide();
            }
        }

        private void OpenLocation(object? parameter)
        {
            if (parameter is SearchResult res)
            {
                try 
                { 
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{res.FullPath}\"")); 
                } 
                catch { }
                Application.Current.MainWindow.Hide();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
