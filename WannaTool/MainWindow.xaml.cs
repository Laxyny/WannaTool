using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using NHotkey;
using NHotkey.Wpf;
using MessageBox = System.Windows.MessageBox;

namespace WannaTool
{
    public partial class MainWindow : Window
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int VK_SPACE = 0x20;
        private const int HOTKEY_ID = 9000;

        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            this.Loaded += OnLoaded;
            this.Deactivated += (s, e) => this.Hide();
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
                this.Show();
                this.Activate();
                this.Topmost = true;
                SearchBox.Focus();
                SearchBox.SelectAll();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            base.OnClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
