using DivaModManager.UI;
using GongSolutions.Wpf.DragDrop.Utilities;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tomlyn;
using Tomlyn.Model;
using WpfAnimatedGif;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string version;
        private FileSystemWatcher ModsWatcher;
        private FlowDocument defaultFlow = new FlowDocument();
        private string defaultText = "Welcome to Diva Mod Manager!\n\n" +
            "To show metadata here:\nRight Click Row > Configure Mod and add author, version, and/or date fields" +
            "\nand/or Right Click Row > Fetch Metadata and confirm the GameBanana URL of the mod";
        private ObservableCollection<String> LauncherOptions = new ObservableCollection<String>(new string[] { "Executable", "Steam" });
        ListSortDirection direction = ListSortDirection.Ascending;
        // --- FileSystemWatcher Debounce 用の追加 ---
        private Timer _debounceTimer;
        private const int DebounceTimeoutMs = 500; // 500ミリ秒待機してからRefreshを実行
        // -----------------------------------------

        public MainWindow()
        {
            InitializeComponent();
            Global.logger = new Logger(ConsoleWindow);
            Global.config = new();

            // Get Version Number
            var DMMVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            version = DMMVersion;
            //version = DMMVersion.Substring(0, DMMVersion.LastIndexOf('.'));

            Global.logger.WriteLine($"Launched Diva Mod Manager v{version}!", LoggerType.Info);
            // Get Global.config if it exists
            if (File.Exists($@"{Global.assemblyLocation}{Global.s}Config.json"))
            {
                try
                {
                    var configString = File.ReadAllText($@"{Global.assemblyLocation}{Global.s}Config.json");
                    Global.config = JsonSerializer.Deserialize<Config>(configString);
                    foreach (var game in Global.config.Configs.Keys)
                    {
                        if (Global.config.Configs[game].FirstOpen && !Global.config.Configs[game].LauncherOptionConverted)
                        {

                            Global.config.Configs[game].LauncherOptionIndex = Convert.ToInt32(Global.config.Configs[game].LauncherOption);
                            Global.config.Configs[game].LauncherOptionConverted = true;
                            Global.UpdateConfig();
                        }
                    }
                }
                catch (Exception e)
                {
                    Global.logger.WriteLine(e.Message, LoggerType.Error);
                }
            }

            // Last saved windows settings
            if (Global.config.Height != null && Global.config.Height >= MinHeight)
                Height = (double)Global.config.Height;
            if (Global.config.Width != null && Global.config.Width >= MinWidth)
                Width = (double)Global.config.Width;
            if (Global.config.Maximized)
                WindowState = WindowState.Maximized;
            if (Global.config.TopGridHeight != null)
                MainGrid.RowDefinitions[2].Height = new GridLength((double)Global.config.TopGridHeight, GridUnitType.Star);
            if (Global.config.BottomGridHeight != null)
                MainGrid.RowDefinitions[4].Height = new GridLength((double)Global.config.BottomGridHeight, GridUnitType.Star);
            if (Global.config.LeftGridWidth != null)
                MiddleGrid.ColumnDefinitions[0].Width = new GridLength((double)Global.config.LeftGridWidth, GridUnitType.Star);
            if (Global.config.RightGridWidth != null)
                MiddleGrid.ColumnDefinitions[2].Width = new GridLength((double)Global.config.RightGridWidth, GridUnitType.Star);

            if (Global.config.EnabledColumnIndex != null)
                ModGrid.Columns[(int)Global.Col.Enabled].DisplayIndex = (int)Global.config.EnabledColumnIndex;
            if (Global.config.PriorityColumnIndex != null)
                ModGrid.Columns[(int)Global.Col.Priority].DisplayIndex = (int)Global.config.PriorityColumnIndex;
            if (Global.config.NameColumnIndex != null)
                ModGrid.Columns[(int)Global.Col.Name].DisplayIndex = (int)Global.config.NameColumnIndex;
            if (Global.config.CategoryColumnIndex != null)
                ModGrid.Columns[(int)Global.Col.Category].DisplayIndex = (int)Global.config.CategoryColumnIndex;
            if (Global.config.NoteColumnIndex != null)
                ModGrid.Columns[(int)Global.Col.Note].DisplayIndex = (int)Global.config.NoteColumnIndex;

            if (Global.config.EnabledColumnWidth != null)
                ModGrid.Columns[(int)Global.Col.Enabled].Width = (double)Global.config.EnabledColumnWidth;
            if (Global.config.PriorityColumnWidth != null)
                ModGrid.Columns[(int)Global.Col.Priority].Width = (double)Global.config.PriorityColumnWidth;
            if (Global.config.NameColumnWidth != null)
                ModGrid.Columns[(int)Global.Col.Name].Width = (double)Global.config.NameColumnWidth;
            if (Global.config.CategoryColumnWidth != null)
                ModGrid.Columns[(int)Global.Col.Category].Width = (double)Global.config.CategoryColumnWidth;
            if (Global.config.NoteColumnWidth != null)
                ModGrid.Columns[(int)Global.Col.Note].Width = (double)Global.config.NoteColumnWidth;

            ModGrid.Columns[(int)Global.Col.Enabled].Visibility = (Visibility)Global.config.EnabledColumnVisible;
            ModGrid.Columns[(int)Global.Col.Priority].Visibility = (Visibility)Global.config.PriorityColumnVisible;
            ModGrid.Columns[(int)Global.Col.Name].Visibility = (Visibility)Global.config.NameColumnVisible;
            ModGrid.Columns[(int)Global.Col.Category].Visibility = (Visibility)Global.config.CategoryColumnVisible;
            ModGrid.Columns[(int)Global.Col.Note].Visibility = (Visibility)Global.config.NoteColumnVisible;

            Global.games = new List<string>();
            foreach (var item in GameBox.Items)
            {
                var game = (((item as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                Global.games.Add(game);
            }

            if (Global.config.Configs == null)
            {
                Global.config.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                Global.config.Configs = new();
                Global.config.Configs.Add(Global.config.CurrentGame, new());
            }
            else
                GameBox.SelectedIndex = Global.games.IndexOf(Global.config.CurrentGame);
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout))
                Global.config.Configs[Global.config.CurrentGame].CurrentLoadout = "Default";
            if (Global.config.Configs[Global.config.CurrentGame].Loadouts == null)
                Global.config.Configs[Global.config.CurrentGame].Loadouts = new();
            if (!Global.config.Configs[Global.config.CurrentGame].Loadouts.ContainsKey(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout))
                Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout, new());
            else if (Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout] == null)
                Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout] = new();
            Global.ModList = Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout];
            Global.ModList_All = Global.ModList;

            Global.LoadoutItems = new ObservableCollection<String>(Global.config.Configs[Global.config.CurrentGame].Loadouts.Keys);

            LoadoutBox.ItemsSource = Global.LoadoutItems;
            LoadoutBox.SelectedItem = Global.config.Configs[Global.config.CurrentGame].CurrentLoadout;

            // --- FileSystemWatcher と Timer の初期化 ---
            InitializeFileSystemWatcherAndTimer(); // 初期化処理をメソッドに分離
            // ----------------------------------------

            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                if (Global.config.Configs[Global.config.CurrentGame].FirstOpen)
                    Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
            }
            else
            {
                // Refresh は InitializeFileSystemWatcherAndTimer 内で必要に応じて行うか、別途呼び出す
                // Refresh(); // ここでは呼ばず、初期化後に明示的に呼ぶか、起動時のチェックに任せる
                StartWatching(); // イベント監視を開始
            }

            CategoryComboInit(0);

            defaultFlow.Blocks.Add(ConvertToFlowParagraph(defaultText));
            DescriptionWindow.Document = defaultFlow;
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
            ImageBehavior.SetAnimatedSource(Preview, bitmap);
            ImageBehavior.SetAnimatedSource(PreviewBG, null);
            App.Current.Dispatcher.Invoke(async () =>
            {
                IsEnabledControls(false);
                Global.logger.WriteLine("Checking for mod updates...", LoggerType.Info);
                //await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this);
                await ModUpdater.CheckForUpdatesInit(this);

                Global.logger.WriteLine("Checking for Diva Mod Manager update...", LoggerType.Info);
                if (await AutoUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    Close();
                // Check for DML update only if its already setup
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion))

                {
                    Global.logger.WriteLine("Checking for DivaModLoader update...", LoggerType.Info);
                    await Setup.CheckForDMLUpdate(new CancellationTokenSource());
                }
                IsEnabledControls(true);

                // 初期表示のために RefreshAsync を呼ぶ
                if (await DirectoryExistsAsync(Global.config.Configs[Global.config.CurrentGame].ModsFolder)) // 非同期チェック
                {
                    await RefreshAsync(); // ★非同期版を呼び出す
                }
            });
        }

        // --- FileSystemWatcher と Timer の初期化・監視開始/停止メソッド ---
        private void InitializeFileSystemWatcherAndTimer()
        {
            DisposeWatcherAndTimer(); // 既存があれば破棄

            string modsFolder = Global.config.Configs[Global.config.CurrentGame].ModsFolder;
            if (!string.IsNullOrEmpty(modsFolder) && Directory.Exists(modsFolder))
            {
                ModsWatcher = new FileSystemWatcher(modsFolder)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite, // 必要に応じて調整
                    IncludeSubdirectories = false // サブディレクトリは監視しない（必要なら true）
                };

                ModsWatcher.Created += OnFileSystemChanged;
                ModsWatcher.Deleted += OnFileSystemChanged;
                ModsWatcher.Renamed += OnFileSystemChanged;
                // 必要なら Changed イベントも監視する
                // ModsWatcher.Changed += OnFileSystemChanged;

                // Debounceタイマーの初期化
                _debounceTimer = new Timer(DebounceTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

                //Global.logger.WriteLine($"Initialized watcher for: {modsFolder}", LoggerType.Debug); // デバッグ用ログ
            }
            else
            {
                Global.logger.WriteLine($"Mods folder not set or does not exist. Watcher not initialized.", LoggerType.Warning);
            }
        }

        private void StartWatching()
        {
            if (ModsWatcher != null)
            {
                try
                {
                    ModsWatcher.EnableRaisingEvents = true;
                    //Global.logger.WriteLine($"Started watching: {ModsWatcher.Path}", LoggerType.Debug); // デバッグ用ログ
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Error starting FileSystemWatcher: {ex.Message}", LoggerType.Error);
                    // 必要であればユーザーに通知
                }
            }
        }

        private void StopWatching()
        {
            if (ModsWatcher != null)
            {
                try
                {
                    ModsWatcher.EnableRaisingEvents = false;
                    //Global.logger.WriteLine($"Stopped watching: {ModsWatcher.Path}", LoggerType.Debug); // デバッグ用ログ
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Error stopping FileSystemWatcher: {ex.Message}", LoggerType.Error);
                }
            }
            // タイマーもリセット（変更中に監視を停止する場合）
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void DisposeWatcherAndTimer()
        {
            StopWatching(); // まず監視を停止

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            ModsWatcher?.Dispose();
            ModsWatcher = null;
            //Global.logger.WriteLine($"Disposed watcher and timer.", LoggerType.Debug); // デバッグ用ログ
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => OnFirstOpen());
            LauncherOptionsBox.IsEnabled = true;
            LauncherOptionsBox.ItemsSource = LauncherOptions;
            LauncherOptionsBox.SelectedIndex = Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex;
        }

        // --- OnModified を OnFileSystemChanged にリネームし、Debounce処理を追加 ---
        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 特定のファイル（例：一時ファイル）を除外したい場合はここでフィルタリング
            // if (e.Name.EndsWith(".tmp")) return;

            //Global.logger.WriteLine($"File system change detected: {e.ChangeType} - {e.FullPath}", LoggerType.Debug); // デバッグ用ログ

            // タイマーが破棄されていないか確認
            if (_debounceTimer == null) return;

            // タイマーをリセットして待機時間を再開/延長
            _debounceTimer.Change(DebounceTimeoutMs, Timeout.Infinite);
        }

        // --- Debounceタイマーのコールバックメソッド ---
        private async void DebounceTimerCallback(object state)
        {
            // タイマーが無効（Dispose済みなど）なら何もしない
            if (_debounceTimer == null) return;

            //Global.logger.WriteLine($"Debounce timer triggered. Refreshing mods...", LoggerType.Debug); // デバッグ用ログ

            // UIスレッドで RefreshAsync() を実行
            // Application.Currentがnullになる可能性も考慮（シャットダウン時など）
            try
            {
                await Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    // Activate() や InitSearchMod() は Refresh の前後どちらで行うか検討
                    InitSearchMod(); // Mod検索状態をリセット
                    await RefreshAsync(); // ★非同期版を呼び出す
                    // Activate(); // 必要であればウィンドウを前面に表示
                });
            }
            catch (TaskCanceledException)
            {
                // アプリケーション終了時などに発生する可能性
                //Global.logger.WriteLine($"RefreshAsync was canceled, likely due to application shutdown.", LoggerType.Debug);
            }
            catch (Exception ex)
            {
                // RefreshAsync 内で捕捉されなかった予期せぬ例外
                Global.logger.WriteLine($"Error during DebounceTimerCallback: {ex}", LoggerType.Critical);
                // 必要に応じてユーザーに通知
                // MessageBox.Show($"An unexpected error occurred during refresh: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshAsync()
        {
            // --- UIスレッドでの事前チェック ---
            string currentModDirectory = Global.config.Configs[Global.config.CurrentGame].ModsFolder;
            if (String.IsNullOrEmpty(currentModDirectory) || !(await DirectoryExistsAsync(currentModDirectory))) // 非同期存在チェック
            {
                if (Global.config.Configs[Global.config.CurrentGame].FirstOpen) // FirstOpen フラグのチェックはUIスレッドで
                    Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }
            // ---------------------------------

            // --- 処理中はUIを無効化 (任意) ---
            IsEnabledControls(false);
            // ---------------------------------

            try // 全体をtry-catchで囲み、予期せぬエラーを捕捉
            {
                // --- 実際のディレクトリ内のModパスを取得 (非同期) ---
                var modPaths = await GetDirectoriesAsync(currentModDirectory);
                var existingModNamesInDirectory = new HashSet<string>(modPaths.Select(Path.GetFileName));
                // ---------------------------------------------

                // --- 各Modディレクトリを処理 (並列化も可能だが、まずは逐次処理で) ---
                foreach (var modPath in modPaths)
                {
                    await ProcessModDirectoryAsync(modPath); // 分割されたメソッドを呼び出す
                }
                // ----------------------------------------------------------

                // --- 削除されたModをリストから除去 ---
                await RemoveDeletedModsAsync(existingModNamesInDirectory); // 分割されたメソッドを呼び出す
                // ----------------------------------

                // --- UI更新とModLoaderビルド ---
                await UpdateUIElementsAndBuildAsync(); // 分割されたメソッドを呼び出す
                // -------------------------------

                Global.logger.WriteLine("Refreshed!", LoggerType.Info);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"An error occurred during RefreshAsync: {ex}", LoggerType.Error);
                // 必要に応じてユーザーに通知
                // MessageBox.Show($"An error occurred during refresh: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // --- UIを再度有効化 (任意) ---
                IsEnabledControls(true);
                // ---------------------------
            }
        }

        #region RefreshAsync の分割メソッド群

        /// <summary>
        /// 指定されたModディレクトリを処理（新規追加または既存更新）
        /// </summary>
        private async Task ProcessModDirectoryAsync(string modPath)
        {
            var modName = Path.GetFileName(modPath);
            var configPath = Path.Combine(modPath, "config.toml");
            var configEPath = Path.Combine(modPath, "config_e.toml");
            var modJsonPath = Path.Combine(modPath, "mod.json");

            // Global.ModList は UI スレッドでアクセスする必要がある場合があるため注意
            // FindIndex などは読み取りなので大丈夫かもしれないが、安全のためコピーを使うかUIスレッドで行う
            Mod modEntry = null;
            await Application.Current.Dispatcher.InvokeAsync(() => {
                // FindIndex は ObservableCollection では低速な可能性があるため FirstOrDefault を検討
                // var index = Global.ModList.ToList().FindIndex(x => x.name == modName); // ToList()は重い可能性
                modEntry = Global.ModList.FirstOrDefault(x => x.name == modName);
            });


            if (modEntry == null) // 新規Mod
            {
                modEntry = new Mod { name = modName };
                bool configExists = await FileExistsAsync(configPath);

                if (configExists)
                {
                    // config.toml からMod情報を更新/設定
                    await TryUpdateModFromConfigAsync(modEntry, configPath, isNewMod: true);
                }
                else
                {
                    // config.toml がない場合の処理 (ユーザー確認含む)
                    bool createConfig = false;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // IsWindowOpen や ChoiceWindow の表示は UI スレッドで行う
                        if (!IsWindowOpen<ChoiceWindow>())
                        {
                            createConfig = ConfirmConfigCreation(configPath, modEntry, true); // ConfirmConfigCreationの結果を bool で返すように変更想定
                        }
                        else
                        {
                            Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                        }
                    });
                    // 結果に基づいて config.toml を作成
                    if (createConfig)
                    {
                        // modEntry.enabled = true; // ConfirmConfigCreation内で設定されるか？
                        TomlTable config = new TomlTable { { "enabled", modEntry.enabled } }; // enabled は ConfirmConfigCreation の結果に依存させるべき
                        AddInclude(config); // AddInclude は同期のまま
                        await TryWriteTomlAsync(configPath, config);
                        Global.logger.WriteLine($"Created default config.toml for {modName}.", LoggerType.Info);
                    }
                    else
                    {
                        // 作成しない場合、modEntry.enabled はどうなる？ デフォルトは true?
                        modEntry.enabled = true; // デフォルト値
                        Global.logger.WriteLine($"User chose not to create config.toml for {modName}.", LoggerType.Info);
                    }
                }

                // config_e.toml, mod.json の読み込み (新規Modでも読み込む)
                await TryLoadExtendedConfigAsync(modEntry, configEPath);
                await TryLoadModJsonAsync(modEntry, modJsonPath);

                // ModListへの追加 (UIスレッドで実行)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Global.config.AddModToTop)
                        Global.ModList.Insert(0, modEntry);
                    else
                        Global.ModList.Add(modEntry);
                    Global.logger.WriteLine($"Added {modName}", LoggerType.Info);
                });
            }
            else // 既存Mod
            {
                bool configExists = await FileExistsAsync(configPath);
                if (configExists)
                {
                    // config.toml からMod情報を更新
                    await TryUpdateModFromConfigAsync(modEntry, configPath, isNewMod: false);
                }
                else
                {
                    // config.toml がない場合の処理 (ユーザー確認含む)
                    bool createConfig = false;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!IsWindowOpen<ChoiceWindow>())
                        {
                            // 既存Modの場合、enabledのデフォルト値は現在のmodEntry.enabledを使う
                            createConfig = ConfirmConfigCreation(configPath, modEntry, modEntry.enabled);
                        }
                        else
                        {
                            Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                        }
                    });
                    if (createConfig)
                    {
                        TomlTable config = new TomlTable { { "enabled", modEntry.enabled } };
                        AddInclude(config);
                        await TryWriteTomlAsync(configPath, config);
                        Global.logger.WriteLine($"Created missing config.toml for existing mod {modName}.", LoggerType.Info);
                    }
                }

                // config_e.toml, mod.json の読み込み (既存Modでも毎回読み込む)
                await TryLoadExtendedConfigAsync(modEntry, configEPath);
                await TryLoadModJsonAsync(modEntry, modJsonPath);
            }
        }

        /// <summary>
        /// config.toml ファイルから Mod オブジェクトを更新する（ファイルがない/読めない場合はデフォルト値やログ出力）
        /// </summary>
        private async Task TryUpdateModFromConfigAsync(Mod mod, string configPath, bool isNewMod)
        {
            TomlTable config = await TryReadTomlAsync(configPath); // 非同期ヘルパーを使用

            if (config == null)
            {
                // 読み取り失敗 or 不正なファイル
                Global.logger.WriteLine($"Couldn't read or parse {configPath}. Using defaults/current state for {mod.name}.", LoggerType.Warning);
                // isNewMod の場合、enabled は true に設定しておくのが安全か？
                if (isNewMod) mod.enabled = true;
                // 既存Modの場合は現在の mod.enabled を維持
                return; // これ以上処理しない
            }

            bool needsWriteBack = false;

            // Enabled プロパティの処理
            if (config.ContainsKey("enabled"))
            {
                try
                {
                    bool enabledFromFile = (bool)config["enabled"];
                    if (isNewMod)
                    {
                        mod.enabled = enabledFromFile;
                    }
                    else // 既存Modの場合、DMM内の状態 (mod.enabled) をファイルに反映
                    {
                        if (enabledFromFile != mod.enabled)
                        {
                            config["enabled"] = mod.enabled;
                            needsWriteBack = true;
                            //Global.logger.WriteLine($"Updating enabled state in {configPath} for {mod.name} to {mod.enabled}.", LoggerType.Debug);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Error reading 'enabled' field from {configPath} for {mod.name}: {ex.Message}. Using default.", LoggerType.Warning);
                    if (isNewMod) mod.enabled = true; // デフォルト
                    // enabled フィールドを正しい型で上書きするか、ファイルを修正する必要があるかも
                    config["enabled"] = mod.enabled; // とりあえず現在の値で上書き試行
                    needsWriteBack = true;
                }
            }
            else // enabled フィールドが存在しない場合
            {
                if (isNewMod) mod.enabled = true; // 新規ならデフォルト true
                // 既存、新規どちらの場合も enabled フィールドを追加
                config.Add("enabled", mod.enabled);
                needsWriteBack = true;
                //Global.logger.WriteLine($"Adding missing 'enabled' field to {configPath} for {mod.name}.", LoggerType.Debug);
            }

            // Include プロパティの処理 (常に確認・追加)
            if (!config.ContainsKey("include"))
            {
                AddInclude(config); // AddInclude は同期のまま？ 中で await してないならOK
                needsWriteBack = true;
                //Global.logger.WriteLine($"Adding missing 'include' field to {configPath} for {mod.name}.", LoggerType.Debug);
            }

            // ファイルに書き戻す必要がある場合
            if (needsWriteBack)
            {
                await TryWriteTomlAsync(configPath, config); // 非同期ヘルパーを使用
            }
        }


        /// <summary>
        /// config_e.toml から拡張設定を読み込む
        /// </summary>
        private async Task TryLoadExtendedConfigAsync(Mod mod, string configEPath)
        {
            TomlTable configE = await TryReadTomlAsync(configEPath);
            if (configE != null)
            {
                // 各キーが存在するか確認してから読み込む
                mod.priority = configE.TryGetValue("priority", out var prio) ? prio?.ToString() ?? "" : "";
                mod.category = configE.TryGetValue("category", out var cat) ? cat?.ToString() ?? "" : "";
                mod.note = configE.TryGetValue("note", out var n) ? n?.ToString() ?? "" : "";
                // config_e.toml に category があれば IsCategoryHighlighted を true にする？
                mod.IsCategoryHighlighted = !string.IsNullOrEmpty(mod.category);
            }
            else
            {
                // ファイルが存在しないか読めない場合、デフォルト値を設定
                // mod.priority = ""; // デフォルトは空のはず
                // mod.category = "";
                // mod.note = "";
                mod.IsCategoryHighlighted = false; // ハイライトしない
            }
        }

        /// <summary>
        /// mod.json からメタデータを読み込む
        /// </summary>
        private async Task TryLoadModJsonAsync(Mod mod, string modJsonPath)
        {
            bool modJsonExists = await FileExistsAsync(modJsonPath);
            if (!modJsonExists) return; // ファイルがなければ何もしない

            string jsonContent = await TryReadAllTextAsync(modJsonPath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                Global.logger.WriteLine($"mod.json content is empty or whitespace: {modJsonPath}", LoggerType.Warning);
                return; // 空なら何もしない
            }

            try
            {
                // 非同期Deserialize (System.Text.Json は Stream からの非同期Deserializeを提供)
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
                Metadata metadata = await JsonSerializer.DeserializeAsync<Metadata>(stream);

                if (metadata != null)
                {
                    // config_e.toml の category を優先
                    if (string.IsNullOrEmpty(mod.category)) // config_e で設定されていなければ
                    {
                        mod.category = metadata.cat;
                        mod.IsCategoryHighlighted = false; // mod.json 由来ならハイライトしない
                    }
                    else if (mod.category == metadata.cat) // config_e と同じならハイライト解除
                    {
                        mod.IsCategoryHighlighted = false;
                    }
                    // 他のメタデータも必要ならここで mod オブジェクトに設定 (例: mod.Author = metadata.submitter など)
                }
            }
            catch (JsonException ex)
            {
                Global.logger.WriteLine($"Failed to parse {modJsonPath}: {ex.Message}", LoggerType.Error);
            }
            catch (Exception ex) // その他の予期せぬエラー
            {
                Global.logger.WriteLine($"Unexpected error processing {modJsonPath}: {ex.Message}", LoggerType.Error);
            }
        }


        /// <summary>
        /// ディレクトリに存在しないModをGlobal.ModListから削除する
        /// </summary>
        private async Task RemoveDeletedModsAsync(HashSet<string> existingModNamesInDirectory)
        {
            // UIスレッドで実行する必要がある
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // ToList() でコピーを作成してから反復処理
                var modsToRemove = Global.ModList.Where(mod => !existingModNamesInDirectory.Contains(mod.name)).ToList();
                foreach (var modToRemove in modsToRemove)
                {
                    Global.ModList.Remove(modToRemove); // ObservableCollectionからの削除はUIスレッドで
                    Global.logger.WriteLine($"Deleted {modToRemove.name}", LoggerType.Info);
                }
            });
        }

        /// <summary>
        /// UI要素（統計情報、ModGrid）の更新とModLoader.Buildの実行
        /// </summary>
        private async Task UpdateUIElementsAndBuildAsync()
        {
            // UI 更新 (UI スレッドで)
            await Application.Current.Dispatcher.InvokeAsync(async () => // await をつける
            {
                ModGrid.ItemsSource = Global.ModList; // 再設定で変更を反映
                ModGrid.Items.Refresh(); // またはこちら、ItemsSource再設定の方が確実な場合も
                CategoryComboInit(0); // カテゴリコンボ更新

                var currentModDirectory = Global.config.Configs[Global.config.CurrentGame].ModsFolder;
                long totalFiles = 0;
                long totalSize = 0;

                if (await DirectoryExistsAsync(currentModDirectory)) // 非同期チェック
                {
                    // これらの情報取得も重い場合は Task.Run でラップ
                    try
                    {
                        totalFiles = await Task.Run(() => Directory.GetFiles(currentModDirectory, "*", SearchOption.AllDirectories).Length);
                        // GetDirectorySize が拡張メソッドで非同期でない場合
                        totalSize = await Task.Run(() => new DirectoryInfo(currentModDirectory).GetDirectorySize());
                        // もし GetDirectorySize が非同期版を提供しているならそれを使う
                        // totalSize = await new DirectoryInfo(currentModDirectory).GetDirectorySizeAsync();
                    }
                    catch (Exception ex)
                    {
                        Global.logger.WriteLine($"Error calculating directory stats for {currentModDirectory}: {ex.Message}", LoggerType.Warning);
                    }
                }

                var enabledCount = Global.ModList.Count(x => x.enabled); // これは高速
                var totalCount = Global.ModList.Count; // これも高速

                var stats = $"{enabledCount}/{totalCount} mods • {totalFiles:N0} files • {StringConverters.FormatSize(totalSize)}";
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion))
                    stats += $" • DML v{Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion}";
                stats += $" • DMM v{version}";
                Stats.Text = stats; // UI要素の更新
            });

            // 設定保存 (同期のまま？ UpdateConfigが軽ければOK)
            Global.UpdateConfig();

            // ModLoader.Build (重い場合は Task.Run で非同期実行)
            try
            {
                await Task.Run(() => ModLoader.Build());
                // もし ModLoader.BuildAsync() があれば await ModLoader.BuildAsync();
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Error during ModLoader.Build: {ex}", LoggerType.Error);
                // 必要に応じてユーザーに通知
            }
        }

        #endregion

        #region 非同期ファイル・ディレクトリ操作ヘルパー

        private async Task<bool> FileExistsAsync(string path)
        {
            return await Task.Run(() => File.Exists(path));
        }

        private async Task<bool> DirectoryExistsAsync(string path)
        {
            return await Task.Run(() => Directory.Exists(path));
        }

        private async Task<string[]> GetDirectoriesAsync(string path)
        {
            try
            {
                return await Task.Run(() => Directory.GetDirectories(path));
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Error getting directories in {path}: {ex.Message}", LoggerType.Error);
                return Array.Empty<string>(); // 空配列を返す
            }
        }

        private async Task<string> TryReadAllTextAsync(string path, int retries = 3, int delayMs = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    if (!await FileExistsAsync(path)) return null; // 非同期存在チェック
                    // BOMハンドリングなどが必要なら ReadAllTextAsync のオーバーロードを検討
                    return await File.ReadAllTextAsync(path);
                }
                catch (IOException ex) when (i < retries - 1) // 最後のリトライ以外はWarning
                {
                    Global.logger.WriteLine($"IOException reading {path} (Attempt {i + 1}/{retries}): {ex.Message}. Retrying...", LoggerType.Warning);
                    await Task.Delay(delayMs);
                }
                catch (IOException ex) // 最後のリトライ
                {
                    Global.logger.WriteLine($"Failed IOException reading {path} after {retries} attempts: {ex.Message}", LoggerType.Error);
                    // UI スレッドで MessageBox 表示が必要なら Dispatcher.InvokeAsync
                    return null;
                }
                catch (Exception ex) // その他の予期せぬ例外
                {
                    Global.logger.WriteLine($"Unexpected error reading {path}: {ex.Message}", LoggerType.Error);
                    return null;
                }
            }
            return null; // リトライ失敗
        }

        private async Task<bool> TryWriteAllTextAsync(string path, string content, int retries = 3, int delayMs = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    // ディレクトリが存在しない可能性があれば作成
                    // string dir = Path.GetDirectoryName(path);
                    // if (!await DirectoryExistsAsync(dir)) await Task.Run(() => Directory.CreateDirectory(dir));
                    await File.WriteAllTextAsync(path, content);
                    return true; // 成功
                }
                catch (IOException ex) when (i < retries - 1)
                {
                    Global.logger.WriteLine($"IOException writing to {path} (Attempt {i + 1}/{retries}): {ex.Message}. Retrying...", LoggerType.Warning);
                    await Task.Delay(delayMs);
                }
                catch (IOException ex) // 最後のリトライ
                {
                    Global.logger.WriteLine($"Failed IOException writing to {path} after {retries} attempts: {ex.Message}", LoggerType.Error);
                    return false; // 失敗
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Unexpected error writing to {path}: {ex.Message}", LoggerType.Error);
                    return false; // 失敗
                }
            }
            return false; // リトライ失敗
        }

        private async Task<TomlTable> TryReadTomlAsync(string path)
        {
            string content = await TryReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content)) // null または空/空白
            {
                // ファイルが存在しないのはエラーではない場合もある
                // if (content == null && !await FileExistsAsync(path)) return null;
                // 読み取り失敗や空ファイルはログしておく
                if (content != null) Global.logger.WriteLine($"Toml file content is empty or whitespace: {path}", LoggerType.Warning);
                return null;
            }

            try
            {
                // Tomlyn のパースは同期的だが、CPU負荷が高ければ Task.Run でラップ
                return await Task.Run(() =>
                {
                    if (Toml.TryToModel(content, out TomlTable model, out var diagnostics))
                    {
                        return model;
                    }
                    else
                    {
                        // diagnostics をログに出力 (最初の１つ)
                        if (diagnostics != null && diagnostics.Count > 0)
                            Global.logger.WriteLine($"Failed to parse Toml file {path}: {diagnostics[0].Message}", LoggerType.Warning);
                        else
                            Global.logger.WriteLine($"Failed to parse Toml file {path} with unknown error.", LoggerType.Warning);
                        return null; // パース失敗
                    }
                });
            }
            catch (Exception ex) // Tomlyn が予期せぬ例外を投げる場合
            {
                Global.logger.WriteLine($"Error parsing Toml file {path}: {ex.Message}", LoggerType.Error);
                return null;
            }
        }

        private async Task<bool> TryWriteTomlAsync(string path, TomlTable model)
        {
            try
            {
                // Tomlyn のモデルからの変換は同期的
                string content = await Task.Run(() => Toml.FromModel(model));
                return await TryWriteAllTextAsync(path, content);
            }
            catch (Exception ex) // Toml.FromModelが例外を投げる場合
            {
                Global.logger.WriteLine($"Error generating Toml content for {path}: {ex.Message}", LoggerType.Error);
                return false;
            }
        }


        #endregion

        private void ModGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            foreach (var add in e.AddedCells)
            {
                var mod = add.Item as Mod;
                if (mod != null)
                {
                    mod.selected = true;
                }
            }
            foreach (var add in e.RemovedCells)
            {
                var mod = add.Item as Mod;
                if (mod != null)
                {
                    mod.selected = false;
                }
            }
        }

        // Events for Enabled checkboxes
        private void OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridCell checkBox && checkBox.IsKeyboardFocusWithin)
            {
                CheckedCommon(sender, e, true);
            }
        }
        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridCell checkBox && checkBox.IsKeyboardFocusWithin)
            {
                CheckedCommon(sender, e, false);
            }
        }
        private async void CheckedCommon(object sender, RoutedEventArgs e, bool setEnabled)
        {
            var checkMods = ModGrid.SelectedItems;
            if (checkMods != null)
            {
                List<Mod> temp = Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout].ToList();
                foreach (var m in temp)
                {
                    foreach (Mod checkMod in checkMods)
                    {
                        if (m.name == checkMod.name)
                        {
                            if (m.selected)
                            {
                                m.enabled = setEnabled;
                                //UpdateModConfigToml(checkMod, setEnabled);
                                RefreshAsync(); // 同期させるためawaitはなし(非同期では非常に遅いため)
                            }
                        }
                    }
                }
                Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout] = new ObservableCollection<Mod>(temp);
                Global.UpdateConfig();
                await Task.Run(() => ModLoader.Build());

                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    var stats = $"{Global.ModList.ToList().Where(x => x.enabled).ToList().Count}/{Global.ModList.Count} mods • {Directory.GetFiles(Global.config.Configs[Global.config.CurrentGame].ModsFolder, "*", SearchOption.AllDirectories).Length.ToString("N0")} files • " +
                    $"{StringConverters.FormatSize(new DirectoryInfo(Global.config.Configs[Global.config.CurrentGame].ModsFolder).GetDirectorySize())}";
                    if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion))
                        stats += $" • DML v{Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion}";
                    stats += $" • DMM v{version}";
                    Stats.Text = stats;
                });
            }
        }

        // UpdateModConfigToml は RefreshAsync 内のロジックに統合されたため不要になる可能性
        /*
        private void UpdateModConfigToml(Mod m, bool value)
        {
            var configPath = $"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{m.name}{Global.s}config.toml";
            if (File.Exists(configPath))
            {
                var configString = File.ReadAllText(configPath);
                if (Toml.TryToModel(configString, out TomlTable config, out var diagnostics))
                {
                    if (config.ContainsKey("enabled"))
                        config["enabled"] = value;
                    else
                        // Add enabled field to be true if it doesn't exist
                        config.Add("enabled", value);
                    AddInclude(config);
                    File.WriteAllText(configPath, Toml.FromModel(config));
                }
                else
                {
                    Global.logger.WriteLine($"{diagnostics[0].Message} for {m.name}. Rewriting {configPath} with only enabled field", LoggerType.Warning);
                    // Create config.toml with enabled field to be true if failed to parse
                    config = new();
                    config.Add("enabled", value);
                    AddInclude(config);
                    File.WriteAllText(configPath, Toml.FromModel(config));
                }
            }
            else
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Create config.toml with enabled field to be true and include set, if the user desires
                    if (!IsWindowOpen<ChoiceWindow>())
                    {
                        ConfirmConfigCreation(configPath, m, false);
                    }
                    else
                    {
                        Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                    }
                });
            }
        }
        */

        private async Task UpdateModConfigToml_e(Mod m, string column, object value)
        {
            var configPath_e = $"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{m.name}{Global.s}config_e.toml";
            TomlTable config_e = await TryReadTomlAsync(configPath_e);

            bool needsWrite = false;
            if (config_e == null) // ファイルがないか読めない場合、新規作成
            {
                config_e = new TomlTable();
                // デフォルト値を追加（空文字列）
                config_e.Add("priority", "");
                config_e.Add("category", "");
                config_e.Add("note", "");
                //Global.logger.WriteLine($"Creating new config_e.toml for {m.name}.", LoggerType.Debug);
                needsWrite = true; // 新規作成なので書き込み必要
            }

            // 要求されたカラムの値を更新
            string valueStr = value?.ToString() ?? ""; // nullを空文字列に
            switch (column)
            {
                case "Priority":
                    if (!config_e.ContainsKey("priority") || config_e["priority"]?.ToString() != valueStr)
                    {
                        config_e["priority"] = valueStr;
                        needsWrite = true;
                    }
                    break;
                case "Category":
                    if (!config_e.ContainsKey("category") || config_e["category"]?.ToString() != valueStr)
                    {
                        config_e["category"] = valueStr;
                        needsWrite = true;
                    }
                    break;
                case "Note":
                    if (!config_e.ContainsKey("note") || config_e["note"]?.ToString() != valueStr)
                    {
                        config_e["note"] = valueStr;
                        needsWrite = true;
                    }
                    break;
                default:
                    Global.logger.WriteLine($"Unknown column '{column}' for config_e.toml update.", LoggerType.Warning);
                    return; // 不明なカラムなら何もしない
            }

            if (needsWrite)
            {
                await TryWriteTomlAsync(configPath_e, config_e);
                //Global.logger.WriteLine($"Updated '{column}' in config_e.toml for {m.name}.", LoggerType.Debug);
            }
        }

        // Triggered when priority is switched on drag and dropped
        private async void ModGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Global.UpdateConfig();
            await Task.Run(() => ModLoader.Build());
        }
        private TomlTable AddInclude(TomlTable config)
        {
            if (!config.ContainsKey("include"))
            {
                config.Add("include", new string[1] { "." });
            }
            return config;
        }

        public static bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
               ? Application.Current.Windows.OfType<T>().Any()
               : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }

        // このメソッドはUIスレッドから呼び出される想定
        private bool ConfirmConfigCreation(string configPath, Mod m, bool enabled)
        {
            var choices = new List<Choice>();
            choices.Add(new Choice()
            {
                OptionText = "Yes",
                OptionSubText = $"Create a new config.toml file in mod: {m.name} with the default values. (Recommended if you are testing/creating a mod.",
                Index = 0
            });
            choices.Add(new Choice()
            {
                OptionText = $"No",
                OptionSubText = $"Do not create a new config.toml file in mod: {m.name}. (Recommended if you are still installing this mod)",
                Index = 1
            });
            var choiceWindow = new ChoiceWindow(choices, $"No config.toml file found, create one?");
            choiceWindow.ShowDialog(); // 同期的にダイアログを表示

            switch (choiceWindow.choice) // choice は nullable int?
            {
                case 0: // Yes
                    m.enabled = enabled; // 引数で渡された enabled 状態を反映
                    // ファイル書き込みはこのメソッドの外 (呼び出し元) で非同期に行う
                    return true; // 作成を選択したことを示す
                case 1: // No
                    Global.logger.WriteLine($"User chose to not create a config.toml file for the aforementioned mod.", LoggerType.Info);
                    return false; // 作成しないことを示す
                default: // Close button or unexpected value
                    Global.logger.WriteLine($"Config creation dialog closed without selection for {m.name}.", LoggerType.Info);
                    return false; // 作成しないことを示す
            }
            // --- 注意 ---
            // 元のコードでは case 0 の中で同期的に File.WriteAllText を呼んでいたが、
            // このメソッドを bool を返すだけにして、ファイル書き込みは呼び出し元の非同期メソッド (ProcessModDirectoryAsync)
            // で await TryWriteTomlAsync を使って行う方が良い。
        }

        private bool SetupGame()
        {
            var index = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                index = GameBox.SelectedIndex;
            });
            var game = (GameFilter)index;
            switch (game)
            {
                case GameFilter.MMP:
                    if (Setup.Generic("DivaMegaMix.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaMegaMix.exe"))
                    {
                        Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
                        return true;
                    }
                    else
                    {
                        Global.logger.WriteLine($"Failed to complete setup for {Global.config.CurrentGame}, please try again.", LoggerType.Error);
                        return false;
                    }
            }
            return false;
        }

        // Setup_Click などで ModsWatcher を再設定する際の処理を変更
        private async void Setup_Click(object sender, RoutedEventArgs e)
        {
            if (Global.SearchModListFlg)
            {
                MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            GameBox.IsEnabled = false;
            await Task.Run(() =>
            {
                var index = 0;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    index = GameBox.SelectedIndex;
                });
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                    || !String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
                {
                    var dialogResult = MessageBox.Show($@"Run setup again?", $@"Notification", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (dialogResult == MessageBoxResult.No)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            GameBox.IsEnabled = true;
                        });
                        return;
                    }
                }
                if (SetupGame())
                {
                    Dispatcher.Invoke(async () => // async を追加
                    {
                        InitializeFileSystemWatcherAndTimer();
                        StartWatching();
                        await RefreshAsync(); // ★非同期版を呼び出す
                        LaunchButton.IsEnabled = true;
                    });
                }
            });
            GameBox.IsEnabled = true;
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (Global.config.Configs[Global.config.CurrentGame].Launcher != null && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
            {
                InitSearchMod();
                Global.UpdateConfig();

                var path = Global.config.Configs[Global.config.CurrentGame].Launcher;
                try
                {
                    Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex = LauncherOptionsBox.SelectedIndex;
                    Global.UpdateConfig();
                    if (Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex > 0)
                    {
                        var id = "";
                        switch ((GameFilter)GameBox.SelectedIndex)
                        {
                            case GameFilter.MMP:
                                id = "1761390";
                                break;
                        }
                        path = $"steam://rungameid/{id}";
                    }
                    Global.logger.WriteLine($"Launching {path}", LoggerType.Info);
                    var ps = new ProcessStartInfo(path)
                    {
                        WorkingDirectory = Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher),
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                    WindowState = WindowState.Minimized;
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Couldn't launch {path} ({ex.Message})", LoggerType.Error);
                }
            }
            else
                Global.logger.WriteLine($"Please click Setup before launching!", LoggerType.Warning);
        }
        private void Github_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ps = new ProcessStartInfo($"https://github.com/enomoto-r02/DivaModManager-by-Enomoto/releases")
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up Github ({ex.Message})", LoggerType.Error);
            }
        }
        private void GameBanana_Click(object sender, RoutedEventArgs e)
        {
            var id = "";
            switch ((GameFilter)GameFilterBox.SelectedIndex)
            {
                case GameFilter.MMP:
                    id = "16522";
                    break;
            }
            try
            {
                var ps = new ProcessStartInfo($"https://gamebanana.com/games/{id}")
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up GameBanana ({ex.Message})", LoggerType.Error);
            }
        }
        private void DMA_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ps = new ProcessStartInfo($"https://divamodarchive.com")
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up DivaModArchive ({ex.Message})", LoggerType.Error);
            }
        }
        private void DMADonate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ps = new ProcessStartInfo($"https://ko-fi.com/brogamer")
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up Ko-Fi ({ex.Message})", LoggerType.Error);
            }
        }
        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var discordLink = "https://discord.gg/cvBVGDZ";
                var ps = new ProcessStartInfo(discordLink)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine(ex.Message, LoggerType.Error);
            }
        }
        private void ScrollToBottom(object sender, TextChangedEventArgs args)
        {
            ConsoleWindow.ScrollToEnd();
        }

        private void ModGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null)
            {
                return;
            }
            if (ModGrid.SelectedItem == null)
            {
                element.ContextMenu.Visibility = Visibility.Collapsed;
            }
            else
            {
                element.ContextMenu.Visibility = Visibility.Visible;

                var SelectModsCount = ModGrid.SelectedCells.Count / ModGrid.Columns.Count;
                if (Global.SearchModListFlg || SelectModsCount > 1)
                {
                    // Restrict the context menu being searched.
                    List<string> inactiveList = new List<string>();
                    inactiveList.Add("MoveToTop");
                    inactiveList.Add("MoveToBottom");

                    for (var i = 0; i < element.ContextMenu.Items.Count; i++)
                    {
                        var contextMenu = element.ContextMenu.Items[i] as MenuItem;
                        if (contextMenu != null && inactiveList.Contains(contextMenu.Name))
                        {
                            contextMenu.IsEnabled = false;
                        }

                    }
                }
                else
                {
                    for (var i = 0; i < element.ContextMenu.Items.Count; i++)
                    {
                        MenuItem contextMenu = element.ContextMenu.Items[i] as MenuItem;
                        if (contextMenu != null)
                        {
                            contextMenu.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
            {
                if (row != null)
                {
                    var dialogResult = MessageBox.Show($@"Are you sure you want to delete {row.name}?" + Environment.NewLine + "This cannot be undone.", $@"Deleting {row.name}: Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Global.logger.WriteLine($@"Deleting {row.name}.", LoggerType.Info);
                            await Task.Run(() => Directory.Delete($@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{row.name}", true));
                            ShowMetadata(null);
                        }
                        catch (Exception ex)
                        {
                            Global.logger.WriteLine($@"Couldn't delete {row.name} ({ex.Message})", LoggerType.Error);
                        }
                    }
                }
            }
        }

        // Window_Closing でリソースを破棄
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // --- Watcher と Timer を破棄 ---
            DisposeWatcherAndTimer();
            // ------------------------------

            if (WindowState == WindowState.Maximized)
            {
                Global.config.Height = RestoreBounds.Height;
                Global.config.Width = RestoreBounds.Width;
                Global.config.Maximized = true;
            }
            else
            {
                Global.config.Height = Height;
                Global.config.Width = Width;
                Global.config.Maximized = false;
            }
            Global.config.TopGridHeight = MainGrid.RowDefinitions[2].Height.Value;
            Global.config.BottomGridHeight = MainGrid.RowDefinitions[4].Height.Value;
            Global.config.LeftGridWidth = MiddleGrid.ColumnDefinitions[0].Width.Value;
            Global.config.RightGridWidth = MiddleGrid.ColumnDefinitions[2].Width.Value;
            InitSearchMod();
            SetColumnDisplayIndex();
            SetColumnVisible();
            Global.UpdateConfig(); // この前に破棄処理を入れるべきか？終了処理内なので問題ないか。
            System.Windows.Application.Current.Shutdown();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
            {
                if (row != null)
                {
                    var folderName = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{row.name}";
                    if (Directory.Exists(folderName))
                    {
                        try
                        {
                            Process process = Process.Start("explorer.exe", folderName);
                            Global.logger.WriteLine($@"Opened {folderName}.", LoggerType.Info);
                        }
                        catch (Exception ex)
                        {
                            Global.logger.WriteLine($@"Couldn't open {folderName}. ({ex.Message})", LoggerType.Error);
                        }
                    }
                }
            }
        }
        // RenameMod_Click での一時停止処理を変更
        private async void RenameMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);

            // --- 監視を一時停止 ---
            StopWatching();
            // ---------------------

            foreach (var row in temp)
            {
                if (row != null)
                {
                    EditWindow ew = new EditWindow(row.name, true);
                    ew.ShowDialog();
                    // EditWindow内で実際にリネームが行われたかどうかの結果を受け取るのが望ましい
                }
            }

            // --- 監視を再開 ---
            StartWatching();
            // -----------------

            Global.UpdateConfig();
            ModGrid.Items.Refresh(); // ViewModelを使えば不要になる可能性

            await Task.Run(() => ModLoader.Build()); // 非同期化推奨

            ModGrid.Focus();
        }


        private void ConfigureModItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    ConfigureModWindow cmw = new ConfigureModWindow(row);
                    cmw.ShowDialog();
                }
        }
        private void FetchItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    FetchWindow fw = new FetchWindow(row);
                    fw.ShowDialog();
                    if (fw.success)
                        ShowMetadata(row.name);
                }
        }
        private async void MoveToTop_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var allMods = Global.ModList;
            Global.ModList.Move(ModGrid.SelectedIndex, 0);

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ModGrid.ItemsSource = Global.ModList;
                });
            });
            Global.UpdateConfig();
            await Task.Run(() => ModLoader.Build());

            e.Handled = true;
        }
        private async void MoveToBottom_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var allMods = Global.ModList;
            Global.ModList.Move(ModGrid.SelectedIndex, Global.ModList.Count - 1);

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ModGrid.ItemsSource = Global.ModList;
                });
            });
            Global.UpdateConfig();
            await Task.Run(() => ModLoader.Build());

            e.Handled = true;
        }


        private void Add_Enter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Move;
                DropBox.Visibility = Visibility.Visible;
            }
        }
        private void Add_Leave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            DropBox.Visibility = Visibility.Collapsed;
        }
        private async void Add_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                DropBox.Visibility = Visibility.Collapsed;
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                await Task.Run(() => ExtractPackages(fileList));
            }
            DropBox.Visibility = Visibility.Collapsed;
        }
        private void ExtractPackages(string[] fileList)
        {
            var temp = $"{Global.assemblyLocation}{Global.s}temp";
            foreach (var file in fileList)
            {
                Directory.CreateDirectory(temp);
                if (Directory.Exists(file))
                {
                    Global.logger.WriteLine($@"Moving {file} into {Global.config.Configs[Global.config.CurrentGame].ModsFolder}", LoggerType.Info);
                    string path = $@"{temp}{Global.s}{Path.GetFileName(file)}";
                    int index = 2;
                    while (Directory.Exists(path))
                    {
                        path = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{Path.GetFileName(file)} ({index})";
                        index += 1;
                    }
                    MoveDirectory(file, path);
                }
                else if (Path.GetExtension(file).ToLower() == ".7z" || Path.GetExtension(file).ToLower() == ".rar" || Path.GetExtension(file).ToLower() == ".zip")
                {
                    string _ArchiveSource = file;
                    string _ArchiveType = Path.GetExtension(file);
                    if (File.Exists(_ArchiveSource))
                    {
                        try
                        {
                            if (Path.GetExtension(_ArchiveSource).Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                            {
                                using (var archive = SevenZipArchive.Open(_ArchiveSource))
                                {
                                    var reader = archive.ExtractAllEntries();
                                    while (reader.MoveToNextEntry())
                                    {
                                        if (!reader.Entry.IsDirectory)
                                            reader.WriteEntryToDirectory(temp, new ExtractionOptions()
                                            {
                                                ExtractFullPath = true,
                                                Overwrite = true
                                            });
                                    }
                                }
                            }
                            else
                            {
                                using (Stream stream = File.OpenRead(_ArchiveSource))
                                using (var reader = ReaderFactory.Open(stream))
                                {
                                    while (reader.MoveToNextEntry())
                                    {
                                        if (!reader.Entry.IsDirectory)
                                        {
                                            reader.WriteEntryToDirectory(temp, new ExtractionOptions()
                                            {
                                                ExtractFullPath = true,
                                                Overwrite = true
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show($"Couldn't extract {file}: {e.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        // ドロップしたファイルを削除しないよう修正
                        //File.Delete(_ArchiveSource);
                    }
                }
                foreach (var folder in Directory.GetDirectories(temp, "*", SearchOption.AllDirectories).Where(x => File.Exists($@"{x}{Global.s}config.toml")))
                {
                    string path = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{Path.GetFileName(folder)}";
                    int index = 2;
                    while (Directory.Exists(path))
                    {
                        path = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{Path.GetFileName(folder)} ({index})";
                        index += 1;
                    }
                    MoveDirectory(folder, path);
                }
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
        }
        private static void MoveDirectory(string sourcePath, string targetPath)
        {
            //Copy all the files & Replaces any files with the same name
            foreach (var path in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                var newPath = path.Replace(sourcePath, targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                File.Copy(path, newPath, true);
            }
        }
        private void CreateMod_Click(object sender, RoutedEventArgs e)
        {
            if (Global.SearchModListFlg)
            {
                MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
                return;
            }
            var cmw = new CreateModWindow();
            cmw.Show();
        }
        private void UpdateAll_Click(object sender, RoutedEventArgs e)
        {
            UpdateCommon(sender, e, false);
        }
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            UpdateCommon(sender, e, true);
        }
        private void UpdateCommon(object sender, RoutedEventArgs e, bool isSelectedUpdate)
        {
            App.Current.Dispatcher.Invoke(async () =>
            {
                IsEnabledControls(false);
                Global.logger.WriteLine("Checking for mod updates...", LoggerType.Info);
                await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this, isSelectedUpdate);
                Global.logger.WriteLine("Checking for Diva Mod Manager update...", LoggerType.Info);
                if (await AutoUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    Close();
                Global.logger.WriteLine("Checking for DivaModLoader update...", LoggerType.Info);
                await Setup.CheckForDMLUpdate(new CancellationTokenSource());
                IsEnabledControls(true);
            });
        }
        private Paragraph ConvertToFlowParagraph(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);


            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };

                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        var ps = new ProcessStartInfo(segment)
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(ps);
                    };

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return paragraph;
        }

        private void ShowMetadata(string mod)
        {
            FlowDocument descFlow = new FlowDocument();
            // Set image
            string path = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}";
            FileInfo[] previewFiles = new DirectoryInfo(path).GetFiles("Preview.*");
            // Add info from mod.json and config.toml
            if (File.Exists($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}mod.json")
                || File.Exists($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}config.toml"))
            {
                Metadata metadata = null;
                if (File.Exists($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}mod.json"))
                {
                    var metadataString = File.ReadAllText($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}mod.json");
                    metadata = JsonSerializer.Deserialize<Metadata>(metadataString);
                }

                TomlTable config = null;
                if (File.Exists($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}config.toml"))
                {
                    var configPath = $"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{mod}{Global.s}config.toml";
                    var configString = File.ReadAllText(configPath);
                    if (!Toml.TryToModel(configString, out config, out var diagnostics))
                    {
                        Global.logger.WriteLine($"{diagnostics[0].Message} for {mod}. Rewriting {configPath} with only the enabled & include fields", LoggerType.Warning);
                        config = new();
                        var enabled = Global.ModList.ToList().Find(x => x.name == mod).enabled;
                        config.Add("enabled", enabled);
                        AddInclude(config);
                        File.WriteAllText(configPath, Toml.FromModel(config));
                    }
                }

                var para = new Paragraph();
                var text = String.Empty;
                if (config != null && config.ContainsKey("author") && (config["author"] as string).Length > 0)
                    text += $"Author: {config["author"]}\n";
                else if (metadata != null)
                {
                    if (metadata.submitter != null)
                    {
                        para.Inlines.Add($"Submitter: ");
                        if (metadata.avi != null && metadata.avi.ToString().Length > 0)
                        {
                            BitmapImage bm = new BitmapImage(metadata.avi);
                            Image image = new Image();
                            image.Source = bm;
                            image.Height = 35;
                            para.Inlines.Add(image);
                            para.Inlines.Add(" ");
                        }
                        if (metadata.upic != null && metadata.upic.ToString().Length > 0)
                        {
                            BitmapImage bm = new BitmapImage(metadata.upic);
                            Image image = new Image();
                            image.Source = bm;
                            image.Height = 25;
                            para.Inlines.Add(image);
                        }
                        else
                            para.Inlines.Add($"{metadata.submitter}");
                        descFlow.Blocks.Add(para);
                    }
                }
                if (config != null && config.ContainsKey("version") && (config["version"] as string).Length > 0)
                {
                    text += $"Version: {config["version"]}";
                    if (config.ContainsKey("date") && config["date"].ToString().Length > 0)
                        text += "\n";
                }
                if (config != null && config.ContainsKey("date") && config["date"].ToString().Length > 0)
                    text += $"Date: {config["date"]}";
                if (metadata != null && !String.IsNullOrEmpty(metadata.cat))
                {
                    if (!String.IsNullOrWhiteSpace(text))
                    {
                        var init = ConvertToFlowParagraph(text);
                        descFlow.Blocks.Add(init);
                    }
                    text = String.Empty;
                    para = new Paragraph();
                    para.Inlines.Add("Category: ");
                    if (metadata.caticon != null && metadata.caticon.ToString().Length > 0)
                    {
                        BitmapImage bm = new BitmapImage(metadata.caticon);
                        Image image = new Image();
                        image.Source = bm;
                        image.Width = 20;
                        para.Inlines.Add(image);
                    }
                    para.Inlines.Add($" {metadata.cat}");
                    descFlow.Blocks.Add(para);
                }
                else if (!String.IsNullOrWhiteSpace(text))
                    text += "\n";

                if (config != null && config.ContainsKey("description") && (config["description"] as string).Length > 0)
                    text += $"Description: {config["description"]}\n";
                else if (metadata != null && metadata.description != null && metadata.description.Length > 0)
                    text += $"Description: {metadata.description}\n";
                if (metadata != null && metadata.homepage != null && metadata.homepage.ToString().Length > 0)
                    text += $"Home Page: {metadata.homepage}";
                if (!String.IsNullOrWhiteSpace(text))
                {
                    var init = ConvertToFlowParagraph(text);
                    descFlow.Blocks.Add(init);
                }
                if (previewFiles.Length > 0)
                {
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(previewFiles[0].FullName);
                        var stream = new MemoryStream(imageBytes);
                        var img = new BitmapImage();

                        img.BeginInit();
                        img.StreamSource = stream;
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.EndInit();
                        ImageBehavior.SetAnimatedSource(Preview, img);
                        ImageBehavior.SetAnimatedSource(PreviewBG, img);
                    }
                    catch (Exception ex)
                    {
                        Global.logger.WriteLine(ex.Message, LoggerType.Error);
                    }
                }
                else if (metadata != null && metadata.preview != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = metadata.preview;
                    bitmap.EndInit();
                    ImageBehavior.SetAnimatedSource(Preview, bitmap);
                    ImageBehavior.SetAnimatedSource(PreviewBG, bitmap);
                }
                else
                {
                    var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
                    ImageBehavior.SetAnimatedSource(Preview, bitmap);
                    ImageBehavior.SetAnimatedSource(PreviewBG, null);
                }
            }
            else if (previewFiles.Length > 0)
            {
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(previewFiles[0].FullName);
                    var stream = new MemoryStream(imageBytes);
                    var img = new BitmapImage();

                    img.BeginInit();
                    img.StreamSource = stream;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    ImageBehavior.SetAnimatedSource(Preview, img);
                    ImageBehavior.SetAnimatedSource(PreviewBG, img);
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine(ex.Message, LoggerType.Error);
                }
            }
            // Set preview if no mod.json or preview exists
            else
            {
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
                ImageBehavior.SetAnimatedSource(Preview, bitmap);
                ImageBehavior.SetAnimatedSource(PreviewBG, null);
            }
            // Default preview if no config.toml or mod.json
            if (descFlow.Blocks.Count == 0)
                DescriptionWindow.Document = defaultFlow;
            else
            {
                DescriptionWindow.Document = descFlow;
                var descriptionText = new TextRange(DescriptionWindow.Document.ContentStart, DescriptionWindow.Document.ContentEnd);
                descriptionText.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Center);
            }
        }
        private void ModGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Mod row = (Mod)ModGrid.SelectedItem;
            if (row != null)
                ShowMetadata(row.name);
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            new ModDownloader().BrowserDownload(Global.games[GameFilterBox.SelectedIndex], item);
        }
        private void DMADownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            new ModDownloader().DMABrowserDownload(Global.games[GameBox.SelectedIndex], item);
        }
        private void AltDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            new AltLinkWindow(item.AlternateFileSources, item.Title,
                (((GameFilterBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty),
                item.Link.AbsoluteUri).ShowDialog();
        }
        private void Homepage_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            try
            {
                var ps = new ProcessStartInfo(item.Link.ToString())
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up {item.Link} ({ex.Message})", LoggerType.Error);
            }
        }
        private void DMAHomepage_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            try
            {
                var ps = new ProcessStartInfo(item.Link.ToString())
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up {item.Link} ({ex.Message})", LoggerType.Error);
            }
        }
        private int imageCounter;
        private int imageCount;
        private FlowDocument ConvertToFlowDocument(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);


            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };

                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        var ps = new ProcessStartInfo(segment)
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(ps);
                    };

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return flowDocument;
        }
        private void MoreInfo_Click(object sender, RoutedEventArgs e)
        {
            HomepageButton.Content = $"{(TypeBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().TrimEnd('s')} Page";
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (item.Compatible)
                DownloadButton.Visibility = Visibility.Visible;
            else
                DownloadButton.Visibility = Visibility.Collapsed;
            if (item.HasAltLinks)
                AltButton.Visibility = Visibility.Visible;
            else
                AltButton.Visibility = Visibility.Collapsed;
            DescPanel.DataContext = button.DataContext;
            MediaPanel.DataContext = button.DataContext;
            DescText.ScrollToHome();
            var text = "";
            text += item.ConvertedText;
            DescText.Document = ConvertToFlowDocument(text);
            ImageLeft.IsEnabled = true;
            ImageRight.IsEnabled = true;
            BigImageLeft.IsEnabled = true;
            BigImageRight.IsEnabled = true;
            imageCount = item.Media.Where(x => x.Type == "image").ToList().Count;
            imageCounter = 0;
            if (imageCount > 0)
            {
                Grid.SetColumnSpan(DescText, 1);
                ImagePanel.Visibility = Visibility.Visible;
                var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
                Screenshot.Source = image;
                BigScreenshot.Source = image;
                CaptionText.Text = item.Media[imageCounter].Caption;
                BigCaptionText.Text = item.Media[imageCounter].Caption;
                if (!String.IsNullOrEmpty(CaptionText.Text))
                {
                    BigCaptionText.Visibility = Visibility.Visible;
                    CaptionText.Visibility = Visibility.Visible;
                }
                else
                {
                    BigCaptionText.Visibility = Visibility.Collapsed;
                    CaptionText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Grid.SetColumnSpan(DescText, 2);
                ImagePanel.Visibility = Visibility.Collapsed;
            }
            if (imageCount == 1)
            {
                ImageLeft.IsEnabled = false;
                ImageRight.IsEnabled = false;
                BigImageLeft.IsEnabled = false;
                BigImageRight.IsEnabled = false;
            }

            DescPanel.Visibility = Visibility.Visible;
        }
        private void DMAMoreInfo_Click(object sender, RoutedEventArgs e)
        {
            DMAHomepageButton.Content = $"Mod Page";
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            DMADescPanel.DataContext = button.DataContext;
            DMAMediaPanel.DataContext = button.DataContext;
            DMADescText.ScrollToHome();
            var text = "";
            text += item.Text;
            DMADescText.Document = ConvertToFlowDocument(text);
            DMAImageLeft.IsEnabled = true;
            DMAImageRight.IsEnabled = true;
            DMABigImageLeft.IsEnabled = true;
            DMABigImageRight.IsEnabled = true;
            imageCount = item.Images.Count;
            imageCounter = 0;
            if (imageCount > 0)
            {
                Grid.SetColumnSpan(DMADescText, 1);
                DMAImagePanel.Visibility = Visibility.Visible;
                var image = new BitmapImage(item.Images[imageCounter]);
                DMAScreenshot.Source = image;
                DMABigScreenshot.Source = image;
            }
            else
            {
                Grid.SetColumnSpan(DMADescText, 2);
                DMAImagePanel.Visibility = Visibility.Collapsed;
            }
            if (imageCount == 1)
            {
                DMAImageLeft.IsEnabled = false;
                DMAImageRight.IsEnabled = false;
                DMABigImageLeft.IsEnabled = false;
                DMABigImageRight.IsEnabled = false;
            }

            DMADescPanel.Visibility = Visibility.Visible;
        }
        private void DMACloseDesc_Click(object sender, RoutedEventArgs e)
        {
            DMADescPanel.Visibility = Visibility.Collapsed;
        }
        private void CloseDesc_Click(object sender, RoutedEventArgs e)
        {
            DescPanel.Visibility = Visibility.Collapsed;
        }
        private void DMACloseMedia_Click(object sender, RoutedEventArgs e)
        {
            DMAMediaPanel.Visibility = Visibility.Collapsed;
        }
        private void CloseMedia_Click(object sender, RoutedEventArgs e)
        {
            MediaPanel.Visibility = Visibility.Collapsed;
        }

        private void DMAImage_Click(object sender, RoutedEventArgs e)
        {
            DMAMediaPanel.Visibility = Visibility.Visible;
        }
        private void Image_Click(object sender, RoutedEventArgs e)
        {
            MediaPanel.Visibility = Visibility.Visible;
        }
        private void DMAImageLeft_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            if (--imageCounter == -1)
                imageCounter = imageCount - 1;
            var image = new BitmapImage(item.Images[imageCounter]);
            DMAScreenshot.Source = image;
            DMABigScreenshot.Source = image;
        }

        private void ImageLeft_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (--imageCounter == -1)
                imageCounter = imageCount - 1;
            var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
            Screenshot.Source = image;
            CaptionText.Text = item.Media[imageCounter].Caption;
            BigScreenshot.Source = image;
            BigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!String.IsNullOrEmpty(CaptionText.Text))
            {
                BigCaptionText.Visibility = Visibility.Visible;
                CaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                BigCaptionText.Visibility = Visibility.Collapsed;
                CaptionText.Visibility = Visibility.Collapsed;
            }
        }
        private void DMAImageRight_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            if (++imageCounter == imageCount)
                imageCounter = 0;
            var image = new BitmapImage(item.Images[imageCounter]);
            DMAScreenshot.Source = image;
            DMABigScreenshot.Source = image;
        }
        private void ImageRight_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (++imageCounter == imageCount)
                imageCounter = 0;
            var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
            Screenshot.Source = image;
            CaptionText.Text = item.Media[imageCounter].Caption;
            BigScreenshot.Source = image;
            BigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!String.IsNullOrEmpty(CaptionText.Text))
            {
                BigCaptionText.Visibility = Visibility.Visible;
                CaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                BigCaptionText.Visibility = Visibility.Collapsed;
                CaptionText.Visibility = Visibility.Collapsed;
            }
        }
        private static bool selected = false;

        private static Dictionary<GameFilter, Dictionary<TypeFilter, List<GameBananaCategory>>> cats = new();

        private static readonly List<GameBananaCategory> All = new GameBananaCategory[]
        {
            new GameBananaCategory()
            {
                Name = "All",
                ID = null
            }
        }.ToList();
        private static readonly List<GameBananaCategory> None = new GameBananaCategory[]
        {
            new GameBananaCategory()
            {
                Name = "- - -",
                ID = null
            }
        }.ToList();
        private async void InitializeBrowser()
        {
            using (var httpClient = new HttpClient())
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
                // Initialize categories and games
                var gameIDS = new string[] { "16522" };
                var types = new string[] { "Mod", "Wip", "Sound" };
                var gameCounter = 0;
                foreach (var gameID in gameIDS)
                {
                    var counter = 0;
                    double totalPages = 0;
                    foreach (var type in types)
                    {
                        var requestUrl = $"https://gamebanana.com/apiv4/{type}Category/ByGame?_aGameRowIds[]={gameID}&_sRecordSchema=Custom" +
                            "&_csvProperties=_idRow,_sName,_sProfileUrl,_sIconUrl,_idParentCategoryRow&_nPerpage=50";
                        string responseString = "";
                        try
                        {
                            var responseMessage = await httpClient.GetAsync(requestUrl);
                            responseString = await responseMessage.Content.ReadAsStringAsync();
                            responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                            var numRecords = responseMessage.GetHeader("X-GbApi-Metadata_nRecordCount");
                            if (numRecords != -1)
                            {
                                totalPages = Math.Ceiling(numRecords / 50);
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            switch (Regex.Match(ex.Message, @"\d+").Value)
                            {
                                case "443":
                                    BrowserMessage.Text = "Your internet connection is down.";
                                    break;
                                case "500":
                                case "503":
                                case "504":
                                    BrowserMessage.Text = "GameBanana's servers are down.";
                                    break;
                                default:
                                    BrowserMessage.Text = ex.Message;
                                    break;
                            }
                            return;
                        }
                        catch (Exception ex)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            BrowserMessage.Text = ex.Message;
                            return;
                        }
                        List<GameBananaCategory> response = new();
                        try
                        {
                            response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                        }
                        catch (Exception)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            BrowserMessage.Text = "Uh oh! Something went wrong while deserializing the categories...";
                            return;
                        }
                        if (!cats.ContainsKey((GameFilter)gameCounter))
                            cats.Add((GameFilter)gameCounter, new Dictionary<TypeFilter, List<GameBananaCategory>>());
                        if (!cats[(GameFilter)gameCounter].ContainsKey((TypeFilter)counter))
                            cats[(GameFilter)gameCounter].Add((TypeFilter)counter, response);

                        // Make more requests if needed
                        if (totalPages > 1)
                        {
                            for (double i = 2; i <= totalPages; i++)
                            {
                                var requestUrlPage = $"{requestUrl}&_nPage={i}";
                                try
                                {
                                    responseString = await httpClient.GetStringAsync(requestUrlPage);
                                    responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                                }
                                catch (HttpRequestException ex)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    switch (Regex.Match(ex.Message, @"\d+").Value)
                                    {
                                        case "443":
                                            BrowserMessage.Text = "Your internet connection is down.";
                                            break;
                                        case "500":
                                        case "503":
                                        case "504":
                                            BrowserMessage.Text = "GameBanana's servers are down.";
                                            break;
                                        default:
                                            BrowserMessage.Text = ex.Message;
                                            break;
                                    }
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    BrowserMessage.Text = ex.Message;
                                    return;
                                }
                                try
                                {
                                    response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                                }
                                catch (Exception)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    BrowserMessage.Text = "Uh oh! Something went wrong while deserializing the categories...";
                                    return;
                                }
                                cats[(GameFilter)gameCounter][(TypeFilter)counter] = cats[(GameFilter)gameCounter][(TypeFilter)counter].Concat(response).ToList();
                            }
                        }
                        counter++;
                    }
                    gameCounter++;
                }
            }
            filterSelect = true;
            GameFilterBox.SelectedIndex = GameBox.SelectedIndex;
            FilterBox.ItemsSource = FilterBoxList;
            CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
            SubCatBox.ItemsSource = None;
            CatBox.SelectedIndex = 0;
            SubCatBox.SelectedIndex = 0;
            FilterBox.SelectedIndex = 1;
            filterSelect = false;
            RefreshFilter();
            selected = true;
        }
        private void OnBrowserTabSelected(object sender, RoutedEventArgs e)
        {
            if (!selected)
                InitializeBrowser();
        }
        private void OnDMABrowserTabSelected(object sender, RoutedEventArgs e)
        {
            if (!DMAselected)
                DMARefreshFilter();
        }
        private void OnManagerTabSelected(object sender, RoutedEventArgs e)
        {

        }

        private static int page = 1;
        private static int DMApage = 1;
        private void DecrementPage(object sender, RoutedEventArgs e)
        {
            --page;
            RefreshFilter();
        }
        private void IncrementPage(object sender, RoutedEventArgs e)
        {
            ++page;
            RefreshFilter();
        }
        private void DMADecrementPage(object sender, RoutedEventArgs e)
        {
            --DMApage;
            DMARefreshFilter();
        }
        private void DMAIncrementPage(object sender, RoutedEventArgs e)
        {
            ++DMApage;
            DMARefreshFilter();
        }
        private void DMABrowserRefresh(object sender, RoutedEventArgs e)
        {
        }
        private void BrowserRefresh(object sender, RoutedEventArgs e)
        {
            if (!selected)
                InitializeBrowser();
            else
                RefreshFilter();
        }
        private void ClearCache(object sender, RoutedEventArgs e)
        {
            FeedGenerator.ClearCache();
            RefreshFilter();
        }
        private void DMAClearCache(object sender, RoutedEventArgs e)
        {
            DMAFeedGenerator.ClearCache();
            DMARefreshFilter();
        }
        private static bool filterSelect;
        private static bool searched = false;
        private async void RefreshFilter()
        {
            NSFWCheckbox.IsEnabled = false;
            SearchBar.IsEnabled = false;
            SearchButton.IsEnabled = false;
            GameFilterBox.IsEnabled = false;
            FilterBox.IsEnabled = false;
            TypeBox.IsEnabled = false;
            CatBox.IsEnabled = false;
            SubCatBox.IsEnabled = false;
            PageLeft.IsEnabled = false;
            PageRight.IsEnabled = false;
            PageBox.IsEnabled = false;
            PerPageBox.IsEnabled = false;
            ClearCacheButton.IsEnabled = false;
            ErrorPanel.Visibility = Visibility.Collapsed;
            filterSelect = true;
            PageBox.SelectedValue = page;
            filterSelect = false;
            Page.Text = $"Page {page}";
            LoadingBar.Visibility = Visibility.Visible;
            FeedBox.Visibility = Visibility.Collapsed;

            try
            {
                var search = searched ? SearchBar.Text : null;
                if (!string.IsNullOrEmpty(search) && search.Contains("'"))
                {
                    search = search.Replace("'", "\\'");
                }
                await FeedGenerator.GetFeed(page, (GameFilter)GameFilterBox.SelectedIndex, (TypeFilter)TypeBox.SelectedIndex, (FeedFilter)FilterBox.SelectedIndex, (GameBananaCategory)CatBox.SelectedItem,
                    (GameBananaCategory)SubCatBox.SelectedItem, (PerPageBox.SelectedIndex + 1) * 10, (bool)NSFWCheckbox.IsChecked, search);
                FeedBox.ItemsSource = FeedGenerator.CurrentFeed.Records;
                if (FeedGenerator.error)
                {
                    LoadingBar.Visibility = Visibility.Collapsed;
                    ErrorPanel.Visibility = Visibility.Visible;
                    BrowserRefreshButton.Visibility = Visibility.Visible;
                    if (FeedGenerator.exception.Message.Contains("JSON tokens"))
                    {
                        BrowserMessage.Text = "Uh oh! Diva Mod Manager failed to deserialize the GameBanana feed.";
                        return;
                    }
                    switch (Regex.Match(FeedGenerator.exception.Message, @"\d+").Value)
                    {
                        case "443":
                            BrowserMessage.Text = "Your internet connection is down.";
                            break;
                        case "500":
                        case "503":
                        case "504":
                            BrowserMessage.Text = "GameBanana's servers are down.";
                            break;
                        default:
                            BrowserMessage.Text = FeedGenerator.exception.Message;
                            break;
                    }
                    return;
                }
                if (page < FeedGenerator.CurrentFeed.TotalPages)
                    PageRight.IsEnabled = true;
                if (page != 1)
                    PageLeft.IsEnabled = true;
                if (FeedBox.Items.Count > 0)
                {
                    FeedBox.ScrollIntoView(FeedBox.Items[0]);
                    FeedBox.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorPanel.Visibility = Visibility.Visible;
                    BrowserRefreshButton.Visibility = Visibility.Collapsed;
                    BrowserMessage.Visibility = Visibility.Visible;
                    BrowserMessage.Text = "Diva Mod Manager couldn't find any mods.";
                }
                PageBox.ItemsSource = Enumerable.Range(1, (int)(FeedGenerator.CurrentFeed.TotalPages));
            }
            finally
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                CatBox.IsEnabled = true;
                SubCatBox.IsEnabled = true;
                TypeBox.IsEnabled = true;
                FilterBox.IsEnabled = true;
                PageBox.IsEnabled = true;
                PerPageBox.IsEnabled = true;
                GameFilterBox.IsEnabled = true;
                SearchBar.IsEnabled = true;
                SearchButton.IsEnabled = true;
                NSFWCheckbox.IsEnabled = true;
                ClearCacheButton.IsEnabled = true;
            }
        }
        private static bool DMAselected = false;
        private async void DMARefreshFilter()
        {
            DMASearchBar.IsEnabled = false;
            DMASearchButton.IsEnabled = false;
            DMASortBox.IsEnabled = false;
            DMAFilterBox.IsEnabled = false;
            DMAClearCacheButton.IsEnabled = false;
            DMAPageLeft.IsEnabled = false;
            DMAPageRight.IsEnabled = false;
            DMAPageBox.IsEnabled = false;
            DMAFilterSelect = true;
            DMAPageBox.SelectedValue = DMApage;
            DMAPerPageBox.IsEnabled = false;
            DMAFilterSelect = false;
            DMAPage.Text = $"Page {DMApage}";
            DMAErrorPanel.Visibility = Visibility.Collapsed;
            DMALoadingBar.Visibility = Visibility.Visible;
            DMAFeedBox.Visibility = Visibility.Collapsed;
            var search = DMASearchBar.Text;
            try
            {
                await DMAFeedGenerator.GetFeed(DMApage, (DMAFeedSort)DMASortBox.SelectedIndex, (DMAFeedFilter)DMAFilterBox.SelectedIndex, search, (DMAPerPageBox.SelectedIndex + 1) * 10);
                DMAFeedBox.ItemsSource = DMAFeedGenerator.CurrentFeed.Posts;
                if (DMAFeedGenerator.error)
                {
                    DMALoadingBar.Visibility = Visibility.Collapsed;
                    DMAErrorPanel.Visibility = Visibility.Visible;
                    DMABrowserRefreshButton.Visibility = Visibility.Visible;
                    if (DMAFeedGenerator.exception.Message.Contains("JSON tokens"))
                    {
                        DMABrowserMessage.Text = "Uh oh! Diva Mod Manager failed to deserialize the DivaModArchive feed.";
                        return;
                    }
                    switch (Regex.Match(DMAFeedGenerator.exception.Message, @"\d+").Value)
                    {
                        case "443":
                            DMABrowserMessage.Text = "Your internet connection is down.";
                            break;
                        case "500":
                        case "503":
                        case "504":
                            DMABrowserMessage.Text = "DivaModArchive's servers are down.";
                            break;
                        default:
                            DMABrowserMessage.Text = DMAFeedGenerator.exception.Message;
                            break;
                    }
                    return;
                }
                if (DMApage < DMAFeedGenerator.CurrentFeed.TotalPages)
                    DMAPageRight.IsEnabled = true;
                if (DMApage != 1)
                    DMAPageLeft.IsEnabled = true;
                if (DMAFeedBox.Items.Count > 0)
                {
                    DMAFeedBox.ScrollIntoView(DMAFeedBox.Items[0]);
                    DMAFeedBox.Visibility = Visibility.Visible;
                }
                else
                {
                    DMAErrorPanel.Visibility = Visibility.Visible;
                    DMABrowserRefreshButton.Visibility = Visibility.Collapsed;
                    DMABrowserMessage.Visibility = Visibility.Visible;
                    DMABrowserMessage.Text = "Diva Mod Manager couldn't find any mods.";
                }
                DMAPageBox.ItemsSource = Enumerable.Range(1, (int)(DMAFeedGenerator.CurrentFeed.TotalPages));
            }
            finally
            {
                DMALoadingBar.Visibility = Visibility.Collapsed;
                DMASortBox.IsEnabled = true;
                DMAFilterBox.IsEnabled = true;
                DMASearchBar.IsEnabled = true;
                DMASearchButton.IsEnabled = true;
                DMAClearCacheButton.IsEnabled = true;
                DMAPageBox.IsEnabled = true;
                DMAPerPageBox.IsEnabled = true;
                DMAselected = true;
            }
        }
        private bool DMAFilterSelect = false;
        private void DMAFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !DMAFilterSelect)
            {
                DMApage = 1;
                DMARefreshFilter();
            }
        }

        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                if (!searched || FilterBox.SelectedIndex != 3)
                {
                    filterSelect = true;
                    var temp = FilterBox.SelectedIndex;
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = temp;
                    filterSelect = false;
                }
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void PerPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                page = 1;
                RefreshFilter();
            }
        }
        private void DMAPerPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                DMApage = 1;
                DMARefreshFilter();
            }
        }
        private void GameFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                if (GameFilterBox.SelectedIndex != 5)
                    DiscordButton.Visibility = Visibility.Visible;
                else
                    DiscordButton.Visibility = Visibility.Collapsed;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void TypeFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void MainFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set Categories
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void SubFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = sender as UniformGrid;
            grid.Columns = (int)grid.ActualWidth / 400 + 1;
        }
        private void OnResize(object sender, RoutedEventArgs e)
        {
            BigScreenshot.MaxHeight = ActualHeight - 240;
        }

        private void PageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                page = (int)PageBox.SelectedValue;
                RefreshFilter();
            }
        }
        private void DMAPageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!DMAFilterSelect && IsLoaded)
            {
                DMApage = (int)DMAPageBox.SelectedValue;
                DMARefreshFilter();
            }
        }
        private void NSFWCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                if (searched)
                {
                    filterSelect = true;
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                    filterSelect = false;
                }
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }

        private void OnFirstOpen()
        {
            if (!Global.config.Configs[Global.config.CurrentGame].FirstOpen)
            {
                var choices = new List<Choice>();
                choices.Add(new Choice()
                {
                    OptionText = "Launch through Executable",
                    OptionSubText = "Launches the executable directly",
                    Index = 0
                });
                choices.Add(new Choice()
                {
                    OptionText = $"Launch through Steam",
                    OptionSubText = $"Uses the Steam shortcut to launch",
                    Index = 1
                });
                Dispatcher.Invoke(() =>
                {
                    var choice = new ChoiceWindow(choices, $"Launcher Options for {Global.config.CurrentGame}");
                    choice.ShowDialog();
                    if (choice.choice != null)
                    {
                        Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex = (int)choice.choice;
                        LauncherOptionsBox.SelectedIndex = (int)choice.choice;
                    }
                    else
                    {
                        Global.logger.WriteLine($"No launch option chosen, defaulting to Steam shortcut", LoggerType.Warning);
                        Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex = 1;
                        LauncherOptionsBox.SelectedIndex = 1;
                    }
                });
                Global.config.Configs[Global.config.CurrentGame].FirstOpen = true;
                Global.UpdateConfig();
                Global.logger.WriteLine($"If you want to switch the Launch Method, use the dropdown box to the right of the Launch Button", LoggerType.Info);

                if (SetupGame())
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Watch mods folder to detect
                        ModsWatcher = new FileSystemWatcher(Global.config.Configs[Global.config.CurrentGame].ModsFolder);
                        ModsWatcher.Created += OnFileSystemChanged;
                        ModsWatcher.Deleted += OnFileSystemChanged;
                        ModsWatcher.Renamed += OnFileSystemChanged;
                        //Refresh();
                        RefreshAsync(); // ★非同期版を呼び出す
                        ModsWatcher.EnableRaisingEvents = true;
                    });
                }
            }
        }
        private bool handle;

        private int GameBox_BeforeSelectedIndex;
        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || Global.config == null)
            {
                return;
            }

            if (Global.SearchModListFlg)
            {
                MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                GameBox.SelectionChanged -= GameBox_SelectionChanged;
                GameBox.SelectedIndex = GameBox_BeforeSelectedIndex;
                GameBox.SelectionChanged += GameBox_SelectionChanged;
                return;
            }

            ComboBox c = (ComboBox)sender;
            GameBox_BeforeSelectedIndex = c.SelectedIndex;
            Global.selected_game = c.Text;
            handle = true;

        }

        private int LauncherOptionsBox_BeforeSelectedIndex;
        private void LauncherOptionsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!handle)
            {
                if (Global.SearchModListFlg)
                {
                    MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                    LauncherOptionsBox.SelectionChanged -= LauncherOptionsBox_SelectionChanged;
                    LauncherOptionsBox.SelectedIndex = LauncherOptionsBox_BeforeSelectedIndex;
                    LauncherOptionsBox.SelectionChanged += LauncherOptionsBox_SelectionChanged;
                    return;
                }
                ComboBox c = (ComboBox)sender;
                LauncherOptionsBox_BeforeSelectedIndex = c.SelectedIndex;
                Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex = LauncherOptionsBox.SelectedIndex;
                Global.UpdateConfig();
            }
        }
        private async void LoadoutsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (LoadoutBox.SelectedItem != null)
            {
                InitSearchMod();
                Global.config.Configs[Global.config.CurrentGame].CurrentLoadout = LoadoutBox.SelectedItem.ToString();

                // Create loadout if it doesn't exist
                if (!Global.config.Configs[Global.config.CurrentGame].Loadouts.ContainsKey(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout))
                    Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout, new());
                else if (Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout] == null)
                    Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout] = new();

                Global.ModList = Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout];
                UpdateSearchMod();
                await RefreshAsync(); // ★非同期版を呼び出す
                Global.logger.WriteLine($"Loadout changed to {LoadoutBox.SelectedItem}", LoggerType.Info);
                // ModLoader.Build は RefreshAsync 内で実行されるように変更済み
            }
        }
        // EditLoadouts_Click で Refresh を呼び出す箇所を変更
        private void EditLoadouts_Click(object sender, RoutedEventArgs e)
        {
            if (Global.SearchModListFlg)
            {
                MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true; // イベント処理済みフラグ
                return;
            }

            var choices = new List<Choice>();
            choices.Add(new Choice()
            {
                OptionText = "Add New Loadout",
                OptionSubText = "Adds a new loadout starting with all mods in the same current enabled state in alphanumeric order",
                Index = 0
            });
            choices.Add(new Choice()
            {
                OptionText = $"Rename Current Loadout",
                OptionSubText = $"Changes the name of the current loadout",
                Index = 1
            });
            choices.Add(new Choice()
            {
                OptionText = $"Delete Current Loadout",
                OptionSubText = $"Deletes current loadout and switches to first available one",
                Index = 2
            });
            choices.Add(new Choice()
            {
                OptionText = $"Copy Current Loadout",
                OptionSubText = $"Copy Current loadout",
                Index = 3
            });

            // Dispatcher.InvokeAsync を使用してUIスレッドで非同期ラムダを実行
            Dispatcher.InvokeAsync(async () =>
            {
                var choiceWindow = new ChoiceWindow(choices, $"Loadout Options for {Global.config.CurrentGame}");
                // ShowDialog は UI スレッドで同期的に実行され、完了を待つ
                choiceWindow.ShowDialog();

                if (choiceWindow.choice != null)
                {
                    bool refreshNeeded = false; // Refreshが必要かどうかのフラグ（状況による）

                    switch ((int)choiceWindow.choice)
                    {
                        // Add new loadout
                        case 0:
                            var newLoadoutWindow = new EditWindow(null, false);
                            newLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(newLoadoutWindow.loadout))
                            {
                                // ObservableCollection への追加は UI スレッドでOK
                                Global.LoadoutItems.Add(newLoadoutWindow.loadout);
                                // LoadoutBox.SelectedItem の変更により SelectionChanged イベントが発生し、
                                // そのハンドラ内で RefreshAsync が呼ばれることを期待する
                                LoadoutBox.SelectedItem = newLoadoutWindow.loadout;
                                // refreshNeeded = true; // SelectionChangedで呼ばれない場合に備えるならフラグを立てる
                            }
                            break;
                        // Rename current loadout
                        case 1:
                            var renameLoadoutWindow = new EditWindow(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout, false);
                            renameLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(renameLoadoutWindow.loadout))
                            {
                                var originalLoadout = Global.config.Configs[Global.config.CurrentGame].CurrentLoadout;
                                var originalIndex = Global.LoadoutItems.IndexOf(originalLoadout);

                                // 新しいロードアウト名とModリスト（ディープコピーが必要か確認）を追加
                                // Modリストのコピー処理（元の case 3 を参考に Clone() を使うべきか検討）
                                ObservableCollection<Mod> ModList_Copy = new ObservableCollection<Mod>(Global.ModList.Select(m => m.Clone())); // Cloneメソッドがあると仮定
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(renameLoadoutWindow.loadout, ModList_Copy);

                                // UI上のリストを更新
                                if (originalIndex >= 0)
                                {
                                    Global.LoadoutItems.Insert(originalIndex, renameLoadoutWindow.loadout);
                                    Global.LoadoutItems.Remove(originalLoadout); // Insert後にRemove
                                }
                                else
                                {
                                    // 元の要素が見つからない場合 (エラーケース？)
                                    Global.LoadoutItems.Add(renameLoadoutWindow.loadout);
                                }

                                // 古い設定を削除
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Remove(originalLoadout);

                                // 選択を新しい名前に変更 (SelectionChanged発生を期待)
                                LoadoutBox.SelectedItem = renameLoadoutWindow.loadout;
                                // refreshNeeded = true;
                            }
                            break;
                        // Delete current loadout
                        case 2:
                            if (Global.config.Configs[Global.config.CurrentGame].Loadouts.Count <= 1)
                            {
                                Global.logger.WriteLine("Unable to delete current loadout since there is only one", LoggerType.Error);
                                MessageBox.Show("Cannot delete the only loadout.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); // ユーザー通知
                                break;
                            }

                            var yesno_choice = new List<Choice>();
                            yesno_choice.Add(new Choice() { OptionText = "Yes", OptionSubText = $"This action cannot be undone.", Index = 0 });
                            yesno_choice.Add(new Choice() { OptionText = "No", OptionSubText = $"", Index = 1 });

                            var yesno = new ChoiceWindow(yesno_choice, $"Delete Current Loadout: {Global.config.Configs[Global.config.CurrentGame].CurrentLoadout}?");
                            yesno.ShowDialog();

                            if (yesno.choice == 0) // Yes
                            {
                                var loadoutToDelete = Global.config.Configs[Global.config.CurrentGame].CurrentLoadout;
                                Global.LoadoutItems.Remove(loadoutToDelete);
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Remove(loadoutToDelete);

                                // 削除後、最初のロードアウトを選択 (SelectionChanged発生を期待)
                                LoadoutBox.SelectedIndex = 0;
                                // refreshNeeded = true;
                            }
                            break;
                        // Copy current loadout
                        case 3:
                            var copyLoadoutWindow = new EditWindow(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout + " Copy", false);
                            copyLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(copyLoadoutWindow.loadout))
                            {
                                // Deep Copy over current loadout (Mod.Clone() を使用)
                                ObservableCollection<Mod> ModList_Copy = new ObservableCollection<Mod>(Global.ModList.Select(m => m.Clone()));

                                Global.LoadoutItems.Add(copyLoadoutWindow.loadout);
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(copyLoadoutWindow.loadout, ModList_Copy);

                                // Trigger selection changed event
                                LoadoutBox.SelectedItem = copyLoadoutWindow.loadout;
                                // refreshNeeded = true;
                            }
                            break;
                    }
                }

                // 必要であれば、ここで refreshNeeded フラグをチェックして RefreshAsync を呼び出す
                // if (refreshNeeded && LoadoutBox.SelectedItem == null) // SelectionChangedが発生しなかった場合など
                // {
                //    await RefreshAsync();
                // }
                // 通常は LoadoutBox.SelectedItem の変更による LoadoutsBox_SelectionChanged 内で RefreshAsync が呼ばれるはず
            });
        }
        private async void GameBox_DropDownClosed(object sender, EventArgs e)
        {
            if (handle) // handle フラグの意図を確認する必要あり
            {
                if (GameBox.SelectedIndex == 5)
                    DiscordButton.Visibility = Visibility.Collapsed;
                else
                    DiscordButton.Visibility = Visibility.Visible;
                Global.config.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                if (!Global.config.Configs.ContainsKey(Global.config.CurrentGame))
                {
                    Global.ModList = new();
                    Global.config.Configs.Add(Global.config.CurrentGame, new());
                    Global.config.Configs[Global.config.CurrentGame].CurrentLoadout = "Default";
                    Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout, new());
                }
                else
                {
                    Global.ModList = Global.config.Configs[Global.config.CurrentGame].Loadouts[Global.config.Configs[Global.config.CurrentGame].CurrentLoadout];
                }
                var currentModDirectory = Global.config.Configs[Global.config.CurrentGame].ModsFolder;
                Directory.CreateDirectory(currentModDirectory);
                ModsWatcher.Path = currentModDirectory;
                // --- 新しいゲームの Mods Folder を監視するように再初期化 ---
                InitializeFileSystemWatcherAndTimer();
                StartWatching();
                // -------------------------------------------------------

                Global.logger.WriteLine($"Game switched to {Global.config.CurrentGame}", LoggerType.Info);
                await RefreshAsync(); // ★非同期版を呼び出す
                if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                    || String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
                {
                    LaunchButton.IsEnabled = false;
                    Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
                }
                else
                {
                    LaunchButton.IsEnabled = true;
                }

                await Task.Run(() => OnFirstOpen());

                LauncherOptionsBox.IsEnabled = true;
                LauncherOptionsBox.ItemsSource = LauncherOptions;
                LauncherOptionsBox.SelectedIndex = Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex;

                DescriptionWindow.Document = defaultFlow;
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
                ImageBehavior.SetAnimatedSource(Preview, bitmap);
                ImageBehavior.SetAnimatedSource(PreviewBG, null);

                // OnFirstOpen も非同期化が必要な場合がある
                // await OnFirstOpenAsync(); // OnFirstOpen が async Task になっていると仮定

                // Updateチェックも非同期なので await する
                await App.Current.Dispatcher.InvokeAsync(async () =>
                {
                    IsEnabledControls(false);
                    Global.logger.WriteLine("Checking for mod updates...", LoggerType.Info);
                    // CheckForUpdates が非同期なら await
                    // await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this, true);
                    await ModUpdater.CheckForUpdatesInit(this); // こちらを呼んでいる場合

                    Global.logger.WriteLine("Checking for Diva Mod Manager update...", LoggerType.Info);
                    if (await AutoUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    {
                        Close(); // DMMアップデートが見つかったら閉じる
                        return; // 以降の処理は不要
                    }
                    IsEnabledControls(true);
                });


                handle = false;
            }
        }

        private async void ModGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGridColumnHeader colHeader) return;

            string header = colHeader.Column?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            if (!string.IsNullOrEmpty(SearchModListTextBox.Text))
            {
                MessageBox.Show("Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Sort by {header}.\nThe priority of the mod will change significantly.\n\nAre you sure?",
                                          "Attention.", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            switch (header)
            {
                case "Enabled":
                    SortByEnabled();
                    break;
                case "Priority":
                    SortByPriority();
                    break;
                case "Name":
                    SortAlphabetically();
                    break;
                case "Category":
                    SortByCategory();
                    break;
                case "Note":
                    SortByNote();
                    break;
                default:
                    return;
            }

            await RefreshUIAndUpdateAsync();
            e.Handled = true;
        }

        private void SortByEnabled()
        {
            Global.ModList = new ObservableCollection<Mod>(Global.ModList.OrderByDescending(x => x.enabled));
            Global.logger.WriteLine("Moved all enabled mods to the top!", LoggerType.Info);
        }

        private void SortAlphabetically()
        {
            Global.ModList = new ObservableCollection<Mod>(Global.ModList.OrderBy(x => x.name, new NaturalSort()));
            Global.logger.WriteLine("Sorted alphanumerically!", LoggerType.Info);
        }

        private void SortByCategory()
        {
            SortByField(m => m.category, "Category");
        }

        private void SortByNote()
        {
            SortByField(m => m.note, "Note");
        }

        private void SortByPriority()
        {
            var list = Global.ModList.ToList();
            var hasMinus = list.Where(x => !string.IsNullOrEmpty(x.priority) && x.priority.StartsWith("-"));
            var noPriority = list.Where(x => string.IsNullOrEmpty(x.priority));
            var rest = list.Where(x => !string.IsNullOrEmpty(x.priority) && !x.priority.StartsWith("-"));

            Global.ModList = new ObservableCollection<Mod>(
                (direction == ListSortDirection.Descending
                    ? rest.OrderByDescending(x => x.priority, new NaturalSort())
                    : rest.OrderBy(x => x.priority, new NaturalSort()))
                .Concat(noPriority)
                .Concat(direction == ListSortDirection.Descending ? hasMinus.OrderByDescending(x => x.priority, new NaturalSort()) : hasMinus.OrderBy(x => x.priority, new NaturalSort()))
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
            Global.logger.WriteLine("Sorted by Priority column!", LoggerType.Info);
        }

        private void SortByField(Func<Mod, string> selector, string fieldName)
        {
            var list = Global.ModList.ToList();
            var noValue = list.Where(x => string.IsNullOrEmpty(selector(x)));
            var hasValue = list.Where(x => !string.IsNullOrEmpty(selector(x)));

            Global.ModList = new ObservableCollection<Mod>(
                (direction == ListSortDirection.Descending
                    ? hasValue.OrderByDescending(selector, new NaturalSort())
                    : hasValue.OrderBy(selector, new NaturalSort()))
                .Concat(noValue)
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
            Global.logger.WriteLine($"Sorted by {fieldName} column!", LoggerType.Info);
        }

        private async Task RefreshUIAndUpdateAsync()
        {
            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    ModGrid.ItemsSource = Global.ModList;
                });
            });

            UpdateSearchMod();
            Global.UpdateConfig();
            await Task.Run(() => ModLoader.Build());
        }

        private void Search()
        {
            if (!filterSelect && IsLoaded && !String.IsNullOrWhiteSpace(SearchBar.Text))
            {
                filterSelect = true;
                FilterBox.ItemsSource = FilterBoxListWhenSearched;
                FilterBox.SelectedIndex = 3;
                NSFWCheckbox.IsChecked = true;
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                searched = true;
                page = 1;
                RefreshFilter();
            }
        }
        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Search();
        }
        private static readonly List<string> FilterBoxList = new string[] { " Featured", " Recent", " Popular" }.ToList();
        private static readonly List<string> FilterBoxListWhenSearched = new string[] { " Featured", " Recent", " Popular", " - - -" }.ToList();

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }
        private void DMASearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DMARefreshFilter();
        }

        private void DMASearchButton_Click(object sender, RoutedEventArgs e)
        {
            DMARefreshFilter();
        }

        private void ModGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            string header = ModGrid.CurrentColumn?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            switch (header)
            {
                case "Name":
                    HandleNameColumnKeyDown(e, sender);
                    break;

                case "Priority":
                    HandlePriorityColumnKeyDown(e);
                    break;
            }
        }

        private void HandleNameColumnKeyDown(KeyEventArgs e, object sender)
        {
            if (e.Key == Key.Space)
            {
                ToggleCheckBoxes();
            }
            else if (e.Key == Key.Enter)
            {
                ExecConfigAction(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                RenameMod_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ToggleCheckBoxes()
        {
            foreach (var item in ModGrid.SelectedItems)
            {
                if (ModGrid.Columns[(int)Global.Col.Enabled].GetCellContent(item) is CheckBox checkbox)
                {
                    checkbox.IsChecked = !checkbox.IsChecked;
                }
            }
        }

        private void HandlePriorityColumnKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            string cellValue = GetPriorityCellValue(e.OriginalSource);

            // Only allow minus sign if value is empty
            if (string.IsNullOrEmpty(cellValue) && (e.Key == Key.Subtract || e.Key == Key.OemMinus))
            {
                e.Handled = false;
                return;
            }

            if (IsNumericOrControlKey(e.Key))
            {
                e.Handled = false;
            }
        }

        private string GetPriorityCellValue(object source)
        {
            return source switch
            {
                TextBox textBox => textBox.Text,
                DataGridCell cell when cell.DataContext is Mod mod => mod.priority,
                _ => null
            };
        }

        private bool IsNumericOrControlKey(Key key)
        {
            return (key >= Key.D0 && key <= Key.D9) ||
                   (key >= Key.NumPad0 && key <= Key.NumPad9) ||
                   key is Key.Enter or Key.Back or Key.Delete or
                        Key.Up or Key.Down or Key.Left or Key.Right or
                        Key.Tab or Key.F2 or Key.Escape;
        }

        private void SearchModList_Click(object sender, RoutedEventArgs e)
        {
            SearchModList(SearchModListTextBox.Text, SearchCategoryComboBox.Text);
        }

        private void SearchClear_Click(object sender, RoutedEventArgs e)
        {
            InitSearchMod();
        }

        private void SearchModListTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchModList(SearchModListTextBox.Text, SearchCategoryComboBox.Text);
            }
        }

        private void SearchModList(string searchModName, string categoryName)
        {
            ModGrid.ClearSelectedItems();

            switch (SearchTargetComboBox.Text)
            {
                case "ALL":
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList_All.ToList()
                        .Where(
                        x => x.name.ToLower().Contains(searchModName.ToLower())
                        || x.note.ToLower().Contains(searchModName.ToLower())
                        ).ToList());
                    break;
                case "Name":
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList_All.ToList()
                        .Where(x => x.name.ToLower().Contains(searchModName.ToLower())).ToList());
                    break;
                case "Note":
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList_All.ToList()
                        .Where(x => x.note.ToLower().Contains(searchModName.ToLower())).ToList());
                    break;
            }

            switch (categoryName)
            {
                case "ALL":
                    if (SearchCategoryComboBox.SelectedIndex != 0)
                    {
                        Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList()
                            .Where(x => x.category == categoryName).ToList());
                        break;
                    }
                    break;
                case "Unspecified":
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList()
                        .Where(x => string.IsNullOrEmpty(x.category)).ToList());
                    break;
                default:
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList()
                        .Where(x => x.category == categoryName).ToList());
                    break;
            }
            Global.SearchModListFlg = true;

            ModGrid.ItemsSource = Global.ModList;
            Global.UpdateConfig();
        }

        private void ModGrid_PreviewDrop(object sender, DragEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchModListTextBox.Text))
            {
                // Prohibit mod movement by dragging when mod search is enabled.
                MessageBox.Show("You cannot change the priority of the MOD while searching. Sorry.",
                    "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
        }

        private void SearchModListTextBox_MouseDoubleClick(object sender, EventArgs e)
        {
            SearchModListTextBox.SelectAll();
        }

        private void InitSearchMod()
        {
            Global.SearchModListFlg = false;
            Global.ModList = Global.ModList_All;
            ModGrid.ItemsSource = Global.ModList;
            SearchModListTextBox.Text = "";
            SearchTargetComboBox.SelectedIndex = 0;
            SearchCategoryComboBox.SelectedIndex = 0;
            ModGrid.ClearSelectedItems();
        }

        private void UpdateSearchMod()
        {
            Global.SearchModListFlg = false;
            Global.ModList_All = Global.ModList;
            SearchModListTextBox.Text = "";
            SearchTargetComboBox.SelectedIndex = 0;
            SearchCategoryComboBox.SelectedIndex = 0;
            ModGrid.ClearSelectedItems();
        }

        private void ModGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is not TextBox tb) return;
            if (tb.Parent is not DataGridCell cell) return;
            if (cell.Column.Header?.ToString() != "Priority") return;

            // Do not show the context menu when editing Priority.
            tb.ContextMenu = null;
        }

        private void ModGrid_Loaded(object sender, RoutedEventArgs e)
        {
            DataGrid modGrid = (DataGrid)sender;
            foreach (var column in modGrid.Columns)
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                descriptor.AddValueChanged(column, ColumnWidthChanged);
            }
        }

        private void ColumnWidthChanged(object sender, EventArgs e)
        {
            DataGridColumn column = (DataGridColumn)sender;
            string header = column.Header?.ToString();

            switch (header)
            {
                case "Priority":
                    Global.config.PriorityColumnWidth = column.Width.DisplayValue;
                    break;
                case "Name":
                    Global.config.NameColumnWidth = column.Width.DisplayValue;
                    break;
                case "Category":
                    Global.config.CategoryColumnWidth = column.Width.DisplayValue;
                    break;
                case "Note":
                    Global.config.NoteColumnWidth = column.Width.DisplayValue;
                    break;
            }
        }

        private void ModGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.DirectlyOver is not FrameworkElement elem) return;
            if (elem.Parent is not DataGridCell cell) return;
            if (cell.Column.Header?.ToString() != "Name") return;

            ExecConfigAction(sender, e);
        }

        private void ExecConfigAction(object sender, EventArgs e)
        {
            string action = Global.config.DoubleClickEvent?.ToLower() ?? "open";
            RoutedEventArgs _e = (RoutedEventArgs)e;

            switch (action)
            {
                case "open":
                    OpenItem_Click(sender, _e);
                    break;
                case "rename":
                    RenameMod_Click(sender, _e);
                    break;
                case "configure":
                    ConfigureModItem_Click(sender, _e);
                    break;
                case "fetch":
                    FetchItem_Click(sender, _e);
                    break;
                case "update":
                    Update_Click(sender, _e);
                    break;
                case "delete":
                    DeleteItem_Click(sender, _e);
                    break;
                case "nothing":
                    break;
                default:
                    // All unknown characters are treated as Open.
                    OpenItem_Click(sender, _e);
                    break;
            }
        }

        // ModGrid_CellEditEnding を非同期化
        private async void ModGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return; // キャンセル時は何もしない
            if (e.Column is not DataGridBoundColumn boundCol) return; // テキスト以外のカラムは対象外？
            if (e.Row.DataContext is not Mod mod) return;
            if (e.EditingElement is not TextBox textBox) return;

            string newText = textBox.Text;
            // Header を直接使うのではなく、Binding Path を使う方がより堅牢
            string bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
            string columnHeader = e.Column.Header.ToString(); // Header も fallback として使う

            // 変更があった場合のみ処理 (UI上は編集完了しているように見えるが、実際の値と比較)
            bool changed = false;
            switch (bindingPath ?? columnHeader) // Binding Path があれば優先
            {
                case "priority": // Binding Path
                case "Priority": // Header
                    if (mod.priority != newText)
                    {
                        mod.priority = newText; // ViewModel があれば setter で処理
                        changed = true;
                        columnHeader = "Priority"; // UpdateModConfigToml_e 用に Header 名を確定
                    }
                    break;
                case "category":
                case "Category":
                    if (mod.category != newText)
                    {
                        mod.category = newText;
                        changed = true;
                        columnHeader = "Category";
                        // カテゴリコンボの更新 (UI スレッドで)
                        await Application.Current.Dispatcher.InvokeAsync(() => CategoryComboInit());
                    }
                    break;
                case "note":
                case "Note":
                    if (mod.note != newText)
                    {
                        mod.note = newText;
                        changed = true;
                        columnHeader = "Note";
                    }
                    break;
                default:
                    return; // 対象外のカラム
            }

            if (changed)
            {
                // config_e.toml の更新 (非同期)
                await UpdateModConfigToml_e(mod, columnHeader, newText);
                // 必要であれば Global.UpdateConfig() や ModLoader.Build() も呼ぶが、
                // RefreshAsync 内で実行されるため、ここでは不要かもしれない。
                // ただし、即時反映が必要な場合は検討。
                // Global.UpdateConfig();
                // await Task.Run(() => ModLoader.Build());
            }
        }

        private void SearchCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox checkBox && checkBox.IsKeyboardFocusWithin)
            {
                SearchModList(SearchModListTextBox.Text, checkBox.SelectedItem?.ToString());
            }
        }

        private void CategoryComboInit(int? selected = null)
        {
            List<Mod> CategoryItems = Global.ModList_All.DistinctBy(x => x.category).OrderBy(x => x.category).ToList();
            Global.CategoryItems = new ObservableCollection<string>();
            Global.CategoryItems.Add("ALL");
            foreach (var CategoryItem in CategoryItems)
            {
                if (!string.IsNullOrEmpty(CategoryItem.category))
                {
                    Global.CategoryItems.Add(CategoryItem.category);
                }
            }
            Global.CategoryItems.Add("Unspecified");
            SearchCategoryComboBox.ItemsSource = Global.CategoryItems;
            if (selected != null)
            {
                SearchCategoryComboBox.SelectedIndex = (int)selected;
            }
        }

        private void DMM_Folder_Click(object sender, RoutedEventArgs e)
        {
            var folderName = $@"{Global.assemblyLocation}";
            if (Directory.Exists(folderName))
            {
                try
                {
                    Process process = Process.Start("explorer.exe", folderName);
                    Global.logger.WriteLine($@"Opened {folderName}.", LoggerType.Info);
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($@"Couldn't open {folderName}. ({ex.Message})", LoggerType.Error);
                }
            }
        }

        private void SetColumnDisplayIndex()
        {
            foreach(var col in ModGrid.Columns)
            {
                string headerName = col.Header.ToString();
                switch (headerName)
                {
                    case "Enabled":
                        Global.config.EnabledColumnIndex = col.DisplayIndex;
                        break;
                    case "Priority":
                        Global.config.PriorityColumnIndex = col.DisplayIndex;
                        break;
                    case "Name":
                        Global.config.NameColumnIndex = col.DisplayIndex;
                        break;
                    case "Category":
                        Global.config.CategoryColumnIndex = col.DisplayIndex;
                        break;
                    case "Note":
                        Global.config.NoteColumnIndex = col.DisplayIndex;
                        break;
                    default:
                        break;
                }
            }
        }

        private void SetColumnVisible()
        {
            foreach (var col in ModGrid.Columns)
            {
                string headerName = col.Header.ToString();
                switch (headerName)
                {
                    case "Enabled":
                        Global.config.EnabledColumnVisible = col.Visibility;
                        break;
                    case "Priority":
                        Global.config.PriorityColumnVisible = col.Visibility;
                        break;
                    case "Name":
                        Global.config.NameColumnVisible = col.Visibility;
                        break;
                    case "Category":
                        Global.config.CategoryColumnVisible = col.Visibility;
                        break;
                    case "Note":
                        Global.config.NoteColumnVisible = col.Visibility;
                        break;
                    default:
                        break;
                }
            }
        }

        private void VisibleColumnComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox checkBox && checkBox.IsKeyboardFocusWithin)
            {
                ComboBoxItem item = VisibleColumnComboBox.SelectedItem as ComboBoxItem;
                if (item != null)
                {
                    DataGridColumn col;
                    switch (item.Content.ToString())
                    {
                        case "Visible Column":
                            break;
                        case "Enabled":
                            col = GetDataGridColumnByName(ModGrid, "Enabled");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Priority":
                            col = GetDataGridColumnByName(ModGrid, "Priority");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Name":
                            col = GetDataGridColumnByName(ModGrid, "Name");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Category":
                            col = GetDataGridColumnByName(ModGrid, "Category");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Note":
                            col = GetDataGridColumnByName(ModGrid, "Note");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        default:
                            break;
                    }
                }
                checkBox.SelectedIndex = 0;
            }
        }

        private DataGridColumn GetDataGridColumnByName(DataGrid grid, string headerName)
        {
            if(grid == null || string.IsNullOrEmpty(headerName))
            {
                return null;
            }

            foreach (DataGridColumn col in grid.Columns)
            {
                if (col.Header.ToString() == headerName)
                {
                    return col;
                }
            }

            return null;
        }

        private void IsEnabledControls(bool isEnabled)
        {
            GBModBrowser.IsEnabled = isEnabled;
            DMAModBrowser.IsEnabled = isEnabled;

            GameBox.IsEnabled = isEnabled;
            LauncherOptionsBox.IsEnabled = isEnabled;
            EditLoadoutsButton.IsEnabled = isEnabled;
            ConfigButton.IsEnabled = isEnabled;
            LaunchButton.IsEnabled = isEnabled;
            OpenModsButton.IsEnabled = isEnabled;
            UpdateCheckAllButton.IsEnabled = isEnabled;
            LoadoutBox.IsEnabled = isEnabled;
            
            SearchModListButton.IsEnabled = isEnabled;
            SearchModListTextBox.IsEnabled = isEnabled;
            SearchModListButton.IsEnabled = IsEnabled;
            SearchClearButton.IsEnabled = isEnabled;
            SearchTargetComboBox.IsEnabled = isEnabled;
            SearchCategoryComboBox.IsEnabled = isEnabled;
            VisibleColumnComboBox.IsEnabled = isEnabled;
            
            ModGrid.IsEnabled = isEnabled;
        }
    }
}
