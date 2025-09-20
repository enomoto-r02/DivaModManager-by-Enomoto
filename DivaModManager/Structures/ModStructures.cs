using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace DivaModManager
{

    // Binding to ModGrid
    [Serializable]
    public class Mod : INotifyPropertyChanged
    {
        // A no-arg constructor is required for when it is deserialized.(read by Config.json)
        public Mod()
        {
            // ここでフォルダを探しに行ってディレクトリサイズ取得等の処理をやるべき？
            // MainWindowsでどのみちConfig.jsonと実際のディレクトリの比較をするので問題ない？
        }

        public Mod Clone()
        {
            return (Mod)MemberwiseClone();
        }

        // デシリアライズなどでインスタンスを生成した場合はメンバのオブジェクト型が初期化されないため、必要時に呼び出す
        public void InitMetadata()
        {
            this.metadataManager = new MetadataManager(this);
        }
        public void InitMetadata(DownloadApiBase apiBase)
        {
            this.metadataManager = new MetadataManager(apiBase);
        }

        public Mod(string modDirectoryPath)
        {
            string dirName = Path.GetFileName(modDirectoryPath);
            if (!string.IsNullOrEmpty(dirName) && dirName.ToLower() != "mods")
            {
                if (string.IsNullOrEmpty(modDirectoryPath))
                {
                    // ここ要る？
                    //this.name = dirName;
                    //this._directory_path = string.Empty;
                }
                else
                {
                    // フォルダの存在確認
                    if (Directory.Exists(modDirectoryPath))
                    {
                        this._directory_path = modDirectoryPath;
                        this.name = dirName;
                    }
                    this.metadataManager = new MetadataManager(this);
                }
            }

            this.metadataManager = new MetadataManager(this);
        }

        private bool _enabled;
        public bool enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                OnPropertyChanged();
            }
        }
        public string? name { get; set; }

        [JsonIgnore]
        public static string CONFIG_TOML_NAME = "config.toml";
        [JsonIgnore]
        public static string CONFIG_E_TOML_NAME = "config_e.toml";
        [JsonIgnore]
        public MetadataManager metadataManager;
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
        private bool? _is_update_data = null;
        [JsonIgnore]
        public bool? is_update_data
        {
            get => _is_update_data;
            set
            {
                if (_is_update_data != value)
                {
                    _is_update_data = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        private DateTime? _last_update_check = null;
        [JsonIgnore]
        public DateTime? last_update_check
        {
            get => _last_update_check;
            set
            {
                if (_last_update_check != value)
                {
                    _last_update_check = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        private string _version = string.Empty;
        [JsonIgnore]
        public string version
        {
            get => _version;
            set
            {
                string newValue = value ?? string.Empty;
                if (_version != newValue)
                {
                    _version = newValue;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public string category { get; set; } = string.Empty;
        [JsonIgnore]
        public bool IsCategoryHighlighted { get; set; } = true;
        [JsonIgnore]
        public long _directorySize { get; set; } = -1;
        [JsonIgnore]
        public string directorySizeString
        {
            get
            {
                if (_directorySize == -1)
                    return string.Empty;
                else if (_directorySize < 1024)
                    return $"{_directorySize} B";
                else if (_directorySize < 1048576)
                    return $"{Math.Round(_directorySize / 1024.0, 2)} KB";
                else if (_directorySize < 1073741824)
                    return $"{Math.Round(_directorySize / 1048576.0, 2)} MB";
                else
                    return $"{Math.Round(_directorySize / 1073741824.0, 2)} GB";
            }
            set
            {
                var newValue = value ?? string.Empty;
                if (_directorySize.ToString() != newValue)
                {
                    _directorySize = long.Parse(newValue);
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        private string _directory_path;
        [JsonIgnore]
        public string directory_path
        {
            get
            {
                var foo = _directory_path;
                if (string.IsNullOrEmpty(foo) && !string.IsNullOrEmpty(name))
                {
                    foo = $"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{name}";
                }
                return foo;
            }
        }
        [JsonIgnore]
        public string? directory_name
        {
            get { return this.name; }
        }
        [JsonIgnore]
        public bool exist_directory
        {
            get
            {
                if (string.IsNullOrEmpty(_directory_path))
                    return false;
                // Always access it instead of keeping it in memory
                return File.Exists(_directory_path) && File.GetAttributes(_directory_path).HasFlag(FileAttributes.Directory);
            }
        }
        [JsonIgnore]
        public string mods_json_path
        {
            get => $"{this.directory_path}{Global.s}mod.json";
        }
        [JsonIgnore]
        public bool exist_mods_json
        {
            get
            {
                if (string.IsNullOrEmpty(directory_path))
                    return false;
                // Always access it instead of keeping it in memory
                return File.Exists(mods_json_path) && File.Exists(mods_json_path);
            }
        }
        [JsonIgnore]
        public string config_toml_path
        {
            get => $"{this.directory_path}{Global.s}config.toml";
        }
        [JsonIgnore]
        public bool exist_config_toml_path
        {
            get
            {
                if (string.IsNullOrEmpty(directory_path))
                    return false;
                // Always access it instead of keeping it in memory
                return File.Exists(config_toml_path) && !File.GetAttributes(config_toml_path).HasFlag(FileAttributes.Directory);
            }
        }
        [JsonIgnore]
        public string config_e_toml_path
        {
            get => $"{this.directory_path}{Global.s}config_e.toml";
        }
        [JsonIgnore]
        public bool exist_config_e_toml_path
        {
            get
            {
                if (string.IsNullOrEmpty(directory_path))
                    return false;
                // Always access it instead of keeping it in memory
                return File.Exists(config_e_toml_path) && !File.GetAttributes(config_e_toml_path).HasFlag(FileAttributes.Directory);
            }
        }
        [JsonIgnore]
        public bool Null
        {
            get { return string.IsNullOrEmpty(name); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [Serializable]
    public class MetadataManager
    {
        [JsonIgnore]
        public readonly string METADATA_DIRECTORY_PATH;
        [JsonIgnore]
        public readonly string METADATA_DIRECTORY_NAME;
        [JsonIgnore]
        public static readonly string MOD_JSON_NAME = "mod.json";
        [JsonIgnore]
        public readonly string MOD_JSON_PATH;
        [JsonIgnore]
        public Metadata metadata;
        [JsonIgnore]
        public Dictionary<string, Metadata> metadata_request;
        [JsonIgnore]
        public Dictionary<string, Metadata> metadata_update;

        public MetadataManager()
        {
            this.metadata = new Metadata();
            this.metadata_request = new Dictionary<string, Metadata>();
            this.metadata_update = new Dictionary<string, Metadata>();
        }
        public MetadataManager(Mod mod)
        {
            this.METADATA_DIRECTORY_PATH = mod.directory_path;
            this.METADATA_DIRECTORY_NAME = mod.directory_name;
            this.MOD_JSON_PATH = $@"{mod.directory_path}{Global.s}{MOD_JSON_NAME}";
            this.metadata_request = new Dictionary<string, Metadata>();
            this.metadata_update = new Dictionary<string, Metadata>();

            if (mod.exist_mods_json)
            {
                metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText($"{mod.mods_json_path}"));
            }
            else
            {
                this.metadata = new Metadata();
            }
        }
        // DownloadApiBaseの派生クラスをコンストラクタにした場合は、型に応じたコンストラクタを呼び出す
        public MetadataManager(DownloadApiBase apiBase)
        {
            switch (apiBase)
            {
                case GameBananaAPIV4 api:
                    this.metadata = new(api);
                    break;
                case GameBananaRecord api:
                    this.metadata = new(api);
                    break;
                case DivaModArchivePost api:
                    this.metadata = new(api);
                    break;
                default:
                    this.metadata = new();
                    break;
            }
        }
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



        [JsonIgnore]
        public string requestUrl { get; set; }
        [JsonIgnore]
        public string jsonPath { get; set; }

        public Metadata()
        {
        }

        public Metadata(GameBananaAPIV4 GbApiV4)
        {
            this.submitter = GbApiV4.Owner.Name;
            this.description = GbApiV4.Description;
            this.preview = GbApiV4.Image;
            this.homepage = GbApiV4.Link;
            this.avi = GbApiV4.Owner.Avatar;
            this.upic = GbApiV4.Owner.Upic;
            this.cat = GbApiV4.CategoryName;
            this.caticon = GbApiV4.Category.Icon;
            this.lastupdate = GbApiV4.DateUpdated;
        }
        public Metadata(DivaModArchivePost DmaPost)
        {
            this.id = DmaPost.ID;
            this.description = DmaPost.Text;
            this.submitter = DmaPost.Authors[0].Name;
            this.preview = DmaPost.Images[0];
            this.homepage = new Uri(Global.DMA_HOMEPAGE_URL_POSTS + DmaPost.ID);
            this.avi = DmaPost.Authors[0].Avatar;
            this.cat = DmaPost.PostType;
            this.lastupdate = DmaPost.Time;
        }
        public Metadata(GameBananaRecord record)
        {
            this.submitter = record.Owner.Name;
            this.description = record.Description;
            this.preview = record.Image;
            this.homepage = record.Link;
            this.avi = record.Owner.Avatar;
            this.upic = record.Owner.Upic;
            this.cat = record.CategoryName;
            this.caticon = record.Category.Icon;
            this.lastupdate = record.DateUpdated;
        }

        private string GetMetadataString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        public bool SaveMetadata(string mod_json_path)
        {
            try
            {
                if (File.Exists(mod_json_path))
                {
                    File.Delete(mod_json_path);
                }
                File.WriteAllText(mod_json_path, this.GetMetadataString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
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
        public Visibility SizeColumnVisible { get; set; } = Visibility.Visible;
        public int? SizeColumnIndex { get; set; } = 4;
        public double? SizeColumnWidth { get; set; }
        public Visibility NoteColumnVisible { get; set; } = Visibility.Visible;
        public int? NoteColumnIndex { get; set; } = 5;
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
