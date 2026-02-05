using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NHotkey;
using NHotkey.Wpf;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WannaTool
{
    public partial class MainWindow : Window
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int VK_SPACE = 0x20;
        private const int HOTKEY_ID = 9000;

        private MainViewModel _viewModel;
        private Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            InitializeTrayIcon();

            this.Loaded += OnLoaded;
            this.Deactivated += (s, e) => this.Hide();
            this.Closing += OnClosing;
            this.IsVisibleChanged += (s, e) => 
            {
                _viewModel.OnVisibilityChanged(this.IsVisible);
            };
            
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    this.Hide();
                }
            };
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Text = "WannaTool"
            };

            try
            {
                var iconUri = new Uri("pack://application:,,,/WannaToolIcon.ico");
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    _notifyIcon.Icon = new Drawing.Icon(streamInfo.Stream);
                }
                else
                {
                    _notifyIcon.Icon = Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = Drawing.SystemIcons.Application;
            }

            _notifyIcon.DoubleClick += (s, e) => ToggleVisibility();

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Settings", null, (s, e) => OpenSettings());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());
            
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("CapturePrimary", Key.F5, ModifierKeys.Control | ModifierKeys.Shift, OnCapturePrimary);
                HotkeyManager.Current.AddOrReplace("CaptureAll", Key.F6, ModifierKeys.Control | ModifierKeys.Shift, OnCaptureAll);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registering hotkeys: {ex.Message}");
            }

            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
            
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_ALT, VK_SPACE);

            this.Hide();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                _notifyIcon?.Dispose();
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
            }
        }

        private void ExitApp()
        {
            _isExiting = true;
            Application.Current.Shutdown();
        }

        private void OnCapturePrimary(object? sender, HotkeyEventArgs e)
        {
            ScreenCaptureManager.CapturePrimary();
            e.Handled = true;
        }

        private void OnCaptureAll(object? sender, HotkeyEventArgs e)
        {
            ScreenCaptureManager.CaptureAll();
            e.Handled = true;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleVisibility();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ToggleVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                ShowWindow();
            }
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        private void ShowWindow()
        {
            GetCursorPos(out var pt);
            var screen = Forms.Screen.FromPoint(new Drawing.Point(pt.X, pt.Y));
            var workingArea = screen.WorkingArea;

            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
            if (transform.HasValue)
            {
                var dpiScaleX = transform.Value.M11;
                var dpiScaleY = transform.Value.M22;

                double winW = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                double winH = this.ActualHeight > 0 ? this.ActualHeight : 60;
                double workLeft = workingArea.Left * dpiScaleX;
                double workTop = workingArea.Top * dpiScaleY;
                double workWidth = workingArea.Width * dpiScaleX;
                double workHeight = workingArea.Height * dpiScaleY;

                this.Left = workLeft + (workWidth - winW) / 2;
                this.Top = workTop + (workHeight - winH) / 2; 
            }
            else
            {
                this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
                this.Top = workingArea.Top + (workingArea.Height - this.Height) / 2;
            }

            this.Show();
            this.Activate();
            this.Topmost = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        private void ResultsListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = (System.Windows.Controls.ListBox)sender;
            var element = e.OriginalSource as DependencyObject;
            while (element != null)
            {
                if (element is ListBoxItem item)
                {
                    listBox.SelectedItem = item.DataContext;
                    break;
                }
                element = VisualTreeHelper.GetParent(element);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
