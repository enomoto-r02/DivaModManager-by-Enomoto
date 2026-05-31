using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Common.MessageWindow;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Download;
using DivaModManager.Features.Extract;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DivaModManager
{
    public partial class App : Application
    {
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static readonly string TestVersion = " for Linux/Wine (alpha)";

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 定義定数
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_NOZORDER = 0x0004;

        //public App()
        //{
        //    this.DispatcherUnhandledException += (s, e) =>
        //    {
        //        MessageBox.Show(e.Exception.ToString(), "UnhandledException");
        //        e.Handled = true;
        //    };
        //    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        //    {
        //        MessageBox.Show(e.ExceptionObject.ToString(), "DomainUnhandled");
        //    };
        //    TaskScheduler.UnobservedTaskException += (s, e) =>
        //    {
        //        MessageBox.Show(e.Exception.ToString(), "TaskUnhandled");
        //    };
        //}

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Logger.Init(e);
            var args = ObjectDumper.Dump(e.Args, "e.Args");
            Logger.WriteLine($"App.OnStartup Start. Version:{Version}", LoggerType.Debug, dump: args);

            // 正常終了時、異常終了時のイベントハンドラ
            Exit += App_Exit;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 設定ファイル初期化
            // 初期化順をミスるとNull Pointer Exceptionで落ちるので注意
            WindowListClass.InitWindowList();

            if (!InitMachineCheck())
                Environment.Exit(0);
            if (!TestVersionMessageView())
                Environment.Exit(0);

            ConfigJson.InitConfig();
            ConfigJson.SetupGame();

            ConfigTomlDmm.InitToml();

            SetLanguage();

            if ((int)Logger.Mode >= (int)Logger.DEBUG_MODE.DEBUG && Global.ConfigToml.DebugDialog)
            {
                var resWindow = WindowHelper.DMMWindowOpenAsync(42).Result;
                if (resWindow == WindowHelper.WindowCloseStatus.YesCheck) { Global.ConfigToml.DebugDialog = false; }
            }

            Extractor.InitSevenZipLocal();
            //Extractor.InitSevenZip();
            Extractor.InitWinRar();

            RegistryConfig.UpdateGBHandler();
            InitHttpClientSetting();

            // check arguments
            var argsIndex = e.Args.ToList().IndexOf("-download");

            var noneOtherProcess = -1;

            // -downloadの場合は既存プロセスチェックを行わない(起動中にワンクリックインストールを押したら落ちるので)
            if (argsIndex != -1)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    new ModDownloader().Download(e.Args[argsIndex + 1]);
                }
            }
            else
            {
                noneOtherProcess = IsAlreadyRunningOtherProcess();
            }

            if (noneOtherProcess == 0)
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                MainWindow mw = new(e);
                MainWindow = mw;
                mw.Show();
            }
            Logger.WriteLine($"App.OnStartup End.", LoggerType.Debug, dump: args);
        }


        public static int IsAlreadyRunningOtherProcess(bool killOtherProccess = true, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            List<Process> otherProsessList = new();

            // Getting collection of process  
            Process currentProcess = Process.GetCurrentProcess();

            // Check with other process already running   
            foreach (var p in Process.GetProcesses())
            {
                // Check running process
                if (p.Id != currentProcess.Id)
                {
                    if (p.ProcessName.Equals(currentProcess.ProcessName))
                    {
                        if (p.MainModule.FileName.Equals(currentProcess.MainModule.FileName))
                        {

                            if (killOtherProccess)
                            {
                                if (!KilledAlreadyRunningOtherProcess(caller))
                                {
                                    otherProsessList.Add(p);
                                }
                            }
                            else
                            {
                                otherProsessList.Add(p);
                            }
                        }
                    }
                }
            }

            //Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{otherProsessList.Count}"), LoggerType.Debug, param: ParamInfo);
            return otherProsessList.Count;
        }

        protected static bool KilledAlreadyRunningOtherProcess([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = true;
            List<Process> otherProsessList = new();

            // Getting collection of process  
            Process currentProcess = Process.GetCurrentProcess();

            // Check with other process already running   
            foreach (var p in Process.GetProcesses())
            {
                // Check running process
                if (p.Id != currentProcess.Id)
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
                var dumpStr = ObjectDumper.Dump(otherProsessList, "otherProsessList");
                var paramStr = $"[otherProsessList.Count:{otherProsessList.Count}]";
                Logger.WriteLine("", LoggerType.Debug, param: paramStr, dump: dumpStr);
                foreach (Process p in otherProsessList)
                {
                    var cnt = 0;
                    if (otherProsessList.Count > 0)
                    {
                        if (!p.Responding)
                        {
                            cnt++;
                            var resMessageBox = MessageBox.Show($"DivaModManager by Enomoto ({cnt}) is already running but is not responding.\nDo you want to force quit it?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
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
                        else
                        {
                            try
                            {
                                // 応答しているプロセスが存在する場合、アクティブにして画面内に移動
                                ActiveAndMoveRunningProcess(p);
                                Logger.WriteLine(string.Join(" ", MeInfo, "Other Process ActiveAndMoveRunningProcess."), LoggerType.Debug, param: ParamInfo);
                                ret = false;
                            }
                            catch (ArgumentException)
                            {
                                // 応答しているプロセスが見つからなかった場合、killする
                                p.Kill();
                                Logger.WriteLine(string.Join(" ", MeInfo, "Other Process Killed."), LoggerType.Debug, param: ParamInfo);
                            }
                        }
                    }
                }
            }
            //Logger.WriteLine(string.Join(" ", MeInfo, "End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        private static void ActiveAndMoveRunningProcess(Process p)
        {
            IntPtr mainWindowHandle = IntPtr.Zero;

            // 応答するが画面に表示されないプロセスはここでエラーになる
            Microsoft.VisualBasic.Interaction.AppActivate(p.Id);
            mainWindowHandle = p.MainWindowHandle;

            if (mainWindowHandle != IntPtr.Zero)
            {
                //p.WaitForInputIdle();

                MessageBox.Show("DivaModManager by Enomoto is already running", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                using MainWindow mainWindow = new();
                var x = (SystemParameters.PrimaryScreenWidth / 2) - (mainWindow.MinWidth / 2);
                var y = (SystemParameters.PrimaryScreenHeight / 2) - (mainWindow.MinHeight / 2);

                // ウィンドウを最前面に移動し、表示状態にする
                // IntPtr.Zero後の引数が座標X、Y
                SetWindowPos(mainWindowHandle, IntPtr.Zero, (int)x, (int)y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }
        }

        private void App_Exit(object sender, EventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            ConfigJson.UpdateConfig();
            Global.ConfigToml.Update();
            Global.GBclient.Dispose();
            Global.DMAclient.Dispose();
            Global.GitHubclient.Dispose();

#if DEBUG
#else
            Logger.WriteLine(string.Join(" ", MeInfo, $"Dump:\n{ObjectDumper.Dump(Global.ConfigJson, "Global.ConfigJson")}"), LoggerType.Debug, param: ParamInfo);
#endif
            Logger.WriteLine(string.Join(" ", MeInfo, $"Dump:\n{ObjectDumper.Dump(Global.ConfigToml, "Global.ConfigToml")}"), LoggerType.Debug, param: ParamInfo);
            Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug);
            Logger.WriteOut();
            Logger.OpenEditor();
            Environment.Exit(0);
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId},\n" +
                $"e.Exception.Message:{e.Exception.Message}\n" +
                $"e.Exception.Message:{e.Exception.Message},\n" +
                $"e.Exception.Demystify().StackTrace:{e.Exception.Demystify().StackTrace}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Info, param: ParamInfo);

            // メッセージを表示して終了
            var message = $"Save a screenshot of this message or contact the developer with the text you see after the process is complete.\n\n\nUnhandled exception occured:\n{e.Exception.Message}\n\nInner Exception:\n{e.Exception.Demystify().InnerException}" +
                $"\n\nStack Trace:\n{e.Exception.Demystify().StackTrace}\n";
            MessageBox.Show(message, "Critical", MessageBoxButton.OK, MessageBoxImage.Error);

            ConfigJson.UpdateConfig();
            Global.ConfigToml.Update();
            Global.GBclient.Dispose();
            Global.DMAclient.Dispose();
            Global.GitHubclient.Dispose();
#if DEBUG
#else
            var dumpJson = ObjectDumper.Dump(Global.ConfigJson, "Global.ConfigJson");
            Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug, dump: dumpJson);
#endif
            var dumpToml = ObjectDumper.Dump(Global.ConfigToml, "Global.ConfigToml");
            Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug, dump: dumpToml);
            Logger.WriteOut();
            Logger.OpenEditor();

            Environment.Exit(0);
        }

        /// <summary>
        /// 言語選択
        /// </summary>
        private static void SetLanguage()
        {
            if (Global.ConfigToml.LanguageDialog)
            {
                var ret = WindowHelper.DMMWindowOpenAsync(28);
                if (ret.Result == WindowHelper.WindowCloseStatus.Cancel)
                    Environment.Exit(0);
                else if (ret.Result == WindowHelper.WindowCloseStatus.Yes || ret.Result == WindowHelper.WindowCloseStatus.YesCheck)
                    Global.ConfigToml.Language = ConfigTomlDmm.Lang.JP.ToString().ToUpper();
                else
                    Global.ConfigToml.Language = ConfigTomlDmm.Lang.EN.ToString().ToUpper();
                if (ret.Result == WindowHelper.WindowCloseStatus.YesCheck || ret.Result == WindowHelper.WindowCloseStatus.NoCheck)
                    Global.ConfigToml.LanguageDialog = false;
            }
        }

        /// <summary>
        /// Windows(64bit)チェック
        /// </summary>
        /// <returns></returns>
        private static bool InitMachineCheck()
        {
            var ret = false;
            if (OperatingSystem.IsWindows())
            {
                if (!Environment.Is64BitOperatingSystem)
                    WindowHelper.MessageBoxOpen(45);
                else
                    ret = true;
            }
            else
            {
                ret = true;
            }

            return ret;
        }


        private static void InitHttpClientSetting()
        {
            string userAgent = Global.ConfigToml.BrowserUserAgent;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            Global.GBclient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            Global.GBclient.DefaultRequestHeaders.Referrer = new Uri("https://gamebanana.com/");
            Global.GBclient.DefaultRequestHeaders.CacheControl = new() { MaxAge = TimeSpan.FromSeconds(Global.ConfigToml.GameBananaCacheControlMaxAge) };
            Global.GBclient.Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GameBananaApiTimeoutSec);
            Global.DMAclient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            Global.DMAclient.DefaultRequestHeaders.Referrer = new Uri("https://divamodarchive.com/");
            Global.DMAclient.DefaultRequestHeaders.CacheControl = new() { MaxAge = TimeSpan.FromSeconds(Global.ConfigToml.DivaModArchiveCacheControlMaxAge) };
            Global.DMAclient.Timeout = TimeSpan.FromSeconds(Global.ConfigToml.DivaModArchiveApiTimeoutSec);
            Global.GitHubclient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            Global.GitHubclient.DefaultRequestHeaders.Referrer = new Uri("https://github.com/");
            Global.GitHubclient.DefaultRequestHeaders.CacheControl = new() { MaxAge = TimeSpan.FromSeconds(Global.ConfigToml.GitHubCacheControlMaxAge) };
            Global.GitHubclient.Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GitHubApiTimeoutSec);
        }

        /// <summary>
        /// テストバージョン用警告
        /// </summary>
        /// <returns></returns>
        private static bool TestVersionMessageView()
        {
            var ret = true;
            if (!string.IsNullOrWhiteSpace(TestVersion))
            {
                if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["TestVersion"]))
                {
                    ret = false;
                    if (WindowHelper.MessageBoxOpen(35, type: MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        if (WindowHelper.MessageBoxOpen(36, type: MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            ret = true;
                            if (WindowHelper.MessageBoxOpen(37, type: MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                                var settings = configFile.AppSettings.Settings;
                                settings.Add("TestVersion", $"{Version}{TestVersion}");
                                configFile.Save(ConfigurationSaveMode.Modified);
                            }
                        }
                    }
                }
            }
            return ret;
        }
    }
}
