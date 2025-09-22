using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using DivaModManager;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 定義定数
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_NOZORDER = 0x0004;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.ToList().Contains("-debug"))
                Global.DEBUG_MODE = Debug.DEBUG_MODE.DEBUG;
            else if (e.Args.ToList().Contains("-developer"))
                Global.DEBUG_MODE = Debug.DEBUG_MODE.DEVELOPER;
            this.Exit += App_Exit;
            Global.config = new();

            TextLogger.Log += ObjectDumper.Dump(e.Args, "e.Args");
            TextLogger.Log += ObjectDumper.Dump(Global.config, "Global.config");

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            RegistryConfig.InstallGBHandler();

            bool noneOtherProcess = IsNoneOrKilledAlreadyRunningOtherProcess();

            if (noneOtherProcess)
            {
                DivaModManager.UI.MainWindow mw = new(e);

                // check arguments
                var argsIndex = e.Args.ToList().IndexOf("-download");
                if (argsIndex != -1)
                {
                    new ModDownloader().Download(e.Args[argsIndex + 1], true);
                }
                else
                {
                    mw.Show();
                }
            }
            else
            {
                Environment.Exit(0);
            }
        }

        protected static bool IsNoneOrKilledAlreadyRunningOtherProcess()
        {
            var ret = true;
            List<Process> otherProsessList = new();

            // Getting collection of process  
            Process currentProcess = Process.GetCurrentProcess();

            // Check with other process already running   
            foreach (var p in Process.GetProcesses())
            {
                if (p.Id != currentProcess.Id) // Check running process   
                {
                    if (p.ProcessName.Equals(currentProcess.ProcessName))
                    {
                        if (p.MainModule.FileName.Equals(currentProcess.MainModule.FileName))
                        {
                            otherProsessList.Add(p);
                        }
                    }
                }
            }
            if (otherProsessList.Count != 0)
            {
                foreach (Process p in otherProsessList)
                {
                    var cnt = 0;
                    if (!p.Responding)
                    {
                        cnt++;
                        var resMessageBox = MessageBox.Show($"Diva Mod Manager by Enomoto ({cnt}) is already running but is not responding.\nDo you want to force quit it?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
                        if (resMessageBox == MessageBoxResult.OK)
                        {
                            try
                            {
                                p.Kill();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Couldn't kill the process ({ex.Message})", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    // 応答しているプロセスが存在する場合、アクティブにして画面内に移動
                    else
                    {
                        ActiveAndMoveRunningProcess(p);
                        ret = false; // 応答するプロセスが存在
                    }
                }
            }
            return ret;
        }

        private static void ActiveAndMoveRunningProcess(Process p)
        {
            MessageBox.Show("Diva Mod Manager by Enomoto is already running", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            Microsoft.VisualBasic.Interaction.AppActivate(p.Id);
            IntPtr mainWindowHandle = p.MainWindowHandle;

            if (mainWindowHandle != IntPtr.Zero)
            {
                //p.WaitForInputIdle();
                using DivaModManager.UI.MainWindow mainWindow = new();
                var x = (SystemParameters.PrimaryScreenWidth / 2) - (mainWindow.MinWidth / 2);
                var y = (SystemParameters.PrimaryScreenHeight / 2) - (mainWindow.MinHeight / 2);

                // ウィンドウを最前面に移動し、表示状態にする
                // IntPtr.Zero後の引数が座標X、Y
                SetWindowPos(mainWindowHandle, IntPtr.Zero, (int)x, (int)y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var message = $"Unhandled exception occured:\n{e.Exception.Message}\n\nInner Exception:\n{e.Exception.InnerException}" +
                $"\n\nStack Trace:\n{e.Exception.StackTrace}\n";

            if (Global.DEBUG_MODE == Debug.DEBUG_MODE.NORMAL)
            {
                // 画面上にメッセージを出力して続行
                Global.logger.WriteLine(message, LoggerType.Critical);
                e.Handled = true;
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ((DivaModManager.UI.MainWindow)Current.MainWindow).IsEnabled = true;
                });
            }
            else
            {
                // メッセージを表示して終了
                MessageBox.Show(message, "Critical", MessageBoxButton.OK, MessageBoxImage.Error);
                TextLogger.Log += message;
                Environment.Exit(0);
            }
        }
    }
}
