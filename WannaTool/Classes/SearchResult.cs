using System.ComponentModel;
using System.Windows.Media;

namespace WannaTool
{
    public class SearchResult : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
