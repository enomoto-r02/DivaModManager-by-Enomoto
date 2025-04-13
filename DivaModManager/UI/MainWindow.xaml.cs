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

            if (Global.config.PriorityColumnWidth != null)
                ModGrid.Columns[1].Width = (double)Global.config.PriorityColumnWidth;
            if (Global.config.NameColumnWidth != null)
                ModGrid.Columns[2].Width = (double)Global.config.NameColumnWidth;
            if (Global.config.CategoryColumnWidth != null)
                ModGrid.Columns[3].Width = (double)Global.config.CategoryColumnWidth;
            if (Global.config.NoteColumnWidth != null)
                ModGrid.Columns[4].Width = (double)Global.config.NoteColumnWidth;

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

            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                if (Global.config.Configs[Global.config.CurrentGame].FirstOpen)
                    Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
            }
            else
            {
                // Watch mods folder to detect
                ModsWatcher = new FileSystemWatcher(Global.config.Configs[Global.config.CurrentGame].ModsFolder);
                ModsWatcher.Created += OnModified;
                ModsWatcher.Deleted += OnModified;
                ModsWatcher.Renamed += OnModified;
                Refresh();
                ModsWatcher.EnableRaisingEvents = true;
            }

            // Category ComboBox Init
            List<Mod> CategoryItems = Global.ModList.DistinctBy(x => x.cat).OrderBy(x => x.cat).ToList();
            Global.CategoryItems = new ObservableCollection<string>();
            Global.CategoryItems.Add("ALL");
            foreach (var CategoryItem in CategoryItems)
            {
                if (!string.IsNullOrEmpty(CategoryItem.cat))
                {
                    Global.CategoryItems.Add(CategoryItem.cat);
                }
            }
            Global.CategoryItems.Add("None");
            SearchCategoryComboBox.ItemsSource = Global.CategoryItems;
            SearchCategoryComboBox.SelectedIndex = 0;

            defaultFlow.Blocks.Add(ConvertToFlowParagraph(defaultText));
            DescriptionWindow.Document = defaultFlow;
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/preview_enomoto.png"));
            ImageBehavior.SetAnimatedSource(Preview, bitmap);
            ImageBehavior.SetAnimatedSource(PreviewBG, null);

            GameBox.IsEnabled = false;
            ModGrid.IsEnabled = false;
            ConfigButton.IsEnabled = false;
            LaunchButton.IsEnabled = false;
            OpenModsButton.IsEnabled = false;
            UpdateCheckAllButton.IsEnabled = false;
            LauncherOptionsBox.IsEnabled = false;
            LoadoutBox.IsEnabled = false;
            EditLoadoutsButton.IsEnabled = false;
            SearchModListButton.IsEnabled = false;
            SearchModListTextBox.IsEnabled = false;
            App.Current.Dispatcher.Invoke(async () =>
            {
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
            });
        }
        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => OnFirstOpen());

            LauncherOptionsBox.IsEnabled = true;
            LauncherOptionsBox.ItemsSource = LauncherOptions;
            LauncherOptionsBox.SelectedIndex = Global.config.Configs[Global.config.CurrentGame].LauncherOptionIndex;
        }
        private void OnModified(object sender, FileSystemEventArgs e)
        {
            Refresh();
            // Bring window to front after download is done
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                Activate();
            });
        }

        private async void Refresh()
        {
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                if (Global.config.Configs[Global.config.CurrentGame].FirstOpen)
                    Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }
            var currentModDirectory = Global.config.Configs[Global.config.CurrentGame].ModsFolder;

            foreach (var mod in Directory.GetDirectories(currentModDirectory))
            {
                var configPath = $"{mod}{Global.s}config.toml";

                bool executeFlg = false;
                // Add new folders found in Mods to the ModList
                if (!Global.ModList.ToList().Where(x => x.name == Path.GetFileName(mod)).Any())
                {
                    Mod m = new Mod();
                    m.name = Path.GetFileName(mod);
                    if (File.Exists(configPath))
                    {
                        executeFlg = true;
                        var configString = String.Empty;
                        while (String.IsNullOrEmpty(configString))
                        {
                            configString = File.ReadAllText(configPath);
                            try
                            {
                                if (string.IsNullOrEmpty(configString))
                                {
                                    string message = $"Config.toml's content is empty! Path : {configPath}";
                                    throw new Exception(message);
                                }
                            }
                            catch (Exception e)
                            {
                                // Check if the exception is related to an IO error.
                                if (e.GetType() != typeof(IOException))
                                {
                                    Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                    MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                }
                                else
                                {
                                    Global.logger.WriteLine($"Other exception {configPath} ({e.Message})", LoggerType.Error);
                                    MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                }
                            }
                        }
                        if (Toml.TryToModel(configString, out TomlTable config, out var diagnostics))
                        {
                            if (config.ContainsKey("enabled"))
                                m.enabled = (bool)config["enabled"];
                            else
                            {
                                // Add enabled field to be true if it doesn't exist
                                m.enabled = true;
                                config.Add("enabled", true);
                                AddInclude(config);
                                var isReady = false;
                                while (!isReady)
                                {
                                    try
                                    {
                                        File.WriteAllText(configPath, Toml.FromModel(config));
                                        isReady = true;
                                    }
                                    catch (Exception e)
                                    {
                                        // Check if the exception is related to an IO error.
                                        if (e.GetType() != typeof(IOException))
                                        {
                                            Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                            break;
                                        }
                                        else
                                        {
                                            Global.logger.WriteLine($"Other exception {configPath} ({e.Message})", LoggerType.Error);
                                            MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Global.logger.WriteLine($"{diagnostics[0].Message} for {m.name}. Rewriting {configPath} with only enabled field", LoggerType.Warning);
                            // Create config.toml with enabled field to be true if failed to parse
                            m.enabled = true;
                            config = new();
                            config.Add("enabled", true);
                            AddInclude(config);
                            var isReady = false;
                            while (!isReady)
                            {
                                try
                                {
                                    File.WriteAllText(configPath, Toml.FromModel(config));
                                    isReady = true;
                                }
                                catch (Exception e)
                                {
                                    // Check if the exception is related to an IO error.
                                    if (e.GetType() != typeof(IOException))
                                    {
                                        Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                        break;
                                    }
                                    else
                                    {
                                        Global.logger.WriteLine($"Other exception {configPath} ({e.Message})", LoggerType.Error);
                                        MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Create config.toml with enabled field to be true and include set, if the user desires
                        if (!IsWindowOpen<ChoiceWindow>())
                        {
                            ConfirmConfigCreation(configPath, m, true);
                        }
                        else
                        {
                            Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                        }
                    }
                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        if (Global.config.AddModToTop)
                        {
                            Global.ModList.Insert(0, m);
                        }
                        else
                        {
                            Global.ModList.Add(m);
                        }
                    });
                    Global.logger.WriteLine($"Added {Path.GetFileName(mod)}", LoggerType.Info);
                }
                // Check if enabled field is changed in existing mods (different loadouts or copy loadouts)
                else
                {
                    executeFlg = true;
                    var index = Global.ModList.ToList().FindIndex(x => x.name == Path.GetFileName(mod));
                    TomlTable config;
                    if (File.Exists(configPath))
                    {
                        var configString = String.Empty;
                        try
                        {
                            configString = File.ReadAllText(configPath);
                            if (String.IsNullOrEmpty(configString))
                            {
                                throw new Exception($"config.toml is Empty!\nPath : {configPath}");
                            }
                        }
                        catch (Exception e)
                        {
                            // Check if the exception is related to an IO error.
                            if (e.GetType() != typeof(IOException))
                            {
                                Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                //break;
                                continue;
                            }
                            else
                            {
                                Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                //break;
                                continue;
                            }
                        }
                        if (!Toml.TryToModel(configString, out config, out var diagnostics))
                        {
                            Global.logger.WriteLine($"{diagnostics[0].Message} for {Global.ModList[index].name}. Rewriting {configPath} with only enabled field", LoggerType.Warning);
                            config = new();
                        }
                        else
                        {
                            // Overwrite the file to have the value of enable.
                            Mod m = new Mod();
                            m.name = Path.GetFileName(mod);
                            var mod_list_m = Global.ModList.ToList().Where(x => x.name == m.name);
                            if (mod_list_m != null && mod_list_m.Count() == 1)
                            {
                                m.enabled = mod_list_m.ToList()[0].enabled;
                                try
                                {
                                    if ((bool)config["enabled"] != m.enabled)
                                    {
                                        config["enabled"] = m.enabled;
                                        try
                                        {
                                            File.WriteAllText(configPath, Toml.FromModel(config));
                                        }
                                        catch (Exception e)
                                        {
                                            // Check if the exception is related to an IO error.
                                            if (e.GetType() != typeof(IOException))
                                            {
                                                Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                                //break;
                                                continue;
                                            }
                                            else
                                            {
                                                Global.logger.WriteLine($"Other exception {configPath} ({e.Message})", LoggerType.Error);
                                                MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                                //break;
                                                continue;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Global.logger.WriteLine($"Other exception {m.name}\"\nThe value of config[enable] could not be read.", LoggerType.Error);
                                    MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                    continue;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (!IsWindowOpen<ChoiceWindow>())
                        {
                            Mod m = new Mod();
                            m.name = Path.GetFileName(mod);
                            ConfirmConfigCreation(configPath, m, true);
                        }
                        else
                        {
                            Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                        }
                    }

                    // Loading Priority and Note
                    var configPath_e = $"{mod}{Global.s}config_e.toml";
                    var mod_g_list = Global.ModList.ToList().Where(x => x.name == Path.GetFileName(mod));
                    foreach (var mod_g in mod_g_list)
                    {
                        if (File.Exists(configPath_e))
                        {
                            var configString_e = String.Empty;
                            while (String.IsNullOrEmpty(configString_e))
                            {
                                configString_e = File.ReadAllText(configPath_e);
                                try
                                {
                                    if (string.IsNullOrEmpty(configPath_e))
                                    {
                                        string message = $"Config_e.toml's content is empty! Path : {configPath_e}";
                                        throw new Exception(message);
                                    }
                                }
                                catch (Exception e)
                                {
                                    // Check if the exception is related to an IO error.
                                    if (e.GetType() != typeof(IOException))
                                    {
                                        Global.logger.WriteLine($"Couldn't access {configPath_e} ({e.Message})", LoggerType.Error);
                                        MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                        break;
                                    }
                                    else
                                    {
                                        Global.logger.WriteLine($"Other exception {configPath_e} ({e.Message})", LoggerType.Error);
                                        MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                        break;
                                    }
                                }
                            }
                            if (Toml.TryToModel(configString_e, out TomlTable config_e, out var diagnostics))
                            {
                                if (config_e.ContainsKey("priority"))
                                {
                                    mod_g.priority = config_e["priority"].ToString();
                                }
                                if (config_e.ContainsKey("note"))
                                {
                                    mod_g.note = config_e["note"].ToString();
                                }
                            }
                            else
                            {
                                // Add enabled field to be true if it doesn't exist
                                mod_g.priority = "";
                                mod_g.note = "";
                                config_e.Add("priority", true);
                                config_e.Add("note", true);
                                var isReady = false;
                                while (!isReady)
                                {
                                    try
                                    {
                                        File.WriteAllText(configPath_e, Toml.FromModel(config_e));
                                        isReady = true;
                                    }
                                    catch (Exception e)
                                    {
                                        // Check if the exception is related to an IO error.
                                        if (e.GetType() != typeof(IOException))
                                        {
                                            Global.logger.WriteLine($"Couldn't access {configPath_e} ({e.Message})", LoggerType.Error);
                                            break;
                                        }
                                        else
                                        {
                                            Global.logger.WriteLine($"Other exception {configPath_e} ({e.Message})", LoggerType.Error);
                                            MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if(executeFlg)
                {
                    // Loading Categor
                    var modPath = $"{mod}{Global.s}mod.json";
                    var mod_g_list = Global.ModList.ToList().Where(x => x.name == Path.GetFileName(mod));
                    foreach (var mod_g in mod_g_list)
                    {
                        if (File.Exists(modPath))
                        {
                            var modJsonString = String.Empty;
                            while (String.IsNullOrEmpty(modJsonString))
                            {
                                modJsonString = File.ReadAllText(modPath);
                                try
                                {
                                    if (string.IsNullOrEmpty(modPath))
                                    {
                                        string message = $"mod.json's content is empty! Path : {modPath}";
                                        throw new Exception(message);
                                    }
                                }
                                catch (Exception e)
                                {
                                    // Check if the exception is related to an IO error.
                                    if (e.GetType() != typeof(IOException))
                                    {
                                        Global.logger.WriteLine($"Couldn't access {modPath} ({e.Message})", LoggerType.Error);
                                        MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                        break;
                                    }
                                    else
                                    {
                                        Global.logger.WriteLine($"Other exception {modPath} ({e.Message})", LoggerType.Error);
                                        MessageBox.Show(e.Message, "Attention.", MessageBoxButton.OK, MessageBoxImage.Error);
                                        break;
                                    }
                                }
                                Metadata metadata = JsonSerializer.Deserialize<Metadata>(modJsonString);
                                mod_g.cat = metadata.cat;
                            }
                        }
                    }
                    executeFlg = false;
                }
            }
            // Remove deleted folders that are still in the ModList
            foreach (var mod in Global.ModList.ToList())
            {
                if (!Directory.GetDirectories(currentModDirectory).ToList().Select(x => Path.GetFileName(x)).Contains(mod.name))
                {
                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        Global.ModList.Remove(mod);
                    });
                    Global.logger.WriteLine($"Deleted {mod.name}", LoggerType.Info);
                    continue;
                }
            }

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ModGrid.ItemsSource = Global.ModList;
                    ModGrid.Items.Refresh();
                    var stats = $"{Global.ModList.ToList().Where(x => x.enabled).ToList().Count}/{Global.ModList.Count} mods • {Directory.GetFiles(currentModDirectory, "*", SearchOption.AllDirectories).Length.ToString("N0")} files • " +
                    $"{StringConverters.FormatSize(new DirectoryInfo(currentModDirectory).GetDirectorySize())}";
                    if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion))
                        stats += $" • DML v{Global.config.Configs[Global.config.CurrentGame].ModLoaderVersion}";
                    stats += $" • DMM v{version}";
                    Stats.Text = stats;
                });
            });
            Global.UpdateConfig();
            await Task.Run(() => ModLoader.Build());
            Global.logger.WriteLine("Refreshed!", LoggerType.Info);
        }

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
                                UpdateModConfigToml(checkMod, setEnabled);
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
                // Create config.toml with enabled field to be true and include set, if the user desires
                if (!IsWindowOpen<ChoiceWindow>())
                {
                    ConfirmConfigCreation(configPath, m, false);
                }
                else
                {
                    Global.logger.WriteLine("No config.toml file window triggered but it was already open.", LoggerType.Info);
                }
            }
        }

        private void UpdateModConfigToml_E(Mod m, string column, object value)
        {
            var configPath_e = $"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{m.name}{Global.s}config_e.toml";

            if (!File.Exists(configPath_e))
            {
                TomlTable config_dmme = new();
                config_dmme.Add("priority", "");
                config_dmme.Add("note", "");
                var isReady = false;
                while (!isReady)
                {
                    try
                    {
                        File.WriteAllText(configPath_e, Toml.FromModel(config_dmme));
                        isReady = true;
                    }
                    catch (Exception e)
                    {
                        // Check if the exception is related to an IO error.
                        if (e.GetType() != typeof(IOException))
                        {
                            Global.logger.WriteLine($"Couldn't access {configPath_e} ({e.Message})", LoggerType.Error);
                            break;
                        }
                    }
                }
            }
            if (File.Exists(configPath_e))
            {
                var configString_e = File.ReadAllText(configPath_e);
                if (Toml.TryToModel(configString_e, out TomlTable config_e, out var diagnostics))
                {
                    switch (column)
                    {
                        case "Priority":
                            config_e["priority"] = value;
                            break;
                        case "Note":
                            config_e["note"] = value;
                            break;
                    }
                    File.WriteAllText(configPath_e, Toml.FromModel(config_e));
                }
                else
                {
                    Global.logger.WriteLine($"{diagnostics[0].Message} for {m.name}. Rewriting {configPath_e} with only enabled field", LoggerType.Warning);
                    // Create config.toml with enabled field to be true if failed to parse
                    config_e = new();
                    config_e.Add("priority", "");
                    config_e.Add("note", "");
                    File.WriteAllText(configPath_e, Toml.FromModel(config_e));
                }
            }
            else
            {
                Global.logger.WriteLine("No config_e.toml file window triggered but it was already open.", LoggerType.Info);
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

        private void ConfirmConfigCreation(string configPath, Mod m, bool enabled)
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
            Dispatcher.Invoke(() =>
            {
                var choice = new ChoiceWindow(choices, $"No config.toml file found, create one?");
                choice.ShowDialog();
                switch (choice.choice)
                {
                    case 0:
                        m.enabled = true;
                        TomlTable config = new();
                        config.Add("enabled", enabled);
                        AddInclude(config);
                        var isReady = false;
                        while (!isReady)
                        {
                            try
                            {
                                File.WriteAllText(configPath, Toml.FromModel(config));
                                isReady = true;
                            }
                            catch (Exception e)
                            {
                                // Check if the exception is related to an IO error.
                                if (e.GetType() != typeof(IOException))
                                {
                                    Global.logger.WriteLine($"Couldn't access {configPath} ({e.Message})", LoggerType.Error);
                                    break;
                                }
                            }
                        }
                        break;
                    case 1:
                        Global.logger.WriteLine($"User chose to not create a config.toml file for the aforementioned mod.", LoggerType.Info);
                        break;
                    default:
                        break;
                }
            });
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
                    Dispatcher.Invoke(() =>
                    {
                        // Watch mods folder to detect
                        ModsWatcher = new FileSystemWatcher(Global.config.Configs[Global.config.CurrentGame].ModsFolder);
                        ModsWatcher.Created += OnModified;
                        ModsWatcher.Deleted += OnModified;
                        ModsWatcher.Renamed += OnModified;
                        Refresh();
                        ModsWatcher.EnableRaisingEvents = true;
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
                        var contextMenu = element.ContextMenu.Items[i] as MenuItem;
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
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
            Global.UpdateConfig();
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
        private async void RenameMod_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);

            // Stop refreshing while renaming folders
            ModsWatcher.EnableRaisingEvents = false;
            foreach (var row in temp)
            {
                if (row != null)
                {
                    EditWindow ew = new EditWindow(row.name, true);
                    ew.ShowDialog();
                }
            }
            ModsWatcher.EnableRaisingEvents = true;
            Global.UpdateConfig();
            ModGrid.Items.Refresh();

            await Task.Run(() => ModLoader.Build());
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
                        File.Delete(_ArchiveSource);
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
            GameBox.IsEnabled = false;
            ModGrid.IsEnabled = false;
            ConfigButton.IsEnabled = false;
            LaunchButton.IsEnabled = false;
            OpenModsButton.IsEnabled = false;
            UpdateCheckAllButton.IsEnabled = false;
            LauncherOptionsBox.IsEnabled = false;
            LoadoutBox.IsEnabled = false;
            EditLoadoutsButton.IsEnabled = false;
            SearchModListButton.IsEnabled = false;
            SearchModListTextBox.IsEnabled = false;
            App.Current.Dispatcher.Invoke(async () =>
            {
                Global.logger.WriteLine("Checking for mod updates...", LoggerType.Info);
                await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this, isSelectedUpdate);
                Global.logger.WriteLine("Checking for Diva Mod Manager update...", LoggerType.Info);
                if (await AutoUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                    Close();
                Global.logger.WriteLine("Checking for DivaModLoader update...", LoggerType.Info);
                await Setup.CheckForDMLUpdate(new CancellationTokenSource());
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
                        ModsWatcher.Created += OnModified;
                        ModsWatcher.Deleted += OnModified;
                        ModsWatcher.Renamed += OnModified;
                        Refresh();
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
            if (!IsLoaded)
            {
                return;
            }
            // Change the loadout
            else if (LoadoutBox.SelectedItem != null)
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
                Refresh();
                Global.logger.WriteLine($"Loadout changed to {LoadoutBox.SelectedItem}", LoggerType.Info);
                await Task.Run(() => ModLoader.Build());
            }
        }
        private void EditLoadouts_Click(object sender, RoutedEventArgs e)
        {
            if (Global.SearchModListFlg)
            {
                MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
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
            Dispatcher.Invoke(() =>
            {
                var choice = new ChoiceWindow(choices, $"Loadout Options for {Global.config.CurrentGame}");
                choice.ShowDialog();
                if (choice.choice != null)
                {
                    switch ((int)choice.choice)
                    {
                        // Add new loadout
                        case 0:
                            var newLoadoutWindow = new EditWindow(null, false);
                            newLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(newLoadoutWindow.loadout))
                            {
                                Global.LoadoutItems.Add(newLoadoutWindow.loadout);
                                LoadoutBox.SelectedItem = newLoadoutWindow.loadout;
                            }
                            break;
                        // Rename current loadout
                        case 1:
                            var renameLoadoutWindow = new EditWindow(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout, false);
                            renameLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(renameLoadoutWindow.loadout))
                            {
                                // Insert new name at index of original loadout
                                Global.LoadoutItems.Insert(Global.LoadoutItems.IndexOf(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout), renameLoadoutWindow.loadout);
                                // Copy over current loadout
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(renameLoadoutWindow.loadout, Global.ModList);
                                // Delete current loadout
                                Global.LoadoutItems.Remove(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout);
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Remove(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout);
                                // Trigger selection changed event
                                LoadoutBox.SelectedItem = renameLoadoutWindow.loadout;
                            }
                            break;
                        // Delete current loadout
                        case 2:
                            var yesno_choice = new List<Choice>();
                            yesno_choice.Add(new Choice()
                            {
                                OptionText = "Yes",
                                OptionSubText = $"This action cannot be undone.",
                                Index = 0
                            });
                            var yesno = new ChoiceWindow(yesno_choice, $"Delete Current Loadout");
                            yesno.ShowDialog();

                            if (yesno.choice != null)
                            {
                                switch ((int)yesno.choice)
                                {
                                    case 0:
                                        if (Global.config.Configs[Global.config.CurrentGame].Loadouts.Count == 1)
                                        {
                                            Global.logger.WriteLine("Unable to delete current loadout since there is only one", LoggerType.Error);
                                            return;
                                        }
                                        else
                                        {
                                            Global.LoadoutItems.Remove(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout);
                                            Global.config.Configs[Global.config.CurrentGame].Loadouts.Remove(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout);
                                            // Triggers selection changed event
                                            LoadoutBox.SelectedIndex = 0;
                                        }
                                        break;
                                    case 1:
                                        break;
                                }
                            }
                            break;
                        // Copy current loadout
                        case 3:
                            var copyLoadoutWindow = new EditWindow(Global.config.Configs[Global.config.CurrentGame].CurrentLoadout + " Copy", false);
                            copyLoadoutWindow.ShowDialog();
                            if (!String.IsNullOrEmpty(copyLoadoutWindow.loadout))
                            {
                                // Insert new name at index of original loadout
                                Global.LoadoutItems.Add(copyLoadoutWindow.loadout);
                                // Deep Copy over current loadout
                                ObservableCollection<Mod> ModList_Copy = new ObservableCollection<Mod>();
                                foreach (Mod m in Global.ModList)
                                {
                                    ModList_Copy.Add(m.Clone());
                                }
                                Global.config.Configs[Global.config.CurrentGame].Loadouts.Add(copyLoadoutWindow.loadout, ModList_Copy);
                                // Trigger selection changed event
                                LoadoutBox.SelectedItem = copyLoadoutWindow.loadout;
                            }
                            break;
                    }
                    Refresh();
                }
            });
        }
        private async void GameBox_DropDownClosed(object sender, EventArgs e)
        {
            if (handle)
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
                Global.logger.WriteLine($"Game switched to {Global.config.CurrentGame}", LoggerType.Info);
                Refresh();
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

                GameBox.IsEnabled = false;
                ModGrid.IsEnabled = false;
                ConfigButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                OpenModsButton.IsEnabled = false;
                UpdateCheckAllButton.IsEnabled = false;
                LauncherOptionsBox.IsEnabled = false;
                LoadoutBox.IsEnabled = false;
                EditLoadoutsButton.IsEnabled = false;
                SearchModListButton.IsEnabled = false;
                SearchModListTextBox.IsEnabled = false;
                await App.Current.Dispatcher.Invoke(async () =>
                {
                    Global.logger.WriteLine("Checking for mod updates...", LoggerType.Info);
                    await ModUpdater.CheckForUpdates(Global.config.Configs[Global.config.CurrentGame].ModsFolder, this, true);
                    Global.logger.WriteLine("Checking for Diva Mod Manager update...", LoggerType.Info);
                    if (await AutoUpdater.CheckForDMMUpdate(new CancellationTokenSource()))
                        Close();
                });
                handle = false;
            }
        }

        private async void SortAlphabeticallyAndGroupEnabled_Click(object sender, RoutedEventArgs e)
        {
            DataGridColumnHeader colHeader = sender as DataGridColumnHeader;
            if (colHeader != null)
            {
                if (!string.IsNullOrEmpty(SearchModListTextBox.Text))
                {
                    MessageBox.Show($"Please do it with the mod search cleared.\nSorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var colStr = colHeader.Column.Header;
                var msgRes = MessageBox.Show($"Sort by {colStr}.\nThe priority of the mod will change significantly.\n\nAre you sure?", "Attention.", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (msgRes != MessageBoxResult.OK)
                {
                    return;
                }

                if (colHeader != null)
                {
                    if (colHeader.Column.Header.Equals("Enabled"))
                    {
                        // Move all enabled mods to top
                        Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().OrderByDescending(x => x.enabled).ToList());
                        Global.logger.WriteLine("Moved all enabled mods to the top!", LoggerType.Info);
                    }
                    else if (colHeader.Column.Header.Equals("Priority"))
                    {
                        if (direction == ListSortDirection.Descending)
                        {
                            ObservableCollection<Mod> ModList_no_priority = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => string.IsNullOrEmpty(x.priority)).ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => !string.IsNullOrEmpty(x.priority)).OrderBy(x => x.priority, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_priority)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            ObservableCollection<Mod> ModList_no_priority = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => string.IsNullOrEmpty(x.priority)).ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => !string.IsNullOrEmpty(x.priority)).OrderByDescending(x => x.priority, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_priority)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Descending;
                        }
                    }
                    else if (colHeader.Column.Header.Equals("Name"))
                    {
                        // Sort alphabetically
                        Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().OrderBy(x => x.name, new NaturalSort()).ToList());
                        Global.logger.WriteLine("Sorted alphanumerically!", LoggerType.Info);
                    }
                    else if (colHeader.Column.Header.Equals("Category"))
                    {
                        if (direction == ListSortDirection.Descending)
                        {
                            ObservableCollection<Mod> ModList_no_cat = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.cat == "").ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.cat != "").OrderBy(x => x.cat, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_cat)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            ObservableCollection<Mod> ModList_no_note = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.cat == "").ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.cat != "").OrderByDescending(x => x.cat, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_note)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Descending;
                        }
                        Global.logger.WriteLine("Sorted by Note column!", LoggerType.Info);
                    }
                    else if (colHeader.Column.Header.Equals("Note"))
                    {
                        if (direction == ListSortDirection.Descending)
                        {
                            ObservableCollection<Mod> ModList_no_note = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.note == "").ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.note != "").OrderBy(x => x.note, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_note)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            ObservableCollection<Mod> ModList_no_note = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.note == "").ToList());
                            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().Where(x => x.note != "").OrderByDescending(x => x.note, new NaturalSort()).ToList());
                            foreach (Mod m in ModList_no_note)
                            {
                                Global.ModList.Add(m);
                            }
                            direction = ListSortDirection.Descending;
                        }
                        Global.logger.WriteLine("Sorted by Note column!", LoggerType.Info);
                    }
                    await Task.Run(() =>
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate
                        {
                            ModGrid.ItemsSource = Global.ModList;
                        });
                    });
                    UpdateSearchMod();
                    Global.UpdateConfig();
                    await Task.Run(() => ModLoader.Build());
                }
                e.Handled = true;
            }
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
            if (ModGrid.CurrentColumn.Header.ToString() == "Name")
            {
                if (e.Key == Key.Space)
                {
                    foreach (var item in ModGrid.SelectedItems)
                    {
                        var checkbox = ModGrid.Columns[0].GetCellContent(item) as CheckBox;
                        if (checkbox != null)
                        {
                            checkbox.IsChecked = !checkbox.IsChecked;
                        }
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    OpenItem_Click(sender, e);
                    e.Handled = true;
                }
            }
            else if (ModGrid.CurrentColumn.Header.ToString() == "Priority")
            {
                e.Handled = true;
                if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    || (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Delete)
                    || (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
                    || (e.Key == Key.Tab || e.Key == Key.F2 || e.Key == Key.Escape))
                {
                    e.Handled = false;
                }
            }
        }

        private void SearchModList_Click(object sender, RoutedEventArgs e)
        {
            SearchModList(SearchModListTextBox.Text, SearchCategoryComboBox.Text);
        }

        private void SearchModListTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchModList(SearchModListTextBox.Text, SearchCategoryComboBox.Text);
            }
        }

        private void SearchModList(string searchModName, string categoryName)
        {
            ModGrid.ClearSelectedItems();

            if (string.IsNullOrEmpty(searchModName) && categoryName == "ALL")
            {
                // Restore all evacuated mods.
                InitSearchMod();
            }
            else
            {
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
                        break;
                    case "None":
                        Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList()
                            .Where(x => string.IsNullOrEmpty(x.cat)).ToList());
                        break;
                    default:
                        Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList()
                            .Where(x => x.cat == categoryName).ToList());
                        break;
                }
                Global.SearchModListFlg = true;
            }

            ModGrid.ItemsSource = Global.ModList;
            Global.UpdateConfig();
        }

        private void ModGrid_PreviewDrop(object sender, DragEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchModListTextBox.Text))
            {
                // Prohibit mod movement by dragging when mod search is enabled.
                MessageBox.Show("You cannot change the priority of the MOD while searching. Sorry.", "Attention.", MessageBoxButton.OK, MessageBoxImage.Information);
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

            string action = Global.config.DoubleClickEvent?.ToLower() ?? "open";

            switch (action)
            {
                case "open":
                    OpenItem_Click(sender, e);
                    break;
                case "rename":
                    RenameMod_Click(sender, e);
                    break;
                case "configure":
                    ConfigureModItem_Click(sender, e);
                    break;
                case "fetch":
                    FetchItem_Click(sender, e);
                    break;
                case "update":
                    Update_Click(sender, e);
                    break;
                case "delete":
                    DeleteItem_Click(sender, e);
                    break;
                case "none":
                    break;
                default:
                    // All unknown characters are treated as Open.
                    OpenItem_Click(sender, e);
                    break;
            }
        }

        private void ModGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column is DataGridTextColumn textCol)
            {
                Mod mod = e.Row.DataContext as Mod;
                var textBox = e.EditingElement as TextBox;
                var newText = textBox.Text;

                if (e.Column.Header.ToString() == "Priority")
                {
                    mod.priority = newText;
                }
                else if (e.Column.Header.ToString() == "Note")
                {
                    mod.note = newText;
                }
                UpdateModConfigToml_E(mod, e.Column.Header.ToString(), newText);
            }
        }

        private void SearchCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            SearchModList(SearchModListTextBox.Text, combo.SelectedItem.ToString());
        }
    }
}
