using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Common.Helpers
{
    public class DMMFileSystemWatcher : FileSystemWatcher
    {
        private Timer Timer;
        private const int DelayTimeMilliSecondMods = 3000;  // リフレッシュを呼び出すまでの秒数
        public Action RefreshAction = default;

        public DMMFileSystemWatcher() : base()
        {
        }

        public DMMFileSystemWatcher(string path) : base(path)
        {
        }

        // フォルダの初期化・監視開始/停止メソッド
        public static DMMFileSystemWatcher InitializeWatcher(
            MainWindow window, string WatchPath, string FilterStr = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            DMMFileSystemWatcher dmmWatcher = new();
            dmmWatcher = new(WatchPath);
            ;
            if (!string.IsNullOrEmpty(WatchPath) && Directory.Exists(WatchPath))
            {
                var fileSystemWatcher = new FileSystemWatcher(WatchPath)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                    IncludeSubdirectories = false
                };
                dmmWatcher.Created += dmmWatcher.ModDirectory_FileSystemChanged;
                dmmWatcher.Deleted += dmmWatcher.ModDirectory_FileSystemChanged;
                dmmWatcher.Renamed += dmmWatcher.ModDirectory_FileSystemChanged;

                // dllの場合、変更も監視
                if (WatchPath == Global.ConfigJson.GetGameLocation())
                {
                    if (FilterStr != null)
                    {
                        dmmWatcher.Filter = FilterStr;
                        if (FilterStr == DMLUpdater.MODULE_NAME_DLL)
                        {
                            dmmWatcher.Changed += dmmWatcher.ModDirectory_FileSystemChanged;
                        }
                    }
                }
                dmmWatcher.Timer = new Timer(dmmWatcher.FileMoveEnd_Callback, null, Timeout.Infinite, Timeout.Infinite);
                Logger.WriteLine($"{MeInfo} Initialized watcher for: \"{WatchPath}\"", LoggerType.Developer, param: ParamInfo);
            }
            else
            {
                Logger.WriteLine($"Mods folder not set or does not exist. Watcher not initialized. for: \"{WatchPath}\"", LoggerType.Warning);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);

            return dmmWatcher;
        }

        /// <summary>
        /// ファイルシステム変更をデバウンスし、一定時間後にリフレッシュを実行する
        /// </summary>
        public static async Task WatchPathChangeAsync(
            DMMFileSystemWatcher Watcher, string ModsPath, WatcherChangeTypes ChangeType)
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Timer.Change(DelayTimeMilliSecondMods, Timeout.Infinite);
        }

        public void StartWatching([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Path:\"{Path}\"";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Developer, param: ParamInfo);

            try
            {
                EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error starting FileSystemWatcher: {ex.Message}", LoggerType.Error, param: ParamInfo);
            }
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Developer, param: ParamInfo);
        }

        public void StopWatching([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Path:\"{Path}\"";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Developer, param: ParamInfo);

            if (this != null)
            {
                try
                {
                    EnableRaisingEvents = false;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error stopping FileSystemWatcher: {ex.Message}", LoggerType.Error, param: ParamInfo);
                }
            }
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Developer, param: ParamInfo);
        }

        private void ModDirectory_FileSystemChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(async () =>
            {
                await WorkManager.RunAsync(async () =>
                {
                    var MoveDirectoryPath = new DirectoryInfo(e.FullPath).FullName;
                    await WatchPathChangeAsync(this, MoveDirectoryPath, e.ChangeType);
                });
            });
        }

        // タイマーのコールバックメソッド
        // RefreshActionを実行する(MainWindowのリフレッシュ)
        private void FileMoveEnd_Callback(object state)
        {
            try
            {
                if (RefreshAction != default)
                {
                    App.Current.Dispatcher.InvokeAsync(() => RefreshAction?.Invoke());
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error during FileMoveEnd_Callback:\nex.Message:{ex.Message}", LoggerType.Error);
            }
        }

        // --- FileSystemWatcher と Timer の破棄処理を DisposeWatcherAndTimer に集約 ---
        public static void DisposeWatcherAndTimer(DMMFileSystemWatcher DMMWatcher, [CallerMemberName] string caller = "")
        {
            if (DMMWatcher == null)
                return;

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            DMMWatcher.StopWatching();

            DMMWatcher.Created -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Deleted -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Renamed -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Changed -= DMMWatcher.ModDirectory_FileSystemChanged;    // 念のため
            DMMWatcher.RefreshAction = default;

            DMMWatcher.Timer?.Dispose();
            DMMWatcher.Timer = null;
            DMMWatcher.Dispose();
            DMMWatcher = null;
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
    }
}
