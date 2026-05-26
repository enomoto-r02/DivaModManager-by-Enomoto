using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.DML;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;

namespace DivaModManager.Models
{

    public class GameConfig
    {
        // 起動時に毎回以下の優先度でベースのパスを決定
        // 1. レジストリ(コンピューター\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1761390\InstallLocation) + "DivaMegaMix.exe"
        // 2. "C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaMegaMix.exe"
        // 3. レジストリが無い場合はOpenFileDialogでユーザーが指定したフォルダの"DivaMegaMix.exe"
        // 4. パス無しでスタートアップ処理を継続する、格納される値はnullの可能性あり
        // ファイル参照ダイアログの結果見つからなかったらGenericメソッドがfalseになり、SetupGame()がfalseになり、ボタン等が活性化されない
        // 互換性のための備忘：DivaMegaMix.exeのフルパスを記載
        public string Launcher { get; set; } = string.Empty;
        // 参照されないため常にnull
        public string GamePath { get; set; }
        public string GetLauncherDirectory() { return $"{Path.GetDirectoryName(Launcher)}{Global.s}"; }
        // Convert.ToInt32される。
        // bool == Boolean
        // True = 1、False(初期値) = 0
        // 代入されないため、どんな時でも常にFalse(0)となる
        public bool LauncherOption { get; set; }
        // 0 : Executable
        // Launcherのパスを参照し、exeを起動
        // 1以上 : Steamで起動 → steam://rungameid/{ハードコーディングされたMM+のID}をコール
        public int LauncherOptionIndex { get; set; } = -1;
        // 互換性のために残す
        // 初回セットアップ時はfalse
        // falseの時、LauncherOptionの値をLauncherOptionIndexに代入して(なお常に0)trueになる
        public bool LauncherOptionConverted { get; set; } = true;
        // 初回起動時boolのためfalse、falseの場合はSetupウインドウ表示(Executable or Steam)
        // Cancel : SteamをデフォルトとしてLauncherOptionIndexは1になる
        // Setup後直後にどれを選んでもtrueが代入
        // 互換性のために残す
        // 回避策：これを参照している部分全てで"dinput8.dll"とDMLの"config.toml"の存在チェックを行う？
        // 存在チェックの結果を常に更新して格納すればオリジナルと処理の不整合は発生しない？
        // 名称と設定値がややこしいので以下の定義にした。
        // 「MM+ディレクトリにconfig.tomlとdinput8.dllが存在していたらtrue、それ以外はfalse」
        [JsonIgnore]
        private bool _FirstOpen { get; set; }
        public bool FirstOpen
        {
            //get { return _FirstOpen; }
            get { return IsDMLFiles(); }
            set { _FirstOpen = value; }
        }
        private static bool IsDMLFiles()
        {
            return File.Exists($"{Global.ConfigJson.CurrentConfig.GetLauncherDirectory()}config.toml") && File.Exists($"{Global.ConfigJson.CurrentConfig.GetLauncherDirectory()}dinput8.dll");
        }
        // Launcherのフォルダに"mods"フォルダを生成し、そのフルパスが設定される(Launcherがnullの時はこちらもnull)
        public string ModsFolder { get; set; }
        // APIにてDMLダウンロード時に設定される
        // 手動でインストールした時などでも設定されないので、DMLインストールしているかの判定はNG
        // 正確にDMLインストールをチェックするならFirstOpenを参照
        // if (Global.config.CurrentConfig.FirstOpen) : true...install済
        private string _ModLoaderVersion { get; set; } = string.Empty;
        public string ModLoaderVersion
        {
            get
            {
                return _ModLoaderVersion;
            }
            set
            {
                _ModLoaderVersion = value;
            }
        }
        public void UpdateModLoaderVersion()
        {
            var beforeModLoaderVersion = _ModLoaderVersion;
            if (!FirstOpen)
            {
                _ModLoaderVersion = null;
            }
            else
            {
                var hash = FileHelper.CalculateSha256($"{Global.ConfigJson.CurrentConfig.GetLauncherDirectory()}{DMLUpdater.MODULE_NAME_DLL}");
                var checkVersionByHash = DMLUpdater.CheckDMLHash256DLL(hash);
                if (!string.IsNullOrEmpty(checkVersionByHash))
                {
                    _ModLoaderVersion = checkVersionByHash;
                }
                _ModLoaderVersion = !string.IsNullOrEmpty(checkVersionByHash) ? checkVersionByHash : _ModLoaderVersion;
            }
            if (beforeModLoaderVersion != _ModLoaderVersion)
                ConfigJson.UpdateConfig();
        }
        public string CurrentLoadout { get; set; }
        public Dictionary<string, ObservableCollection<Mod>> Loadouts { get; set; } = new();

        public bool Validate()
        {
            bool ret = false;

            if (!string.IsNullOrEmpty(Launcher)
                && !string.IsNullOrEmpty(ModsFolder))
            {
                ret = true;
            }
            return ret;
        }
    }
}
