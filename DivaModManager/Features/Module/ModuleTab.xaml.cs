using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DivaModManager.Features.Module
{
    public partial class ModuleTab : UserControl
    {
        public static string[] ViewKeys = new[] 
        {
            @"(cos$)", @"(name$)", @"(id$)", @"(chara$)", @"(obj_id)", @"(sort_index$)",
            @"(parts$)",
        };
        List<ModuleTabView> viewModuleDataList = new();

        public ModuleTab()
        {
            InitializeComponent();
        }

        public void View()
        {
            viewModuleDataList.AddRange(Global.GameBase.moduleData.moduleTabViewList.Where(x => x.ViewFlg));
            var moduleDataList = Global.ModList.Select(x => x.moduleData).ToList();
            var moduleTabViewList = moduleDataList.Select(x => x.moduleTabViewList);
            List<ModuleTabView> moduleTabViewAll = new();
            foreach (var moduleTabView in moduleTabViewList)
            {
                moduleTabViewAll.AddRange(moduleTabView);
            }
            viewModuleDataList.AddRange(moduleTabViewAll.Where(x => x.ViewFlg));

            ModuleGrid.ItemsSource = viewModuleDataList;
        }

        public void Clear()
        {
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
                    SortByField(viewModuleDataList => viewModuleDataList.ItemNo.ToString(), "");
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
    }

    public partial class ModuleTabView
    {
        public bool ViewFlg { get; set; } = false;
        public string ModName { get; set; }
        public string ModuleFilePath { get; set; }
        public int Line { get; set; }
        public string ModPath { get; set; }
        public int? ItemNo { get; set; } = null;
        public string Key { get; set; }
        public string Value { get; set; }

        public ModuleTabView()
        {
        }

        public void Set(string[] viewKeys, string modName, string moduleFilePath, int line, string modPath, List<string> keyList, string value)
        {
            ModName = modName;
            ModuleFilePath = moduleFilePath;
            Line = line;
            ModPath = modPath;
            if (keyList.Count >= 2)
            {
                int ItemNoInt;
                var success = int.TryParse(keyList[1], out ItemNoInt);
                if (success)
                {
                    ItemNo = ItemNoInt;
                }
            }
            Key = string.Join(".", keyList);
            Value = value;
            string matchKey = string.Join("|", viewKeys);
            if (Regex.IsMatch(Key, matchKey))
            {
                ViewFlg = true;
            }
        }
    }
}
