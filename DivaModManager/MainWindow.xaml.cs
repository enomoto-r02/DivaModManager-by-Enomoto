using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Common.MessageWindow;
using DivaModManager.Features.AltLink;
using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using DivaModManager.Features.DMM;
using DivaModManager.Features.Download;
using DivaModManager.Features.Extract;
using DivaModManager.Features.Feed;
using DivaModManager.Features.Module;
using DivaModManager.Features.Song;
using DivaModManager.Misk;
using DivaModManager.Models;
using DivaModManager.Structures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
using System.Windows.Threading;
using Tomlyn;
using Tomlyn.Model;
using WpfAnimatedGif;

namespace DivaModManager
{
    public partial class MainWindow : Window, IDisposable
    {
        public enum Column
        {
            Enabled = 0,
            Priority,
            Name,
            Version,
            Site,
            Category,
            Size,
            Note,
        }

        #region Cache constants (後日外部ファイル化予定)
        private const int GB_API_CACHE_HOURS = 3;       // -1 で永続
        private const int GB_IMAGE_CACHE_HOURS = 72;    // -1 で永続
        private const int DMA_API_CACHE_HOURS = 3;      // -1 で永続
        private const int DMA_IMAGE_CACHE_HOURS = 72;   // -1 で永続
        #endregion

        private FlowDocument defaultFlow = new();
        private string defaultText = "Welcome to DivaModManager by Enomoto!\n\n" +
            "To show metadata here:\nRight Click Row > Configure Mod and add author, version, and/or date fields" +
            "\nand/or Right Click Row > Fetch Metadata and confirm the GameBanana or DivaModArchive URL of the mod";
        ListSortDirection direction = ListSortDirection.Ascending;
        // Modsフォルダ監視
        private DMMFileSystemWatcher ModsWatcher;
        // MM+フォルダ監視
        private DMMFileSystemWatcher MMPWatcherDLL;
        private DMMFileSystemWatcher MMPWatcherTOML;
        // Add Loadoutで追加を選択した場合、リフレッシュ後にアルファベット順に並び替えるAction
        private Action ModGridEdifAfterAction = default;
        // 検索中にダウンロード完了などでModList_Allが更新された場合、同じ条件で再検索するAction
        private Action ModGridAddAfterAction = default;
        // 検索時の条件保持
        private SearchMod ModGridSearch = new();
        // DMMeやDMLバージョンなど
        private string InfoText = string.Empty;
        // 現在表示しているmetadataの画像情報
        private List<string> previewFiles = new();
        private int previewFilesIndex = 0;

        #region IDisposable 実装

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                DMMFileSystemWatcher.DisposeWatcherAndTimer(ModsWatcher);
                DMMFileSystemWatcher.DisposeWatcherAndTimer(MMPWatcherDLL);
                DMMFileSystemWatcher.DisposeWatcherAndTimer(MMPWatcherTOML);
                RemoveColumnWidthChangedHandlers();
            }
            disposed = true;
        }

        // ファイナライザ (通常は不要だが、管理対象外リソースを持つ場合に備える)
        // ~MainWindow()
        // {
        //     Dispose(false);
        // }

        #endregion

        public MainWindow(StartupEventArgs e) : this()
        {
            this.Title = $"{this.Title} {App.Version}{App.TestVersion}";
            if (Logger.Mode == Features.Debug.Logger.DEBUG_MODE.DEVELOPER)
            {
                this.DebugTabItem.Header = $"Developer";
                DebugTabItem.Visibility = Visibility.Visible;
                this.Title += $" (Developer)";
            }
            else if (Logger.Mode == Features.Debug.Logger.DEBUG_MODE.DEBUG)
            {
                this.DebugTabItem.Header = $"Debug";
                DebugTabItem.Visibility = Visibility.Visible;
                this.Title += $" (Debug)";
            }
            else
            {
                DebugTabItem.Visibility = Visibility.Hidden;
            }
        }

        public MainWindow([CallerMemberName] string caller = "")
        {
            InitializeComponent();

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                Global.WindowLogger = new WindowLogger(ConsoleWindow);

                Logger.WriteLine(string.Join(" ", $"{MeInfo}", "Start."), LoggerType.Debug, param: ParamInfo);
                Logger.WriteLine($"DivaModManager by Enomoto v{App.Version}{App.TestVersion} Loading...", LoggerType.Info);

                IsEnabledControls(false);

                // Last saved windows settings
                if (Global.ConfigJson.Height != null && Global.ConfigJson.Height >= MinHeight)
                    Height = (double)Global.ConfigJson.Height;
                if (Global.ConfigJson.Width != null && Global.ConfigJson.Width >= MinWidth)
                    Width = (double)Global.ConfigJson.Width;
                if (Global.ConfigJson.Maximized)
                    WindowState = WindowState.Maximized;
                if (Global.ConfigJson.TopGridHeight != null)
                    MainGrid.RowDefinitions[2].Height = new GridLength((double)Global.ConfigJson.TopGridHeight, GridUnitType.Star);
                if (Global.ConfigJson.BottomGridHeight != null)
                    MainGrid.RowDefinitions[4].Height = new GridLength((double)Global.ConfigJson.BottomGridHeight, GridUnitType.Star);
                if (Global.ConfigJson.LeftGridWidth != null)
                    MiddleGrid.ColumnDefinitions[0].Width = new GridLength((double)Global.ConfigJson.LeftGridWidth, GridUnitType.Star);
                if (Global.ConfigJson.RightGridWidth != null)
                    MiddleGrid.ColumnDefinitions[2].Width = new GridLength((double)Global.ConfigJson.RightGridWidth, GridUnitType.Star);

                if (Global.ConfigJson.EnabledColumnIndex != null)
                    ModGrid.Columns[(int)Column.Enabled].DisplayIndex = (int)Global.ConfigJson.EnabledColumnIndex;
                if (Global.ConfigJson.PriorityColumnIndex != null)
                    ModGrid.Columns[(int)Column.Priority].DisplayIndex = (int)Global.ConfigJson.PriorityColumnIndex;
                if (Global.ConfigJson.NameColumnIndex != null)
                    ModGrid.Columns[(int)Column.Name].DisplayIndex = (int)Global.ConfigJson.NameColumnIndex;
                if (Global.ConfigJson.VersionColumnIndex != null)
                    ModGrid.Columns[(int)Column.Version].DisplayIndex = (int)Global.ConfigJson.VersionColumnIndex;
                if (Global.ConfigJson.CategoryColumnIndex != null)
                    ModGrid.Columns[(int)Column.Category].DisplayIndex = (int)Global.ConfigJson.CategoryColumnIndex;
                if (Global.ConfigJson.SizeColumnIndex != null)
                    ModGrid.Columns[(int)Column.Size].DisplayIndex = (int)Global.ConfigJson.SizeColumnIndex;
                if (Global.ConfigJson.NoteColumnIndex != null)
                    ModGrid.Columns[(int)Column.Note].DisplayIndex = (int)Global.ConfigJson.NoteColumnIndex;

                try
                {
                    if (Global.ConfigJson.EnabledColumnWidth != null)
                        ModGrid.Columns[(int)Column.Enabled].Width = (double)Global.ConfigJson.EnabledColumnWidth;
                    if (Global.ConfigJson.PriorityColumnWidth != null)
                        ModGrid.Columns[(int)Column.Priority].Width = (double)Global.ConfigJson.PriorityColumnWidth;
                    if (Global.ConfigJson.NameColumnWidth != null)
                        ModGrid.Columns[(int)Column.Name].Width = (double)Global.ConfigJson.NameColumnWidth;
                    if (Global.ConfigJson.VersionColumnWidth != null)
                        ModGrid.Columns[(int)Column.Version].Width = (double)Global.ConfigJson.VersionColumnWidth;
                    if (Global.ConfigJson.SiteColumnWidth != null)
                        ModGrid.Columns[(int)Column.Site].Width = (double)Global.ConfigJson.SiteColumnWidth;
                    if (Global.ConfigJson.CategoryColumnWidth != null)
                        ModGrid.Columns[(int)Column.Category].Width = (double)Global.ConfigJson.CategoryColumnWidth;
                    if (Global.ConfigJson.SizeColumnWidth != null)
                        ModGrid.Columns[(int)Column.Size].Width = (double)Global.ConfigJson.SizeColumnWidth;
                    if (Global.ConfigJson.NoteColumnWidth != null)
                        ModGrid.Columns[(int)Column.Note].Width = (double)Global.ConfigJson.NoteColumnWidth;
                }
                catch (Exception ex)
                {
                }

                ModGrid.Columns[(int)Column.Enabled].Visibility = (Visibility)Global.ConfigJson.EnabledColumnVisible;
                ModGrid.Columns[(int)Column.Priority].Visibility = (Visibility)Global.ConfigJson.PriorityColumnVisible;
                ModGrid.Columns[(int)Column.Name].Visibility = (Visibility)Global.ConfigJson.NameColumnVisible;
                ModGrid.Columns[(int)Column.Version].Visibility = (Visibility)Global.ConfigJson.VersionColumnVisible;
                ModGrid.Columns[(int)Column.Site].Visibility = (Visibility)Global.ConfigJson.SiteColumnVisible;
                ModGrid.Columns[(int)Column.Category].Visibility = (Visibility)Global.ConfigJson.CategoryColumnVisible;
                ModGrid.Columns[(int)Column.Size].Visibility = (Visibility)Global.ConfigJson.SizeColumnVisible;
                ModGrid.Columns[(int)Column.Note].Visibility = (Visibility)Global.ConfigJson.NoteColumnVisible;

                Global.games = new List<string>();

                // GameBox内のTextBlockの名称を設定している(MainWindow.xamlにベタ書きのため、実質ハードコーディング)
                foreach (var item in GameBox.Items)
                {
                    var game = (((item as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", string.Empty);
                    Global.games.Add(game);
                }

                if (Global.ConfigJson?.Configs?.Count == null || Global.ConfigJson?.Configs?.Count == 0)
                {
                    Global.ConfigJson.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", string.Empty);
                    Global.ConfigJson.Configs = new()
                    {
                        { Global.ConfigJson.CurrentGame, new() }
                    };
                }
                else
                    GameBox.SelectedIndex = Global.games.IndexOf(Global.ConfigJson.CurrentGame);
                if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout))
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout = "Default";
                if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts == null)
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts = new();
                if (!Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.ContainsKey(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout))
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Add(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout, new());
                else if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout] == null)
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout] = new();
                Global.ModList_All = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout];
                Global.ModList = Global.ModList_All;

                Global.LoadoutItems = new ObservableCollection<string>(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Keys);

                LoadoutBox.ItemsSource = Global.LoadoutItems;
                LoadoutBox.SelectedItem = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout;

                if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                    || !Directory.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
                {
                    if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].FirstOpen)
                    {
                        Logger.WriteLine($"Please click Setup before installing mods!", LoggerType.Warning);
                    }
                }

                defaultFlow.Blocks.Add(ConvertToFlowParagraph(defaultText));
                DescriptionWindow.Document = defaultFlow;
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
                ImageBehavior.SetAnimatedSource(Preview, bitmap);
                ImageBehavior.SetAnimatedSource(PreviewBG, null);

                ThumbnailCountRect.Visibility = Visibility.Collapsed;
                ThumbnailCountText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A critical error occurred during application startup:\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            finally
            {
                Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug);
            }
        }

        private void RemoveColumnWidthChangedHandlers()
        {
            if (ModGrid != null)
            {
                foreach (var column in ModGrid.Columns)
                {
                    try
                    {
                        var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                        descriptor?.RemoveValueChanged(column, ColumnWidthChanged);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Error removing WidthChanged handler for column '{column.Header}': {ex.Message}", LoggerType.Warning);
                    }
                }
            }
        }

        /// <summary>
        /// LoadoutBox_SelectionChangedの後に呼ばれる
        /// IsLoadedはtrue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            await App.Current.Dispatcher.InvokeAsync(() => OnFirstOpenAsync());
            if (!Global.IsWine && Global.ConfigToml.DivaModManagerUpdateCheck)
            {
                Logger.WriteLine("Checking for DivaModManager by Enomoto update...", LoggerType.Info);
                if (await DMMUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    Close();
            }
            if (!Global.IsWine && Global.ConfigToml.DivaModLoaderUpdateCheck)
            {
                Logger.WriteLine("Checking for DivaModLoader update...", LoggerType.Info);
                var ret = await DMLUpdater.CheckForDMLUpdate(new CancellationTokenSource());
            }

            CheckTemporaryDirectorySize();

            Global.IsMainWindowLoaded = true;

            if (Global.ConfigJson.IsNew)
                await ConfigJson.UpdateConfigAsync();

            await WatcherSetup(false);

            await App.Current.Dispatcher.InvokeAsync(async () =>
            {
                await RefreshAsync();
            });

            Logger.WriteLine($"DivaModManager by Enomoto v{App.Version}{App.TestVersion} Loaded!", LoggerType.Info);

            // セットアップが完了していない場合は次のステップを表示
            if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher))
            {
                Logger.WriteLine($"Setup is not yet complete because the DivaMegaMix+.exe files is missing. Press the \"{SetupButton.Content}\" Button to look for DivaMegaMix.exe!", LoggerType.Warning);
            }
            else if (!Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                Logger.WriteLine($"Setup is not yet complete because the DivaModLoader files is missing. \"{CoreUpdateButton.Content}\" Button to install DivaModLoader!", LoggerType.Warning);
            }
            else if (Global.ConfigJson.IsNew)
            {
                Logger.WriteLine($"The initial setup is complete!", LoggerType.Info);
            }
            else if (Global.ModList_All.Count == 0)
            {
                Logger.WriteLine($"Click on the \"{GBModBrowserTab.Header}\" Tab or \"{DMAModBrowserTab.Header}\" Tab or Drag and drop local mod to install the mod and Play DivaMegaMix to push {LaunchButton.Content} Button!", LoggerType.Info);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        /// <summary>
        /// 全フォルダを再読み込みする
        /// フォルダの増減があった場合はFileSystemWatcherによって呼び出されるため
        /// それ以外の変化の場合は明示的に呼び出してください
        /// </summary>
        /// <param name="isEnabledControls">更新中は操作を禁止させる</param>
        /// <param name="called"></param>
        /// <returns></returns>
        private async Task RefreshAsync(bool isEnabledControls = false, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            if (!isEnabledControls) IsEnabledControls(false);
            ModsWatcher?.StopWatching();
            MMPWatcherDLL?.StopWatching();
            MMPWatcherTOML?.StopWatching();

            Global.IsModGridLoaded = false;

            try
            {
                // DMLが設定されている
                if (!string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher)
                    && Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].FirstOpen)
                {
                    string currentModDirectory = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder;
                    if (string.IsNullOrEmpty(currentModDirectory) || !Directory.Exists(currentModDirectory))
                    {
                        Logger.WriteLine($"Mods Path is not Found! Path: \"{currentModDirectory}\"", LoggerType.Error);
                        Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug);
                        return;
                    }
                    var modPaths = Directory.GetDirectories(currentModDirectory);
                    var existingModNamesInDirectory = new HashSet<string>(modPaths.Select(Path.GetFileName));

                    var processingTasks = new List<Task>();

                    foreach (var modPath in modPaths)
                    {
                        Logger.WriteLine(string.Join(" ", MeInfo, $"modPaths foreach.", $"modPaths in '{modPath}'"), LoggerType.Debug);

                        // Task.Run でラップするか、ProcessModDirectoryAsync をそのまま await する
                        // 並列化する場合: tasks.Add(ProcessModDirectoryAsync(modPath));
                        // 逐次処理の場合: await ProcessModDirectoryAsync(modPath);
                        try
                        {
                            await ProcessModDirectoryAsync(modPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine($"Error processing mod directory {Path.GetFileName(modPath)}: {ex}", LoggerType.Error);
                        }
                    }

                    // 並列化した場合: await Task.WhenAll(processingTasks);

                    await RemoveDeletedModsAsync(existingModNamesInDirectory);

                    // EditLoadoutでAddした時にソートActionを実行
                    ModGridEdifAfterAction?.Invoke();
                    ModGridEdifAfterAction = default;

                    // 検索中にダウンロード完了などでModList_Allが更新された場合、再検索するActionを実行
                    ModGridAddAfterAction?.Invoke();
                }
                else
                {
                    Global.ConfigJson.CurrentConfig.CurrentLoadout = Global.ConfigJson.CurrentConfig.Loadouts.FirstOrDefault().Key;
                    ModGrid.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"A critical error occurred during RefreshAsync: {ex}", LoggerType.Error);
            }
            finally
            {
                Global.IsModGridLoaded = true;
                UpdateModGridAsync(isSearch: Global.SearchModListFlg);
                UpdateUIElementsAsync();
                await Global.ConfigUpdatesAsync();

                ModGridAddAfterAction?.Invoke();
                await Dispatcher.InvokeAsync(() => ShowMetadata(null));

                LoadoutBox.SelectedItem = Global.ConfigJson.CurrentConfig.CurrentLoadout;
                CategoryComboInit(0);

                ModsWatcher?.StartWatching();
                MMPWatcherDLL?.StartWatching();
                MMPWatcherTOML?.StartWatching();

                if (Extractor.MoveCount <= 0)
                {
                    IsEnabledControls(true);
                }
                Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            }
        }

        #region RefreshAsync の分割メソッド群

        /// <summary>
        /// ModをModList_Allに追加する
        /// </summary>
        /// <param name="modPath"></param>
        /// <param name="caller"></param>
        /// <returns></returns>
        private async Task ProcessModDirectoryAsync(string modPath, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            var modName = Path.GetFileName(modPath);

            Mod mod = new();
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                mod = Global.ModList_All.FirstOrDefault(x => x.name == modName);
            });

            if (mod == null)
            {
                mod = new Mod { name = modName };
                bool configExists = await Task.Run(() => FileHelper.FileExistsAsync(mod.config_toml_path));

                await TryUpdateModFromConfigAsync(mod, isNewMod: true);
                await mod.InitModAsync();
                await TryLoadDirectorySizeAsync(mod);
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Global.ConfigJson.AddModToTop)
                    {
                        Global.ModList_All.Insert(0, mod);
                    }
                    else
                    {
                        Global.ModList_All.Add(mod);

                    }
                    Logger.WriteLine($"Added \"{modName}\"", LoggerType.Info, param: ParamInfo);
                });
            }
            else
            {
                await TryUpdateModFromConfigAsync(mod, isNewMod: false);
                await mod.InitModAsync();
                await TryLoadDirectorySizeAsync(mod);
            }
        }

        /// <summary>
        /// config.toml ファイルから Mod オブジェクトを更新する（ファイルがない/読めない場合はデフォルト値やログ出力）
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="isNewMod"></param>
        /// <param name="caller"></param>
        private async Task TryUpdateModFromConfigAsync(Mod mod, bool isNewMod, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            bool IsWrite = false;

            TomlTable config = await TomlHelper.TryReadTomlAsync(mod.config_toml_path);
            if (config == null)
            {
                // 読み取り失敗 or 不正なファイル
                return;
            }
            else if (config.ContainsKey("enabled"))
            {
                try
                {
                    bool enabledFromFile = (bool)config["enabled"];
                    if (isNewMod)
                    {
                        mod.enabled = enabledFromFile;
                    }
                    else
                    {
                        if (enabledFromFile != mod.enabled)
                        {
                            config["enabled"] = mod.enabled;
                            IsWrite = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error reading 'enabled' field from {mod.config_toml_path} for {mod.name}: {ex.Message}. Using default.", LoggerType.Warning);
                    if (isNewMod) mod.enabled = true;
                    config["enabled"] = mod.enabled;
                    IsWrite = true;
                }
            }
            if (config.ContainsKey("name"))
            {
                mod.ConfigTomlModName = config["name"].ToString();
            }
            if (config.ContainsKey("version"))
            {
                mod.version = config["version"].ToString();
            }
            IsWrite = IsWrite || TomlHelper.AddInclude(config);

            if (IsWrite)
            {
                await TomlHelper.TryWriteTomlAsync(mod.config_toml_path, config);
            }
        }

        /// <summary>
        /// ディレクトリに存在しないModをGlobal.ModListから削除する
        /// </summary>
        /// <param name="existingModNamesInDirectory"></param>
        /// <returns></returns>
        private async Task RemoveDeletedModsAsync(HashSet<string> existingModNamesInDirectory, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, existingModNamesInDirectory:{existingModNamesInDirectory}";

            if (Global.ModList_All != null && Global.ModList_All.Count > 0)
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    // ToList() でコピーを作成してから反復処理
                    var modsToRemove = Global.ModList_All.Where(mod => !existingModNamesInDirectory.Contains(mod.name)).ToList();
                    foreach (var modToRemove in modsToRemove)
                    {
                        Global.ModList_All.Remove(modToRemove);
                        Logger.WriteLine($"Deleted \"{modToRemove.name}\"", LoggerType.Info);
                    }
                });
            }
        }

        /// <summary>
        /// UI要素（統計情報、ModGrid）の更新
        /// </summary>
        /// <param name="isModGridCountChanged">Mod数に変更が無い場合のみfalse(Enableの変更など)</param>
        /// <param name="caller"></param>
        /// <returns></returns>
        private async void UpdateUIElementsAsync([CallerMemberName] string caller = "")
        {
            if (App.Current.Dispatcher.CheckAccess())
            {
                UpdateUIElements();
            }
            else
            {
                await App.Current.Dispatcher.BeginInvoke(() =>
                {
                    UpdateUIElements();
                });
            }
        }
        /// <summary>
        /// 画面右上、統計情報のラベル更新
        /// </summary>
        /// <param name="caller"></param>
        private void UpdateUIElements([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var enabledCount = Global.ModList_All.Count(x => x.enabled);
            var totalCount = Global.ModList_All.Count;

            var infos = new List<string>();

            if (Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].UpdateModLoaderVersion();
                if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion))
                    infos.Add($"DivaModLoader");
                else
                    infos.Add($"DivaModLoader v{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion}");
            }
            else
            {
                infos.Add($"DivaModLoader(nothing)");
            }
            infos.Add($"DivaModManager by Enomoto v{App.Version}{App.TestVersion}");

            if (Global.ConfigJson.CurrentConfig.FirstOpen && totalCount > 0)
            {
                infos.Add($"{enabledCount}/{totalCount} mods");
            }

            InfoText = string.Join("\n", infos);

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        /// <summary>
        /// ディレクトリのサイズを読み込む
        /// </summary>
        [Obsolete("FileHelperかModに移譲予定")]
        private static async Task<bool> TryLoadDirectorySizeAsync(Mod mod)
        {
            var ret = true;
            bool isDirectoryPath = Directory.Exists(mod.directory_path);
            if (!isDirectoryPath) return ret;

            try
            {
                mod.directorySize = await FileHelper.GetDirectorySizeAsyncAuto(mod.directory_path, mod.SkipFileWhenSizeCheckPathList);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Unexpected error processing in TryLoadDirectorySizeAsync at {mod.directory_path}: {ex.Message}", LoggerType.Error);
                ret = false;
            }
            return ret;
        }

        #endregion

        private void ModGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            foreach (var add in e.AddedCells)
            {
                if (add.Item is Mod mod)
                {
                    mod.selected = true;
                    break;
                }
            }
            foreach (var add in e.RemovedCells)
            {
                if (add.Item is Mod mod)
                {
                    mod.selected = false;
                    break;
                }
            }
        }

        private void EnabledColumn_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridCell checkBox && checkBox.IsKeyboardFocusWithin)
            {
                var checkCell = e.Source as CheckBox;
                var mod = checkCell.DataContext as Mod;
                CheckedCommon(mod, true);
            }
        }
        private void EnabledColumn_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridCell checkBox && checkBox.IsKeyboardFocusWithin)
            {
                var checkCell = e.Source as CheckBox;
                var mod = checkCell.DataContext as Mod;
                CheckedCommon(mod, false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="checkMod"></param>
        /// <param name="setEnabled"></param>
        /// <param name="caller"></param>
        private async void CheckedCommon(Mod checkMod, bool setEnabled, [CallerMemberName] string caller = "")
        {
            var checkMods = ModGrid.SelectedItems.OfType<Mod>().Where(x => x.exist_config_toml == true).ToList();
            if (checkMods.Count == 0) return;

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"checkMod:{new DirectoryInfo(checkMod.name).Name}, setEnabled:{setEnabled}, caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            List<Task> updateTasks = new List<Task>();

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (Mod checkMod in checkMods)
                {
                    var modInList = Global.ModList_All.FirstOrDefault(m => m.directory_name == checkMod.directory_name);
                    if (modInList != null)
                    {
                        modInList.enabled = setEnabled;
                        // 複数チェックされた場合、チェックされた行以外は手動で設定
                        updateTasks.Add(TomlHelper.UpdateModConfigTomlAsync(modInList));
                        Logger.WriteLine($"{MeInfo} {new DirectoryInfo(checkMod.name).Name}.enabled:{setEnabled}", LoggerType.Debug);
                    }
                }
            });

            await Global.ConfigUpdatesAsync();
            UpdateUIElementsAsync();
            await Dispatcher.InvokeAsync(() => ShowMetadata(checkMod));
        }

        private async void SetupButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }

            GameBox.IsEnabled = false;
            await WatcherSetup();
            GameBox.IsEnabled = true;
        }

        /// <summary>
        /// DMLがインストールされていたらModsWatcherをスタートしMMPWatcherDLL、MMPWatcherTOMLをストップ、
        /// DMLがインストールされていなければMMPWatcherDLL、MMPWatcherTOMLをスタートしModsWatcherをストップ
        /// </summary>
        /// <param name="isResetMessageView">メッセージボックスを表示するか</param>
        /// <returns></returns>
        private async Task WatcherSetup(bool isResetMessageView = true, [CallerMemberName] string caller = "")
        {
            await WorkManager.RunAsync(async () =>
            {
                await Task.Run(() =>
                {
                    if (isResetMessageView && (
                        !string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                        || (!string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher)
                        && File.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher))))
                    {
                        var dialogResult = WindowHelper.MessageBoxOpen(70, type: MessageBoxButton.YesNo, icon: MessageBoxImage.Question, window: this);
                        if (dialogResult == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                    MMPWatcherDLL?.StopWatching();
                    MMPWatcherDLL = DMMFileSystemWatcher.InitializeWatcher(
                        this, Global.ConfigJson.CurrentConfig.GetLauncherDirectory(), DMLUpdater.MODULE_NAME_DLL);
                    MMPWatcherDLL.RefreshAction = default;
                    MMPWatcherDLL.RefreshAction += async () => await WatcherSetup(false);
                    //MMPWatcherDLL.RefreshAction += async () => await RefreshAsync(caller: "MMPWatcherDLL.RefreshAction");
                    MMPWatcherDLL.StartWatching();
                    MMPWatcherTOML?.StopWatching();
                    MMPWatcherTOML = DMMFileSystemWatcher.InitializeWatcher(
                        this, Global.ConfigJson.CurrentConfig.GetLauncherDirectory(), DMLUpdater.MODULE_NAME_TOML);
                    MMPWatcherTOML.RefreshAction = default;
                    MMPWatcherTOML.RefreshAction += async () => await WatcherSetup(false);
                    //MMPWatcherTOML.RefreshAction += async () => await RefreshAsync(caller: "MMPWatcherTOML.RefreshAction");
                    MMPWatcherTOML.StartWatching();

                    ModsWatcher?.StopWatching();

                    if (ConfigJson.SetupGame())
                    {
                        DMMFileSystemWatcher.DisposeWatcherAndTimer(ModsWatcher);
                        ModsWatcher = DMMFileSystemWatcher.InitializeWatcher(
                            this, Global.ConfigJson.CurrentConfig.ModsFolder);
                        ModsWatcher.RefreshAction = default;
                        ModsWatcher.RefreshAction += async () => await RefreshAsync(caller: "ModsWatcher.RefreshAction");
                        ConfigJson.UpdateConfig();
                        ModsWatcher.StartWatching();
                    }
                    return;
                });
            });
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }

            if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher != null && File.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher))
            {
                var path = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher;
                try
                {
                    if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].LauncherOptionIndex > 0)
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

                    await Global.ConfigUpdatesAsync();

                    Logger.WriteLine($"Launching {path}", LoggerType.Info);
                    var ps = new ProcessStartInfo(path)
                    {
                        WorkingDirectory = Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher),
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't launch {path} ({ex.Message})", LoggerType.Error);
                }
            }
            else
            {
                Logger.WriteLine($"Please click Setup before launching!", LoggerType.Warning);
            }
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.TryStartProcess($"https://github.com/enomoto-r02/DivaModManager-by-Enomoto/releases");
        }

        private void GitHubSponsorsButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.TryStartProcess($"https://github.com/sponsors/enomoto-r02?frequency=one-time");
        }

        private void GBGameBananaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Global.GAME_ID))
            {
                ProcessHelper.TryStartProcess($"https://gamebanana.com/games/{Global.GAME_ID}");
            }
            else
            {
                Logger.WriteLine($"GameBanana link not configured for selected game index: {Global.GAME_ID}", LoggerType.Warning);
            }
        }
        private void DmaButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.TryStartProcess($"https://divamodarchive.com");
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            var discordLink = "https://discord.gg/cvBVGDZ";
            ProcessHelper.TryStartProcess(discordLink);
        }

        private void ScrollToBottom(object sender, TextChangedEventArgs args)
        {
            ConsoleWindow.ScrollToEnd();
        }

        private void ModGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (!Global.IsMainWindowLoaded || element == null)
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

                var SelectMods = ModGrid.SelectedCells.ToList();
                var SelectModList = new List<Mod>();
                foreach (var SelectMod in SelectMods) { SelectModList.Add((Mod)SelectMod.Item); }
                var IsOpenHomePage = SelectModList.Where(x => x.metadataManager?.metadata?.homepage?.ToString() != "-").Count() != 0;

                var SelectModsCount = ModGrid.SelectedCells.Count / ModGrid.Columns.Count;
                List<string> inactiveList = new();
                if (SelectModsCount > 1)
                {
                    if (Global.SearchModListFlg) { inactiveList.AddRange(new[] { "MoveToTop", "MoveToBottom" }); }
                }
                if (!IsOpenHomePage) { inactiveList.Add("HomePageOpen"); }

                for (var i = 0; i < element.ContextMenu.Items.Count; i++)
                {
                    bool enabled = false;
                    if (element.ContextMenu.Items[i] is MenuItem contextMenu)
                    {
                        if (!inactiveList.Contains(contextMenu.Name) && Global.IsWindows)
                        {
                            enabled = true;
                        }
                        contextMenu.IsEnabled = enabled;
                    }
                }
            }
        }

        private async void DeleteMod_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var selectedMods = ModGrid.SelectedItems.OfType<Mod>().ToList();
            if (!selectedMods.Any()) return;

            List<string> replaceList = new() { selectedMods.Count.ToString() };
            var dialogResult = WindowHelper.MessageBoxOpen(75, replaceList, MessageBoxButton.OKCancel);
            if (dialogResult == MessageBoxResult.OK)
            {
                // ModGrid_SelectionChangedを解除しても発火したので、フラグで…。foreachをタスク化すれば大丈夫か？
                Global.IsModGridLoaded = false;
                ModsWatcher.StopWatching();

                IsEnabledControls(false);

                foreach (var row in selectedMods)
                {
                    string modPath = Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, row.name);
                    Logger.WriteLine($@"{MeInfo} Attempting to delete {row.name} at '{modPath}'.", LoggerType.Debug);

                    try
                    {
                        var deleteResult = await Task.Run(() => FileHelper.DeleteDirectory(modPath));
                        if (!deleteResult)
                        {
                            Logger.WriteLine($@"Failed to delete '{modPath}'", LoggerType.Warning);
                            await Dispatcher.InvokeAsync(() => MessageBox.Show($"Cannot delete '{row.name}':\nThe folder contains symbolic links, junctions, hard links, or another unsafe file system reference. Please remove them manually first.", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                            continue;
                        }
                        Logger.WriteLine(string.Join(" ", MeInfo, $"Successfully delete '{modPath}'"), LoggerType.Debug);
                    }
                    catch (IOException ex)
                    {
                        Logger.WriteLine($@"IO error deleting '{modPath}': {ex.Message}", LoggerType.Error);
                        await Dispatcher.InvokeAsync(() => MessageBox.Show($"Could not delete '{row.name}':\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger.WriteLine($@"Permission error deleting '{modPath}': {ex.Message}", LoggerType.Error);
                        await Dispatcher.InvokeAsync(() => MessageBox.Show($"Permission denied while deleting '{row.name}':\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($@"Unexpected error deleting '{modPath}': {ex}", LoggerType.Error);
                        await Dispatcher.InvokeAsync(() => MessageBox.Show($"An unexpected error occurred while deleting '{row.name}':\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }

                Global.IsModGridLoaded = true;
                ModsWatcher.StartWatching();
                await RefreshAsync();
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);


            if (WorkManager.IsBusy || Extractor.MoveCount > 0)
            {
                var ret = WindowHelper.DMMWindowOpenAsync(74).Result;
                if (ret == WindowHelper.WindowCloseStatus.No || ret == WindowHelper.WindowCloseStatus.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (WindowState == WindowState.Maximized)
            {
                Global.ConfigJson.Height = RestoreBounds.Height;
                Global.ConfigJson.Width = RestoreBounds.Width;
                Global.ConfigJson.Maximized = true;
            }
            else
            {
                Global.ConfigJson.Height = Height;
                Global.ConfigJson.Width = Width;
                Global.ConfigJson.Maximized = false;
            }

            Global.ConfigJson.TopGridHeight = MainGrid.RowDefinitions[2].Height.Value;
            Global.ConfigJson.BottomGridHeight = MainGrid.RowDefinitions[4].Height.Value;
            Global.ConfigJson.LeftGridWidth = MiddleGrid.ColumnDefinitions[0].Width.Value;
            Global.ConfigJson.RightGridWidth = MiddleGrid.ColumnDefinitions[2].Width.Value;
            UpdateModGridAsync();
            SetColumnDisplayIndex();
            SetColumnVisible();
            ConfigJson.UpdateConfig();

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);

            Dispose();
        }

        private void OpenMod_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            var selectedMods = ModGrid.SelectedItems.OfType<Mod>().ToList();
            foreach (var row in selectedMods)
            {
                string folderName = Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, row.name);
                if (Directory.Exists(folderName))
                {
                    ProcessHelper.TryStartProcess(folderName);
                }
                else
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Directory not found: '{folderName}'. Cannot open."), LoggerType.Warning);
                }
            }
        }

        private async void RenameMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);

            foreach (var row in temp)
            {
                if (row != null)
                {
                    EditWindow ew = new(row.name, true);
                    ew.ShowDialog();
                }
            }
            await RefreshAsync();
            ModGrid.Focus();
        }

        private void ConfigureMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
            {
                if (row != null)
                {
                    ConfigureModWindow cmw = new(row);
                    cmw.ShowDialog();
                }
            }
        }
        private void FetchMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
            {
                if (row != null)
                {
                    FetchMetadataWindow fw = new(row);
                    fw.ShowDialog();
                    if (fw.success)
                        ShowMetadata(row);
                }
            }
        }
        private void MoveToTopMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedObjects = ModGrid.SelectedItems as ObservableCollection<Object>;
            var selectedMods = selectedObjects.Cast<Mod>().ToList();
            for (int i = selectedMods.Count - 1; i >= 0; i--)
            {
                var n = Global.ModList_All.IndexOf(selectedMods[i]);
                Global.ModList_All.Move(n, 0);
            }

            e.Handled = true;
        }
        private void MoveToBottomMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedObjects = ModGrid.SelectedItems as ObservableCollection<Object>;
            var selectedMods = selectedObjects.Cast<Mod>().ToList();
            for (int i = 0; i < selectedMods.Count; i++)
            {
                var n = Global.ModList_All.IndexOf(selectedMods[i]);
                Global.ModList_All.Move(n, Global.ModList_All.Count - 1);
            }

            e.Handled = true;
        }

        private void ModGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Move;
                DropBox.Visibility = Visibility.Visible;
            }
        }
        private void ModGrid_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            DropBox.Visibility = Visibility.Collapsed;
        }
        /// <summary>
        /// ModGridのドラッグ＆ドロップイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ModGrid_Drop(object sender, DragEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());

            DropBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
            if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"Please click Setup before installing mods!"), LoggerType.Warning);
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                IsEnabledControls(false);
                ModsWatcher?.StopWatching();
                MMPWatcherDLL?.StopWatching();
                MMPWatcherTOML?.StopWatching();

                try
                {
                    await WorkManager.RunAsync(async () =>
                    {
                        string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                        foreach (var filePath in fileList)
                        {
                            var parentPath = Directory.GetParent(Path.GetFullPath(filePath)).FullName;
                            if (!Logger.MaskAddDropFilePathList.Contains(parentPath)) Logger.MaskAddDropFilePathList.Add(parentPath);

                            var fileName = Path.GetFileName(filePath);
                            Logger.WriteLine($"Expanding the dropped file. [{fileName}]", LoggerType.Info);

                            var extract = new ExtractInfo()
                            {
                                Site = ExtractInfo.SITE.LOCAL,
                                Type = ExtractInfo.TYPE.DROP,
                            };
                            MoveInfoData moveInfo = new() { FullPath = $"{filePath}", Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE };
                            extract.MoveInfoList.Add(moveInfo);

                            await Task.Run(() => Extractor.ExtractMain(extract));
                        }
                    });
                }
                finally
                {
                    DropBox.Visibility = Visibility.Collapsed;
                    await RefreshAsync(isEnabledControls: true);
                    ModsWatcher?.StartWatching();
                    MMPWatcherDLL?.StartWatching();
                    MMPWatcherTOML?.StartWatching();
                }
            }
        }


        private void DMLConsoleBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                MessageBox.Show(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].DMLConfig);
            }
        }
        private void CoreUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }

            App.Current.Dispatcher.Invoke(async () =>
            {
                IsEnabledControls(false);

                // Modの全アップデートチェック
                //Global.logger.WriteLine("Checking for mod all updates...", LoggerType.Info);
                //await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this, false);

                // Update Checkボタンなら設定ファイルを参照しない
                Logger.WriteLine("Checking for DivaModManager by Enomoto update...", LoggerType.Info);
                if (await DMMUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    Close();    // DMMeDownloadで展開、手動更新のためCloseしない
                Logger.WriteLine($"Checking for DivaModLoader update...", LoggerType.Info);
                await DMLUpdater.CheckForDMLUpdate(new CancellationTokenSource());
                await RefreshAsync();
            });
        }
        private async void CleanUpdateMod_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                Logger.WriteLine($"Checking for mod updates...", LoggerType.Info);
                List<Mod> selectedMods = ModGrid.SelectedItems.OfType<Mod>().ToList();
                await ModUpdater.CheckForUpdates(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, selectedMods, true,
                    onExtractAsync: async (extractInfo) =>
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            IsEnabledControls(false);
                            ModsWatcher?.StopWatching();
                            MMPWatcherDLL?.StopWatching();
                            MMPWatcherTOML?.StopWatching();
                        });
                        await Task.Run(() => Extractor.ExtractMain(extractInfo));
                    });
            }
            finally
            {
                DropBox.Visibility = Visibility.Collapsed;
                await RefreshAsync(isEnabledControls: true);
                ModsWatcher?.StartWatching();
                MMPWatcherDLL?.StartWatching();
                MMPWatcherTOML?.StartWatching();
            }
        }

        /// <summary>
        /// Preview、PreviewBG、DescriptionWindowの更新
        /// 　＊各Modのエラーチェックもここで行う
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="called"></param>
        private void ShowMetadata(Mod mod, [CallerMemberName] string caller = "")
        {
            string path = mod?.directory_path;

            DescriptionWindowInit();
            ViewErrorAndWarningDescriptionWindow(mod);

            previewFilesIndex = 0;
            previewFiles = null;

            try
            {
                // Delete後に呼ばれた場合はmod == null(初期状態の表示を行うため)
                if (mod == null)
                {
                }
                else if (Directory.Exists(path))
                {
                    var allFiles = Directory.GetFiles(path, "*.*").ToList();
                    previewFiles = allFiles.Where(f =>
                        f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }
                else
                {
                    Logger.WriteLine($"Mod directory not found for metadata: '{path}'", LoggerType.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error accessing preview files in '{path}': {ex.Message}", LoggerType.Error);
                previewFiles = new();
            }

            if (File.Exists(mod?.mods_json_path)
                || File.Exists(mod?.config_toml_path))
            {
                Metadata metadata = null;
                TomlTable config = null;
                if (File.Exists(mod.mods_json_path))
                {
                    var metadataString = File.ReadAllText(mod.mods_json_path);
                    metadata = JsonSerializer.Deserialize<Metadata>(metadataString);
                }
                else if (File.Exists(mod.config_toml_path))
                {
                    SetDefaultPreviewImage();

                    var configString = File.ReadAllText(mod.config_toml_path);
                    if (!Toml.TryToModel(configString, out config, out var diagnostics))
                    {
                        Logger.WriteLine($"{diagnostics[0].Message} for {mod}. Rewriting {mod.config_toml_path} with only the enabled & include fields", LoggerType.Warning);
                        config = new();
                        var enabled = Global.ModList_All.ToList().Find(x => x.directory_name == mod.directory_name).enabled;
                        config.Add("enabled", enabled);
                        TomlHelper.AddInclude(config);
                        File.WriteAllText(mod.config_toml_path, Toml.FromModel(config));
                    }
                }

                var para = new Paragraph();
                var text = string.Empty;
                if (config != null && config.ContainsKey("author") && (config["author"] as string).Length > 0)
                    text += $"Author: {config["author"]}\n";
                else if (metadata != null)
                {
                    if (metadata.submitter != null)
                    {
                        para.Inlines.Add($"Submitter: ");
                        if (metadata.avi != null && metadata.avi.ToString().Length > 0)
                        {
                            BitmapImage bm = ImageCacheManager.GetCachedBitmap(metadata.avi);
                            Image image = new()
                            {
                                Source = bm,
                                Height = 35
                            };
                            para.Inlines.Add(image);
                            para.Inlines.Add(" ");
                        }
                        if (metadata.upic != null && metadata.upic.ToString().Length > 0)
                        {
                            BitmapImage bm = ImageCacheManager.GetCachedBitmap(metadata.upic);
                            Image image = new()
                            {
                                Source = bm,
                                Height = 25
                            };
                            para.Inlines.Add(image);
                        }
                        else
                            para.Inlines.Add($"{metadata.submitter}");
                        DescriptionWindow.Document.Blocks.Add(para);
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
                if (metadata != null && !string.IsNullOrEmpty(metadata.cat))
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var init = ConvertToFlowParagraph(text);
                        DescriptionWindow.Document.Blocks.Add(init);
                    }
                    text = string.Empty;
                    para = new Paragraph();
                    para.Inlines.Add("Category: ");
                    if (metadata.caticon != null && metadata.caticon.ToString().Length > 0)
                    {
                        BitmapImage bm = ImageCacheManager.GetCachedBitmap(metadata.caticon);
                        Image image = new()
                        {
                            Source = bm,
                            Width = 20
                        };
                        para.Inlines.Add(image);
                    }
                    para.Inlines.Add($" {metadata.cat}");
                    DescriptionWindow.Document.Blocks.Add(para);
                }
                else if (!string.IsNullOrWhiteSpace(text))
                    text += "\n";

                if (config != null && config.ContainsKey("description") && (config["description"] as string).Length > 0)
                    text += $"Description: {config["description"]}\n";
                else if (metadata != null && metadata.description != null && metadata.description.Length > 0)
                    text += $"Description: {metadata.description}\n";
                if (metadata != null && metadata.homepage != null && metadata.homepage.ToString().Length > 0)
                    text += $"Home Page: {metadata.homepage}";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var init = ConvertToFlowParagraph(text);
                    DescriptionWindow.Document.Blocks.Add(init);
                }
                // メタデータ画像をバックグラウンドでキャッシュ（次回選択時に即表示）
                if (metadata != null)
                {
                    if (metadata.avi != null && metadata.avi.ToString().Length > 0)
                        _ = ImageCacheManager.PreCacheAsync(metadata.avi, -1);
                    if (metadata.upic != null && metadata.upic.ToString().Length > 0)
                        _ = ImageCacheManager.PreCacheAsync(metadata.upic, -1);
                    if (metadata.caticon != null && metadata.caticon.ToString().Length > 0)
                        _ = ImageCacheManager.PreCacheAsync(metadata.caticon, -1);
                }
                if (previewFiles != null && previewFiles.Count > 0)
                {
                    try
                    {
                        string imagePath = previewFiles[previewFilesIndex]; // ファイルのフルパスを取得

                        // --- MemoryStream を使わずに UriSource で直接読み込む ---
                        var img = new BitmapImage();
                        img.BeginInit();
                        // UriSource にファイルパスを設定 (絶対パスを指定)
                        var imageUri = new Uri(imagePath, UriKind.Absolute);
                        img.UriSource = imageUri;
                        // CacheOption は OnLoad のまま推奨 (読み込み完了後にファイルを解放するため)
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        // DecodePixelWidth/Height を設定するとメモリ効率が良くなる場合がある (任意)
                        // img.DecodePixelWidth = (int)Preview.ActualWidth; // または固定値
                        img.EndInit();

                        // Freeze すると別スレッドからのアクセスでも安全になる場合がある
                        if (img.CanFreeze) img.Freeze();

                        Dispatcher.InvokeAsync(async () =>
                        {
                            ImageBehavior.SetAnimatedSource(Preview, img);
                            ImageBehavior.SetAnimatedSource(PreviewBG, img);
                        });
                    }
                    catch (UriFormatException ex)
                    {
                        Logger.WriteLine($"Invalid URI format for image path '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                        SetDefaultPreviewImage();
                    }
                    catch (FileNotFoundException) // UriSource でもファイルが見つからない場合
                    {
                        Logger.WriteLine($"Preview file not found (UriSource): '{previewFiles[0]}'", LoggerType.Warning);
                        SetDefaultPreviewImage();
                    }
                    catch (IOException ex) // ファイル読み込み中のIOエラー
                    {
                        Logger.WriteLine($"IO error loading preview image from UriSource '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                        SetDefaultPreviewImage();
                    }
                    catch (NotSupportedException ex) // サポートされていない画像形式
                    {
                        Logger.WriteLine($"Unsupported image format for preview file '{previewFiles[0]}': {ex.Message}", LoggerType.Warning);
                        SetDefaultPreviewImage();
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Error loading preview image '{previewFiles[0]}': {ex}", LoggerType.Error);
                        SetDefaultPreviewImage();
                    }
                }
                else if (File.Exists(Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, mod.name, "mod.json")))
                {
                    try
                    {
                        // metadata.preview (Uri) から BitmapImage を作成
                        metadata = null; // (mod.json からロードする処理が必要)
                        if (metadata?.preview != null)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = metadata.preview; // ネットワークアクセスが発生
                            bitmap.CacheOption = BitmapCacheOption.OnLoad; // OnLoad推奨
                            bitmap.EndInit();
                            if (bitmap.CanFreeze) bitmap.Freeze();
                            ImageBehavior.SetAnimatedSource(Preview, bitmap);
                            ImageBehavior.SetAnimatedSource(PreviewBG, bitmap);

                            Logger.WriteLine($"Download preview: {metadata.preview}", LoggerType.Debug);
                        }
                        else
                        {
                            SetDefaultPreviewImage();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Error loading preview image from URI '{mod.name}/mod.json': {ex.Message}", LoggerType.Error);
                        SetDefaultPreviewImage();
                    }
                }
                else
                {
                    SetDefaultPreviewImage();
                }
                if (DescriptionWindow.Document.Blocks.Count == 0)
                {
                    defaultFlow.Blocks.Add(ConvertToFlowParagraph(defaultText));
                    DescriptionWindow.Document = defaultFlow;
                }
                else
                {
                    var descriptionText = new TextRange(DescriptionWindow.Document.ContentStart, DescriptionWindow.Document.ContentEnd);
                    descriptionText.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Center);
                }
                // サムネイル枚数表示
                if (previewFiles.Count > 1)
                {
                    ThumbnailCountText.Text = $"{previewFilesIndex + 1}/{previewFiles.Count.ToString()}";

                    ThumbnailCountRect.Visibility = Visibility.Visible;
                    ThumbnailCountText.Visibility = Visibility.Visible;
                }
                DescriptionWindow.ScrollToHome();
            }
        }

        private void ModPreviewLeftButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (previewFiles == null || previewFiles.Count == 0)
                {
                    SetDefaultPreviewImage();
                    return;
                }
                if (previewFiles.Count == 1)
                {
                    return;
                }
                else if (previewFilesIndex == 0)
                {
                    previewFilesIndex = previewFiles.Count - 1;
                }
                else
                {
                    previewFilesIndex--;
                }

                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(previewFiles[previewFilesIndex], UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();

                Dispatcher.InvokeAsync(async () =>
                {
                    ImageBehavior.SetAnimatedSource(Preview, img);
                    ImageBehavior.SetAnimatedSource(PreviewBG, img);
                });

                if (img.CanFreeze) img.Freeze();

                ThumbnailCountText.Text = $"{previewFilesIndex + 1}/{previewFiles.Count.ToString()}";

                Logger.WriteLine($"previewFiles[previewFilesIndex] : {previewFiles[previewFilesIndex]}", LoggerType.Debug);
                Logger.WriteLine($"previewFilesIndex : {previewFilesIndex}", LoggerType.Debug);
            }
            catch (UriFormatException ex)
            {
                Logger.WriteLine($"Invalid URI format for image path '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
            catch (FileNotFoundException) // UriSource でもファイルが見つからない場合
            {
                Logger.WriteLine($"Preview file not found (UriSource): '{previewFiles[0]}'", LoggerType.Warning);
                SetDefaultPreviewImage();
            }
            catch (IOException ex) // ファイル読み込み中のIOエラー
            {
                Logger.WriteLine($"IO error loading preview image from UriSource '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
            catch (NotSupportedException ex) // サポートされていない画像形式
            {
                Logger.WriteLine($"Unsupported image format for preview file '{previewFiles[0]}': {ex.Message}", LoggerType.Warning);
                SetDefaultPreviewImage();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading preview image '{previewFiles[0]}': {ex}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
        }
        private void ModPreviewRightButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (previewFiles == null || previewFiles.Count == 0)
                {
                    SetDefaultPreviewImage();
                    return;
                }
                if (previewFiles.Count == 1)
                {
                    return;
                }
                else if (previewFilesIndex == previewFiles.Count - 1)
                {
                    previewFilesIndex = 0;
                }
                else
                {
                    previewFilesIndex++;
                }

                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(previewFiles[previewFilesIndex], UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();

                Dispatcher.InvokeAsync(async () =>
                {
                    ImageBehavior.SetAnimatedSource(Preview, img);
                    ImageBehavior.SetAnimatedSource(PreviewBG, img);
                });

                if (img.CanFreeze) img.Freeze();

                ThumbnailCountText.Text = $"{previewFilesIndex + 1}/{previewFiles.Count.ToString()}";

                Logger.WriteLine($"previewFiles[previewFilesIndex] : {previewFiles[previewFilesIndex]}", LoggerType.Debug);
                Logger.WriteLine($"previewFilesIndex : {previewFilesIndex}", LoggerType.Debug);
            }
            catch (UriFormatException ex)
            {
                Logger.WriteLine($"Invalid URI format for image path '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
            catch (FileNotFoundException) // UriSource でもファイルが見つからない場合
            {
                Logger.WriteLine($"Preview file not found (UriSource): '{previewFiles[0]}'", LoggerType.Warning);
                SetDefaultPreviewImage();
            }
            catch (IOException ex) // ファイル読み込み中のIOエラー
            {
                Logger.WriteLine($"IO error loading preview image from UriSource '{previewFiles[0]}': {ex.Message}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
            catch (NotSupportedException ex) // サポートされていない画像形式
            {
                Logger.WriteLine($"Unsupported image format for preview file '{previewFiles[0]}': {ex.Message}", LoggerType.Warning);
                SetDefaultPreviewImage();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading preview image '{previewFiles[0]}': {ex}", LoggerType.Error);
                SetDefaultPreviewImage();
            }
        }

        // --- デフォルトプレビュー画像設定の共通化 ---
        private void SetDefaultPreviewImage()
        {
            try
            {
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
                // if (bitmap.CanFreeze) bitmap.Freeze();
                ImageBehavior.SetAnimatedSource(Preview, bitmap);
                ImageBehavior.SetAnimatedSource(PreviewBG, null); // BG はクリア
                ThumbnailCountRect.Visibility = Visibility.Collapsed;
                ThumbnailCountText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading default preview image: {ex.Message}", LoggerType.Error);
                // デフォルト画像すら読み込めない場合のフォールバック？
                ImageBehavior.SetAnimatedSource(Preview, null);
                ImageBehavior.SetAnimatedSource(PreviewBG, null);
                ThumbnailCountRect.Visibility = Visibility.Collapsed;
                ThumbnailCountText.Visibility = Visibility.Collapsed;
            }
        }

        private void DescriptionWindowInit()
        {
            TextRange tr = new(DescriptionWindow.Document.ContentStart, DescriptionWindow.Document.ContentEnd);
            tr.Text = "";
            var brush = DescriptionWindow.Foreground;
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
        }

        private void ModGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Mod row = (Mod)ModGrid.SelectedItem;
            if (Global.IsModGridLoaded && row != null)
            {
                ShowMetadata(row);
            }
        }

        private async void GBDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (item.IsNsfw)
            {
                ExplicitWindow explicitWindow = new(WindowList.MessageString(80));
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }
            if (item.IsSpoiler)
            {
                ExplicitWindow explicitWindow = new(WindowList.MessageString(81));
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }

            try
            {
                await WorkManager.RunAsync(async () =>
                {
                    await new ModDownloader().BrowserDownload(Global.GAME_ID, item, onExtractAsync: async (extractInfo) =>
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            IsEnabledControls(false);
                            ModsWatcher?.StopWatching();
                            MMPWatcherDLL?.StopWatching();
                            MMPWatcherTOML?.StopWatching();
                        });
                        await Task.Run(() => Extractor.ExtractMain(extractInfo));
                    });
                });
            }
            finally
            {
                DropBox.Visibility = Visibility.Collapsed;
                await RefreshAsync(isEnabledControls: true);
                ModsWatcher?.StartWatching();
                MMPWatcherDLL?.StartWatching();
                MMPWatcherTOML?.StartWatching();
            }
        }
        private async void DMADownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var post = button.DataContext as DivaModArchivePost;

            if (post.Explicit)
            {
                ExplicitWindow explicitWindow = new(post.Explicit_Reason);
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }

            try
            {
                await WorkManager.RunAsync(async () =>
                {
                    await new ModDownloader().DMABrowserDownload(Global.games[GameBox.SelectedIndex], post, onExtractAsync: async (extractInfo) =>
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            IsEnabledControls(false);
                            ModsWatcher?.StopWatching();
                            MMPWatcherDLL?.StopWatching();
                            MMPWatcherTOML?.StopWatching();
                        });
                        await Task.Run(() => Extractor.ExtractMain(extractInfo));
                    });
                });
            }
            finally
            {
                DropBox.Visibility = Visibility.Collapsed;
                await RefreshAsync(isEnabledControls: true);
                ModsWatcher?.StartWatching();
                MMPWatcherDLL?.StartWatching();
                MMPWatcherTOML?.StartWatching();
            }
        }
        private void GBAltDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            new AltLinkWindow(item.AlternateFileSources, item.Title,
                item.Link.AbsoluteUri).ShowDialog();
        }

        private void GBHomepageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is GameBananaRecord item && item.Link != null)
            {
                ProcessHelper.TryStartProcess(item.Link.AbsoluteUri);
            }
        }

        private void DMAHomepage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DivaModArchivePost item)
            {
                ProcessHelper.TryStartProcess(Global.DMA_HOMEPAGE_URL_POSTS + item.ID);
            }
        }

        private int imageCounter;
        private int imageCount;

        private async void MoreInfo_Click(object sender, RoutedEventArgs e)
        {
            GBHomepageButton.Content = $"{(GBTypeBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().TrimEnd('s')} Page";
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (item.IsNsfw)
            {
                ExplicitWindow explicitWindow = new(WindowList.MessageString(80));
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }
            if (item.IsSpoiler)
            {
                ExplicitWindow explicitWindow = new(WindowList.MessageString(81));
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }

            if (item.Compatible)
                GBDownloadButton.Visibility = Visibility.Visible;
            else
                GBDownloadButton.Visibility = Visibility.Collapsed;
            if (item.HasAltLinks)
                GBAltButton.Visibility = Visibility.Visible;
            else
                GBAltButton.Visibility = Visibility.Collapsed;
            GBDescPanel.DataContext = button.DataContext;
            GBMediaPanel.DataContext = button.DataContext;
            GBDescText.ScrollToHome();
            var text = "";
            text += item.ConvertedText;
            GBDescText.Document = ConvertToFlowDocument(text);
            GBImageLeft.IsEnabled = true;
            GBImageRight.IsEnabled = true;
            GBBigImageLeft.IsEnabled = true;
            GBBigImageRight.IsEnabled = true;
            imageCount = item.Media.Where(x => x.Type == "image").ToList().Count;
            imageCounter = 0;
            if (imageCount > 0)
            {
                Grid.SetColumnSpan(GBDescText, 1);
                GBImagePanel.Visibility = Visibility.Visible;
                var image = await ImageCacheManager.GetCachedBitmapAsync(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"), GB_IMAGE_CACHE_HOURS);
                GBScreenshot.Source = image;
                GBBigScreenshot.Source = image;
                GBCaptionText.Text = item.Media[imageCounter].Caption;
                GBBigCaptionText.Text = item.Media[imageCounter].Caption;
                if (!string.IsNullOrEmpty(GBCaptionText.Text))
                {
                    GBBigCaptionText.Visibility = Visibility.Visible;
                    GBCaptionText.Visibility = Visibility.Visible;
                }
                else
                {
                    GBBigCaptionText.Visibility = Visibility.Collapsed;
                    GBCaptionText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Grid.SetColumnSpan(GBDescText, 2);
                GBImagePanel.Visibility = Visibility.Collapsed;
            }
            if (imageCount == 1)
            {
                GBImageLeft.IsEnabled = false;
                GBImageRight.IsEnabled = false;
                GBBigImageLeft.IsEnabled = false;
                GBBigImageRight.IsEnabled = false;
            }

            GBDescPanel.Visibility = Visibility.Visible;
        }
        private async void DMAMoreInfo_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)e.Source;
            var post = (DivaModArchivePost)btn.DataContext;
            if (post.Explicit)
            {
                ExplicitWindow explicitWindow = new(post.Explicit_Reason);
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }

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
                var image = await ImageCacheManager.GetCachedBitmapAsync(item.Images[imageCounter], DMA_IMAGE_CACHE_HOURS);
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
        private void GBCloseDesc_Click(object sender, RoutedEventArgs e)
        {
            GBDescPanel.Visibility = Visibility.Collapsed;
        }
        private void DMACloseMedia_Click(object sender, RoutedEventArgs e)
        {
            DMAMediaPanel.Visibility = Visibility.Collapsed;
        }
        private void GBCloseMedia_Click(object sender, RoutedEventArgs e)
        {
            GBMediaPanel.Visibility = Visibility.Collapsed;
        }
        private void DMAImage_Click(object sender, RoutedEventArgs e)
        {
            DMAMediaPanel.Visibility = Visibility.Visible;
        }
        private void GBImage_Click(object sender, RoutedEventArgs e)
        {
            GBMediaPanel.Visibility = Visibility.Visible;
        }
        private async void DMAImageLeft_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            if (--imageCounter == -1)
                imageCounter = imageCount - 1;
            var image = await ImageCacheManager.GetCachedBitmapAsync(item.Images[imageCounter], DMA_IMAGE_CACHE_HOURS);
            DMAScreenshot.Source = image;
            DMABigScreenshot.Source = image;
        }
        private async void GBImageLeft_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (--imageCounter == -1)
                imageCounter = imageCount - 1;
            var image = await ImageCacheManager.GetCachedBitmapAsync(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"), GB_IMAGE_CACHE_HOURS);
            GBScreenshot.Source = image;
            GBCaptionText.Text = item.Media[imageCounter].Caption;
            GBBigScreenshot.Source = image;
            GBBigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!string.IsNullOrEmpty(GBCaptionText.Text))
            {
                GBBigCaptionText.Visibility = Visibility.Visible;
                GBCaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                GBBigCaptionText.Visibility = Visibility.Collapsed;
                GBCaptionText.Visibility = Visibility.Collapsed;
            }
        }
        private async void DMAImageRight_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DivaModArchivePost;
            if (++imageCounter == imageCount)
                imageCounter = 0;
            var image = await ImageCacheManager.GetCachedBitmapAsync(item.Images[imageCounter], DMA_IMAGE_CACHE_HOURS);
            DMAScreenshot.Source = image;
            DMABigScreenshot.Source = image;
        }
        private async void GBImageRight_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (++imageCounter == imageCount)
                imageCounter = 0;
            var image = await ImageCacheManager.GetCachedBitmapAsync(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"), GB_IMAGE_CACHE_HOURS);
            GBScreenshot.Source = image;
            GBCaptionText.Text = item.Media[imageCounter].Caption;
            GBBigScreenshot.Source = image;
            GBBigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!string.IsNullOrEmpty(GBCaptionText.Text))
            {
                GBBigCaptionText.Visibility = Visibility.Visible;
                GBCaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                GBBigCaptionText.Visibility = Visibility.Collapsed;
                GBCaptionText.Visibility = Visibility.Collapsed;
            }
        }

        private static bool selected = false;

        private static Dictionary<GameFilter, Dictionary<Features.Feed.TypeFilter, List<GameBananaCategory>>> cats = new();

        private static readonly List<GameBananaCategory> All = new GameBananaCategory[]
        {
            new()
            {
                Name = "All",
                ID = null
            }
        }.ToList();
        private static readonly List<GameBananaCategory> None = new GameBananaCategory[]
        {
            new()
            {
                Name = "- - -",
                ID = null
            }
        }.ToList();
        private async void InitializeGBBrowser()
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                GBLoadingBar.Visibility = Visibility.Visible;
                GBErrorPanel.Visibility = Visibility.Collapsed;
                GBBrowserRefreshButton.Visibility = Visibility.Collapsed;
            });

            var gameIDS = new string[] { Global.GAME_ID };
            var types = new string[] { "Mod", "Wip", "Sound" };
            var gameCounter = 0;

            try
            {
                foreach (var gameID in gameIDS)
                {
                    var counter = 0;
                    double totalPages = 0;
                    var perPage = 5;
                    foreach (var type in types)
                    {
                        var requestUrl = $"https://gamebanana.com/apiv4/{type}Category/ByGame?_aGameRowIds[]={gameID}&_sRecordSchema=Custom" +
                            $"&_csvProperties=_idRow,_sName,_sProfileUrl,_sIconUrl,_idParentCategoryRow&_nPerpage={perPage}";
                        string responseString = "";
                        HttpResponseMessage responseMessage = null;
                        try
                        {
                            responseMessage = await Global.GBclient.GetAsync(requestUrl);
                            responseMessage.EnsureSuccessStatusCode(); // これで 2xx 以外は HttpRequestException をスロー
                            responseString = await responseMessage.Content.ReadAsStringAsync();
                            responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                            var numRecords = responseMessage.GetHeader("X-GbApi-Metadata_nRecordCount");
                            if (numRecords != -1)
                            {
                                totalPages = Math.Ceiling(numRecords / perPage);
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            string errorMsg = $"Failed to fetch category data ({type} for game {gameID}).";
                            HandleHttpRequestError(ex, responseMessage, errorMsg);
                            return;
                        }
                        catch (TaskCanceledException ex) // タイムアウトなど
                        {
                            Logger.WriteLine($"Category fetch cancelled or timed out ({type} for game {gameID}): {ex.Message}", LoggerType.Warning);
                            ShowBrowserError("The request timed out or was canceled.");
                            return;
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine($"Unexpected error fetching category data ({type} for game {gameID}): {ex}", LoggerType.Error);
                            ShowBrowserError($"An unexpected error occurred: {ex.Message}");
                            return;
                        }

                        List<GameBananaCategory> response = null;
                        try
                        {
                            response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                            if (response == null) throw new JsonException("Deserialization resulted in null.");
                        }
                        catch (JsonException ex)
                        {
                            Logger.WriteLine($"Error parsing category JSON ({type} for game {gameID}): {ex.Message}", LoggerType.Error);
                            ShowBrowserError("Failed to parse category data from GameBanana.");
                            return;
                        }
                        catch (Exception ex) // Regex.Replace など他の箇所の例外
                        {
                            Logger.WriteLine($"Error processing category data ({type} for game {gameID}): {ex}", LoggerType.Error);
                            ShowBrowserError($"An unexpected error occurred while processing category data: {ex.Message}");
                            return;
                        }
                        if (!cats.ContainsKey((GameFilter)gameCounter))
                            cats.Add((GameFilter)gameCounter, new Dictionary<Features.Feed.TypeFilter, List<GameBananaCategory>>());
                        if (!cats[(GameFilter)gameCounter].ContainsKey((Features.Feed.TypeFilter)counter))
                            cats[(GameFilter)gameCounter].Add((Features.Feed.TypeFilter)counter, response);

                        if (totalPages > 1)
                        {
                            for (double i = 2; i <= totalPages; i++)
                            {
                                var requestUrlPage = $"{requestUrl}&_nPage={i}";
                                try
                                {
                                    responseString = await Global.GBclient.GetStringAsync(requestUrlPage);
                                    responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                                }
                                catch (HttpRequestException ex)
                                {
                                    GBLoadingBar.Visibility = Visibility.Collapsed;
                                    GBErrorPanel.Visibility = Visibility.Visible;
                                    GBBrowserRefreshButton.Visibility = Visibility.Visible;
                                    GBBrowserMessage.Text = Regex.Match(ex.Message, @"\d+").Value switch
                                    {
                                        "443" => "Your internet connection is down.",
                                        "500" or "503" or "504" => "GameBanana's servers are down.",
                                        _ => ex.Message,
                                    };
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    GBLoadingBar.Visibility = Visibility.Collapsed;
                                    GBErrorPanel.Visibility = Visibility.Visible;
                                    GBBrowserRefreshButton.Visibility = Visibility.Visible;
                                    GBBrowserMessage.Text = ex.Message;
                                    return;
                                }
                                try
                                {
                                    response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                                }
                                catch (Exception)
                                {
                                    GBLoadingBar.Visibility = Visibility.Collapsed;
                                    GBErrorPanel.Visibility = Visibility.Visible;
                                    GBBrowserRefreshButton.Visibility = Visibility.Visible;
                                    GBBrowserMessage.Text = "Uh oh! Something went wrong while deserializing the categories...";
                                    return;
                                }
                                cats[(GameFilter)gameCounter][(Features.Feed.TypeFilter)counter] = cats[(GameFilter)gameCounter][(Features.Feed.TypeFilter)counter].Concat(response).ToList();
                            }
                        }
                        counter++;
                    }
                    gameCounter++;
                }

                await Dispatcher.InvokeAsync(async () =>
                {
                    filterSelect = true;
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                    GBSubCatBox.ItemsSource = None;
                    GBCatBox.SelectedIndex = 0;
                    GBSubCatBox.SelectedIndex = 0;
                    GBSortBox.SelectedIndex = 1;
                    filterSelect = false;
                    GBLoadingBar.Visibility = Visibility.Collapsed;
                    selected = true;
                    GBRefreshFilterAsync();
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Unexpected critical error during browser initialization: {ex}", LoggerType.Error);
                ShowBrowserError($"A critical error occurred during initialization: {ex.Message}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (GBLoadingBar.Visibility == Visibility.Visible) GBLoadingBar.Visibility = Visibility.Collapsed;
                });
            }

            filterSelect = true;
            GBSortBox.ItemsSource = FilterBoxList;
            GBCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
            GBSubCatBox.ItemsSource = None;
            GBCatBox.SelectedIndex = 0;
            GBSubCatBox.SelectedIndex = 0;
            GBSortBox.SelectedIndex = 1;
            filterSelect = false;
            GBRefreshFilterAsync();
            selected = true;
        }

        // --- HttpRequestException の共通エラーハンドリング ---
        private void HandleHttpRequestError(HttpRequestException ex, HttpResponseMessage response, string contextMessage)
        {
            Logger.WriteLine($"{contextMessage} Status: {response?.StatusCode}, Error: {ex.Message}", LoggerType.Error);
            string userMessage;
            if (ex.InnerException is System.Net.Sockets.SocketException sockEx)
            {
                userMessage = $"Network error: {sockEx.Message}";
                Logger.WriteLine($"Socket Error Code: {sockEx.SocketErrorCode}", LoggerType.Error);
            }
            else if (response?.StatusCode != null)
            {
                userMessage = response.StatusCode switch
                {
                    // 404
                    System.Net.HttpStatusCode.NotFound => "The requested resource was not found on the server.",
                    // 503
                    System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.InternalServerError or System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.GatewayTimeout => "The server is currently unavailable or experiencing issues.",
                    // 401
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Access denied by the server.",
                    _ => $"Server returned an error: {(int)response.StatusCode} {response.ReasonPhrase}",
                };
            }
            else // ステータスコードがない場合 (DNS解決失敗、接続拒否など)
            {
                userMessage = $"Could not connect to the server: {ex.Message}";
            }
            ShowBrowserError(userMessage);
        }

        private void ShowBrowserError(string message)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                GBLoadingBar.Visibility = Visibility.Collapsed;
                GBErrorPanel.Visibility = Visibility.Visible;
                GBBrowserRefreshButton.Visibility = Visibility.Visible;
                GBBrowserMessage.Text = message;
                GBFeedBox.ItemsSource = null;
                GBFeedBox.Visibility = Visibility.Collapsed;
            });
        }
        private void ShowDMAError(string message)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                DMALoadPanel.Visibility = Visibility.Collapsed;
                DMAErrorPanel.Visibility = Visibility.Visible;
                DMABrowserRefreshButton.Visibility = Visibility.Visible;
                DMABrowserMessage.Text = message;
                DMAFeedBox.ItemsSource = null;
                DMAFeedBox.Visibility = Visibility.Collapsed;
            });
        }
        private void GBModBrowserTab_Selected(object sender, RoutedEventArgs e)
        {
            if (!selected)
            {
                InitializeGBBrowser();
            }
        }
        private void DMAModBrowserTab_Selected(object sender, RoutedEventArgs e)
        {
            if (!DMAselected)
            {
                DMARefreshFilterAsync();
            }
        }
        private void ModManagerTab_Selected(object sender, RoutedEventArgs e)
        {
            IsEnabledControls(true);
        }

        private static int page = 1;
        private static int DMApage = 1;
        private void GBPageLeft_Click(object sender, RoutedEventArgs e)
        {
            --page;
            GBRefreshFilterAsync();
        }
        private void GBPageRight_Click(object sender, RoutedEventArgs e)
        {
            ++page;
            GBRefreshFilterAsync();
        }
        private void DMADecrementPage(object sender, RoutedEventArgs e)
        {
            --DMApage;
            DMARefreshFilterAsync();
        }
        private void DMAIncrementPage(object sender, RoutedEventArgs e)
        {
            ++DMApage;
            DMARefreshFilterAsync();
        }
        private void DMABrowserRefreshButton_Click(object sender, RoutedEventArgs e)
        {
        }
        private void GBBrowserRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!selected)
                InitializeGBBrowser();
            else
                GBRefreshFilterAsync();
        }
        private void GBClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            FeedGenerator.ClearCache();
            ImageCacheManager.ClearCache();
            GBRefreshFilterAsync();
        }
        private void DMAClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            DMAFeedGenerator.DMAClearCache();
            ImageCacheManager.ClearCache();
            DMARefreshFilterAsync();
        }
        private static bool filterSelect;
        private static bool searched = false;
        private async void GBRefreshFilterAsync()
        {
            IsEnabledControls(false, isEnableTabs: true);

            await Dispatcher.InvokeAsync(async () =>
            {
                GBErrorPanel.Visibility = Visibility.Collapsed;
                GBLoadingBar.Visibility = Visibility.Visible;
                GBFeedBox.Visibility = Visibility.Collapsed;
                GBPageText.Text = $"Page {page}";
            });

            try
            {
                var search = searched ? GBSearchBar.Text : null;
                if (!string.IsNullOrEmpty(search) && search.Contains("'"))
                {
                    search = search.Replace("'", "\\'");
                }
                try
                {
                    await FeedGenerator.GetFeed(page, (GameFilter)GameBox.SelectedIndex, (Features.Feed.TypeFilter)GBTypeBox.SelectedIndex, (FeedFilter)GBSortBox.SelectedIndex, (GameBananaCategory)GBCatBox.SelectedItem,
                         (GameBananaCategory)GBSubCatBox.SelectedItem, (GBPerPageBox.SelectedIndex + 1) * 10, (bool)GBNSFWCheckbox.IsChecked, search, GB_API_CACHE_HOURS);
                }
                catch (HttpRequestException ex)
                {
                    HandleHttpRequestError(ex, null, "Failed to get GameBanana feed.");
                    return;
                }
                catch (JsonException ex)
                {
                    Logger.WriteLine($"Error parsing GameBanana feed JSON: {ex.Message}", LoggerType.Error);
                    ShowBrowserError("Failed to parse feed data from GameBanana.");
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    Logger.WriteLine($"GameBanana feed request cancelled or timed out: {ex.Message}", LoggerType.Warning);
                    ShowBrowserError("The request timed out or was canceled.");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Unexpected error in FeedGenerator.GetFeed: {ex}", LoggerType.Error);
                    ShowBrowserError($"An unexpected error occurred while fetching the feed: {ex.Message}");
                    return;
                }
                GBFeedBox.ItemsSource = FeedGenerator.CurrentFeed.Records;
                if (FeedGenerator.error)
                {
                    GBLoadingBar.Visibility = Visibility.Collapsed;
                    GBErrorPanel.Visibility = Visibility.Visible;
                    GBBrowserRefreshButton.Visibility = Visibility.Visible;
                    if (FeedGenerator.exception.Message.Contains("JSON tokens"))
                    {
                        GBBrowserMessage.Text = "Uh oh! DivaModManager by Enomoto failed to deserialize the GameBanana feed.";
                        return;
                    }
                    GBBrowserMessage.Text = Regex.Match(FeedGenerator.exception.Message, @"\d+").Value switch
                    {
                        "443" => "Your internet connection is down.",
                        "500" or "503" or "504" => "GameBanana's servers are down.",
                        _ => FeedGenerator.exception.Message,
                    };
                    return;
                }
                if (page < FeedGenerator.CurrentFeed.TotalPages)
                    GBPageRight.IsEnabled = true;
                if (page != 1)
                    GBPageLeft.IsEnabled = true;
                if (GBFeedBox.Items.Count > 0)
                {
                    GBFeedBox.ScrollIntoView(GBFeedBox.Items[0]);
                    GBFeedBox.Visibility = Visibility.Visible;
                }
                else
                {
                    GBErrorPanel.Visibility = Visibility.Visible;
                    GBBrowserRefreshButton.Visibility = Visibility.Collapsed;
                    GBBrowserMessage.Visibility = Visibility.Visible;
                    GBBrowserMessage.Text = "DivaModManager by Enomoto couldn't find any mods.";
                }
                GBPageBox.ItemsSource = Enumerable.Range(1, (int)FeedGenerator.CurrentFeed.TotalPages);
                await Dispatcher.InvokeAsync(async () =>
                {
                    GBFeedBox.ItemsSource = FeedGenerator.CurrentFeed?.Records;

                    if (FeedGenerator.CurrentFeed?.Records != null && FeedGenerator.CurrentFeed.Records.Any())
                    {
                        GBFeedBox.Visibility = Visibility.Visible;
                        GBFeedBox.ScrollIntoView(GBFeedBox.Items[0]);
                        // ページネーションボタンの有効/無効設定
                        GBPageRight.IsEnabled = page < FeedGenerator.CurrentFeed.TotalPages;
                        GBPageLeft.IsEnabled = page > 1;
                        GBPageBox.ItemsSource = Enumerable.Range(1, (int)FeedGenerator.CurrentFeed.TotalPages);
                        filterSelect = true; //ItemsSource変更後にSelectedIndexを設定するため
                        GBPageBox.SelectedValue = page;
                        filterSelect = false;

                    }
                    else
                    {
                        GBFeedBox.Visibility = Visibility.Collapsed;
                        ShowBrowserError("DivaModManager by Enomoto couldn't find any mods matching the criteria.");
                        GBPageRight.IsEnabled = false;
                        GBPageLeft.IsEnabled = false;
                        GBPageBox.ItemsSource = null;
                    }
                    GBLoadingBar.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Unexpected error in RefreshFilter: {ex}", LoggerType.Error);
                // ShowBrowserError 内で LoadingBar は隠されるはず
                ShowBrowserError($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                IsEnabledControls(true);
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (GBLoadingBar.Visibility == Visibility.Visible) GBLoadingBar.Visibility = Visibility.Collapsed;
                });
            }
        }
        private static bool DMAselected = false;
        private async void DMARefreshFilterAsync()
        {
            Logger.WriteLine($"DMARefreshFilterAsync Start. id:{Environment.CurrentManagedThreadId}", LoggerType.Debug);

            IsEnabledControls(false, isEnableTabs: true);
            await Dispatcher.InvokeAsync(async () =>
            {
                DMAErrorPanel.Visibility = Visibility.Collapsed;
                DMALoadPanel.Visibility = Visibility.Visible;
                DMAFeedBox.Visibility = Visibility.Collapsed;
                DMAPage.Text = $"Page {DMApage}";
            });
            try
            {
                try
                {
                    await DMAFeedGenerator.GetFeed(DMApage, (DMAFeedSort)DMASortBox.SelectedIndex, (DMAFeedFilter)DMAFilterBox.SelectedIndex, DMASearchBar.Text, (DMAPerPageBox.SelectedIndex + 1) * 10, DMA_API_CACHE_HOURS);
                }
                catch (HttpRequestException ex)
                {
                    HandleHttpRequestError(ex, null, "Failed to get GameBanana feed.");
                    return;
                }
                catch (JsonException ex)
                {
                    Logger.WriteLine($"Error parsing GameBanana feed JSON: {ex.Message}", LoggerType.Error);
                    ShowBrowserError("Failed to parse feed data from GameBanana.");
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    Logger.WriteLine($"GameBanana feed request cancelled or timed out: {ex.Message}", LoggerType.Warning);
                    ShowBrowserError("The request timed out or was canceled.");
                    return;
                }
                catch (Exception ex) // FeedGenerator 内の予期せぬエラー
                {
                    Logger.WriteLine($"Unexpected error in FeedGenerator.GetFeed: {ex}", LoggerType.Error);
                    ShowBrowserError($"An unexpected error occurred while fetching the feed: {ex.Message}");
                    return;
                }
                await Dispatcher.InvokeAsync(async () =>
                {
                    DMAFeedBox.ItemsSource = DMAFeedGenerator.CurrentFeed.Posts;
                    if (DMAFeedGenerator.error)
                    {
                        DMALoadPanel.Visibility = Visibility.Collapsed;
                        DMAErrorPanel.Visibility = Visibility.Visible;
                        DMABrowserRefreshButton.Visibility = Visibility.Visible;
                        if (DMAFeedGenerator.exception.Message.Contains("JSON tokens"))
                        {
                            DMABrowserMessage.Text = "Uh oh! DivaModManager by Enomoto failed to deserialize the DivaModArchive feed.";
                            return;
                        }
                        DMABrowserMessage.Text = Regex.Match(DMAFeedGenerator.exception.Message, @"\d+").Value switch
                        {
                            "443" => "Your internet connection is down.",
                            "500" or "503" or "504" => "DivaModArchive's servers are down.",
                            _ => DMAFeedGenerator.exception.Message,
                        };
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
                        DMABrowserMessage.Text = "DivaModManager by Enomoto couldn't find any mods.";
                    }
                    DMAPageBox.ItemsSource = Enumerable.Range(1, (int)DMAFeedGenerator.CurrentFeed.TotalPages);
                    DMAPageBox.SelectedValue = DMApage;
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Unexpected error in DMARefreshFilter: {ex}", LoggerType.Error);
                ShowDMAError($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                IsEnabledControls(true);
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (DMALoadPanel.Visibility == Visibility.Visible)
                    {
                        DMALoadPanel.Visibility = Visibility.Collapsed;
                    }
                    if (DMALoadingBar.Visibility == Visibility.Visible)
                    {
                        DMALoadingBar.Visibility = Visibility.Collapsed;
                    }
                });
            }
            Logger.WriteLine($"DMARefreshFilterAsync End. CurrentManagedThreadId:{Environment.CurrentManagedThreadId}", LoggerType.Debug);
        }
        private bool DMAFilterSelect = false;
        private void DMAFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !DMAFilterSelect)
            {
                DMApage = 1;
                DMARefreshFilterAsync();
            }
        }
        private void GBSortSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                if (!searched || GBSortBox.SelectedIndex != 3)
                {
                    filterSelect = true;
                    var temp = GBSortBox.SelectedIndex;
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = temp;
                    filterSelect = false;
                }
                GBSearchBar.Clear();
                searched = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void GBPerPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void DMAPerPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                DMApage = 1;
                DMARefreshFilterAsync();
            }
        }
        private void GameFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                GBSearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    GBCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    GBCatBox.ItemsSource = None;
                GBCatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)GBCatBox.SelectedValue;
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    GBSubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    GBSubCatBox.ItemsSource = None;
                GBSubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void GBTypeFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                GBSearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    GBCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    GBCatBox.ItemsSource = None;
                GBCatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)GBCatBox.SelectedValue;
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    GBSubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    GBSubCatBox.ItemsSource = None;
                GBSubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void GBMainFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                GBSearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = 1;
                }
                // Set Categories
                var cat = (GameBananaCategory)GBCatBox.SelectedValue;
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    GBSubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    GBSubCatBox.ItemsSource = None;
                GBSubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void GBSubFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                GBSearchBar.Clear();
                searched = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = sender as UniformGrid;
            grid.Columns = (int)grid.ActualWidth / 400 + 1;
        }
        private void OnResize(object sender, RoutedEventArgs e)
        {
            GBBigScreenshot.MaxHeight = ActualHeight - 240;
        }

        private void GBPageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                page = GBPageBox.SelectedValue == null ? 1 : (int)GBPageBox.SelectedValue;
                GBRefreshFilterAsync();
            }
        }
        private void DMAPageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!DMAFilterSelect && IsLoaded)
            {
                DMApage = DMAPageBox.SelectedValue == null ? 1 : (int)DMAPageBox.SelectedValue;
                DMARefreshFilterAsync();
            }
        }
        private void GBNSFWCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                if (searched)
                {
                    filterSelect = true;
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = 1;
                    filterSelect = false;
                }
                GBSearchBar.Clear();
                searched = false;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void NSFWCheckboxDMA_Checked(object sender, RoutedEventArgs e)
        {
            if (!DMAFilterSelect && IsLoaded)
            {
                if (searched)
                {
                    DMAFilterSelect = true;
                    GBSortBox.ItemsSource = FilterBoxList;
                    GBSortBox.SelectedIndex = 1;
                    DMAFilterSelect = false;
                }
                DMASearchBar.Clear();
                searched = false;
                page = 1;
                DMARefreshFilterAsync();
            }
        }

        /// <summary>
        /// セットアップ時にExecutable、Steamの設定とMM+、DMLのファイル存在チェックを行う
        /// </summary>
        /// <returns></returns>
        // call by MainWindow_Loaded, GameBox_DropDownClosed
        private async void OnFirstOpenAsync([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            ConfigJson.SetupGame();
            while (Global.ConfigJson.IsNew || Global.ConfigJson.CurrentConfig.LauncherOptionIndex == -1)
            {
                if (!Global.IsWine)
                {
                    List<int> messageNoList = new() { 32, 33, 34 };
                    var sel = await WindowHelper.DMMWindowChoiceOpenAsync(messageNoList);
                    if (sel == -1)
                    {
                        if (await WindowHelper.DMMWindowOpenAsync(26) == WindowHelper.WindowCloseStatus.Yes)
                        {
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].LauncherOptionIndex = (int)sel;
                        LauncherOptionsBox.SelectedIndex = (int)sel;
                        break;
                    }
                    LauncherOptionsBox.SelectedIndex = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].LauncherOptionIndex;
                }
                else
                {
                    // 効いてない？？
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].LauncherOptionIndex = 1;
                    LauncherOptionsBox.SelectedIndex = 1;
                    LauncherOptionsBox.IsEnabled = false;

                    break;
                }

            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        // GameBox_DropDownClosedとGameBox_SelectionChangedイベントが重複して動作しないためのフラグ
        private bool SelectionChangedEventHandle;
        private int GameBox_BeforeSelectedIndex;
        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || Global.ConfigJson == null)
            {
                return;
            }
            if (Global.SearchModListFlg)
            {
                WindowHelper.MessageBoxOpen(41);
                GameBox.SelectionChanged -= GameBox_SelectionChanged;
                GameBox.SelectedIndex = GameBox_BeforeSelectedIndex;
                GameBox.SelectionChanged += GameBox_SelectionChanged;
                return;
            }
            ComboBox c = (ComboBox)sender;
            GameBox_BeforeSelectedIndex = c.SelectedIndex;
            Global.selected_game = c.Text;
            SelectionChangedEventHandle = true;
        }
        private int LauncherOptionsBox_BeforeSelectedIndex;
        private void LauncherOptionsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var obj = (ComboBox)sender;
            if (!SelectionChangedEventHandle)
            {
                ComboBox c = (ComboBox)sender;
                LauncherOptionsBox_BeforeSelectedIndex = c.SelectedIndex;
                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].LauncherOptionIndex = LauncherOptionsBox.SelectedIndex;
                ConfigJson.UpdateConfig();
            }
        }
        private async void LoadoutBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            if (!IsLoaded) return;

            if (LoadoutBox.SelectedItem != null)
            {
                await WorkManager.RunAsync(async () =>
                {
                    Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout = LoadoutBox.SelectedItem.ToString();

                    // Create loadout if it doesn't exist
                    if (!Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.ContainsKey(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout))
                        Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Add(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout, new());
                    else if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout] == null)
                        Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout] = new();

                    Global.ModList_All = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout];
                    await RefreshAsync();
                    await Global.ConfigUpdatesAsync();
                    Util.DataGrid_ScrollToTop(ModGrid);
                    Logger.WriteLine($"Loadout changed to {LoadoutBox.SelectedItem}", LoggerType.Info);
                });
            }
        }
        private async void EditLoadoutsButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }

            List<int> messageNoList = new() { 60, 61, 62, 63, 34 };
            var sel = await WindowHelper.DMMWindowChoiceOpenAsync(messageNoList);
            if (sel == -1) return;

            await WorkManager.RunAsync(async () =>
            {
                // UIスレッドで非同期ラムダを実行
                await Dispatcher.InvokeAsync(async () =>
                {
                    ModsWatcher.StopWatching();
                    switch (sel)
                    {
                        // Add new loadout
                        case 0:
                            var newLoadoutWindow = new EditWindow(null, false);
                            newLoadoutWindow.ShowDialog();
                            if (!string.IsNullOrEmpty(newLoadoutWindow.loadout))
                            {
                                // ModList更新後のイベントの登録(実行後に全て解除されるので注意)
                                ModGridEdifAfterAction += () => SortAlphabetically(false);
                                ModGridEdifAfterAction += () => App.Current.Dispatcher.Invoke(() => Logger.WriteLine($"Loadout Added {newLoadoutWindow.loadout}", LoggerType.Info));
                                Global.LoadoutItems.Add(newLoadoutWindow.loadout);
                                LoadoutBox.SelectedItem = newLoadoutWindow.loadout;
                            }
                            break;
                        // Rename current loadout
                        case 1:
                            var renameLoadoutWindow = new EditWindow(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout, false);
                            renameLoadoutWindow.ShowDialog();
                            if (!string.IsNullOrEmpty(renameLoadoutWindow.loadout))
                            {
                                var originalLoadout = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout;
                                var originalIndex = Global.LoadoutItems.IndexOf(originalLoadout);
                                ObservableCollection<Mod> ModList_Copy = new(Global.ModList_All.Select(m => m.Clone()));
                                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Add(renameLoadoutWindow.loadout, ModList_Copy);
                                Logger.WriteLine($"Loadout Renamed {originalLoadout} to {renameLoadoutWindow.loadout}", LoggerType.Info);
                                if (originalIndex >= 0)
                                {
                                    Global.LoadoutItems.Insert(originalIndex, renameLoadoutWindow.loadout);
                                    Global.LoadoutItems.Remove(originalLoadout);
                                }
                                else
                                {
                                    Global.LoadoutItems.Add(renameLoadoutWindow.loadout);
                                }
                                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Remove(originalLoadout);
                                LoadoutBox.SelectedItem = renameLoadoutWindow.loadout;
                            }
                            break;
                        // Delete current loadout
                        case 2:
                            if (Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Count <= 1)
                            {
                                Logger.WriteLine("Unable to delete current loadout since there is only one", LoggerType.Error);
                                MessageBox.Show("Cannot delete the only loadout.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); // ユーザー通知
                                break;
                            }
                            List<int> messageNoList2 = new() { 65, 34 };
                            List<string> replaceList = new List<string>() { Global.ConfigJson.CurrentConfig.CurrentLoadout };
                            var sel2 = await WindowHelper.DMMWindowChoiceOpenAsync(messageNoList2, replaceList: replaceList);
                            if (sel2 == -1) return;
                            if (sel2 == 0)
                            {
                                var loadoutToDelete = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout;
                                Global.LoadoutItems.Remove(loadoutToDelete);
                                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Remove(loadoutToDelete);
                                Logger.WriteLine($"Loadout Deleted {loadoutToDelete}", LoggerType.Info);
                                LoadoutBox.SelectedIndex = 0;
                            }
                            break;
                        // Copy current loadout
                        case 3:
                            var copyLoadoutWindow = new EditWindow(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout + " Copy", false);
                            copyLoadoutWindow.ShowDialog();
                            if (!string.IsNullOrEmpty(copyLoadoutWindow.loadout))
                            {
                                ObservableCollection<Mod> ModList_Copy = new(Global.ModList_All.Select(m => m.Clone()));
                                Global.LoadoutItems.Add(copyLoadoutWindow.loadout);
                                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Add(copyLoadoutWindow.loadout, ModList_Copy);
                                Logger.WriteLine($"Loadout Copied {copyLoadoutWindow.loadout}", LoggerType.Info);
                                LoadoutBox.SelectedItem = copyLoadoutWindow.loadout;
                            }
                            break;
                    }
                });
            });
        }

        // MM+以外のゲームを追加する予定がないため、何もしない
        private void GameBox_DropDownClosed(object sender, EventArgs e)
        {
        }

        private async void ModGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGridColumnHeader colHeader) return;
            if (colHeader.Content.ToString() == "Version" || colHeader.Content.ToString() == "Site") return;

            string header = colHeader.Column?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            if (Global.SearchModListFlg)
            {
                WindowHelper.MessageBoxOpen(41);
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
                case "Version":
                    // None
                    break;
                case "Site":
                    // None
                    break;
                case "Category":
                    SortByCategory();
                    break;
                case "Size":
                    SortBySize();
                    break;
                case "Note":
                    SortByNote();
                    break;
                default:
                    return;
            }

            UpdateModGridAsync(isSearch: Global.SearchModListFlg);
            await Global.ConfigUpdatesAsync(true);
        }

        private void SortByEnabled([CallerMemberName] string caller = "")
        {
            Global.ModList_All = new ObservableCollection<Mod>(Global.ModList_All.OrderByDescending(x => x.enabled));
        }

        private void SortAlphabetically(bool logView = true, [CallerMemberName] string caller = "")
        {
            Global.ModList_All = new ObservableCollection<Mod>(Global.ModList_All.OrderBy(x => x.name.ToLowerInvariant(), new NaturalSort()));
        }

        private void SortByCategory()
        {
            SortByField(m => m.category, "Category");
        }

        private void SortBySize()
        {
            SortByField(m => m.directorySize.ToString(), "Size");
        }

        private void SortByNote()
        {
            SortByField(m => m.note, "Note");
        }

        private void SortByPriority()
        {
            var list = Global.ModList_All.ToList();
            var hasMinus = list.Where(x => !string.IsNullOrEmpty(x.priority) && x.priority.StartsWith("-"));
            var noPriority = list.Where(x => string.IsNullOrEmpty(x.priority));
            var rest = list.Where(x => !string.IsNullOrEmpty(x.priority) && !x.priority.StartsWith("-"));

            Global.ModList_All = new ObservableCollection<Mod>(
                (direction == ListSortDirection.Descending
                    ? rest.OrderByDescending(x => x.priority, new NaturalSort())
                    : rest.OrderBy(x => x.priority, new NaturalSort()))
                .Concat(noPriority)
                .Concat(direction == ListSortDirection.Descending ? hasMinus.OrderByDescending(x => x.priority, new NaturalSort()) : hasMinus.OrderBy(x => x.priority, new NaturalSort()))
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        private void SortByField(Func<Mod, string> selector, string fieldName)
        {
            var list = Global.ModList_All.ToList();
            var noValue = list.Where(x => string.IsNullOrEmpty(selector(x)));
            var hasValue = list.Where(x => !string.IsNullOrEmpty(selector(x)));

            Global.ModList_All = new ObservableCollection<Mod>(
                (direction == ListSortDirection.Descending
                    ? hasValue.OrderByDescending(selector, new NaturalSort())
                    : hasValue.OrderBy(selector, new NaturalSort()))
                .Concat(noValue)
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        // call by GBSearchBar_KeyDown, GBSearchButton_Click
        private void GBSearch()
        {
            if (!filterSelect && IsLoaded)
            {
                filterSelect = true;
                GBSortBox.ItemsSource = FilterBoxListWhenSearched;
                GBSortBox.SelectedIndex = 3;
                // Set categories
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    GBCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    GBCatBox.ItemsSource = None;
                GBCatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)GBCatBox.SelectedValue;
                if (cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    GBSubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameBox.SelectedIndex][(Features.Feed.TypeFilter)GBTypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    GBSubCatBox.ItemsSource = None;
                GBSubCatBox.SelectedIndex = 0;
                filterSelect = false;
                searched = true;
                page = 1;
                GBRefreshFilterAsync();
            }
        }
        private void GBSearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                GBSearch();
        }
        private static readonly List<string> FilterBoxList = new string[] { " Featured", " Recent", " Popular" }.ToList();
        private static readonly List<string> FilterBoxListWhenSearched = new string[] { " Featured", " Recent", " Popular", " - - -" }.ToList();

        private void GBSearchButton_Click(object sender, RoutedEventArgs e)
        {
            GBSearch();
        }
        private void DMASearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DMARefreshFilterAsync();
        }

        private void DMASearchButton_Click(object sender, RoutedEventArgs e)
        {
            DMARefreshFilterAsync();
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
                //ToggleCheckBoxes();
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

        private static string GetPriorityCellValue(object source)
        {
            return source switch
            {
                TextBox textBox => textBox.Text,
                DataGridCell cell when cell.DataContext is Mod mod => mod.priority,
                _ => null
            };
        }

        private static bool IsNumericOrControlKey(Key key)
        {
            return key is >= Key.D0 and <= Key.D9 or
                   >= Key.NumPad0 and <= Key.NumPad9 or
                   Key.Enter or Key.Back or Key.Delete or
                        Key.Up or Key.Down or Key.Left or Key.Right or
                        Key.Tab or Key.F2 or Key.Escape;
        }

        private void SearchModListButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModGridAddAfterAction != default)
                ModGridAddAfterAction = default;
            ModGridAddAfterAction += () => SearchModListAsync();
            UpdateModGridAsync(isSearch: true);
        }

        private void SearchClearButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateModGridAsync();
        }

        private void SearchModListTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ModGridAddAfterAction != default)
                    ModGridAddAfterAction = default;
                ModGridAddAfterAction += () => SearchModListAsync();
                UpdateModGridAsync(isSearch: true);
            }
        }

        private async void SearchModListAsync()
        {
            if (App.Current.Dispatcher.CheckAccess())
            {
                SearchModList();
            }
            else
            {
                await App.Current.Dispatcher.BeginInvoke(() =>
                {
                    SearchModList();
                });
            }
        }
        private void SearchModList()
        {
            if (!Global.IsMainWindowLoaded || !Global.IsModGridLoaded)
            {
                return;
            }

            Global.ModList = Global.ModList_All;

            // Name or Note
            if (!string.IsNullOrEmpty(SearchModListTextBox.Text))
            {
                if (ModGridSearch.name == ModGridSearch.note)
                {
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                        x.name.ToLowerInvariant().Contains(SearchModListTextBox.Text.ToLowerInvariant())
                        || x.note.ToLowerInvariant().Contains(SearchModListTextBox.Text.ToLowerInvariant())).ToList());
                }
                else if (!string.IsNullOrEmpty(ModGridSearch.name))
                {
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                        x.name.ToLowerInvariant().Contains(SearchModListTextBox.Text.ToLowerInvariant())).ToList());
                }
                else if (!string.IsNullOrEmpty(ModGridSearch.note))
                {
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                        x.note.ToLowerInvariant().Contains(SearchModListTextBox.Text.ToLowerInvariant())).ToList());
                }
            }
            // Enabled
            if (ModGridSearch.isEnabledSearch != null)
            {
                Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                    x.enabled == ModGridSearch.enabled).ToList());
            }
            // Category
            if (ModGridSearch.isCategorySearch != null)
            {
                if (string.IsNullOrEmpty(ModGridSearch.category))
                {
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                        string.IsNullOrEmpty(x.category)).ToList());
                }
                else
                {
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.Where(x =>
                        x.category != null && x.category.ToLowerInvariant() == ModGridSearch.category.ToLowerInvariant()
                    ).ToList());
                }
            }
        }

        private void SearchModListTextBox_MouseDoubleClick(object sender, EventArgs e)
        {
            SearchModListTextBox.SelectAll();
        }

        /// <summary>
        /// Mod数に変化がない場合のみ呼んでください（ソートや並び替えなど）
        /// Mod数に変化がある場合はRefreshAsyncを呼んでください。
        /// </summary>
        /// <param name="isSearch"></param>
        private async void UpdateModGridAsync(bool isSearch = false)
        {
            if (App.Current.Dispatcher.CheckAccess())
            {
                UpdateModGrid(isSearch: isSearch);
            }
            else
            {
                await App.Current.Dispatcher.BeginInvoke(() =>
                {
                    UpdateModGrid(isSearch: isSearch);
                });
            }
        }
        private async void UpdateModGrid(bool isSearch = false)
        {
            await App.Current.Dispatcher.BeginInvoke(async () =>
            {
                Global.SearchModListFlg = isSearch;

                Global.ModListInitError();
                foreach (var mod in Global.ModList_All)
                {
                    mod.CheckErrorAndWarning();
                }

                if (!isSearch)
                {
                    ModGridSearch = new();
                    Global.ModList = Global.ModList_All;
                    SearchModListTextBox.Text = "";
                    SearchTargetComboBox.SelectedIndex = 0;
                    SearchEnabledComboBox.SelectedIndex = 0;
                    SearchCategoryComboBox.SelectedIndex = 0;
                    ModGrid.SelectedItems.Clear();
                    ModGridAddAfterAction = default;
                }
                else if (ModGridAddAfterAction != default)
                {
                    ModGridAddAfterAction();
                }

                if (Global.ConfigJson.CurrentConfig.FirstOpen) ModGrid.ItemsSource = Global.ModList;
                ModGrid.IsEnabled = true;
            });
        }

        private void ModGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is not TextBox tb) return;
            if (tb.Parent is not DataGridCell cell) return;
            if (cell.Column.Header?.ToString() != "Priority") return;

            tb.ContextMenu = null;
        }

        private void ModGrid_Loaded(object sender, RoutedEventArgs e)
        {
            DataGrid modGrid = (DataGrid)sender;
            foreach (var column in modGrid.Columns)
            {
                // 行幅変更時にGlobalの値を上書きするイベントを追加
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
                    Global.ConfigJson.PriorityColumnWidth = column.Width.DisplayValue;
                    break;
                case "Name":
                    Global.ConfigJson.NameColumnWidth = column.Width.DisplayValue;
                    break;
                case "Version":
                    Global.ConfigJson.VersionColumnWidth = column.Width.DisplayValue;
                    break;
                case "Site":
                    Global.ConfigJson.SiteColumnWidth = column.Width.DisplayValue;
                    break;
                case "Category":
                    Global.ConfigJson.CategoryColumnWidth = column.Width.DisplayValue;
                    break;
                case "Size":
                    Global.ConfigJson.SizeColumnWidth = column.Width.DisplayValue;
                    break;
                case "Note":
                    Global.ConfigJson.NoteColumnWidth = column.Width.DisplayValue;
                    break;
            }
        }

        private void ModGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.DirectlyOver is not FrameworkElement elem) return;
            if (elem.Parent is not DataGridCell cell) return;
            if (cell.Column.Header?.ToString() is not "Name" and not "Size" and not "Version" and not "Site") return;

            ExecConfigAction(sender, e);
        }

        private void ExecConfigAction(object sender, EventArgs e)
        {
            string action = Global.ConfigToml.DoubleClickEvent?.ToLowerInvariant() ?? "open";
            RoutedEventArgs _e = (RoutedEventArgs)e;

            switch (action)
            {
                case "open":
                    OpenMod_Click(sender, _e);
                    break;
                //case "rename":
                //    RenameMod_Click(sender, _e);
                //    break;
                case "configure":
                    ConfigureMod_Click(sender, _e);
                    break;
                case "homepage":
                    OpenHomePage_Click(sender, _e);
                    break;
                //case "fetch":
                //    FetchMod_Click(sender, _e);
                //    break;
                //case "update":
                //    CleanUpdateMod_Click(sender, _e);
                //    break;
                //case "delete":
                //    DeleteMod_Click(sender, _e);
                //    break;
                case "nothing":
                    break;
                default:
                    // All unknown characters are treated as Open.
                    OpenMod_Click(sender, _e);
                    break;
            }
        }
        private async void ModGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.Column is not DataGridBoundColumn boundCol) return;
            if (e.Row.DataContext is not Mod mod) return;
            if (e.EditingElement is not TextBox textBox) return;

            string newText = textBox.Text;

            // Binding Path を使う方がより堅牢
            string bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;

            // 変更があった場合のみ処理 (UI上は編集完了しているように見えるが、実際の値と比較)
            switch (bindingPath.ToLowerInvariant())
            {
                case "category":
                    if (mod.category != newText)
                    {
                        await App.Current.Dispatcher.InvokeAsync(async () => CategoryComboInit());
                    }
                    break;
                default:
                    return;
            }
        }

        private void SearchCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && combo.IsKeyboardFocusWithin)
            {
                ModGridSearch.isCategorySearch = combo.SelectedItem.ToString() switch
                {
                    "Category" => null,     // 条件なし
                    _ => true,              // "Unspecified"の場合はstring.IsNullOrEmpty(x.category)で検索
                };
                if (ModGridSearch.isCategorySearch != null)
                    ModGridSearch.category = combo.SelectedItem.ToString() == "Unspecified" ? string.Empty : combo.SelectedItem.ToString();
                if (ModGridAddAfterAction != default)
                    ModGridAddAfterAction = default;
                ModGridAddAfterAction += () => SearchModListAsync();
                UpdateModGridAsync(isSearch: true);
            }
        }

        private void CategoryComboInit(int? selected = null)
        {
            List<Mod> CategoryItems = Global.ModList_All.DistinctBy(x => x.category).OrderBy(x => x.category).ToList();
            Global.CategoryItems = new ObservableCollection<string>
            {
                "Category"
            };
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

        private void SearchEnabledComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && combo.IsKeyboardFocusWithin)
            {
                var selectedValue = combo.SelectedItem.ToString().Split(": ")[1];
                bool? setEnabled = selectedValue switch
                {
                    "True" => true,
                    "False" => false,
                    _ => null,
                };
                ModGridSearch.isEnabledSearch = setEnabled;
                if (setEnabled != null) ModGridSearch.enabled = (bool)setEnabled;
                if (ModGridAddAfterAction != default)
                    ModGridAddAfterAction = default;
                ModGridAddAfterAction += () => SearchModListAsync();
                UpdateModGridAsync(isSearch: true);
            }
        }

        private void SearchTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && combo.IsKeyboardFocusWithin)
            {
                ModGridSearch.name = string.Empty;
                ModGridSearch.note = string.Empty;
                var selectedValue = combo.SelectedItem.ToString().Split(": ")[1];
                ModGridSearch.name = selectedValue switch
                {
                    // SearchModList側でnameとnoteが同じならOR検索にする
                    "Name & Note" => SearchModListTextBox.Text,
                    "Name" => SearchModListTextBox.Text,
                    _ => null,
                };
                ModGridSearch.note = selectedValue switch
                {
                    // SearchModList側でnameとnoteが同じならOR検索にする
                    "Name & Note" => SearchModListTextBox.Text,
                    "Note" => SearchModListTextBox.Text,
                    _ => null,
                };
                if (ModGridAddAfterAction != default)
                    ModGridAddAfterAction = default;
                ModGridAddAfterAction += () => SearchModListAsync();
            }
        }

        private void InfoText_Click(object sender, RoutedEventArgs e)
        {
            List<string> replaceList = new() { InfoText.ToString() };
            var resultWindow = WindowHelper.DMMWindowOpenAsync(76, replaceList);
        }

        private void MMPFolder_Click(object sender, RoutedEventArgs e)
        {
            var launcherPath = Global.ConfigJson?.CurrentConfig?.Launcher;
            if (string.IsNullOrEmpty(launcherPath)) { return; }
            var folderName = new DirectoryInfo(launcherPath).Parent.ToString();
            if (Directory.Exists(folderName))
            {
                ProcessHelper.TryStartProcess(folderName);
            }
            else
            {
                Logger.WriteLine($"MM+ application directory not found: '{folderName}'.", LoggerType.Warning);
            }
        }

        private void DmmFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderName = Global.assemblyLocation;
            if (Directory.Exists(folderName))
            {
                ProcessHelper.TryStartProcess(folderName);
            }
            else
            {
                Logger.WriteLine($"DMM application directory not found: '{folderName}'.", LoggerType.Warning);
            }
        }

        private void SetColumnDisplayIndex()
        {
            foreach (var col in ModGrid.Columns)
            {
                string headerName = col.Header.ToString();
                switch (headerName)
                {
                    case "Enabled":
                        Global.ConfigJson.EnabledColumnIndex = col.DisplayIndex;
                        break;
                    case "Priority":
                        Global.ConfigJson.PriorityColumnIndex = col.DisplayIndex;
                        break;
                    case "Name":
                        Global.ConfigJson.NameColumnIndex = col.DisplayIndex;
                        break;
                    case "Version":
                        Global.ConfigJson.VersionColumnIndex = col.DisplayIndex;
                        break;
                    case "Category":
                        Global.ConfigJson.CategoryColumnIndex = col.DisplayIndex;
                        break;
                    case "Size":
                        Global.ConfigJson.SizeColumnIndex = col.DisplayIndex;
                        break;
                    case "Note":
                        Global.ConfigJson.NoteColumnIndex = col.DisplayIndex;
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
                        Global.ConfigJson.EnabledColumnVisible = col.Visibility;
                        break;
                    case "Priority":
                        Global.ConfigJson.PriorityColumnVisible = col.Visibility;
                        break;
                    case "Name":
                        Global.ConfigJson.NameColumnVisible = col.Visibility;
                        break;
                    case "Version":
                        Global.ConfigJson.VersionColumnVisible = col.Visibility;
                        break;
                    case "Site":
                        Global.ConfigJson.SiteColumnVisible = col.Visibility;
                        break;
                    case "Category":
                        Global.ConfigJson.CategoryColumnVisible = col.Visibility;
                        break;
                    case "Size":
                        Global.ConfigJson.SizeColumnVisible = col.Visibility;
                        break;
                    case "Note":
                        Global.ConfigJson.NoteColumnVisible = col.Visibility;
                        break;
                    default:
                        break;
                }
            }
        }

        private void VisibleColumnComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && combo.IsKeyboardFocusWithin)
            {
                var selectedValue = combo.SelectedItem.ToString().Split(": ")[1];
                if (VisibleColumnComboBox.SelectedItem is ComboBoxItem item)
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
                        case "Version":
                            col = GetDataGridColumnByName(ModGrid, "Version");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Site":
                            col = GetDataGridColumnByName(ModGrid, "Site");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Category":
                            col = GetDataGridColumnByName(ModGrid, "Category");
                            col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Hidden : col.Visibility = Visibility.Visible;
                            break;
                        case "Size":
                            col = GetDataGridColumnByName(ModGrid, "Size");
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
                combo.SelectedIndex = 0;
            }
        }

        private DataGridColumn GetDataGridColumnByName(DataGrid grid, string headerName)
        {
            if (grid == null || string.IsNullOrEmpty(headerName))
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

        private void CheckTemporaryDirectorySize()
        {
            if (Global.ConfigToml == null)
            {
                return;
            }
            var tempDirectorySizeNow = FileHelper.GetDirectoriesSize(Global.downloadBaseLocation);
            var tempDirectorySizeWarn = Global.ConfigToml.WarningTemporaryDirectorySize * 1048576; // MB
            if (tempDirectorySizeNow >= tempDirectorySizeWarn)
            {
                List<string> replaceList = new() { FileHelper.GetDirectorySizeView10(tempDirectorySizeWarn), FileHelper.GetDirectorySizeView10(tempDirectorySizeNow), ConfigTomlDmm.CONFIG_E_TOML_NAME };
                var resultWindow = WindowHelper.DMMWindowOpenAsync(23, replaceList);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isEnabled"></param>
        public void IsEnabledControls(bool isEnable, bool isEnableTabs = false, [CallerMemberName] string caller = "")
        {
            // MM+がインストールされている状態
            var isMMPInstalled = !string.IsNullOrEmpty(Global.ConfigJson.CurrentConfig.Launcher)
                && isEnable;
            // MM+とDMLがインストールされている状態
            var isDMLInstalled = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].FirstOpen
                && isMMPInstalled && isEnable;
            // セットアップが完了しない場合でもtrue
            var setEnabledFirst = isEnable;
            // Wine実行環境か
            var isWine = Util.IsWine();

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"isEnable:{isEnable}, isMMPInstalled:{isMMPInstalled}, isDMLInstalled:{isDMLInstalled}, setEnabledFirst:{setEnabledFirst}, isWine:{isWine}, caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);


            // ブラウザタブ
            ModManagerTab.IsEnabled = setEnabledFirst | isEnableTabs;
            GBModBrowserTab.Visibility = Visibility.Visible;
            GBModBrowserTab.IsEnabled = isDMLInstalled | isEnableTabs;
            DMAModBrowserTab.Visibility = Visibility.Visible;
            DMAModBrowserTab.IsEnabled = isDMLInstalled;
            SongTab.Visibility = Visibility.Visible;
            SongTab.IsEnabled = isDMLInstalled;
            ModuleTab.Visibility = Visibility.Visible;
            ModuleTab.IsEnabled = isDMLInstalled;
            OptionTab.Visibility = isWine ? Visibility.Hidden : Visibility.Visible;
            OptionTab.IsEnabled = setEnabledFirst && !isWine;
            DebugTabItem.Visibility = isWine ? Visibility.Hidden : (Logger.Mode != Features.Debug.Logger.DEBUG_MODE.NORMAL ? Visibility.Visible : Visibility.Hidden);
            DebugTabItem.IsEnabled = setEnabledFirst && !isWine;

            // 上部コントロール
            GameBox.IsEnabled = isMMPInstalled;
            LauncherOptionsBox.IsEnabled = isMMPInstalled && !isWine;
            EditLoadoutsButton.IsEnabled = isDMLInstalled;
            SetupButton.IsEnabled = setEnabledFirst && !isWine;
            LaunchButton.IsEnabled = isMMPInstalled;
            //DMLConsoleBox.IsEnabled = isDMLInstalled;
            DMLConsoleBox.IsEnabled = false;
            CoreUpdateButton.IsEnabled = setEnabledFirst && !isWine;
            LoadoutBox.IsEnabled = isDMLInstalled;

            // Modリスト上部検索/フィルタ関連
            SearchModListButton.IsEnabled = isDMLInstalled;
            SearchModListTextBox.IsEnabled = isDMLInstalled;
            SearchClearButton.IsEnabled = isDMLInstalled;
            SearchTargetComboBox.IsEnabled = isDMLInstalled;
            SearchCategoryComboBox.IsEnabled = isDMLInstalled;
            SearchEnabledComboBox.IsEnabled = isDMLInstalled;
            VisibleColumnComboBox.IsEnabled = isDMLInstalled;
            ModGridScreenShotButton.IsEnabled = isDMLInstalled;

            // Modグリッド
            ModGrid.IsEnabled = isDMLInstalled;

            GBSearchBar.IsEnabled = isDMLInstalled;
            GBSearchButton.IsEnabled = isDMLInstalled;
            GBSortBox.IsEnabled = isDMLInstalled;
            GBTypeBox.IsEnabled = isDMLInstalled;
            GBCatBox.IsEnabled = isDMLInstalled;
            GBSubCatBox.IsEnabled = isDMLInstalled;
            GBPageLeft.IsEnabled = isDMLInstalled && page > 1;
            GBPageRight.IsEnabled = FeedGenerator.CurrentFeed == null ? false : isDMLInstalled && page < FeedGenerator.CurrentFeed.TotalPages;
            GBPageBox.IsEnabled = isDMLInstalled;
            GBPerPageBox.IsEnabled = isDMLInstalled;
            GBClearCacheButton.IsEnabled = isDMLInstalled;
            GBNSFWCheckbox.IsEnabled = isDMLInstalled;

            DMASearchBar.IsEnabled = isDMLInstalled;
            DMASearchButton.IsEnabled = isDMLInstalled;
            DMASortBox.IsEnabled = isDMLInstalled;
            DMAFilterBox.IsEnabled = isDMLInstalled;
            DMAClearCacheButton.IsEnabled = isDMLInstalled;
            DMAPageLeft.IsEnabled = isDMLInstalled && DMApage > 1;
            DMAPageRight.IsEnabled = DMAFeedGenerator.CurrentFeed == null ? false : isDMLInstalled && DMApage < DMAFeedGenerator.CurrentFeed.TotalPages;
            DMAPageBox.IsEnabled = isDMLInstalled;
            DMAPerPageBox.IsEnabled = isDMLInstalled;

            // Optionタブ
            OneClickInstallButton.IsEnabled = setEnabledFirst;

            //DmmFolderButton.IsEnabled = setEnabledFirst;
            //GitHubButton.IsEnabled = setEnabledFirst;
            //DiscordButton.IsEnabled = setEnabledFirst;

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        #region FlowDocument 変換ロジック共通化

        /// <summary>
        /// 指定されたテキストを FlowDocument の Paragraph に変換します。テキスト内のURLはハイパーリンクになります。
        /// </summary>
        /// <param name="text">変換するテキスト。</param>
        /// <returns>変換された Paragraph。</returns>
        private Paragraph ConvertToFlowParagraph(string text)
        {
            var paragraph = new Paragraph();
            if (string.IsNullOrEmpty(text)) return paragraph;
            AddHyperlinksToParagraph(paragraph, text);

            return paragraph;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text">変換するテキスト</param>
        /// <returns></returns>
        private void ViewErrorAndWarningDescriptionWindow(Mod mod)
        {
            if (mod == null) return;
            if (mod.Errors == null) return;
            if (mod.Errors.Count == 0) return;

            //App.Current.Dispatcher.BeginInvoke(() =>
            App.Current.Dispatcher.Invoke(() =>
            {
                if (mod.IsError)
                {
                    RichTextBoxExtensions.AppendTextFirst(DescriptionWindow, mod.GetErrorString(), "#FFFF8A8A");
                }
                else if (mod.IsWarn)
                {
                    RichTextBoxExtensions.AppendTextFirst(DescriptionWindow, mod.GetWarningString(), "#FFD700");
                }
            });
        }

        /// <summary>
        /// 指定された Paragraph に、テキスト内のURLをハイパーリンクとして追加します。
        /// </summary>
        /// <param name="paragraph">インライン要素を追加する Paragraph。</param>
        /// <param name="text">解析するテキスト。</param>
        private void AddHyperlinksToParagraph(Paragraph paragraph, string text)
        {
            // URLを検出する正規表現 (より多くの形式に対応させることも可能)
            var regex = new Regex(@"(https?://[^\s""'<>()]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var lastIndex = 0;

            foreach (Match match in regex.Matches(text))
            {
                // URLの前のテキスト部分を追加
                if (match.Index > lastIndex)
                {
                    paragraph.Inlines.Add(new Run(text[lastIndex..match.Index]));
                }

                // ハイパーリンク部分を追加
                string url = match.Value;
                try
                {
                    var hyperlink = new Hyperlink(new Run(url))
                    {
                        NavigateUri = new Uri(url),
                        //ToolTip = $"Open link: {url}"
                    };
                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        ProcessHelper.TryStartProcess(args.Uri.AbsoluteUri);
                        args.Handled = true;
                    };
                    paragraph.Inlines.Add(hyperlink);
                }
                catch (UriFormatException ex)
                {
                    // URLとして認識されたがURIとして不正な場合は、通常のテキストとして追加
                    Logger.WriteLine($"Invalid URI format detected in text: '{url}'. Treating as plain text. Error: {ex.Message}", LoggerType.Debug);
                    paragraph.Inlines.Add(new Run(url));
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error creating hyperlink for '{url}': {ex.Message}. Treating as plain text.", LoggerType.Warning);
                    paragraph.Inlines.Add(new Run(url));
                }

                lastIndex = match.Index + match.Length;
            }

            // 最後のURLの後のテキスト部分を追加
            if (lastIndex < text.Length)
            {
                paragraph.Inlines.Add(new Run(text[lastIndex..]));
            }
        }

        // ConvertToFlowDocument メソッドは ConvertToFlowParagraph を使うように修正するか、
        // 共通のヘルパー AddHyperlinksToParagraph を使うようにリファクタリング可能
        private FlowDocument ConvertToFlowDocument(string text)
        {
            var flowDocument = new FlowDocument();
            if (string.IsNullOrEmpty(text)) return flowDocument;

            var paragraph = new Paragraph();
            AddHyperlinksToParagraph(paragraph, text);
            flowDocument.Blocks.Add(paragraph);

            return flowDocument;
        }

        #endregion

        private async void ModGridScreenShotButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            if (Global.ModList_All.Count == 0)
            {
                await WindowHelper.DMMWindowOpenAsync(40);
                return;
            }

            if (Global.ConfigToml.ConflictModWhenCreateScreenShot)
            {
                var msgResult = await WindowHelper.DMMWindowOpenAsync(69);
                if (msgResult == WindowHelper.WindowCloseStatus.YesCheck)
                {
                    Global.ConfigToml.ConflictModWhenCreateScreenShot = false;
                    Global.ConfigToml.Update();
                }
            }

            await WorkManager.RunAsync(async () =>
            {
                Logger.WriteLine($"GBScreenshot making...", LoggerType.Info);
                Directory.CreateDirectory(Global.screenshotBaseLocation);
                string filePathNoExtention = Path.Combine(Global.screenshotBaseLocation, $"ModGrid_{DateTime.Now:yyyyMMdd_HHmmss}");

                await ModGrid.CaptureFullDataGridAsync(
                    filePathNoExtention,
                    dpi: Global.ConfigToml.ScreenShotDPI,
                    maxLinesPerPage: Global.ConfigToml.ScreenShotMaxLine,
                    maxHeightPx: Global.ConfigToml.ScreenShotMaxPixel
                );

                Logger.WriteLine($"GBScreenshot making Complete! Path: \"{Path.GetDirectoryName(filePathNoExtention)}\"", LoggerType.Info);
                ProcessHelper.TryStartProcess(Path.GetDirectoryName(filePathNoExtention));
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void DebugTab_TabSelected(object sender, RoutedEventArgs e)
        {
            //if (Global.IsWine)
            //{
            //    App.Current.Dispatcher.Invoke(() =>
            //    {
            //        WindowHelper.DMMWindowOpen(27);
            //    });
            //    return;
            //}

            var tabName = DebugTabItem.Header;
            if (Logger.Mode == Logger.DEBUG_MODE.DEBUG)
            {
                List<string> replaceList = new() { DebugTabItem.Header.ToString() };
                var resultWindow = WindowHelper.DMMWindowOpenAsync(21, replaceList);
            }
        }

        private void SongTab_TabSelected(object sender, RoutedEventArgs e)
        {
            var tabItem = sender as TabItem;
            var msg = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(82));
            if (msg == WindowHelper.WindowCloseStatus.Yes)
            {
                SongLogic.Init((SongTab)tabItem.Content);
            }
            else
            {
                SongLogic.Clear((SongTab)tabItem.Content);
                tabItem.IsSelected = false;
                e.Handled = true;
            }
        }

        private void ModuleTab_TabSelected(object sender, RoutedEventArgs e)
        {
            var tabItem = sender as TabItem;
            var msg = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(83));
            if (msg == WindowHelper.WindowCloseStatus.Yes)
            {
                ModuleLogic.Init((ModuleTab)tabItem.Content);
            }
            else
            {
                ModuleLogic.Clear((ModuleTab)tabItem.Content);
                tabItem.IsSelected = false;
                e.Handled = true;
            }
        }

        // タブを離れた時にメモリを解放する
        private void SongTab_TabUnselected(object sender, RoutedEventArgs e)
        {
            var tabItem = sender as TabItem;
            var songTab = (SongTab)tabItem.Content;
            SongLogic.Clear(songTab);
            Global.GameBase.songData.Clear();
            foreach (var mod in Global.ModList)
            {
                mod.songData.Clear();
            }
        }

        // タブを離れた時にメモリを解放する
        private void ModuleTab_TabUnselected(object sender, RoutedEventArgs e)
        {
            var tabItem = sender as TabItem;
            var moduleTab = (ModuleTab)tabItem.Content;
            ModuleLogic.Clear(moduleTab);
            Global.GameBase.moduleData.Clear();
            foreach (var mod in Global.ModList)
            {
                mod.moduleData.Clear();
            }
        }

        private void OptionTab_TabSelected(object sender, RoutedEventArgs e)
        {
            //if (Global.IsWine)
            //{
            //    App.Current.Dispatcher.Invoke(() =>
            //    {
            //        WindowHelper.DMMWindowOpen(27);
            //    });
            //    return;
            //}
        }

        private void OneClickInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }
            RegistryConfig.CheckGBHandler();
        }

        private void OpenHomePage_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems.OfType<Mod>().ToList().Where(x => !string.IsNullOrEmpty(x.metadataManager?.metadata?.homepage?.ToString()));
            // 設定ファイルいらないと思ったのでハードコーディング(開くタブが多い場合はメッセージ)
            if (selectedMods.Count() >= 5)
            {
                List<string> replaceList = new() { selectedMods.Count().ToString() };
                var ret = WindowHelper.DMMWindowOpen(55, replaceList);
                if (ret == WindowHelper.WindowCloseStatus.No || ret == WindowHelper.WindowCloseStatus.Cancel)
                {
                    return;
                }
            }
            foreach (var row in selectedMods)
            {
                ProcessHelper.TryStartProcess(row.metadataManager.metadata.homepage.ToString());
            }
        }

        /// <summary>
        /// DMM、DML設定ファイルのバックアップ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsBackUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                return;
            }

            var retDMLbackupToml = string.Empty;
            var retDMMbackupJson = string.Empty;
            var retDMMbackupToml = string.Empty;
            var retWindow = WindowHelper.DMMWindowOpen(68);
            if (retWindow == WindowHelper.WindowCloseStatus.Yes)
            {
                List<string> replaceList = new();

                // DML(config.toml)
                retDMLbackupToml = FileHelper.CopyFile(ModLoader.CONFIG_TOML_PATH);
                if (string.IsNullOrEmpty(retDMLbackupToml))
                {
                    replaceList.Add(ModLoader.CONFIG_TOML_PATH);
                    Logger.WriteLine($"Failed to back up the configuration file.\nFailed file name:{ModLoader.CONFIG_TOML_PATH}", LoggerType.Error);
                }
                // DMM(Config.json)
                retDMMbackupJson = FileHelper.CopyFile(ConfigJson.CONFIG_JSON_PATH);
                if (string.IsNullOrEmpty(retDMMbackupJson))
                {
                    replaceList.Add(ConfigJson.CONFIG_JSON_NAME);
                    Logger.WriteLine($"Failed to back up the configuration file.\nFailed file name:{ConfigJson.CONFIG_JSON_PATH}", LoggerType.Error);
                }
                // DMM(config_e.toml)
                retDMMbackupToml = FileHelper.CopyFile(ConfigTomlDmm.CONFIG_E_TOML_PATH);
                if (string.IsNullOrEmpty(retDMMbackupToml))
                {
                    replaceList.Add(ConfigTomlDmm.CONFIG_E_TOML_NAME);
                    Logger.WriteLine($"Failed to back up the configuration file.\nFailed file name:{ConfigTomlDmm.CONFIG_E_TOML_PATH}", LoggerType.Error);
                }

                if (replaceList.Count == 0)
                {
                    WindowHelper.DMMWindowOpen(52);
                }
                else
                {
                    WindowHelper.DMMWindowOpen(53, replaceList);
                }
            }
        }

        private void LoadoutBox_DropDownOpened(object sender, EventArgs e)
        {
            if (WorkManager.IsBusy || App.IsAlreadyRunningOtherProcess(false) != 0)
            {
                WindowHelper.DMMWindowOpen(66);
                LoadoutBox_DropDownClosed(sender, e);
            }
        }

        private void LoadoutBox_DropDownClosed(object sender, EventArgs e)
        {
            // LoadoutBox_DropDownOpenedから呼び出されるため実装
        }
    }
}
