using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;

namespace DivaModManager
{
    public class Mod : INotifyPropertyChanged
    {
        private bool _enabled;
        public bool enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string name { get; set; } = string.Empty;

        [JsonIgnore]
        public bool selected { get; set; }
        [JsonIgnore]
        private string? _priority;
        [JsonIgnore]
        public string? priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        private string _note = string.Empty;
        [JsonIgnore]
        public string note
        {
            get => _note;
            set
            {
                string newValue = value ?? string.Empty;
                if (_note != newValue)
                {
                    _note = newValue;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string category { get; set; } = string.Empty;
        [JsonIgnore]
        public bool IsCategoryHighlighted { get; set; } = true;


        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Simple copy
        public Mod Clone() => (Mod)MemberwiseClone();
    }

    public class Metadata
    {
        public int? id { get; set; }
        public Uri preview { get; set; }
        public string submitter { get; set; }
        public Uri avi { get; set; }
        public Uri upic { get; set; }
        public Uri caticon { get; set; }
        public string cat { get; set; }
        public string description { get; set; }
        public Uri homepage { get; set; }
        public DateTime? lastupdate { get; set; }
    }
    public class Config
    {
        public string CurrentGame { get; set; }
        public Dictionary<string, GameConfig> Configs { get; set; }
        public double? LeftGridWidth { get; set; }
        public double? RightGridWidth { get; set; }
        public double? TopGridHeight { get; set; }
        public double? BottomGridHeight { get; set; }
        public double? Height { get; set; }
        public double? Width { get; set; }
        public bool Maximized { get; set; }
        public bool AddModToTop { get; set; }

        public Visibility EnabledColumnVisible { get; set; } = Visibility.Visible;
        public int? EnabledColumnIndex { get; set; } = 0;
        public double? EnabledColumnWidth { get; set; }
        public Visibility PriorityColumnVisible { get; set; } = Visibility.Visible;
        public int? PriorityColumnIndex { get; set; } = 1;
        public double? PriorityColumnWidth { get; set; }
        public Visibility NameColumnVisible { get; set; } = Visibility.Visible;
        public int? NameColumnIndex { get; set; } = 2;
        public double? NameColumnWidth { get; set; }
        public Visibility CategoryColumnVisible { get; set; } = Visibility.Visible;
        public int? CategoryColumnIndex { get; set; } = 3;
        public double? CategoryColumnWidth { get; set; }
        public Visibility NoteColumnVisible { get; set; } = Visibility.Visible;
        public int? NoteColumnIndex { get; set; } = 4;
        public double? NoteColumnWidth { get; set; }

        public bool CategoryColumnColor { get; set; } = false;
        public string? DoubleClickEvent { get; set; }
    }
    public class GameConfig
    {
        public string Launcher { get; set; }
        public string GamePath { get; set; }
        public bool LauncherOption { get; set; }
        public int LauncherOptionIndex { get; set; }
        public bool LauncherOptionConverted { get; set; }
        public bool FirstOpen { get; set; }
        public string ModsFolder { get; set; }
        public string ModLoaderVersion { get; set; }
        public string CurrentLoadout { get; set; }
        public Dictionary<string, ObservableCollection<Mod>> Loadouts { get; set; }
    }
    public class Choice
    {
        public string OptionText { get; set; }
        public string OptionSubText { get; set; }
        public int Index { get; set; }
    }
}
