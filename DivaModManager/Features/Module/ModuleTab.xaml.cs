using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DivaModManager.Features.Module
{
    public partial class ModuleTab : UserControl
    {
        public static string[] ViewKeys = new[]
        {
            "cos", "name",
            "id", "attr",
            "chara", "sort_index",
        };
        List<ModuleTabView> viewModuleDataListAll = new();
        List<ModuleTabView> viewModuleDataList = new();

        public ModuleTab()
        {
            InitializeComponent();
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
            // CosID
            SearchKeyFilter = 3;
            KeyFilterComboBox.SelectedIndex = 3;
            // Conflict Filter
            SearchConflictFilter = 0;
            ConflictFilterComboBox.SelectedIndex = 0;

            FilterSearch();
        }

        public void Clear()
        {
            viewModuleDataListAll.Clear();
            viewModuleDataList.Clear();
            ModuleGrid.DataContext = null;
            ModuleGrid.ItemsSource = null;
        }

        private async void ModuleGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGridColumnHeader colHeader) return;
            string header = colHeader.Column?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            switch (header)
            {
                default:
                    SortByField(viewModuleDataList => viewModuleDataList.CosID.ToString(), "");
                    break;
            }
        }

        ListSortDirection direction = ListSortDirection.Ascending;

        private void SortByField(Func<ModuleTabView, string> selector, string fieldName)
        {
            var noValue = viewModuleDataList.Where(x => string.IsNullOrEmpty(selector(x)));
            var hasValue = viewModuleDataList.Where(x => !string.IsNullOrEmpty(selector(x)));

            var list = new ObservableCollection<ModuleTabView>(
                (direction == ListSortDirection.Descending
                    ? hasValue.OrderByDescending(selector, new NaturalSort())
                    : hasValue.OrderBy(selector, new NaturalSort()))
                .Concat(noValue)
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
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

        private void FilterSearch()
        {
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
            if (SearchConflictFilter != 0)
            {
                var conflictIds = viewModuleDataList
                    .Where(v => v.CosID.HasValue)
                    .GroupBy(v => v.CosID.Value)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                viewModuleDataList = viewModuleDataList
                    .Where(v => v.CosID.HasValue && conflictIds.Contains(v.CosID.Value))
                    .ToList();
            }
            ModuleGrid.ItemsSource = viewModuleDataList;
        }
    }

    public partial class ModuleTabView
    {

        public enum MODULE_CHARA
        {
            NONE = 0,
            MIKU,
            RIN,
            LEN,
            LUKA,
            MEIKO,
            KAITO,
            NERU,
            HAKU,
            SAKINE,
        };

        public MODULE_CHARA Chara { get; set; } = MODULE_CHARA.NONE;
        public bool ViewFlg { get; set; } = false;
        public string ModName { get; set; }
        public string ModuleFilePath { get; set; }
        public int Line { get; set; }
        public string ModPath { get; set; }
        public int? CosID { get; set; } = null;
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

        public void Set(string[] viewKeys, string modName, string moduleFilePath, int line, string modPath, List<string> keyList, string value)
        {
            ModName = modName;
            ModuleFilePath = moduleFilePath;
            Line = line;
            ModPath = modPath;
            if (keyList[keyList.Count-1] == "cos" && !string.IsNullOrWhiteSpace(value))
            {
                var success = int.TryParse(value.Replace("COS_", ""), out int cosIDInt);
                if (success)
                {
                    CosID = cosIDInt;
                }
            }
            Key = string.Join(".", keyList);
            Value = value;
            foreach (string ViewKey in ModuleTab.ViewKeys)
            {
                if (Key.EndsWith(ViewKey))
                {
                    ViewFlg = true;
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
        }
    }
}
