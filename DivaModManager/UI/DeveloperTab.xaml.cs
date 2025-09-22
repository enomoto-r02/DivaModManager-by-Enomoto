using System;
using System.Windows;
using System.Threading;
using System.Linq;
using System.Windows.Controls;

namespace DivaModManager.UI
{
    /// <summary>
    /// DeveloperTab.xaml の相互作用ロジック
    /// </summary>
    public partial class DeveloperTab : UserControl
    {
        public DeveloperTab()
        {
            InitializeComponent();
        }
        private void Hang_Click(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(Timeout.Infinite); // 無限ループで応答なし状態をシミュレート
        }
        // 画面外に移動
        private void WindowOut_Click(object sender, RoutedEventArgs e)
        {
            var window = (MainWindow)Window.GetWindow(this);
            window.Left = -1000; window.Top = -1000;
        }
        private void CmdGet_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
