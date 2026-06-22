using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.DML;
using DivaModManager.Features.MikuMikuLibrary;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

// 実装上、MainWindowのnamespaceに合わせる
namespace DivaModManager.Features.Debug
{
    /// <summary>
    /// DebugTab.xaml
    /// </summary>
    public partial class DebugTab : UserControl
    {
        public DebugTab()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 無限ループで応答なし状態をシミュレート
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hang_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This Tool will be Hang Up!  Is it OK?", "Message", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                Thread.Sleep(Timeout.Infinite);
            }
        }
        /// <summary>
        /// 画面外に移動させる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowOut_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This Tool will be Window Out. Is it OK?", "Message", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                var window = (MainWindow)Window.GetWindow(this);
                window.Left = -1000; window.Top = -1000;
            }
        }
        /// <summary>
        /// テキストログにGlobal.ModList_Allのダンプを出力する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Global_ModList_Dump_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);
            Logger.WriteLine(string.Join(" ", MeInfo, $"Dump:\n{ObjectDumper.Dump(Global.ModList_All, "Global.ModList_All")}"), LoggerType.Debug, param: ParamInfo);
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            MessageBox.Show("Please exit this tool!", "Message");
        }

        private void SettingFilesOpen_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.TryStartProcess(ConfigJson.CONFIG_JSON_PATH);
            ProcessHelper.TryStartProcess(ConfigTomlDmm.CONFIG_E_TOML_PATH);
            ProcessHelper.TryStartProcess(ModLoader.CONFIG_TOML_PATH);
        }

        /// <summary>
        /// MikuMikuLibrary動作確認用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MMLLoadButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);
            MikuMikuLibraryHelper.Test_Extract();
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);


        }
    }
}
