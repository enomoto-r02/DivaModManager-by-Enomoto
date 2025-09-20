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
            GAMEBANANA_API,
            GAMEBANANA_API_V11,
            GAMEBANANA_BROWSER,
            DIVAMODARCHIVE_API
        }
        public enum CALL_TYPE
        {
            DROP = 0,
            DOWNLOAD,
            UPDATE,
        }

        public DownloadApiBase(TARGET_TO site = TARGET_TO.NONE, CALL_TYPE type = CALL_TYPE.DROP)
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

        [JsonIgnore]
        public TARGET_TO SITE { get; set; }
        [JsonIgnore]
        public CALL_TYPE TYPE { get; set; }

        [JsonIgnore]
        public string Url { get; set; }
        [JsonIgnore]
        public string ArchiveFilePath { get; set; }
        [JsonIgnore]
        public string TemporaryDirectoryPath { get; set; }
        [JsonIgnore]
        public string TemporaryDirectoryRootPath { get; set; }
        [JsonIgnore]
        public string MoveDirectoryRootPath { get; set; }
    }
}
