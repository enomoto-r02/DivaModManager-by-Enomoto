using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Common.MessageWindow;
using DivaModManager.Features.DML;
using DivaModManager.Features.Module;
using DivaModManager.Features.Song;
using DivaModManager.Structures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#nullable enable

namespace DivaModManager.Models
{
    // Binding to ModGrid
    [Serializable]
    public class Mod : INotifyPropertyChanged
    {
        [JsonIgnore]
        public static string CONFIG_TOML_NAME = "config.toml";
        [JsonIgnore]
        public static string CONFIG_E_TOML_NAME = "config_e.toml";

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ModList、ModList_AllはGlobalクラスを参照
        public Mod()
        {
            // for Shift_JIS encoding Check
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public Mod Clone()
        {
            var ret = (Mod)MemberwiseClone();
            return ret;
        }

        // デシリアライズなどでインスタンスを生成した場合はメンバのオブジェクト型が初期化されないため、必要時に呼び出す
        public async Task<bool> InitModAsync()
        {
            metadataManager = new MetadataManager(this);
            var ret = await metadataManager.InitMetadataAsync(this);

            var homepage = metadataManager.metadata.homepage?.DnsSafeHost;
            if (!string.IsNullOrEmpty(homepage))
                _site = Metadata.HOSTS.Where(x => x.Value == homepage).FirstOrDefault().Key;
            else
                _site = "-";

            _SkipFileWhenSizeCheckPathList.AddRange(new[]
            {
                Path.Combine(Global.ModsFolder, name, "config.toml"),
                Path.Combine(Global.ModsFolder, name, "config_e.toml"),
                Path.Combine(Global.ModsFolder, name, "mod.json"),
                Path.Combine(Global.ModsFolder, name, "preview")
            });

            ConfigToml.Init(this);

            return ret;
        }

        protected bool? _enabled { get; set; } = false;
        public virtual bool enabled
        {
            get => _enabled ?? false;
            set
            {
                if (Global.IsMainWindowLoaded && Global.IsModGridLoaded)
                {
                    if (exist_config_toml)
                    {
                        _enabled = value;
                        OnPropertyChanged();
                    }
                }
                else
                {
                    _enabled = value;
                    OnPropertyChanged();
                }
            }
        }
        // 現在、nameはdirectory_pathと同じなので注意
        public virtual string name { get; set; } = string.Empty;
        // config.tomlに書かれた"name"
        [JsonIgnore]
        public string ConfigTomlModName { get; set; } = string.Empty;
        [JsonIgnore]
        public List<WindowInfo> Errors = new();
        [JsonIgnore]
        public MetadataManager metadataManager = new();
        [JsonIgnore]
        public bool selected { get; set; }
        // Priority列(rowとは違うので注意)
        [JsonIgnore]
        public virtual string? priority
        {
            get => ConfigToml?.Priority ?? string.Empty;
            set
            {
                string newValue = value ?? string.Empty;
                if (ConfigToml.Priority != newValue)
                {
                    ConfigToml.Priority = newValue;
                    ConfigToml.Update(this);
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public virtual string note
        {
            get => ConfigToml?.Note ?? string.Empty;
            set
            {
                string newValue = value ?? string.Empty;
                if (ConfigToml.Note != newValue)
                {
                    ConfigToml.Note = newValue;
                    ConfigToml.Update(this);
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        private string _version = "-";
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
        protected string? _site { get; set; } = null;
        [JsonIgnore]
        public virtual string site
        {
            get
            {
                return string.IsNullOrWhiteSpace(_site) ? "-" : _site;
            }
            private set
            {
                if (string.IsNullOrWhiteSpace(value))
                    _site = "-";
                else
                    _site = value;
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public virtual string? category
        {
            get => !string.IsNullOrWhiteSpace(ConfigToml?.Category) ? ConfigToml?.Category : metadataManager?.metadata?.cat;
            set
            {
                string newValue = value ?? string.Empty;
                if (metadataManager.metadata.cat != newValue && ConfigToml.Category != newValue)
                {
                    ConfigToml.Category = newValue;
                    ConfigToml.Update(this);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCategoryHighlighted));
                }
                else if (metadataManager.metadata.cat == newValue || string.IsNullOrWhiteSpace(newValue))
                {
                    ConfigToml.Category = string.Empty;
                    ConfigToml.Update(this);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCategoryHighlighted));
                }
            }
        }
        [JsonIgnore]
        public bool IsCategoryHighlighted
        {
            get { return !string.IsNullOrEmpty(ConfigToml?.Category) && ConfigToml?.Category != metadataManager?.metadata?.cat; }
        }

        [JsonIgnore]
        private List<string> _SkipFileWhenSizeCheckPathList { get; set; } = new();
        [JsonIgnore]
        public List<string> SkipFileWhenSizeCheckPathList
        {
            get
            {
                return _SkipFileWhenSizeCheckPathList;
            }
        }
        [JsonIgnore]
        private long _directorySize { get; set; } = -1;
        [JsonIgnore]
        public long directorySize
        {
            get
            {
                return _directorySize;
            }
            set
            {
                _directorySize = value; OnPropertyChanged();
            }
        }
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
        private string _directory_path { get; set; } = string.Empty;
        [JsonIgnore]
        public string directory_path
        {
            get
            {
                var foo = _directory_path;
                if (string.IsNullOrEmpty(foo) && !string.IsNullOrEmpty(name))
                {
                    var currentGame = Global.ConfigJson?.CurrentGame ?? string.Empty;
                    if (!string.IsNullOrEmpty(currentGame)
                        && Global.ConfigJson != null
                        && Global.ConfigJson.Configs.ContainsKey(currentGame)
                        && !string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
                        foo = Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, name);
                }
                return foo;
            }
        }
        [JsonIgnore]
        public string? directory_name
        {
            get { return name; }
        }
        [JsonIgnore]
        public bool exist_directory
        {
            get
            {
                return string.IsNullOrEmpty(_directory_path) || !Directory.Exists(_directory_path);
            }
        }
        [JsonIgnore]
        public string mods_json_path
        {
            get => Path.Combine(directory_path, "mod.json");
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
            get => Path.Combine(directory_path, Mod.CONFIG_TOML_NAME);
        }
        [JsonIgnore]
        public bool exist_config_toml
        {
            get
            {
                if (string.IsNullOrEmpty(directory_path))
                    return false;
                return File.Exists(config_toml_path) && !File.GetAttributes(config_toml_path).HasFlag(FileAttributes.Directory);
            }
        }
        [JsonIgnore]
        public string config_e_toml_path
        {
            get => Path.Combine(directory_path, Mod.CONFIG_E_TOML_NAME);
        }
        [JsonIgnore]
        public bool exist_config_e_toml
        {
            get
            {
                if (string.IsNullOrEmpty(directory_path))
                    return false;
                return File.Exists(config_e_toml_path) && !File.GetAttributes(config_e_toml_path).HasFlag(FileAttributes.Directory);
            }
        }
        [JsonIgnore]
        public bool CheckErrorWarningAlready { get; set; } = false;
        [JsonIgnore]
        private bool _IsError { get; set; } = false;
        [JsonIgnore]
        public bool IsError
        {
            get
            {
                return !string.IsNullOrWhiteSpace(GetErrorString());
            }
            set
            {
                _IsError = value;
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        private bool _IsWarn { get; set; } = false;
        [JsonIgnore]
        public bool IsWarn
        {
            get
            {
                return !string.IsNullOrWhiteSpace(GetWarningString());
            }
            set
            {
                _IsWarn = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public ConfigTomlMod ConfigToml { get; set; } = new();
        // 現在設定されているModGridでの順番(Priorityとは違い実際のリストに並んでいる値)
        [JsonIgnore]
        private int row { get; set; } = -1;

        public virtual string GetErrorString()
        {
            if (!CheckErrorWarningAlready)
                CheckErrorAndWarning();
            var ret = string.Empty;
            foreach (var e in Errors.Where(x => x.Type == WindowInfo.TYPE.ERROR))
            {
                if (Global.ConfigToml.NG_ID_List == null ? true : !Global.ConfigToml.NG_ID_List.Contains((long)e.ID))
                {
                    if (ConfigToml.NG_ID_List == null ? true : !ConfigToml.NG_ID_List.Contains((long)e.ID))
                    {
                        ret += e?.ToString();
                    }
                }
            }
            return ret;
        }
        public virtual string GetWarningString()
        {
            if (!CheckErrorWarningAlready)
                CheckErrorAndWarning();
            var ret = string.Empty;
            foreach (var e in Errors.Where(x => x.Type == WindowInfo.TYPE.WARNING))
            {
                if (Global.ConfigToml.NG_ID_List == null ? true : !Global.ConfigToml.NG_ID_List.Contains((long)e.ID))
                {
                    if (ConfigToml.NG_ID_List == null ? true : !ConfigToml.NG_ID_List.Contains((long)e.ID))
                    {
                        ret += e?.ToString();
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// 自身のエラーをチェックする
        /// </summary>
        public virtual void CheckErrorAndWarning()
        {
            if (string.IsNullOrEmpty(name)) { return; }

            var prefix = "    ";

            var config_toml_all = Directory.GetFiles(directory_path, CONFIG_TOML_NAME, SearchOption.AllDirectories);
            var config_toml_top = Directory.GetFiles(directory_path, CONFIG_TOML_NAME, SearchOption.TopDirectoryOnly);

            // config.tomlなし(0002E)
            if (config_toml_all.Length == 0)
            {
                var addError = WindowList.MessageWindowNo(2);
                if (!Errors.Contains(addError)) Errors.Add(addError);
            }

            // config.tomlの配置誤り(0003E)
            if (config_toml_top.Length == 0 && config_toml_all.Count() == 1)
            {
                var addError = WindowList.MessageWindowNo(3);
                if (!Errors.Contains(addError)) Errors.Add(addError);
            }


            // config.tomlが複数(0001W)
            if (config_toml_all.Length >= 2)
            {
                var addError = WindowList.MessageWindowNo(1);
                if (!Errors.Contains(addError)) Errors.Add(addError);
            }

            // フォルダ内にModのファイルが存在しない(0056W)
            if (FileHelper.GetDirectorySize(directory_path, SkipFileWhenSizeCheckPathList) == 0)
            {
                var addError = WindowList.MessageWindowNo(56);
                if (!Errors.Contains(addError)) Errors.Add(addError);
            }

            // 同一Modの複数存在チェック(0057W)
            var Error_57_List = new List<Mod>();

            // config.tomlで検証
            if (!string.IsNullOrEmpty(ConfigTomlModName))
            {
                // nameのみ
                if (Global.ConfigToml.ModsListDuplicatioCheckConfigTomlName)
                {
                    Error_57_List.AddRange(Global.ModList_All.Where(
                        x => x.ConfigTomlModName == ConfigTomlModName).ToList());
                }
                // nameとversion
                if (Global.ConfigToml.ModsListDuplicatioCheckConfigTomlVersion && !string.IsNullOrEmpty(version))
                {
                    Error_57_List.AddRange(Global.ModList_All.Where(
                        x => x.ConfigTomlModName == ConfigTomlModName
                        && x.version == version).ToList());
                }
            }
            // mod.jsonで検証
            if (metadataManager != null && metadataManager.metadata != null)
            {
                // homepageのみ
                if (Global.ConfigToml.ModsListDuplicatioCheckModJsonHomepage
                    && !string.IsNullOrEmpty(metadataManager?.metadata?.homepage?.ToString()))
                {
                    Error_57_List.AddRange(Global.ModList_All.Where(x =>
                        (x.metadataManager.metadata.homepage == metadataManager.metadata.homepage)).ToList());
                }
                // homepageとlastupdate
                if (Global.ConfigToml.ModsListDuplicatioCheckModJsonLastupdate
                    && !string.IsNullOrEmpty(metadataManager?.metadata?.homepage?.ToString())
                    && metadataManager?.metadata?.lastupdate != null)
                {
                    Error_57_List.AddRange(Global.ModList_All.Where(x =>
                        (x.metadataManager.metadata.homepage == metadataManager.metadata.homepage)
                        && x.metadataManager.metadata.lastupdate == metadataManager.metadata.lastupdate).ToList());
                }
            }
            // フォルダサイズを検証
            if (Global.ConfigToml.ModsListDuplicatioCheckSize)
            {
                // config.tomlのname
                if (Global.ConfigToml.ModsListDuplicatioCheckConfigTomlName)
                {
                    Error_57_List = Error_57_List.Where(
                        x => directorySize != 0
                        && x.directorySize == directorySize
                        && x.ConfigTomlModName == ConfigTomlModName).ToList();
                }
                // mod.jsonのhomepage
                if (Global.ConfigToml.ModsListDuplicatioCheckModJsonHomepage
                    && metadataManager != null && metadataManager.metadata != null)
                {
                    Error_57_List = Error_57_List.Where(
                        x => directorySize != 0
                        && x.directorySize == directorySize
                        && x.metadataManager.metadata.homepage == metadataManager.metadata.homepage).ToList();
                }
            }

            Error_57_List = Error_57_List.Distinct().Where(x => x.directory_path != directory_path).ToList();   // 重複削除
            if (Error_57_List.Count > 0)
            {
                // エラー文字列生成用にnameだけ抽出
                var sameModNameList = Error_57_List.Select(x => x.name).ToList();
                sameModNameList.Insert(0, name);
                sameModNameList.Sort();
                var sameModListDirStr = $"{prefix}{string.Join($"\n{prefix}", sameModNameList)}";
                var addError = WindowList.MessageWindowNo(57, new List<string>() { sameModListDirStr });
                if (!Errors.Any(x => x.ID == 57)) Errors.Add(addError);
                // 一致判定されたModのErrorsに情報を追加する
                // 理由：New Classic → New Classic (1)は検索できるが、逆は不可のため
                foreach (var sameMod in Error_57_List)
                {
                    if (!Global.ModList_All.Where(x => x.directory_name == sameMod.directory_name).FirstOrDefault()
                        .Errors.Where(x => x.ID == 57).Any())
                    {
                        Global.ModList_All.Where(x => x.directory_name == sameMod.directory_name).FirstOrDefault()
                            .Errors.Add(addError);
                    }
                }
            }

            // MM+、DMLで取り込み済(0058W)
            if (metadataManager != null && metadataManager.metadata != null && metadataManager.metadata.homepage != null
                && WindowList.WINDOW_LIST_NO_58.ContainsKey(metadataManager.metadata.homepage.ToString()))
            {
                var replaceList = new List<string>() { DMLUpdater.MODULE_NAME };
                var addError = WindowList.MessageWindowNo(58, replaceList);
                if (!Errors.Any(x => x.ID == 58)) Errors.Add(addError);
            }

            // セーブデータ注意(0059W)
            var Error_59_List = Global.ModList_All.Where(x => x.directory_name != directory_name).ToList();
            if (!string.IsNullOrEmpty(metadataManager?.metadata?.homepage?.ToString())
                && WindowList.WINDOW_LIST_NO_59.Contains(metadataManager?.metadata?.homepage?.ToString()))
            {
                var addError = WindowList.MessageWindowNo(59);
                if (!Errors.Any(x => x.ID == 59)) Errors.Add(addError);
            }

            // 日本語文字(0073E)
            if (Encoding.GetEncoding("Shift_JIS").GetByteCount(name) > name.Length)
            {
                var addError = WindowList.MessageWindowNo(73, new List<string>());
                if (!Errors.Any(x => x.ID == 73)) Errors.Add(addError);
            }

            //var dumpStr = ObjectDumper.Dump(Errors, "Errors");
            //Logger.WriteLine($"CheckErrorAndWarning End. modName:{name}", LoggerType.Debug, dump:dumpStr);

            CheckErrorWarningAlready = true;

            // 描画を更新させる
            OnPropertyChanged("IsError");
            OnPropertyChanged("IsWarn");
        }

        [JsonIgnore]
        public SongData songData = new();

        [JsonIgnore]
        public ModuleData moduleData = new();
    }

    public class SearchMod : Mod
    {
        // enabled
        public override bool enabled { get => _enabled ?? false; set => _enabled = value; }
        public bool? isEnabledSearch { get; set; }
        // note
        private string? _note { get; set; } = null;
        public override string note { get => _note ?? string.Empty; set => _note = value ?? string.Empty; }
        // category
        public override string? category { get; set; }
        public bool? isCategorySearch { get; set; }

    }
}
