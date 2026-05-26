using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Setup;
using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Common.Config
{
    // It is initialized by calling the InitConfig method.
    // Holds information that should not be edited by the user.
    // For information that can be edited by the user, see Setting.Setting(Window).
    public class ConfigJson
    {
        public static readonly string CONFIG_JSON_NAME = "Config.json";
        public static readonly string CONFIG_JSON_PATH = $"{Global.assemblyLocation}{CONFIG_JSON_NAME}";
        public static readonly string CURRENT_GAME = "Project DIVA Mega Mix\u002B";
        public static readonly string CURRENT_LOADOUT = "Default";

        public string CurrentVersion { get; set; } = "1.3.1.0";     // 初期値
        public string CreateDateTime { get; set; } = DateTime.Now.ToString();
        public string LastUpdateDateTime { get; set; } = DateTime.Now.ToString();
        // 互換性のための備忘："Project DIVA Mega Mix\u002B"
        // GameBox_DropDownClosedにて
        // Global.config.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
        // MainWindow.xamlのTextBlockにベタ書きされているので固定でOK
        public string CurrentGame { get; set; } = CURRENT_GAME;
        // 互換性のための備忘：key:"Project DIVA Mega Mix\u002B", value:GameConfig
        public Dictionary<string, GameConfig> Configs { get; set; } = new();
        [JsonIgnore]
        public GameConfig CurrentConfig
        {
            get { return Global.ConfigJson?.Configs[CURRENT_GAME]; }
            set { _CurrentConfig = value; }
        }
        private GameConfig _CurrentConfig { get; set; } = new();
        public double? LeftGridWidth { get; set; }
        public double? RightGridWidth { get; set; }
        public double? TopGridHeight { get; set; }
        public double? BottomGridHeight { get; set; }
        public double? Height { get; set; }
        public double? Width { get; set; }
        public bool Maximized { get; set; }
        public bool AddModToTop { get; set; }
        [JsonIgnore]
        public bool IsNew { get; set; } = true;

        // todo: ここいつかなんとかしよう
        public Visibility EnabledColumnVisible { get; set; } = Visibility.Visible;
        public int? EnabledColumnIndex { get; set; } = 0;
        public double? EnabledColumnWidth { get; set; }
        public Visibility PriorityColumnVisible { get; set; } = Visibility.Visible;
        public int? PriorityColumnIndex { get; set; } = 1;
        public double? PriorityColumnWidth { get; set; }
        public Visibility NameColumnVisible { get; set; } = Visibility.Visible;
        public int? NameColumnIndex { get; set; } = 2;
        public double? NameColumnWidth { get; set; }
        public Visibility VersionColumnVisible { get; set; } = Visibility.Visible;
        public int? VersionColumnIndex { get; set; } = 3;
        public double? VersionColumnWidth { get; set; }
        public Visibility SiteColumnVisible { get; set; } = Visibility.Visible;
        public int? SiteColumnIndex { get; set; } = 4;
        public double? SiteColumnWidth { get; set; }
        public Visibility CategoryColumnVisible { get; set; } = Visibility.Visible;
        public int? CategoryColumnIndex { get; set; } = 5;
        public double? CategoryColumnWidth { get; set; }
        public Visibility SizeColumnVisible { get; set; } = Visibility.Visible;
        public int? SizeColumnIndex { get; set; } = 6;
        public double? SizeColumnWidth { get; set; }
        public Visibility NoteColumnVisible { get; set; } = Visibility.Visible;
        public int? NoteColumnIndex { get; set; } = 7;
        public double? NoteColumnWidth { get; set; }

        // 配布しない場合はハッシュチェックは不要？
        // それともバージョンチェックとどちらもやるべき？
        public string WinRarConsolePath { get; set; } = string.Empty;
        public string WinRarConsoleVersion { get; set; } = string.Empty;
        public string WinRarConsoleLastUseHashSHA256 { get; set; } = string.Empty;
        // Rar.exe Confirmation Date: 2026.05.21
        public static Version WINRAR_MINIMUM_PRODUCT_VERSION { get; set; } = new("7.22.0");
        public string SevenZipConsolePath { get; set; } = string.Empty;
        public string SevenZipConsoleVersion { get; set; } = string.Empty;
        public string SevenZipConsoleLastUseHashSHA256 { get; set; } = string.Empty;
        // 7z.exe Confirmation Date: 2026.05.21
        public static Version SEVENZIP_INCLUDE_PRODUCT_VERSION { get; set; } = new("26.01");

        public static bool InitConfig([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            // catchに入るとloggerが使えないため、一度newしてから読み込んで置き換え
            ConfigJson config = new() { IsNew = true };

            // --- Config.json 読み込みのエラーハンドリング改善 ---
            string configFilePath = $"{Global.assemblyLocation}{CONFIG_JSON_NAME}";
            if (FileHelper.FileExists(configFilePath))
            {
                try
                {
                    var configString = File.ReadAllText(configFilePath);
                    if (!string.IsNullOrWhiteSpace(configString))
                    {
                        // LauncherOption の移行処理などは Deserialize 後に行う
                        var loadedConfig = JsonSerializer.Deserialize<ConfigJson>(configString);
                        if (loadedConfig != null)
                        {
                            Global.ConfigJson = loadedConfig;
                            Global.ConfigJson.IsNew = false;
                            ret = true;
                        }
                        else
                        {
                            Logger.WriteLine($"Failed to deserialize {CONFIG_JSON_NAME} (result was null). Using default config.", LoggerType.Warning);
                        }
                        config = loadedConfig;
                        if (VersionHelper.CompareVersions(App.Version, loadedConfig.CurrentVersion) != VersionHelper.Result.SAME)
                        {
                            var backupFilePath = FileHelper.CopyFile(configFilePath, oldVersion: loadedConfig.CurrentVersion, IsOriginalFileDelete: true);
                            config.CurrentVersion = App.Version;
                        }
                    }
                    else
                    {
                        Logger.WriteLine($"{CONFIG_JSON_NAME} is empty or whitespace. Using default config.", LoggerType.Warning);
                    }
                }
                catch (JsonException ex)
                {
                    Logger.WriteLine($"Error parsing {CONFIG_JSON_NAME}: {ex.Message}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(configFilePath, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_JSON_NAME} and generate a new one. Save file name:{new FileInfo(configFilePath).Name}", LoggerType.Error, param: ParamInfo);
                }
                catch (IOException ex)
                {
                    Logger.WriteLine($"Error reading {CONFIG_JSON_NAME}: {ex.Message}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(configFilePath, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_JSON_NAME} and generate a new one. Save file name:{new FileInfo(configFilePath).Name}", LoggerType.Error, param: ParamInfo);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.WriteLine($"Permission error reading {CONFIG_JSON_NAME}: {ex.Message}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(configFilePath, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_JSON_NAME} and generate a new one. Save file name:{new FileInfo(configFilePath).Name}", LoggerType.Error, param: ParamInfo);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Unexpected error loading {CONFIG_JSON_NAME}: {ex}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(configFilePath, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_JSON_NAME} and generate a new one. Save file name:{new FileInfo(configFilePath).Name}", LoggerType.Error, param: ParamInfo);
                }
            }
            else
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"{CONFIG_JSON_NAME} not found. Creating default config."), LoggerType.Debug, param: ParamInfo);
            }

            // nullなら初期値を設定
            Global.ConfigJson ??= new();
            Global.ConfigJson.Configs ??= new();
            if (Global.ConfigJson.Configs.Count == 0)
            {
                Global.ConfigJson.Configs.Add(ConfigJson.CURRENT_GAME, new GameConfig() { CurrentLoadout = ConfigJson.CURRENT_LOADOUT });
            }

            if (Global.ConfigJson.IsNew)
            {
                UpdateConfig();
            }

            Logger.WriteLine($"{MeInfo} End. Return:{ret}", LoggerType.Debug);
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        public string GetGameLocation()
        {
            return new DirectoryInfo(CurrentConfig?.Launcher).Parent.ToString() + Global.s;
        }

        public static async Task<bool> UpdateConfigAsync([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                ret = UpdateConfig();
            }
            else
            {
                // 非UIスレッドならDispatcherでUIスレッドに委譲
                ret = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return UpdateConfig();
                });
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Result:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// DMMのConfig.jsonを更新
        /// </summary>
        /// <param name="callerMethodName"></param>
        /// <returns></returns>
        public static bool UpdateConfig([CallerMemberName] string caller = "")
        {
            var isReady = false;
            var retryCnt = 0;
            var retryCntOver = false;
            if (Global.IsMainWindowLoaded)
            {
                Global.ConfigJson.LastUpdateDateTime = DateTime.Now.ToString();
                Global.ConfigJson.CurrentVersion = App.Version;
                Global.ConfigJson.CurrentConfig.Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout] = Global.ModList_All;
                Global.ConfigJson.CurrentConfig.UpdateModLoaderVersion();
                string configString = JsonSerializer.Serialize(Global.ConfigJson, new JsonSerializerOptions { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip });
                // オリジナルのConfig.jsonとの互換性が保たれなくなるためコメント付きシリアライズは使わない
                //string configString = JsonWithComments.SerializeWithComments(Global.configJson);

                do
                {
                    try
                    {
                        File.WriteAllText(CONFIG_JSON_PATH, configString);
                        isReady = true;
                    }
                    catch (Exception e)
                    {
                        // Check if the exception is related to an IO error.
                        if (e.GetType() != typeof(IOException))
                        {
                            Logger.WriteLine($"Couldn't write to {CONFIG_JSON_NAME} ({e.Message})", LoggerType.Error);
                            break;
                        }
                        //await Task.Delay(100);
                        retryCnt++;
                        retryCntOver = 5 >= retryCnt;
                    }
                } while (!isReady && !retryCntOver);
            }
            return isReady;
        }

        /// <summary>
        /// MM+がインストールされているか確認する
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        // called by App#OnStartup、MainWindow#Setup_Click
        public static bool SetupGame([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            var ret = false;
            if (Setup.Generic("DivaMegaMix.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaMegaMix.exe"))
            {
                ret = true;
            }
            else
            {
                Logger.WriteLine($"Failed to complete setup for {Global.ConfigJson.CurrentGame}, please push \"Setup\" button try again.", LoggerType.Error);
                ret = false;
            }
            if (!ret)
            {
                WindowHelper.MessageBoxOpen(43);
                Environment.Exit(0);
            }

            return ret;
        }
    }
}
