using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DivaModManager.Features.Song
{
    public partial class SongTab : UserControl
    {
        public static string[] ViewKeys = new[]
        {
            "song_name", "song_name_en", "song_name_reading", "vocal_disp_name", "vocal_disp_name_en"
        };
        List<SongTabView> viewSongDataList = new();

        public SongTab()
        {
            InitializeComponent();
        }

        public void View()
        {
            viewSongDataList.AddRange(Global.GameBase.songData.songTabViewList.Where(x => x.ViewFlg));
            var songDataList = Global.ModList.Select(x => x.songData).ToList();
            var songTabViewList = songDataList.Select(x => x.songTabViewList);
            List<SongTabView> songTabViewAll = new();
            foreach (var songTabView in songTabViewList)
            {
                songTabViewAll.AddRange(songTabView);
            }
            viewSongDataList.AddRange(songTabViewAll.Where(x => x.ViewFlg));

            SongGrid.ItemsSource = viewSongDataList;
        }

        public void Clear()
        {
            viewSongDataList.Clear();
            SongGrid.DataContext = null;
            SongGrid.ItemsSource = null;
        }

        private async void SongGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGridColumnHeader colHeader) return;
            string header = colHeader.Column?.Header?.ToString();
            if (string.IsNullOrEmpty(header)) return;

            switch (header)
            {
                default:
                    SortByField(viewSongDataList => viewSongDataList.SongID, "");
                    break;
            }
        }

        ListSortDirection direction = ListSortDirection.Ascending;

        private void SortByField(Func<SongTabView, string> selector, string fieldName)
        {
            var noValue = viewSongDataList.Where(x => string.IsNullOrEmpty(selector(x)));
            var hasValue = viewSongDataList.Where(x => !string.IsNullOrEmpty(selector(x)));

            var list = new ObservableCollection<SongTabView>(
                (direction == ListSortDirection.Descending
                    ? hasValue.OrderByDescending(selector, new NaturalSort())
                    : hasValue.OrderBy(selector, new NaturalSort()))
                .Concat(noValue)
            );

            direction = direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }
    }

    public partial class SongTabView
    {
        public bool ViewFlg { get; set; } = false;
        public string ModName { get; set; }
        public string SongFilePath { get; set; }
        public int Line { get; set; }
        public string ModPath { get; set; }
        public string SongID { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }

        public SongTabView()
        {
        }

        public void Set(string modName, string songFilePath, int line, string modPath, List<string> keyList, string value)
        {
            ModName = modName;
            SongFilePath = songFilePath;
            Line = line;
            ModPath = modPath;
            SongID = keyList[0];
            Key = string.Join(".", keyList);
            Value = value;
            ViewFlg = SongTab.ViewKeys.Contains(keyList[keyList.Count - 1]);
        }
    }
}
