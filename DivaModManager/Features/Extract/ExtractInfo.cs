using DivaModManager.Models;
using DivaModManager.Structures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DivaModManager.Features.Extract
{
    // modsフォルダに配置されるまでの状態遷移
    public class MoveInfoData
    {
        public ExtractInfo.EXTRACT_STATUS Status { get; set; }
        public ExtractInfo.EXTRACT_RESULT Result { get; set; }
        public ExtractInfo.EXTRACT_CANCEL MoveCancel { get; set; }
        public string FullPath { get; set; }
        public string FullPathResult { get; set; }

        public List<FileSystemInfo> FullPathInfo { get; set; } = new();
        public List<FileSystemInfo> FullPathResultInfo { get; set; } = new();

        // ディレクトリ移動(減算)
        public List<FileSystemInfo> FullPathOffset { get; set; } = new();
        // スキップ(減算)
        public List<FileSystemInfo> FullPathSkip { get; set; } = new();

        //public int FileAndDirectoryCount { get; set; }
        public List<FileSystemInfo> FileAndDirectoryCount { get; set; }
        //public int FileAndDirectoryCountResult { get; set; }
        public List<FileSystemInfo> FileAndDirectoryCountResult { get; set; }
        // ネスト内にディレクトリを移動した時などの差分用
        //public int FileAndDirectoryCountOffset { get; set; }
        public long DirectorySize { get; set; }
        public long DirectorySizeResult { get; set; }
        // UPDATE時のmod.jsonやconfig.tomlなどの差分用
        //public long DirectorySizeOffset { get; set; }
        //private bool _CheckFilesCountAndSize { get; set; }
        //public bool CheckFilesCountAndSize
        //{
        //    get
        //    {
        //        _CheckFilesCountAndSize = true;
        //        return FileAndDirectoryCount != 0
        //            && (FileAndDirectoryCount == FileAndDirectoryCountResult - FileAndDirectoryCountOffset)
        //            && DirectorySize != 0
        //            && (DirectorySize == DirectorySizeResult - DirectorySizeOffset);
        //    }
        //    private set { }
        //}

        // エラーが発生したファイル
        [JsonIgnore]
        public List<string> ErrorFilePathList { get; set; } = new();

        public long GetFullPathSize()
        {
            return FullPathInfo.Count == 0 ? DirectorySize : FullPathInfo.OfType<FileInfo>().ToList().Sum(x => x.Length) + FullPathInfo.OfType<DirectoryInfo>().ToList().Count;
        }
        public long GetFullPathResultSize()
        {
            return FullPathResultInfo.Count == 0 ? DirectorySizeResult : FullPathResultInfo.OfType<FileInfo>().ToList().Sum(x => x.Length) + FullPathInfo.OfType<DirectoryInfo>().ToList().Count;
        }

        public bool CheckFilesCountAndSize()
        {
            var fullPathLength = FullPathInfo.OfType<FileInfo>().ToList().Sum(x => x.Length);
            var fullPathResultLength = FullPathResultInfo.OfType<FileInfo>().ToList().Sum(x => x.Length);
            if (FullPathInfo.Count == 0)
            {
                // 解凍ライブラリ等でFileInfoとして取得できなかった場合は参照先を変更
                return
                    FileAndDirectoryCount.Count() != 0
                    && FileAndDirectoryCount.Count() == FullPathResultInfo.Count
                    && DirectorySize == fullPathResultLength;
            }
            else
            {
                return
                    FullPathInfo.Count != 0
                    && FullPathInfo.Count == FullPathResultInfo.Count
                    && GetFullPathSize() == GetFullPathResultSize();
            }
        }



        public MoveInfoData()
        {
        }

        public MoveInfoData(MoveInfoData mv)
        {
            // 前の情報を引き継ぐ
            FullPath = mv.FullPathResult;
            FullPathInfo = mv.FullPathResultInfo;
            FullPathOffset = mv.FullPathOffset;
            FullPathSkip = mv.FullPathSkip;
        }

    }
    public class ExtractInfo
    {
        public enum SITE
        {
            NONE = 0,
            LOCAL,
            GAMEBANANA_API,
            GAMEBANANA_BROWSER,
            DIVAMODARCHIVE_API,
            DML,
            DMM,
        }
        public enum TYPE
        {
            NONE = 0,
            DROP,
            DOWNLOAD,
            CLEAN_UPDATE,
        }
        public enum EXTRACT_STATUS
        {
            // Exception.
            NONE,

            // FullPath : DMM/Download
            // FullPathResult : /Download/xxxxx.zip
            DOWNLOAD_FILE,

            // FullPath : .../xxxxx.zip
            // FullPathResult : /Download/temp_xxxxx
            ARCHIVE_EXTRACT,

            // FullPath : DMM/Download/temp_xxxxx
            // FullPathResult : /Download/temp_xxxxx/Mod_Name
            CHECK_ROOT_COMPRESS_FILE_DIRECT,

            // FullPath : DMM/Download/temp_xxxxx
            // FullPathResult : /Download/temp_xxxxx/Mod_Name
            CHECK_CONFIG_TOML_DIRECTORY,

            // FullPath : .../Mod_Name
            // FullPathResult : /Download/temp_xxxxx
            DIRECTORY_DROP,

            // FullPath : /Download/temp_xxxxx
            // FullPathResult : /Download/temp_xxxxx/Mod_Name
            DIRECTORY_DROP_TEMPORARY,

            // Example: mod
            // FullPath : DMM/Download/temp_xxxxx/Mod_Name
            // FullPathResult : MM+_Dir/mods/Mod_Name
            MOVE_DIRECTORY,
        }
        public enum EXTRACT_RESULT
        {
            NONE = 0,
            NO_INSTALL_DML,
            SIZE_ZERO,
            ZIPSLIP_SUCCESS,
            SIZE_UNMATCH,
            UNSUPPORTED,
            EXCEPTION,
            PATH_LENGTH_OVER_LIMIT,
            DANGEROUS_FILE,
            NO_CONFIG_TOML,
            MULTI_CONFIG_TOML,
            NOT_NEST_DIRECTORY,
            NEST_DIRECTORY,
            NEST_DIRECTORY_AND_SIZE_UNMATCH,
            CANCELED,
            NOT_FOUND_ARCHIVE_PATH,
            NOT_FOUND_WINRAR,
            NOT_FOUND_SEVENZIP,
            DUMMY_MOD,
            // Modフォルダ移動前の状態(二重フォルダや解凍サイズが異なった場合に確認するため)
            EXTRACT_SUCCESS,
            ALL_COMPLETE,
        }
        public enum EXTRACT_CANCEL
        {
            NONE = 0,
            YES,
            NO,
        }
        public enum EXTERNAL_EXTRACTOR
        {
            NOT_CHECK = 0,
            NOT_USE,
            NO_INSTALL,
            NOT_FOUND,
            OLD_VERSION,
            USE,
            EXCEPTION,
        }

        public ExtractInfo()
        {
            Site = SITE.NONE;
            Type = TYPE.NONE;
            MoveInfoList = new();
        }

        [JsonIgnore]
        public SITE Site { get; set; }
        [JsonIgnore]
        public TYPE Type { get; set; }
        [JsonIgnore]
        public EXTRACT_RESULT ExtractResult { get; set; }
        [JsonIgnore]
        public EXTRACT_CANCEL ExtractCancel { get; set; }
        [JsonIgnore]
        // ダミーファイルをダウンロードした時に、ダウンロードページをブラウザで呼び出す
        public string Url { get; set; }

        // MoveInfoに設定されているが、記述が長くなるのと使用頻度が高いので別に保持
        public MoveInfoData TempRootInfo()
        {
            return MoveInfoList.Where(x => x.Result == EXTRACT_RESULT.EXTRACT_SUCCESS).LastOrDefault();
        }

        // Logger表示用(xxxxx.zip or Mod_Name)
        [JsonIgnore]
        public string WindowLoggerViewFileName { get; set; } = string.Empty;

        // 移動履歴(チェック用)
        [JsonIgnore]
        public List<MoveInfoData> MoveInfoList { get; set; }

        // 上書き対象外とするファイル
        // Example (1): .../MOD_NAME/config_e.toml
        // * config.tomlはダウンロード後のファイルを既存のconfig.tomlの設定値を引き継いでから上書き対象とする
        // * mod.jsonは上書き
        [JsonIgnore]
        public List<string> SkipFilePathList { get; set; } = new();

        /// <summary>
        /// ダウンロード、アップロード時などに圧縮ファイル後を展開後やファイル移動時に
        /// 上書きの対象外とするファイルを設定する
        /// 判定する時はStartsWith(SetSkipPathListの中身)なので注意！
        /// </summary>
        /// <returns>処理の継続可否(イレギュラーな組み合わせな呼び出しは以降の処理を停止)</returns>
        public bool SetSkipPathList()
        {
            var ret = false;

            if (Site == SITE.NONE || Type == TYPE.NONE)
            {
                ret = false;
                return ret;
            }
            else if (Site == SITE.DML)
            {
                // DMLの設定ファイルはテンポラリフォルダで設定を引き継いでから上書きするため
                ret = true;
            }
            else if (Site == SITE.DMM)
            {
                ret = true;
            }
            else
            {
                // mods
                if (Type == TYPE.DOWNLOAD)
                {
                    ret = true;
                }
                else if (Type == TYPE.CLEAN_UPDATE)
                {
                    var modsDirectoryName = new DirectoryInfo(MoveInfoList.LastOrDefault().FullPathResult).Name;
                    SkipFilePathList = new List<string>
                    {
                        // config.tomlはテンポラリフォルダで設定を引き継いでから上書きする
                        //Path.Combine(Global.ModsFolder, Mod.CONFIG_TOML_NAME),
                        Path.Combine(Global.ModsFolder, modsDirectoryName, Mod.CONFIG_E_TOML_NAME),
                        Path.Combine(Global.ModsFolder, modsDirectoryName, MetadataManager.MOD_JSON_NAME),
                    };
                    ret = true;
                }
                else if (Type == TYPE.DROP)
                {
                    ret = true;
                }
                else
                {
                    ret = false;
                }
            }
            return ret;
        }

        // アップデート用MoveInfoData
        [JsonIgnore]
        public List<MoveInfoData> MoveInfoWhenUpdateList { get; set; } = new();

        /// <summary>
        /// サイズチェックの対象外とするファイルを設定する
        /// </summary>
        /// <returns>処理の継続可否(イレギュラーな組み合わせな呼び出しは以降の処理を停止)</returns>
        [JsonIgnore]
        public List<string> SkipFileWhenSizeCheckPathList { get; set; } = new();

        /// <summary>
        /// ファイルサイズチェックの対象外とするファイルを設定する
        /// </summary>
        /// <returns>処理の継続可否(イレギュラーな組み合わせな呼び出しは以降の処理を停止)</returns>
        public bool SetSkipFileWhenSizeCheckPathList()
        {
            var ret = false;

            if (Site == SITE.NONE || Type == TYPE.NONE)
            {
                ret = false;
                return ret;
            }
            else if (Site == SITE.DML)
            {
                ret = true;
            }
            else if (Site == SITE.DMM)
            {
                ret = true;
            }
            else if (Type == TYPE.DROP || Type == TYPE.DOWNLOAD || Type == TYPE.CLEAN_UPDATE)
            {
                var tempDirectoryName = new DirectoryInfo(MoveInfoList.Where(x => x.Status == EXTRACT_STATUS.MOVE_DIRECTORY).LastOrDefault().FullPath).FullName;
                var modsDirectoryName = new DirectoryInfo(MoveInfoList.Where(x => x.Status == EXTRACT_STATUS.MOVE_DIRECTORY).LastOrDefault().FullPathResult).FullName;
                SkipFileWhenSizeCheckPathList = new List<string>
                {
                    Path.Combine(tempDirectoryName, "config.toml"),
                    Path.Combine(tempDirectoryName, "config_e.toml"),
                    Path.Combine(tempDirectoryName, "mod.json"),
                    //Path.Combine(tempDirectoryName, "preview"),
                    Path.Combine(modsDirectoryName, "config.toml"),
                    Path.Combine(modsDirectoryName, "config_e.toml"),
                    Path.Combine(modsDirectoryName, "mod.json"),
                    //Path.Combine(modsDirectoryName, "preview"),
                };
                ret = true;
            }
            else
            {
                ret = false;
            }

            return ret;
        }

        [JsonIgnore]
        public string UseExtractComponentZipSlipCheck { get; set; }

        [JsonIgnore]
        public string UseExtractComponentExtract { get; set; }

        [JsonIgnore]
        public TimeSpan ExtractMainTime { get; set; }

        [JsonIgnore]
        public TimeSpan ExtractCoreTime { get; set; }

        [JsonIgnore]
        public TimeSpan MoveTime { get; set; }

        public string GetextractorCallPattern()
        {
            return Site switch
            {
                SITE.NONE => "",
                SITE.LOCAL => "",
                SITE.GAMEBANANA_API => "https://gamebanana.com/apiv4/",
                SITE.GAMEBANANA_BROWSER => "",
                SITE.DIVAMODARCHIVE_API => "https://divamodarchive.com/api/v1/",
                _ => "",
            };
        }

        public string GetTemporaryDirectoryPath()
        {
            return MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT).FirstOrDefault().FullPath;
        }

        public string GetModsDirectoryPath()
        {
            return MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.MOVE_DIRECTORY).FirstOrDefault().FullPath;
        }


    }
}
