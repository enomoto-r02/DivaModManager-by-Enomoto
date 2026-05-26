using DivaModManager.Common.ExtendToml;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using Tomlyn;
using Tomlyn.Model;

#nullable enable

namespace DivaModManager.Common.Config
{
    public class ConfigTomlMod : ITomlMetadataProvider
    {
        TomlPropertiesMetadata? ITomlMetadataProvider.PropertiesMetadata { get; set; }

        [IgnoreDataMember]
        private string ConfigETomlFileName = "config_e.toml";

        [DataMember(Name = "create_divamodmanager_version")]
        [DataMemberCommentEN("This is the version of DivaModManager that updated this file.")]
        [DataMemberCommentJP("このファイルを更新したDivaModManagerのバージョンです。")]
        public string CurrentVersion
        {
            get { return _CurrentVersion; }
            set
            {
                _CurrentVersion = value;
            }
        }
        private string _CurrentVersion { get; set; } = "1.3.1.0";

        [DataMember(Name = "create_datetime")]
        [DataMemberCommentEN("The date and time this file was created.")]
        [DataMemberCommentJP("このファイルの作成日時です。")]
        public string CreateDateTime
        {
            get { return string.IsNullOrEmpty(_CreateDateTime) ? DateTime.Now.ToString() : _CreateDateTime; }
            set { _CreateDateTime = value; }
        }
        private string? _CreateDateTime { get; set; } = string.Empty;

        [DataMember(Name = "last_update_time")]
        [DataMemberCommentEN("The date and time this file was last updated.")]
        [DataMemberCommentJP("このファイルの最終更新日時です。")]
        public string LastUpdateDateTime
        {
            get { return string.IsNullOrEmpty(_LastUpdateDateTime) ? DateTime.Now.ToString() : _LastUpdateDateTime; }
            set { _LastUpdateDateTime = value; }
        }
        private string? _LastUpdateDateTime { get; set; } = string.Empty;

        [DataMember(Name = "ng_id_list")]
        [DataMemberCommentEN("Enter the WINDOW-ID number to suppress warnings and errors that appear when displaying the mod list. \nThis setting is overridden by the ng_id_list in the DivaModManager folder. \nExample: ng_id_list = [0, 9999]")]
        [DataMemberCommentJP("Mod一覧を表示した際に表示される警告やエラーを抑制するWINDOW-IDの数字を入力してください。\nこの設定よりもDivaModManagerフォルダにあるng_id_listの方が優先されます。\n例：ng_id_list = [0, 9999]")]
        public TomlArray NG_ID_List { get; set; } = new() { };

        [DataMember(Name = "priority")]
        [DataMemberCommentEN("Please use this to sort by priority, etc. Valid values ​​are 8 digits. \nThis item can be set in the DivaModManager screen.")]
        [DataMemberCommentJP("優先度などを記載しソートに活用してください。有効値は数字8桁です。\nこの項目はDivaModManagerの画面内で設定できます。")]
        public string? Priority { get; set; } = string.Empty;

        [DataMember(Name = "note")]
        [DataMemberCommentEN("Please write down any notes you would like to use for searching.\nThis item can be set within the DivaModManager screen.")]
        [DataMemberCommentJP("メモなどを記載し検索に活用してください。\nこの項目はDivaModManagerの画面内で設定できます。")]
        public string? Note { get; set; } = string.Empty;

        [DataMember(Name = "category")]
        [DataMemberCommentEN("Please enter a category and use it as a filter.\nThis item can be set within the DivaModManager screen.")]
        [DataMemberCommentJP("カテゴリを記載しフィルタに活用してください。\nこの項目はDivaModManagerの画面内で設定できます。")]
        public string? Category { get; set; } = string.Empty;

        public ConfigTomlMod()
        {
        }

        public bool Init(Mod mod, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Mod.Name:{mod.name}";

            var ret = false;
            var modName = mod.name;
            var directoryPath = mod.directory_path;
            var configETomlFilePath = $"{directoryPath}{Global.s}{ConfigETomlFileName}";
            if (FileHelper.FileExists(configETomlFilePath))
            {
                try
                {
                    string content = FileHelper.TryReadAllText(configETomlFilePath, eraseCommentLine: true);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        mod.ConfigToml = Toml.ToModel<ConfigTomlMod>(content, options: new TomlModelOptions() { IgnoreMissingProperties = true });
                        ret = true;
                    }
                }
                catch (Exception)
                {
                    Logger.WriteLine($"Initialization was performed because {configETomlFilePath} could not be read.", LoggerType.Error);
                    var configEfilePath = FileHelper.CopyFile(configETomlFilePath, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {ConfigETomlFileName} and generate a new one. Save file name:{new FileInfo(configEfilePath).Name}", LoggerType.Error, param: ParamInfo);

                }
            }

            return ret;
        }

        public bool Update(Mod mod, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Mod.Name:{mod.name}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            var modName = mod.name;
            var directoryPath = mod.directory_path;
            var configETomlFilePath = $"{directoryPath}{Global.s}{ConfigETomlFileName}";

            try
            {
                // 元の設定ファイルのバージョンが異なる場合はバックアップを取る
                if (FileHelper.FileExists(configETomlFilePath) && VersionHelper.CompareVersions(App.Version, mod.ConfigToml.CurrentVersion) != VersionHelper.Result.SAME)
                {
                    var backupFilePath = FileHelper.CopyFile(configETomlFilePath, oldVersion: mod.ConfigToml.CurrentVersion, IsOriginalFileDelete: true);
                }
                var now = DateTime.Now.ToString();
                if (string.IsNullOrEmpty(CreateDateTime)) { CreateDateTime = now; }
                LastUpdateDateTime = now;
                CurrentVersion = App.Version;
                var tomlStr = TomlWithComments.SerializeWithComments(this);
                ret = FileHelper.TryWriteAllText(configETomlFilePath, tomlStr);
                if (ret)
                    Global.ModList_All.FirstOrDefault(x => x.directory_path == directoryPath).ConfigToml = this;
            }
            catch (AmbiguousMatchException amex)
            {
                Logger.WriteLine($"AmbiguousMatchException error writing {configETomlFilePath}: Message:{amex.Message}.\nUsing default {configETomlFilePath} in {AssemblyName.GetAssemblyName}.", LoggerType.Error);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Exception error writing {configETomlFilePath}: Message:{ex.Message}.\nUsing default {configETomlFilePath} in {AssemblyName.GetAssemblyName}.", LoggerType.Error);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }
    }
}
