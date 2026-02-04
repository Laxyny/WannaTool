using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace WannaTool
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
            this.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) this.DragMove(); };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && vm.IsDirty)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save them before closing?", 
                    "Unsaved Changes", 
                    MessageBoxButton.YesNoCancel, 
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    vm.SaveCommand.Execute(null);
                    this.Close();
                }
                else if (result == MessageBoxResult.No)
                {
                    this.Close();
                }
            }
            else
            {
                this.Close();
            }
        }
    }
}
