using DivaModManager.Common.Helpers;
using DivaModManager.Features.MikuMikuLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static DivaModManager.Features.Module.ModuleTabView;

namespace DivaModManager.Features.Module
{
    public partial class ModuleTab : UserControl
    {
        public static readonly string TAB_NAME = "Module";

        public static string[] ViewKeys = new[]
        {
            "cos", "name",
            "id", "attr",
            "chara", "sort_index",
        };
        List<ModuleTabView> viewModuleDataListAll = new();
        List<ModuleTabView> viewModuleDataList = new();

        // Linux対応するためPath.Combine
        List<string> BaseModulePath = new()
        {
            @"\BASE\diva_main_region\rom_steam_region\rom\gm_module_tbl\gm_module_id.bin",
            @"\BASE\diva_main_region\rom_steam_region\rom\gm_customize_item_tbl\gm_customize_item_id.bin",
            @"\BASE\diva_main\rom_switch\rom\gm_module_tbl\gm_module_id.bin",
            @"\BASE\diva_main\rom_switch\rom\gm_customize_item_tbl\gm_customize_item_id.bin",
            @"\BASE\diva_main\rom_ps4\rom\gm_module_tbl\gm_module_id.bin",
            @"\BASE\diva_main\rom_ps4\rom\gm_customize_item_tbl\gm_customize_item_id.bin",
            @"\BASE\diva_dlc00_region\rom_steam_region_dlc\rom\gm_module_tbl\gm_module_id.bin",
            @"\BASE\diva_dlc00_region\rom_steam_region_dlc\rom\gm_customize_item_tbl\gm_customize_item_id.bin",
        };

        public ModuleTab()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this))
                return;
        }

        public void View()
        {
            viewModuleDataListAll.Clear();
            viewModuleDataListAll.AddRange(Global.GameBase.moduleData.moduleTabViewList);
            var moduleTabViewListAll = Global.ModList.Select(x => x.moduleData.moduleTabViewList).ToList();
            foreach (var moduleTabViewList in moduleTabViewListAll)
            {
                viewModuleDataListAll.AddRange(moduleTabViewList);
            }

            // Mod
            SearchModFilter = 2;
            ModFilterComboBox.SelectedIndex = 2;
            // Module
            SearchTypeFilter = 1;
            TypeFilterComboBox.SelectedIndex = 1;
            // CosID/ID
            SearchKeyFilter = 3;
            KeyFilterComboBox.SelectedIndex = 3;
            // Chara Filter
            SearchCharaFilter = 0;
            CharaFilterComboBox.SelectedIndex = 0;
            // Conflict Filter
            SearchConflictFilter = 0;
            ConflictFilterComboBox.SelectedIndex = 0;

            FilterSearch(true);
        }

        public void Clear()
        {
            viewModuleDataListAll.Clear();
            viewModuleDataList.Clear();
            ModuleGrid.DataContext = null;
            ModuleGrid.ItemsSource = null;
        }

        // 実装途中
        public void InitSetting()
        {
            var isMMLSetting = false;

            // ここにベースファイルのチェック


            // ベースファイルが足りない場合、MMLのインストールを促す
            if (string.IsNullOrEmpty(Global.ConfigJson.MikuMikuLibraryDllFilePath) || Directory.Exists(Global.ConfigJson.MikuMikuLibraryDllFilePath))
            {
                if (File.Exists(System.IO.Path.Combine(Global.ConfigJson.MikuMikuLibraryDllFilePath, Global.MIKU_MIKU_LIBRALY_DLL)))
                {
                    isMMLSetting = true;
                }
            }
            if (!isMMLSetting)
            {
                var InstallMsg = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(84, replaceList: new List<string>() { TAB_NAME }));
                if (InstallMsg == WindowHelper.WindowCloseStatus.Yes)
                {
                    var mmlDllFilePath = Global.assemblyLocation;
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Multiselect = false,
                        Title = $"{Global.MIKU_MIKU_LIBRALY_DLL}を選択してください",
                        InitialDirectory = mmlDllFilePath,

                    };
                    if (dialog.ShowDialog() == true)
                    {
                        mmlDllFilePath = dialog.FileName;
                    }

                    if (File.Exists(mmlDllFilePath))
                    {
                        Global.ConfigJson.MikuMikuLibraryDllFilePath = mmlDllFilePath;
                    }
                }
                else if (InstallMsg == WindowHelper.WindowCloseStatus.Cancel)
                {
                    ProcessHelper.TryStartProcess(MikuMikuLibraryHelper.MIKUMIKULIBRARY_URL_GITHUB);
                }
            }
        }

        private async void ModuleGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGridColumnHeader colHeader) return;
            string header = colHeader.Column?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            switch (header)
            {
                default:
                    SortByField(viewModuleDataList => viewModuleDataList.Id.ToString(), "");
                    break;
            }
        }

        ListSortDirection Direction = ListSortDirection.Ascending;

        private void SortByField(Func<ModuleTabView, string> selector, string fieldName)
        {
            var noValue = viewModuleDataList.Where(x => string.IsNullOrEmpty(selector(x)));
            var hasValue = viewModuleDataList.Where(x => !string.IsNullOrEmpty(selector(x)));

            var list = new ObservableCollection<ModuleTabView>(
                (Direction == ListSortDirection.Descending
                    ? hasValue.OrderByDescending(selector, new NaturalSort())
                    : hasValue.OrderBy(selector, new NaturalSort()))
                .Concat(noValue)
            );

            Direction = Direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        #region ModFilterComboBox

        public int SearchModFilter;

        private void ModFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewModuleDataListAll.Count != 0 && SearchModFilter != ModFilterComboBox.SelectedIndex)
            {
                SearchModFilter = ModFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(ModuleGrid);
            }
        }

        #endregion

        #region TypeFilterComboBox

        public int SearchTypeFilter;
        private void TypeFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewModuleDataListAll.Count != 0 && SearchTypeFilter != TypeFilterComboBox.SelectedIndex)
            {
                SearchTypeFilter = TypeFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(ModuleGrid);
            }
        }

        #endregion

        #region KeyFilterComboBox

        public int SearchKeyFilter;
        private void KeyFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewModuleDataListAll.Count != 0 && SearchKeyFilter != KeyFilterComboBox.SelectedIndex)
            {
                SearchKeyFilter = KeyFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(ModuleGrid);
            }
        }

        #endregion

        #region CharaFilterComboBox

        public int SearchCharaFilter;
        private void CharaFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewModuleDataListAll.Count != 0 && SearchCharaFilter != CharaFilterComboBox.SelectedIndex)
            {
                SearchCharaFilter = CharaFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(ModuleGrid);
            }
        }

        #endregion

        #region ConflictFilterComboBox

        public int SearchConflictFilter;
        private void ConflictFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewModuleDataListAll.Count != 0 && SearchConflictFilter != ConflictFilterComboBox.SelectedIndex)
            {
                SearchConflictFilter = ConflictFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(ModuleGrid);
            }
        }

        #endregion

        private void FilterSearch(bool viewInit = false)
        {
            var direction = Direction;
            viewModuleDataList = viewModuleDataListAll;
            if (SearchModFilter != 0)
            {
                viewModuleDataList = viewModuleDataList.Where(x => x.GameValue == SearchModFilter).ToList();
            }
            if (SearchTypeFilter != 0)
            {
                viewModuleDataList = viewModuleDataList.Where(x => x.TypeValue == SearchTypeFilter).ToList();
            }
            if (SearchKeyFilter != 0)
            {
                viewModuleDataList = viewModuleDataList.Where(x => x.KeyValue == SearchKeyFilter).ToList();
            }
            if (SearchCharaFilter != 0)
            {
                viewModuleDataList = viewModuleDataList.Where(x => x.Chara == (MODULE_CHARA)Enum.Parse(typeof(MODULE_CHARA), SearchCharaFilter.ToString())).ToList();
            }
            if (SearchConflictFilter != 0)
            {
                var conflictIds = viewModuleDataList
                    .Where(v => v.Id != (int)ID.OTHER)
                    .GroupBy(v => v.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                viewModuleDataList = viewModuleDataList
                    .Where(v => v.Id != (int)ID.OTHER && conflictIds.Contains(v.Id))
                    .ToList();
            }
            ModuleGrid.ItemsSource = viewModuleDataList;

            if (viewInit)
            {
                var InstallMsg = App.Current.Dispatcher.BeginInvoke(() => WindowHelper.DMMWindowOpen(27));
            }
        }
    }

    public partial class ModuleTabView
    {
        public enum MODULE_CHARA
        {
            MIKU = 1,       // コンボボックスの関係上、1から(enum参照に直す時に一緒に直す)
            RIN,
            LEN,
            LUKA,
            MEIKO,
            KAITO,
            TETO,
            NERU,
            HAKU,
            SAKINE,
            ALL,
            OTHER = 65535,
        };
        public enum ID
        {
            OTHER = -1,
        }

        public MODULE_CHARA Chara { get; set; } = MODULE_CHARA.OTHER;
        public int No { get; set; } = -1;       // module.n.xxx の "n"の部分(不要かも)
        public bool HasError { get; set; } = false;
        public bool ViewFlg { get; set; } = false;
        public int LoadPriority { get; set; } = -1;
        public string ModName { get; set; }
        public string ModuleFilePath { get; set; }
        public int Line { get; set; }
        public string ModPath { get; set; }
        public int Id { get; set; } = (int)ID.OTHER;     // モジュールは"cos"、カスタマイズアイテムは"id"の値
        public string Key { get; set; }
        public string Value { get; set; }
        // 0 : ALL
        // 1 : BASE
        // 2 : other(Mod)
        public int GameValue { get; set; }
        // 0 : ALL
        // 1 : module
        // 2 : other(cstm_item)
        public int TypeValue { get; set; }
        // 0 : ALL
        // 1 : xxx.xxx.attr
        // 2 : xxx.xxx.chara
        // 3 : xxx.xxx.cos
        // 4 : xxx.xxx.id
        // 5 : xxx.xxx.name
        // 6 : xxx.xxx.sort_index
        public int KeyValue { get; set; }

        public ModuleTabView()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="viewKeys"></param>
        /// <param name="loadPriority"></param>
        /// <param name="modName"></param>
        /// <param name="moduleFilePath"></param>
        /// <param name="line"></param>
        /// <param name="no"></param>
        /// <param name="modPath"></param>
        /// <param name="keyList"></param>
        /// <param name="value"></param>
        public void Set(string[] viewKeys, int loadPriority, string modName, string moduleFilePath, int line, int no, string modPath, List<string> keyList, string value)
        {
            LoadPriority = loadPriority;
            ModName = modName;
            ModuleFilePath = moduleFilePath;
            Line = line;
            ModPath = modPath;
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (keyList[0] == "module" && keyList[keyList.Count - 1] == "cos")
                {
                    var success = int.TryParse(value.Replace("COS_", ""), out int cosIdTmp);
                    if (success)
                    {
                        Id = cosIdTmp;
                    }
                }
                else if (keyList[0] == "cstm_item" && keyList[keyList.Count - 1] == "id")
                {
                    var success = int.TryParse(value, out int IdTmp);
                    if (success)
                    {
                        Id = IdTmp;
                    }
                }
                else if (keyList[keyList.Count - 1] == "chara")
                {
                    var success = MODULE_CHARA.TryParse(value, out MODULE_CHARA charaTmp);
                    if (success)
                    {
                        Chara = charaTmp;
                    }
                }
            }
            Key = string.Join(".", keyList);
            Value = value;
            foreach (string ViewKey in ModuleTab.ViewKeys)
            {
                if (Key.EndsWith(ViewKey))
                {
                    ViewFlg = true;
                    break;
                }
            }

            GameValue = ModName == ModuleData.BASE_FOLDER_NAME ? 1 : 2;
            TypeValue = keyList[0] == "module" ? 1 : 2;
            KeyValue = keyList[keyList.Count - 1] switch
            {
                "attr" => 1,
                "chara" => 2,
                "cos" => 3,
                "id" => 4,
                "name" => 5,
                "sort_index" => 6,
                _ => 0,
            };
            No = no;
        }
    }
}
