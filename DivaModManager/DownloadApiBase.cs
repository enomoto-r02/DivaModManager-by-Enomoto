using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DivaModManager
{
    public class DownloadApiBase
    {
        public enum TARGET_TO
        {
            NONE = 0,
            LOCAL,
            GAMEBANANA_API,
            GAMEBANANA_API_V11,
            GAMEBANANA_BROWSER,
            DIVAMODARCHIVE_API
        }
        public enum CALL_TYPE
        {
            NONE = 0,
            DROP,
            DOWNLOAD,
            UPDATE,
        }
        public DownloadApiBase()
        {
            SITE = TARGET_TO.NONE;
            TYPE = CALL_TYPE.NONE;
        }
        public DownloadApiBase(TARGET_TO site, CALL_TYPE type)
        {
            TemporaryDirectoryPath = $@"{Global.assemblyLocation}Downloads{Global.s}temp_{DateTime.Now:yyyyMMddHHmmssFFF}";
            SITE = site;
            TYPE = type;
        }
        public string GetApiBase()
        {
            switch (SITE)
            {
                case TARGET_TO.NONE:
                    return "";
                case TARGET_TO.LOCAL:
                    return "";
                case TARGET_TO.GAMEBANANA_API:
                    return "https://gamebanana.com/apiv4/";
                case TARGET_TO.GAMEBANANA_API_V11:
                    return "https://gamebanana.com/apiv11/";
                case TARGET_TO.GAMEBANANA_BROWSER:
                    return "";
                case TARGET_TO.DIVAMODARCHIVE_API:
                    return "https://divamodarchive.com/api/v1/";
                default:
                    return "";
            }
        }
        public bool SetSkipPathList()
        {
            var ret = false;
            if (TYPE == CALL_TYPE.UPDATE && !string.IsNullOrEmpty(MoveDirectoryRootPath))
            {
                SkipFilePathList = new List<string>
                {
                    $@"{MoveDirectoryRootPath}{Global.s}config.toml",
                    $@"{MoveDirectoryRootPath}{Global.s}config_e.toml",
                    $@"{MoveDirectoryRootPath}{Global.s}preview"
                };
                ret = true;
            }
            else if (TYPE == CALL_TYPE.DOWNLOAD)
            {
                SkipFilePathList = new List<string>
                {
                    $@"{MoveDirectoryRootPath}{Global.s}config.toml",
                    $@"{MoveDirectoryRootPath}{Global.s}config_e.toml",
                    $@"{MoveDirectoryRootPath}{Global.s}preview"
                };
                ret = true;
            }
            else if (TYPE == CALL_TYPE.DROP)
            {
                ret = true;
            }
            return ret;
        }

        [JsonIgnore]
        public TARGET_TO SITE { get; set; }
        [JsonIgnore]
        public CALL_TYPE TYPE { get; set; }

        [JsonIgnore]
        public string Url { get; set; }
        [JsonIgnore]
        public string ArchiveFilePath { get; set; }

        [JsonIgnore]
        // Example: .../DivaModManager/Downloads/temp_xxxx
        public string TemporaryDirectoryPath { get; set; }

        [JsonIgnore]
        // Example: .../DivaModManager/Downloads/temp_xxxx/MOD_NAME
        // ただしフォルダがDropされた場合は、そのディレクトリ(テンポラリでない場合がある)
        public string TemporaryDirectoryRootPath { get; set; }

        [JsonIgnore]
        public long TemporaryDirectoryRootSize { get; set; }

        [JsonIgnore]
        // Example: .../Hatsune Miku Project DIVA Mega Mix Plus/mods/MOO_NAME
        public string MoveDirectoryRootPath { get; set; }

        [JsonIgnore]
        public long MoveDirectoryRootSize { get; set; }

        [JsonIgnore]
        // Example (1): .../MOD_NAME/config.toml
        // Example (2): .../MOD_NAME/config_e.toml
        public List<string> SkipFilePathList { get; set; } = new();
    }
}
