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
            Task.Run(() => Indexer.InitializeAsync());

            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;
            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;

            ApplyBlurEffect();
            SearchBox.Focus();

            ShowLoadingIndicatorWhileIndexing();
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

        private async void LoadIconForResult(SearchResult result)
        {
            if (result.Icon != null) return; // Déjà chargé

            await Task.Run(() =>
            {
                try
                {
                    var shinfo = new SHFILEINFO();
                    IntPtr hImg = SHGetFileInfo(result.FullPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x000000100 | 0x000000001);

                    if (hImg != IntPtr.Zero)
                    {
                        var icon = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        icon.Freeze(); // Important pour éviter des erreurs threading

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            result.Icon = icon;
                        });
                    }
                }
                catch
                {
                    // Ignore erreurs fichiers non trouvés
                }
            });
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

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Escape)
            {
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
                    handled = true; // Ignorez l'événement si le cooldown n'est pas terminé
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

                        Indexer.ClearIndex();

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
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
                return;
            }

            if (!Indexer.IsReady)
            {
                await Indexer.LoadIndexAsync();
                Indexer.MarkAsReady();
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

            foreach (var item in results)
            {
                LoadIconForResult(item);
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem == null) return;

            var selected = ResultsList.SelectedItem as SearchResult;
            if (selected == null) return;

            if (selected.FullPath.StartsWith("http"))
            {
                Process.Start(new ProcessStartInfo(selected.FullPath)
                {
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo(selected.FullPath)
                {
                    UseShellExecute = true
                });
            }

            this.Hide();
        }

        public class SearchResult
        {
            public string DisplayName { get; set; }
            public string FullPath { get; set; }
            public ImageSource Icon { get; set; }
            public bool IsFolder { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
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

        // Menu contextuel des différents éléments de la liste
        /*
        
        <MenuItem Header="Ouvrir" Click="OpenFile_Click"/>
                                    <MenuItem Header="Ouvrir l'emplacement" Click="OpenLocation_Click"/>
                                    <MenuItem Header="Copier le chemin" Click="CopyPath_Click"/>
                                    <MenuItem Header="Supprimer" Click="DeleteFile_Click"/>
                                    <MenuItem Header="Renommer" Click="RenameFile_Click"/>
                                    <MenuItem Header="Propriétés" Click="ShowProperties_Click"/>
        
        */

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Process.Start(new ProcessStartInfo(selected.FullPath) { UseShellExecute = true });
            }
        }
        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.FullPath}\""));
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
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Entrez le nouveau nom :", "Renommer le fichier", selected.DisplayName);
                if (!string.IsNullOrEmpty(newName))
                {
                    try
                    {
                        string newPath = Path.Combine(Path.GetDirectoryName(selected.FullPath), newName);
                        File.Move(selected.FullPath, newPath);
                        MessageBox.Show($"{selected.DisplayName} a été renommé en {newName}.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors du renommage : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void ShowProperties_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResult selected)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.FullPath}\""));
            }
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