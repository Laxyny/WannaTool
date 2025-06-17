using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using System.IO;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Microsoft.Toolkit.Uwp.Notifications;
using NHotkey.Wpf;
using NHotkey;
using DotNetEnv;



namespace WannaTool
{
    public partial class MainWindow : Window
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int VK_F9 = 0x78;
        private const int VK_SPACE = 0x20;

        public MainWindow()
        {
            InitializeComponent();

            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;
            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;

            SearchBox.PreviewKeyDown += SearchBox_KeyDown;

            ApplyBlurEffect();
            SearchBox.Focus();

            ShowLoadingIndicatorWhileIndexing();

            try
            {
                HotkeyManager.Current.AddOrReplace("CapturePrimary", Key.F5, ModifierKeys.Control | ModifierKeys.Shift, OnCapturePrimary);
                HotkeyManager.Current.AddOrReplace("CaptureAll", Key.F6, ModifierKeys.Control | ModifierKeys.Shift, OnCaptureAll);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'enregistrement des raccourcis : {ex.Message}");
            }
        }

        private void OnCapturePrimary(object sender, HotkeyEventArgs e)
        {
            ScreenCaptureManager.CapturePrimary();
            e.Handled = true;
        }

        private void OnCaptureAll(object sender, HotkeyEventArgs e)
        {
            ScreenCaptureManager.CaptureAll();
            e.Handled = true;
        }

        private async void ShowLoadingIndicatorWhileIndexing()
        {
            LoadingIndicator.Visibility = Visibility.Visible;

            await Task.Run(() => Indexer.InitializeAsync());

            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowToast(string title, string message)
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);

            if (source != null)
            {
                source.AddHook(WndProc);

                bool registered = RegisterHotKey(helper.Handle, 9000, MOD_ALT, VK_SPACE);
                if (!registered)
                {
                    MessageBox.Show("Failed to register hotkey.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Ne cacher la fenêtre qu'après le hook ET le register
                Dispatcher.BeginInvoke(new Action(() => this.Hide()), DispatcherPriority.ApplicationIdle);
            }
            else
            {
                MessageBox.Show("Impossible d'obtenir le handle de la fenêtre.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsList.HasItems)
            {
                var first = ResultsList.Items[0] as SearchResult;
                if (first != null)
                {
                    await ExecuteSearchResult(first);
                    ResultsList.SelectedItem = null;
                }
                e.Handled = true;
                return;
            }

            if ((e.Key == Key.Up || e.Key == Key.Down) && ResultsList.HasItems)
            {
                // choisir l'index
                int idx = e.Key == Key.Down ? 0 : ResultsList.Items.Count - 1;
                ResultsList.SelectedIndex = idx;

                // récupérer le conteneur et y mettre le focus clavier
                var container = ResultsList.ItemContainerGenerator
                                   .ContainerFromIndex(idx) as ListBoxItem;
                if (container != null)
                {
                    container.IsSelected = true;
                    container.Focus();
                    Keyboard.Focus(container);
                }

                e.Handled = true;
                return;
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Hide();
            }
        }

        private async void ResultsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsList.SelectedItem is SearchResult selected)
            {
                await ExecuteSearchResult(selected);
                ResultsList.SelectedItem = null;
                this.Hide();
            }
        }

        private DateTime _lastHotkeyPress = DateTime.MinValue; // Stocke le dernier moment où le hotkey a été pressé
        private readonly TimeSpan _hotkeyCooldown = TimeSpan.FromMilliseconds(500); // Cooldown de 500ms

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (DateTime.Now - _lastHotkeyPress < _hotkeyCooldown)
                {
                    handled = true;
                    return IntPtr.Zero;
                }

                _lastHotkeyPress = DateTime.Now;


                if (this.Visibility == Visibility.Visible)
                {
                    var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                    fadeOut.Completed += (s, e) =>
                    {
                        this.Hide();
                        ResultsList.ItemsSource = null;
                        ResultsList.Visibility = Visibility.Collapsed;
                        SearchBox.Text = string.Empty;
                    };
                    this.BeginAnimation(Window.OpacityProperty, fadeOut);
                }
                else
                {
                    this.Opacity = 0;
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    this.Topmost = true;
                    var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                    this.BeginAnimation(Window.OpacityProperty, fadeIn);
                }

                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, 9000);
            base.OnClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ResultsList == null) return;

            var query = SearchBox.Text.Trim();
            if (query.Length < 2)
            {
                ResultsList.Visibility = Visibility.Collapsed;
                ResultsList.ItemsSource = null;
                GC.Collect();
                return;
            }


            var colon = query.IndexOf(':');
            if (colon > 0 && colon < query.Length - 1)
            {
                var prefix = query[..colon].ToLowerInvariant();
                var term = query[(colon + 1)..].Trim();
                if (!string.IsNullOrEmpty(term))
                {
                    string[] exts = prefix switch
                    {
                        "pdf" => new[] { ".pdf" },
                        "img" => new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" },
                        "code" => new[] { ".cs", ".js", ".ts", ".html", ".css", ".json", ".xml", ".cpp", ".java" },
                        _ => null
                    };
                    if (exts != null)
                    {
                        var filtered = Indexer.Search(term, exts);
                        if (filtered.Any())
                        {
                            ResultsList.ItemsSource = filtered;
                            ResultsList.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }
            }

            var redirect = SearchRedirects.TryParseRedirect(query);
            if (redirect != null)
            {
                ResultsList.ItemsSource = new List<SearchResult> { redirect };
                ResultsList.Visibility = Visibility.Visible;
                return;
            }

            var results = Indexer.Search(query);

            if (!results.Any())
            {


                results.Add(new SearchResult
                {
                    DisplayName = $"Search Google for \"{query}\"",
                    FullPath = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
                });
            }

            ResultsList.ItemsSource = results;
            ResultsList.Visibility = Visibility.Visible;
        }

        public class SearchResult : INotifyPropertyChanged
        {
            public string DisplayName { get; set; }
            public string FullPath { get; set; }
            public bool IsFolder { get; set; }

            private ImageSource? _icon;
            public ImageSource? Icon
            {
                get
                {
                    if (_icon == null)
                        _icon = IconLoader.GetIcon(FullPath, IsFolder);
                    return _icon;
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }

            private bool _isRenaming;
            public bool IsRenaming
            {
                get => _isRenaming;
                set { _isRenaming = value; OnPropertyChanged(nameof(IsRenaming)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static readonly HttpClient _httpClient = new();

        private void SafeSetClipboardText(string text)
        {
            int retries = 10;
            int delay = 50;

            while (retries > 0)
            {
                try
                {
                    Clipboard.SetText(text);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    retries--;
                    Thread.Sleep(delay);
                }
            }

            MessageBox.Show("Impossible d'accéder au presse-papier. Réessaie ou ferme les applications qui pourraient le bloquer.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private void ApplyBlurEffect()
        {
            var windowHelper = new WindowInteropHelper(this);
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND
            };

            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Process.Start(new ProcessStartInfo(selected.FullPath) { UseShellExecute = true });
                this.Hide();
            }
        }
        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.FullPath}\""));
                this.Hide();
            }
        }
        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Clipboard.SetText(selected.FullPath);
            }
        }
        private void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                this.Hide();
                if (MessageBox.Show($"Êtes-vous sûr de vouloir supprimer {selected.DisplayName} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(selected.FullPath);
                        MessageBox.Show($"{selected.DisplayName} a été supprimé avec succès.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la suppression : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            if (ResultsList.SelectedItem is not SearchResult selected) return;

            string currentName = Path.GetFileName(selected.FullPath);
            string input = Microsoft.VisualBasic.Interaction.InputBox("Entrez le nouveau nom :", "Renommer", currentName);

            if (string.IsNullOrWhiteSpace(input)) return;

            string newPath = Path.Combine(Path.GetDirectoryName(selected.FullPath), input);

            try
            {
                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    MessageBox.Show("Un fichier ou dossier portant ce nom existe déjà.", "Conflit", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                File.Move(selected.FullPath, newPath);
                selected.FullPath = newPath;
                selected.DisplayName = input;

                ResultsList.Items.Refresh();

                Utils.ShowToast("Fichier renommé", $"{currentName} renommé en {input}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Renommage", MessageBoxButton.OK, MessageBoxImage.Error);
                Utils.ShowToast("Erreur renommage", ex.Message, true);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        private void ShowProperties_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                try
                {
                    SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
                    info.cbSize = Marshal.SizeOf(info);
                    info.lpFile = selected.FullPath;
                    info.lpVerb = "properties";
                    info.nShow = 5; // SW_SHOW
                    info.fMask = 0x0000000C; // SEE_MASK_INVOKEIDLIST | SEE_MASK_FLAG_NO_UI

                    ShellExecuteEx(ref info);
                    this.Hide();

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de l'ouverture des propriétés : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    Utils.ShowToast("Erreur", $"Erreur propriétés : {ex.Message}", true);
                }
            }
        }

        private async Task ExecuteSearchResult(SearchResult selected)
        {
            if (selected.FullPath == "!help")
            {
                string helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "index.html");
                if (File.Exists(helpPath))
                {
                    Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
                    this.Hide();
                }
                else
                {
                    MessageBox.Show("Le fichier d'aide est introuvable.");
                }
                return;
            }

            if (selected.FullPath.StartsWith("http"))
            {
                Process.Start(new ProcessStartInfo(selected.FullPath) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(selected.FullPath) { UseShellExecute = true });
            }

            ResultsList.SelectedItem = null;
            this.Hide();
        }

    }

    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is string str && !string.IsNullOrEmpty(str))
                {
                    return Visibility.Visible;
                }
                return Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public class StringNotNullOrEmptyToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
}