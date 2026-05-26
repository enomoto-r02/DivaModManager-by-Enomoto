using DivaModManager.Common.Helpers;
using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Interaction logic for DmmMessageWindowYesNoOpen.xaml
    /// </summary>
    public partial class DmmMessageWindowYesNoOpen : Window
    {
        public bool YesNo = false;
        public bool IsCancel = true;
        public string Path = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strInfo"></param>
        /// <param name="strText"></param>
        /// <param name="title"></param>
        /// <param name="yesno"></param>
        /// 右上の×ボタン等で閉じられた時はコンストラクタのパラメータが設定される
        /// YesNoの初期値に注意！
        /// (コンストラクタで未設定の場合はfalse)
        public DmmMessageWindowYesNoOpen(string strInfo, string strText, string title, string path, bool yesno = false)
        {
            InitializeComponent();

            MessageInfo.Text = strInfo;
            if (string.IsNullOrEmpty(MessageInfo.Text)) MessageInfo.Visibility = Visibility.Collapsed;
            MessageText.Text = strText;
            if (string.IsNullOrEmpty(MessageText.Text)) MessageText.Visibility = Visibility.Collapsed;
            YesNo = yesno;
            Title = title;
            Path = path;
        }
        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            YesNo = true;
            IsCancel = false;
            Close();
        }
        private void No_Click(object sender, RoutedEventArgs e)
        {
            YesNo = false;
            IsCancel = false;
            Close();
        }
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.TryStartProcess(Path);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                YesNo = false;
                IsCancel = true;
                Close();
            }
        }
    }
}
