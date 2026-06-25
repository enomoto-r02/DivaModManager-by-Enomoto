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
            "another_song",
            "arranger",
            "chara",
            "extra",
            "file_name",
            "illustrator",
            "level",
            "lyrics",
            "movie_file_name",
            "music",
            "script_file_name",
            "song_name",
            "song_name_en",
            "song_name_en2",
            "song_name_reading",
            "song_name_reading_en",
            "song_name_ro",
            "type",
            "vocal_disp_name",
            "vocal_disp_name_en"
        };
        List<SongTabView> viewSongDataListAll = new();
        List<SongTabView> viewSongDataList = new();

        public SongTab()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this))
                return;
        }

        public void View()
        {
            viewSongDataListAll.Clear();
            viewSongDataListAll.AddRange(Global.GameBase.songData.songTabViewList);
            var songTabViewListAll = Global.ModList.Select(x => x.songData.songTabViewList).ToList();
            foreach (var songTabViewList in songTabViewListAll)
            {
                viewSongDataListAll.AddRange(songTabViewList);
            }

            // Mod
            SearchModFilter = 2;
            ModFilterComboBox.SelectedIndex = 2;
            // Song
            SearchTypeFilter = 1;
            TypeFilterComboBox.SelectedIndex = 1;
            // song_name
            SearchKeyFilter = 11;
            KeyFilterComboBox.SelectedIndex = 11;

            FilterSearch(true);
        }

        public void Clear()
        {
            viewSongDataListAll.Clear();
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
                    SortByField(viewSongDataList => viewSongDataList.SongID.ToString(), "");
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

        #region ModFilterComboBox

        public int SearchModFilter;

        private void ModFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewSongDataListAll.Count != 0 && SearchModFilter != ModFilterComboBox.SelectedIndex)
            {
                SearchModFilter = ModFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(SongGrid);
            }
        }

        #endregion

        #region TypeFilterComboBox

        public int SearchTypeFilter;
        private void TypeFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewSongDataListAll.Count != 0 && SearchTypeFilter != TypeFilterComboBox.SelectedIndex)
            {
                SearchTypeFilter = TypeFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(SongGrid);
            }
        }

        #endregion

        #region KeyFilterComboBox

        public int SearchKeyFilter;
        private void KeyFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewSongDataListAll.Count != 0 && SearchKeyFilter != KeyFilterComboBox.SelectedIndex)
            {
                SearchKeyFilter = KeyFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(SongGrid);
            }
        }

        #endregion

        #region ConflictFilterComboBox

        public int SearchConflictFilter;
        private void ConflictFilterComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (viewSongDataListAll.Count != 0 && SearchConflictFilter != ConflictFilterComboBox.SelectedIndex)
            {
                SearchConflictFilter = ConflictFilterComboBox.SelectedIndex;
                FilterSearch();
                Util.DataGrid_ScrollToTop(SongGrid);
            }
        }

        #endregion

        private void FilterSearch(bool viewInit = false)
        {
            viewSongDataList = viewSongDataListAll;
            if (SearchModFilter != 0)
            {
                viewSongDataList = viewSongDataList.Where(x => x.GameValue == SearchModFilter).ToList();
            }
            if (SearchTypeFilter != 0)
            {
                viewSongDataList = viewSongDataList.Where(x => x.TypeValue == SearchTypeFilter).ToList();
            }
            if (SearchKeyFilter != 0)
            {
                this.viewSongDataList = viewSongDataList.Where(x => x.KeyValue == SearchKeyFilter).ToList();
            }
            if (SearchConflictFilter != 0)
            {
                var conflictIds = viewSongDataList
                    .Where(v => v.SongID.HasValue)
                    .GroupBy(v => v.SongID.Value)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                viewSongDataList = viewSongDataList
                    .Where(v => v.SongID.HasValue && conflictIds.Contains(v.SongID.Value))
                    .ToList();
            }
            SongGrid.ItemsSource = viewSongDataList;

            if (viewInit)
            {
                var InstallMsg = App.Current.Dispatcher.BeginInvoke(() => WindowHelper.DMMWindowOpen(27));
            }
        }
    }

    public partial class SongTabView
    {
        public bool ViewFlg { get; set; } = false;
        public string ModName { get; set; }
        //public string SongFilePath { get; set; }
        public int Line { get; set; }
        public string ModPath { get; set; }
        public int? SongID { get; set; } = null;
        public string Key { get; set; }
        public string Value { get; set; }
        // 0 : ALL
        // 1 : BASE
        // 2 : other(Mod)
        public int GameValue { get; set; }
        // 0 : ALL
        // 1 : Song
        // 2 : other(another_song)
        public int TypeValue { get; set; }
        // 0 : ALL
        public int KeyValue { get; set; }

        public SongTabView()
        {
        }

        public void Set(string[] viewKeys, string modName, string songFilePath, int line, string modPath, List<string> keyList, string value)
        {
            ModName = modName;
            //SongFilePath = songFilePath;
            Line = line;
            ModPath = modPath;
            if (keyList.Count >= 2)
            {
                var success = int.TryParse(keyList[0].Replace("pv_", ""), out int songID);
                if (success)
                {
                    SongID = songID;
                }
            }
            Key = string.Join(".", keyList);
            Value = value;
            foreach (string ViewKey in SongTab.ViewKeys)
            {
                if (Key.EndsWith(ViewKey))
                {
                    ViewFlg = true;
                }
            }

            GameValue = ModName == SongData.BASE_FOLDER_NAME ? 1 : 2;
            TypeValue = keyList.Count >= 2 && keyList[1] == "another_song" ? 2 : 1;
            KeyValue = keyList[keyList.Count - 1] switch
            {
                "another_song" => 1,
                "arranger" => 2,
                "chara" => 3,
                "extra" => 4,
                "illustrator" => 5,
                "level" => 6,
                "lyrics" => 7,
                "movie_file_name" => 8,
                "music" => 9,
                "script_file_name" => 10,
                "song_name" => 11,
                "song_name_en" => 12,
                "song_name_en2" => 13,
                "song_name_reading" => 14,
                "song_name_reading_en" => 15,
                "song_name_ro" => 16,
                "type" => 17,
                "vocal_disp_name" => 18,
                "vocal_disp_name_en" => 19,
                _ => 0,
            };
        }
    }
}
