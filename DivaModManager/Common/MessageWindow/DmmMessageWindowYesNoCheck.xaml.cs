using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow;

/// <summary>
/// Interaction logic for DmmMessageWindowYesNoCheck.xaml
/// </summary> 
public partial class DmmMessageWindowYesNoCheck : Window
{
    public bool YesNo = false;
    public bool IsCancel = true;
    public bool Checked = false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="strInfo"></param>
    /// <param name="strText"></param>
    /// <param name="title"></param>
    /// <param name="yesno"></param>
    /// <param name="check"></param>
    /// 右上の×ボタン等で閉じられた時はコンストラクタのパラメータが設定される
    /// YesNoの初期値に注意！
    /// (コンストラクタで未設定の場合は両方false)
    public DmmMessageWindowYesNoCheck(string strInfo, string strText, string title, bool yesno = false, bool check = false)
    {
        InitializeComponent();

        MessageInfo.Text = strInfo;
        if (string.IsNullOrEmpty(MessageInfo.Text)) MessageInfo.Visibility = Visibility.Collapsed;
        MessageText.Text = strText;
        if (string.IsNullOrEmpty(MessageText.Text)) MessageText.Visibility = Visibility.Collapsed;
        YesNo = yesno;
        Checked = check;
        Title = title;

        Activate();
    }
    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        YesNo = true;
        IsCancel = false;
        Checked = (bool)Check_1.IsChecked;
        Close();
    }
    private void No_Click(object sender, RoutedEventArgs e)
    {
        YesNo = false;
        IsCancel = false;
        Checked = (bool)Check_1.IsChecked;
        Close();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            YesNo = false;
            IsCancel = true;
            Checked = false;
            Close();
        }
    }
}
