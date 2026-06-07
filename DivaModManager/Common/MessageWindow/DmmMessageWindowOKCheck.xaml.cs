using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Interaction logic for DmmMessageWindowOK.xaml
    /// </summary>
    public partial class DmmMessageWindowOKCheck : Window
    {
        public bool OK = false;
        public bool IsCancel = true;
        public bool Checked = false;

        public DmmMessageWindowOKCheck(string strInfo, string strText, string title, bool ok = false)
        {
            InitializeComponent();

            MessageInfo.Text = strInfo;
            if (string.IsNullOrEmpty(MessageInfo.Text)) MessageInfo.Visibility = Visibility.Collapsed;
            MessageText.Text = strText;
            if (string.IsNullOrEmpty(MessageText.Text)) MessageText.Visibility = Visibility.Collapsed;
            Title = title;

            if (Global.IsWine)
                Check_1.Background = System.Windows.Media.Brushes.Transparent;
            else
                Check_1.Background = System.Windows.Media.Brushes.White;

            OK = ok;

            Activate();
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            OK = true;
            Checked = (bool)Check_1.IsChecked;
            IsCancel = false;
            Close();
        }
        private void Check_Click(object sender, RoutedEventArgs e)
        {
            InvalidateVisual();
            UpdateLayout();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OK = false;
                Checked = false;
                IsCancel = true;
                Close();
            }
        }
    }
}
