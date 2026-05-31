using DivaModManager.Common.ExtendToml;
using DivaModManager.Common.Helpers;
using DivaModManager.Common.MessageWindow;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Extract;
using System;
using System.Collections.Generic;
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
    public class ConfigTomlDmm : ITomlMetadataProvider
    {
        TomlPropertiesMetadata? ITomlMetadataProvider.PropertiesMetadata { get; set; }

        public enum Lang
        {
            None,
            JP,
            EN,
        }

        [IgnoreDataMember]
        public static readonly string CONFIG_E_TOML_NAME = $"config_e.toml";
        [IgnoreDataMember]
        public static readonly string CONFIG_E_TOML_PATH = Path.Combine(Global.assemblyLocation, CONFIG_E_TOML_NAME);
        [IgnoreDataMember]
        public ExtractInfo.EXTERNAL_EXTRACTOR ExtractorWinRar { get; set; } = ExtractInfo.EXTERNAL_EXTRACTOR.NOT_CHECK;
        [IgnoreDataMember]
        public ExtractInfo.EXTERNAL_EXTRACTOR ExtractorSevenZip { get; set; } = ExtractInfo.EXTERNAL_EXTRACTOR.NOT_CHECK;

        [IgnoreDataMember]
        public List<WindowInfo> Error { get; set; } = new();

        [DataMember(Name = "current_version")]
        [DataMemberCommentEN("This is the version of DivaModManager that updated this file.")]
        [DataMemberCommentJP("このファイルを更新したDivaModManagerのバージョンです。")]
        public string CurrentVersion
        {
            get { return _CurrentVersion; }
            set { _CurrentVersion = value; }
        }
        private string? _CurrentVersion { get; set; } = "1.3.1.0";

        [DataMember(Name = "create_datetime")]
        [DataMemberCommentEN("The date and time this file was created.")]
        [DataMemberCommentJP("このファイルの作成日時です。")]
        public string CreateDateTime
        {
            get { return string.IsNullOrEmpty(_CreateDateTime) ? DateTime.Now.ToString() : _CreateDateTime; }
            private set { _CreateDateTime = value; }
        }
        private string? _CreateDateTime { get; set; } = string.Empty;

        [DataMember(Name = "last_update_time")]
        [DataMemberCommentEN("The date and time this file was last updated.")]
        [DataMemberCommentJP("このファイルの最終更新日時です。")]
        public string LastUpdateDateTime
        {
            get { return string.IsNullOrEmpty(_LastUpdateDateTime) ? CreateDateTime : _LastUpdateDateTime; }
            private set { _LastUpdateDateTime = value; }
        }
        private string? _LastUpdateDateTime { get; set; } = string.Empty;

        [DataMember(Name = "language")]
        [DataMemberCommentEN("Set the tool language. Supported languages are 'JP' and 'EN'.\nDefault : \"EN\"")]
        [DataMemberCommentJP("ツールの言語を設定します。サポートされている言語は\"JP\"と\"EN\"です。\nDefault：\"EN\"")]
        public string Language
        {
            get { return _Language == null ? string.Empty : _Language.ToUpper(); }
            set { _Language = value.ToUpper(); }
        }
        private string? _Language { get; set; } = Lang.EN.ToString();

        [DataMember(Name = "language_dialog")]
        [DataMemberCommentEN("Show language selection window.\nDefault : true")]
        [DataMemberCommentJP("起動時に言語選択ウィンドウを表示します。\nDefault：true")]
        public bool LanguageDialog
        {
            get { return _LanguageDialog != null && (bool)_LanguageDialog; }
            set { _LanguageDialog = value; }
        }
        private bool? _LanguageDialog { get; set; } = true;

        [DataMember(Name = "divamodloader_update_check")]
        [DataMemberCommentEN("Check for updates to DivaModLoader when the tool starts.\nDefault : false")]
        [DataMemberCommentJP("ツールの起動時にDivaModLoader のアップデートを確認します。\nDefault：false")]
        public bool DivaModLoaderUpdateCheck
        {
            get { return _DivaModLoaderUpdateCheck != null && (bool)_DivaModLoaderUpdateCheck; }
            set { _DivaModLoaderUpdateCheck = value; }
        }
        private bool? _DivaModLoaderUpdateCheck { get; set; } = false;

        [DataMember(Name = "divamodmanager_update_check")]
        [DataMemberCommentEN("Check for updates to DivaModManager by Enomoto when the tool starts.\nDefault : false")]
        [DataMemberCommentJP("ツールの起動時にDivaModManager by Enomoto のアップデートを確認します。\nDefault：false")]
        public bool DivaModManagerUpdateCheck
        {
            get { return _DivaModManagerUpdateCheck != null && (bool)_DivaModManagerUpdateCheck; }
            set { _DivaModManagerUpdateCheck = value; }
        }
        private bool? _DivaModManagerUpdateCheck { get; set; } = false;

        [DataMember(Name = "github_api_timeout")]
        [DataMemberCommentEN("GitHub API timeout in seconds.\n(we are currently testing whether this parameter works as expected)\nDefault : 100")]
        [DataMemberCommentJP("GitHub API のタイムアウト（単位：秒）\n（現在、このパラメータが期待どおりに動作するかどうかをテスト中です）\nDefault：100")]
        public int GitHubApiTimeoutSec
        {
            get { return _GitHubApiTimeoutSec == null ? 100 : (int)_GitHubApiTimeoutSec; }
            set { _GitHubApiTimeoutSec = value; }
        }
        private int? _GitHubApiTimeoutSec { get; set; } = 100;

        [DataMember(Name = "gamebanana_api_timeout")]
        [DataMemberCommentEN("GameBanana API timeout in seconds.\n(we are currently testing whether this parameter works as expected)\nDefault : 100")]
        [DataMemberCommentJP("GameBanana API のタイムアウト（単位：秒）（現在、このパラメータが期待どおりに動作するかどうかをテスト中です）\nDefault：100")]
        public int GameBananaApiTimeoutSec
        {
            get { return _GameBananaApiTimeoutSec == null ? 100 : (int)_GameBananaApiTimeoutSec; }
            set { _GameBananaApiTimeoutSec = value; }
        }
        private int? _GameBananaApiTimeoutSec { get; set; } = 100;

        [DataMember(Name = "divamodarchive_api_timeout")]
        [DataMemberCommentEN("DivaModArchive API timeout in seconds.\n(we are currently testing whether this parameter works as expected)\nDefault : 100")]
        [DataMemberCommentJP("DivaModArchive API のタイムアウト（単位：秒）\n（現在、このパラメータが期待どおりに動作するかどうかをテスト中です）\nDefault：100")]
        public int DivaModArchiveApiTimeoutSec
        {
            get { return _DivaModArchiveApiTimeoutSec == null ? 100 : (int)_DivaModArchiveApiTimeoutSec; }
            set { _DivaModArchiveApiTimeoutSec = value; }
        }
        private int? _DivaModArchiveApiTimeoutSec { get; set; } = 100;

        [DataMember(Name = "browser_user_agent")]
        [DataMemberCommentEN("Set the UserAgent (do not change from default unless necessary)\nDefault : \"Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36\"")]
        [DataMemberCommentJP("UserAgentを設定します（必要がない限りデフォルトから変更しないでください）\nDefault：\"Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36\"")]
        public string BrowserUserAgent
        {
            get { return _BrowserUserAgent == null ? "\"Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36\";" : (string)_BrowserUserAgent; }
            set { _BrowserUserAgent = value; }
        }
        private string? _BrowserUserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

        [DataMember(Name = "gamebanana_cache_control_max_age")]
        [DataMemberCommentEN("GameBanana's Cache-Control (Unit: seconds)\nDefault : 600")]
        [DataMemberCommentJP("GameBananaのCache-Control（単位：秒）\nDefault：600")]
        public int GameBananaCacheControlMaxAge
        {
            get { return _GameBananaCacheControlMaxAge == null ? 600 : (int)_GameBananaCacheControlMaxAge; }
            set { _GameBananaCacheControlMaxAge = value; }
        }
        private int? _GameBananaCacheControlMaxAge { get; set; } = 600;

        [DataMember(Name = "diva_mod_archive_cache_control_max_age")]
        [DataMemberCommentEN("DivaModArchive's Cache-Control (Unit: seconds)\nDefault : 600")]
        [DataMemberCommentJP("DivaModArchiveのCache-Control（単位：秒）\nDefault：600")]
        public int DivaModArchiveCacheControlMaxAge
        {
            get { return _DivaModArchiveCacheControlMaxAge == null ? 600 : (int)_DivaModArchiveCacheControlMaxAge; }
            set { _DivaModArchiveCacheControlMaxAge = value; }
        }
        private int? _DivaModArchiveCacheControlMaxAge { get; set; } = 600;

        [DataMember(Name = "git_hub_cache_control_max_age")]
        [DataMemberCommentEN("GitHub's Cache-Control (Unit: seconds)\nDefault : 600")]
        [DataMemberCommentJP("GitHubのCache-Control（単位：秒）\nDefault：600")]
        public int GitHubCacheControlMaxAge
        {
            get { return _GitHubCacheControlMaxAge == null ? 600 : (int)_GitHubCacheControlMaxAge; }
            set { _GitHubCacheControlMaxAge = value; }
        }
        private int? _GitHubCacheControlMaxAge { get; set; } = 600;

        // 注意：使用する場合でもバージョンチェックは毎回行う
        [DataMember(Name = "winrar_check_dialog")]
        [DataMemberCommentEN("Use WinRAR to extract the .rar file? pop-up window.\nDefault : false")]
        [DataMemberCommentJP("起動時に「WinRARの使用を確認する」ポップアップを表示します。\nDefault：false")]
        public bool WinRarCheckDialog
        {
            get { return _WinRarCheckDialog != null && (bool)_WinRarCheckDialog; }
            set { _WinRarCheckDialog = value; }
        }
        private bool? _WinRarCheckDialog { get; set; } = false;

        [DataMember(Name = "winrar_use")]
        [DataMemberCommentEN("Current WinRAR usage setting.(To be implemented in the future)\nDefault : false")]
        [DataMemberCommentJP("現在の WinRAR の使用設定(今後実装予定)\nDefault：false")]
        public bool WinRarUse
        {
            get { return _WinRarUse != null && (bool)_WinRarUse; }
            set { _WinRarUse = value; }
        }
        private bool? _WinRarUse { get; set; } = false;

        [DataMember(Name = "sevenzip_check_dialog")]
        [DataMemberCommentEN("Use 7-Zip to extract the .7z file? pop-up window.\nDefault : true")]
        [DataMemberCommentJP("起動時に「7-Zipの使用を確認する」ポップアップを表示します。\nDefault：true")]
        public bool SevenZipCheckDialog
        {
            get { return _SevenZipCheckDialog != null && (bool)_SevenZipCheckDialog; }
            set { _SevenZipCheckDialog = value; }
        }
        private bool? _SevenZipCheckDialog { get; set; } = true;

        // 注意：使用する場合でもバージョンチェックは毎回行う
        [DataMember(Name = "sevenzip_use")]
        [DataMemberCommentEN("Current 7-Zip usage setting.\nDefault : true")]
        [DataMemberCommentJP("現在の 7-Zip の使用設定\nDefault：true")]
        public bool SevenZipUse
        {
            get { return _SevenZipUse != null && (bool)_SevenZipUse; }
            set { _SevenZipUse = value; }
        }
        private bool? _SevenZipUse { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private String WinRarUseView()
        {
            return WinRarUse ? "W" : string.Empty;
        }
        private String SevenZipUseView()
        {
            return SevenZipUse ? "S" : string.Empty;
        }
        public String ExternalExtractorUseView()
        {
            var s = WinRarUseView() + SevenZipUseView();
            if (s.Length > 1) { s = string.Join("/", s.ToList()); }
            return $"{s}";
        }
        public String ExternalExtractorUseToolTip()
        {
            // 要Linux対応
            return $"WinRAR({Global.ConfigJson?.WinRarConsoleVersion})/7-Zip({Global.ConfigJson?.SevenZipConsoleVersion})";
        }

        [DataMember(Name = "mods_list_duplication_check_config_toml_name")]
        [DataMemberCommentEN("Compares the mod name (\"name\" in config.toml) when checking for duplicate mod matches. \nDefault: true")]
        [DataMemberCommentJP("Mod重複チェック時にMod名(config.tomlの\"name\")を比較します。\nDefault：true")]
        public bool ModsListDuplicatioCheckConfigTomlName
        {
            get { return _ModsListDuplicatioCheckConfigTomlName != null && (bool)_ModsListDuplicatioCheckConfigTomlName; }
            set { _ModsListDuplicatioCheckConfigTomlName = value; }
        }
        private bool? _ModsListDuplicatioCheckConfigTomlName { get; set; } = true;

        [DataMember(Name = "mods_list_duplication_check_config_toml_version")]
        [DataMemberCommentEN("Compares the version (\"version\" in config.toml) when checking for duplicate mod matches.\nDefault: false")]
        [DataMemberCommentJP("Mod重複チェック時にバージョン(config.tomlの\"version\")を比較します。\nDefault：false")]
        public bool ModsListDuplicatioCheckConfigTomlVersion
        {
            get { return _ModsListDuplicatioCheckConfigTomlVersion != null && (bool)_ModsListDuplicatioCheckConfigTomlVersion; }
            set { _ModsListDuplicatioCheckConfigTomlVersion = value; }
        }
        private bool? _ModsListDuplicatioCheckConfigTomlVersion { get; set; } = false;

        [DataMember(Name = "mods_list_duplication_check_mod_json_homepage")]
        [DataMemberCommentEN("When checking for duplicate mod matches, the download source URL (\"homepage\" in mod.json) is compared. \nNote: Mods with different content may be detected unintentionally if they are distributed under the same URL. In that case, please compare \"name\" in config.toml at the same time. \nDefault: false")]
        [DataMemberCommentJP("Mod重複チェック時にダウンロード元URL(mod.jsonの\"homepage\")を比較します。\n注意：内容が異なるModでも、同一URLで配布されている場合は意図しない形で検出されることがあります。その場合はconfig.tomlの\"name\"を同時に比較してください。\nDefault：false")]
        public bool ModsListDuplicatioCheckModJsonHomepage
        {
            get { return _ModsListDuplicatioCheckModJsonHomepage != null && (bool)_ModsListDuplicatioCheckModJsonHomepage; }
            set { _ModsListDuplicatioCheckModJsonHomepage = value; }
        }
        private bool? _ModsListDuplicatioCheckModJsonHomepage { get; set; } = false;

        [DataMember(Name = "mods_list_duplication_check_mod_json_lastupdate")]
        [DataMemberCommentEN("When checking for duplicate Mod matches, the last update date (\"lastupdate\" in mod.json) is compared. \nNote: Mods with different content may be detected unintentionally if they are distributed under the same URL. In that case, please compare \"name\" in config.toml at the same time. \nDefault: false")]
        [DataMemberCommentJP("Mod重複チェック時に最終更新日(mod.jsonの\"lastupdate\")を比較します。\n注意：内容が異なるModでも、同一URLで配布されている場合は意図しない形で検出されることがあります。その場合はconfig.tomlの\"name\"を同時に比較してください。\nDefault：false")]
        public bool ModsListDuplicatioCheckModJsonLastupdate
        {
            get { return _ModsListDuplicatioCheckModJsonLastupdate != null && (bool)_ModsListDuplicatioCheckModJsonLastupdate; }
            set { _ModsListDuplicatioCheckModJsonLastupdate = value; }
        }
        private bool? _ModsListDuplicatioCheckModJsonLastupdate { get; set; } = false;

        [DataMember(Name = "mods_list_duplication_check_compare_size")]
        [DataMemberCommentEN("Compare folder sizes when checking for duplicate mod matches.\nDefault : true")]
        [DataMemberCommentJP("Mod重複チェック時にフォルダサイズを比較します。\nDefault：true")]
        public bool ModsListDuplicatioCheckSize
        {
            get { return _ModsListDuplicatioCheckSize != null && (bool)_ModsListDuplicatioCheckSize; }
            set { _ModsListDuplicatioCheckSize = value; }
        }
        private bool? _ModsListDuplicatioCheckSize { get; set; } = true;

        [DataMember(Name = "ng_id_list")]
        [DataMemberCommentEN("Enter the WINDOW-ID to suppress warnings and errors when displaying the mod list.\nThis setting takes precedence over the ng_id_list in each mod folder.\nExample: ng_id_list = [0, 9999]")]
        [DataMemberCommentJP("Mod一覧を表示した際に表示される警告やエラーを抑制するWINDOW-IDを入力してください。\nこちらの設定は各Modフォルダにあるng_id_listよりも優先されます。\n例：ng_id_list = [0, 9999]")]
        public TomlArray NG_ID_List { get; set; } = new() { };

        [DataMember(Name = "mask_log")]
        [DataMemberCommentEN("Masks personal information (such as various directory information) from log file output.\nDefault : true")]
        [DataMemberCommentJP("ログファイル出力に個人情報 (各種ディレクトリ情報など) をマスクします。\nDefault：true")]
#if DEBUG
        public bool MaskTextLog { get; set; } = false;
#else
        public bool MaskTextLog { get; set; } = true;
#endif

        [DataMember(Name = "screenshot_dpi")]
        [DataMemberCommentEN("DPI of screenshot (100 to 300)\nOther values ​​will be set to Default.\nDefault : 200")]
        [DataMemberCommentJP("スクリーンショットのDPI（範囲：100〜300）\n不正な値の場合、デフォルト値が設定されます。\nDefault：200")]
        public int ScreenShotDPI
        {
            get { return _ScreenShotDPI; }
            set
            {
                if (value <= 100) _ScreenShotDPI = 200;
                if (value > 300) _ScreenShotDPI = 300;
            }
        }
        private int _ScreenShotDPI { get; set; } = 200;

        [DataMember(Name = "screenshot_max_line")]
        [DataMemberCommentEN("The maximum number of lines per screenshot. If you set this to 0, all lines will be output to a single image file.\nIf both screenshot_max_line and screenshot_max_pixel are set, the image will be split when either condition is met.\nDefault : 50")]
        [DataMemberCommentJP("スクリーンショット1枚あたりの最大行数。0に設定すると、すべての行が1つの画像ファイルに出力されます。\nscreenshot_max_lineとscreenshot_max_pixelの両方が設定されていた場合、どちらか一方の条件を満たした時点で画像が分割されます。\nDefault：50")]
        public int ScreenShotMaxLine
        {
            get { return _ScreenShotMaxLine == null ? 50 : (int)_ScreenShotMaxLine; }
            set { _ScreenShotMaxLine = value; }
        }
        private int? _ScreenShotMaxLine { get; set; } = 50;

        [DataMember(Name = "screenshot_max_pixel")]
        [DataMemberCommentEN("The maximum number of pixels per screenshot. If you set this to 0, all lines will be output to a single image file.\nIf both screenshot_max_line and screenshot_max_pixel are set, the image will be split when either condition is met.\nDefault : 0")]
        [DataMemberCommentJP("スクリーンショット1枚あたりの最大ピクセル数。0に設定すると、すべての行が1つの画像ファイルに出力されます。\nscreenshot_max_lineとscreenshot_max_pixelの両方が設定されていた場合、どちらか一方の条件を満たした時点で画像が分割されます。\nDefault：0")]
        public int ScreenShotMaxPixel
        {
            get { return _ScreenShotMaxPixel == null ? 0 : (int)_ScreenShotMaxPixel; }
            set { _ScreenShotMaxPixel = value; }
        }
        private int? _ScreenShotMaxPixel { get; set; } = 0;

        [DataMember(Name = "doubleclick_event")]
        [DataMemberCommentEN("Action to take when double-clicking on a mod list.\nPlease enter one of open, configure, homepage, or nothing.\nDefault : \"open\"")]
        [DataMemberCommentJP("Mod一覧をダブルクリックしたときに実行するアクションです。\n有効な値は open, configure, homepage, nothingです。\nDefault：\"open\"")]
        public string DoubleClickEvent
        {
            get { return _DoubleClickEvent == null ? "open" : _DoubleClickEvent.ToLowerInvariant(); }
            set { _DoubleClickEvent = value; }
        }
        private string? _DoubleClickEvent { get; set; } = "open";

        [DataMember(Name = "overwrite_log")]
        [DataMemberCommentEN("Delete previous log files on startup.\nDefault : true")]
        [DataMemberCommentJP("起動時に前回のログファイルを削除します。\nDefault：true")]
        public bool OverwriteLog
        {
            get { return _OverwriteLog != null && (bool)_OverwriteLog; }
            set { _OverwriteLog = value; }
        }
        private bool? _OverwriteLog { get; set; } = true;

        [DataMember(Name = "delete_directory_warning_size")]
        [DataMemberCommentEN("This message appears when the folder you are trying to delete is larger than a certain size. \nThis is to prevent accidental deletion by the tool.\n(Unit: MB) Default : 5000")]
        [DataMemberCommentJP("削除するModフォルダサイズがこの値よりも大きい場合にポップアップを表示します。\n（ツールの誤動作によるフォルダの削除を防止する目的です）\n (単位：MB) Default：5000")]
        public long WarningDeleteDirectorySize
        {
            get { return _WarningDeleteDirectorySize == null ? 5000 : (long)_WarningDeleteDirectorySize; }
            set { _WarningDeleteDirectorySize = value; }
        }
        private long? _WarningDeleteDirectorySize { get; set; } = 5000;


        [DataMember(Name = "temporary_directory_size")]
        [DataMemberCommentEN("Set the folder size of the popup that appears when the temporary folder capacity is exceeded.\n(Unit: MB) Default : 1000")]
        [DataMemberCommentJP("一時フォルダがこの値よりも大きい場合に表示されるポップアップを表示します。\n(単位：MB) Default：1000")]
        public long WarningTemporaryDirectorySize
        {
            get { return _WarningTemporaryDirectorySize == null ? 1000 : (long)_WarningTemporaryDirectorySize; }
            set { _WarningTemporaryDirectorySize = value; }
        }
        private long? _WarningTemporaryDirectorySize { get; set; } = 1000;


        [DataMember(Name = "gamebanana_tab_enable")]
        [DataMemberCommentEN("Whether or not you can press the GameBanana tab. If you want to deny GameBanana connections for any reason, set this to false.\nDefault : true")]
        [DataMemberCommentJP("GameBananaタブを有効にします。何らかの理由でGameBananaの接続を拒否したい場合はfalseに設定してください。\nDefault：true")]
        public bool GameBananaTabEnable { get; set; } = true;

        [DataMember(Name = "divamodarchive_tab_enable")]
        [DataMemberCommentEN("Whether or not you can press the Diva Mod Archive tab. If you want to deny Diva Mod Archive connections for any reason, set this to false.\nDefault : true")]
        [DataMemberCommentJP("Diva Mod Archiveタブを有効にします。何らかの理由でDiva Mod Archiveの接続を拒否したい場合はfalseに設定してください。\nDefault：true")]
        public bool DivaModArchiveTabEnable { get; set; } = true;

        [DataMember(Name = "debug_dialog")]
        [DataMemberCommentEN("Displaying a pop-up window when the debug tab is displayed.\nDefault : true")]
        [DataMemberCommentJP("デバッグタブが表示された時にポップアップを表示します。\nDefault：true")]
        public bool DebugDialog
        {
            get { return _DebugDialog != null && (bool)_DebugDialog; }
            set { _DebugDialog = value; }
        }
        private bool? _DebugDialog { get; set; } = true;

        [DataMember(Name = "drop_divamodloader_check")]
        [DataMemberCommentEN("Enables installation of DivaModLoader by drag and drop. \n (This feature is in the experimental stage. If DivaModLoader is not recognized correctly set it to false, and install it using the Update Check button instead of drag and drop.)\nDefault : true")]
        [DataMemberCommentJP("DivaModLoaderをドラッグ＆ドロップでインストール可能にします。\n（この機能は試験段階です。DivaModLoaderが正しく認識されない場合はfalseに設定して、ドラッグ＆ドロップではなくUpdate Checkボタンからインストールしてください。）\nDefault：true")]
        public bool DropDivaModLoaderCheck
        {
            get { return _DropDivaModLoaderCheck != null && (bool)_DropDivaModLoaderCheck; }
            set { _DropDivaModLoaderCheck = value; }
        }
        private bool? _DropDivaModLoaderCheck { get; set; } = true;

        [DataMember(Name = "conflict_mod_when_create_screenshot")]
        [DataMemberCommentEN("When you press the screenshot button, a popup will appear saying \"About conflicts between multiple mods.\"\nDefault : true")]
        [DataMemberCommentJP("スクリーンショットボタンを押した時に\"複数Mod間の競合について\"のポップアップを表示する。\nDefault：true")]
        public bool ConflictModWhenCreateScreenShot
        {
            get { return _ConflictModWhenCreateScreenShot != null && (bool)_ConflictModWhenCreateScreenShot; }
            set { _ConflictModWhenCreateScreenShot = value; }
        }
        private bool? _ConflictModWhenCreateScreenShot { get; set; } = true;

        public ConfigTomlDmm()
        {
        }

        // 読み込んだtomlにエラー項目があったか
        // todo: Tomlynで個々の項目をチェックできるらしい？
        // https://github.com/xoofx/Tomlyn/blob/main/doc/readme.md

        /// <summary>
        /// 
        /// </summary>
        /// <param name="caller"></param>
        /// <returns>既存のファイルを読み込んだ場合にtrue</returns>
        public static bool InitToml([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            var ret = false;

            if (FileHelper.FileExists(CONFIG_E_TOML_PATH))
            {
                try
                {
                    string content = FileHelper.TryReadAllText(CONFIG_E_TOML_PATH, eraseCommentLine: true);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Logger.WriteLine($"Toml file content is empty or whitespace: {CONFIG_E_TOML_PATH}", LoggerType.Warning);
                        return false;
                    }
                    Global.ConfigToml = Toml.ToModel<ConfigTomlDmm>(content);
                    if (VersionHelper.CompareVersions(App.Version, Global.ConfigToml.CurrentVersion) != VersionHelper.Result.SAME)
                    {
                        var backupFilePath = FileHelper.CopyFile(CONFIG_E_TOML_PATH, oldVersion: Global.ConfigToml.CurrentVersion, IsOriginalFileDelete: true);
                        Global.ConfigToml.CurrentVersion = App.Version;
                    }
                    ret = true;
                }
                catch (Exception)
                {
                    Logger.WriteLine($"Initialization was performed because {CONFIG_E_TOML_PATH} could not be read.", LoggerType.Error, param: ParamInfo);
                    var backupFilePath = FileHelper.CopyFile(CONFIG_E_TOML_PATH, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_E_TOML_NAME} and generate a new one. Save file name:{Path.GetFileName(backupFilePath)}", LoggerType.Error, param: ParamInfo);
                }
            }
            Global.ConfigToml.Update();

            if (Global.ConfigToml.OverwriteLog)
            {
                FileHelper.DeleteFile(Global.textLogLocation);
                FileHelper.DeleteFile(Global.textLogBackgroundLocation);
            }

            Logger.WriteLine($"{MeInfo} End.", LoggerType.Debug, param: $"Return:{ret}");
            return ret;
        }

        public bool Update()
        {
            var ret = false;
            try
            {
                Global.ConfigToml.LastUpdateDateTime = DateTime.Now.ToString();
                Global.ConfigToml.CurrentVersion = App.Version;
                var tomlStr = TomlWithComments.SerializeWithComments(Global.ConfigToml);
                ret = FileHelper.TryWriteAllText(CONFIG_E_TOML_PATH, tomlStr);
            }
            catch (AmbiguousMatchException amex)
            {
                Logger.WriteLine($"AmbiguousMatchException error loading {CONFIG_E_TOML_PATH}: Message:{amex.Message}.\nUsing default {CONFIG_E_TOML_NAME} in {AssemblyName.GetAssemblyName}.", LoggerType.Error);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Exception error loading {CONFIG_E_TOML_PATH}: Message:{ex.Message}.\nUsing default {CONFIG_E_TOML_NAME} in {AssemblyName.GetAssemblyName}.", LoggerType.Error);
            }
            return ret;
        }
    }
}
