using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Interaction logic for DmmInfoWindowOK.xaml
    /// </summary>
    public partial class DmmInfoWindowOK : Window
    {
        public bool OK = false;
        public bool IsCancel = true;

        public DmmInfoWindowOK(string strInfo, string strText, string title, bool ok = false)
        {
            InitializeComponent();

            InfoText.Text = strInfo;
            if (string.IsNullOrEmpty(InfoText.Text)) InfoText.Visibility = Visibility.Collapsed;
            OK = ok;
            Title = title;

            Activate();
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            OK = true;
            IsCancel = false;
            Close();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OK = false;
                IsCancel = true;
                Close();
            }
        }
    }
}
