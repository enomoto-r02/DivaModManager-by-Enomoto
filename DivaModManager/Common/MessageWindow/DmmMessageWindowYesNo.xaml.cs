using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Interaction logic for DmmMessageWindowYesNo.xaml
    /// </summary>
    public partial class DmmMessageWindowYesNo : Window
    {
        public bool YesNo = false;
        public bool IsCancel = true;

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
        public DmmMessageWindowYesNo(string strInfo, string strText, string title, bool yesno = false)
        {
            InitializeComponent();

            MessageInfo.Text = strInfo;
            if (string.IsNullOrEmpty(MessageInfo.Text)) MessageInfo.Visibility = Visibility.Collapsed;
            MessageText.Text = strText;
            if (string.IsNullOrEmpty(MessageText.Text)) MessageText.Visibility = Visibility.Collapsed;
            YesNo = yesno;
            Title = title;

            Activate();
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
