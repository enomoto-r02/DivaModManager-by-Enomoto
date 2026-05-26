using DivaModManager.Features.Extract;
using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DivaModManager.Structures
{
    // GameBananaとDivaModArchiveの緩衝用基底クラス
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
            metadata = new Metadata();
            metadata_request = new Dictionary<string, Metadata>();
            metadata_update = new Dictionary<string, Metadata>();
        }
        public MetadataManager(Mod mod)
        {
            metadata = new Metadata();
            METADATA_DIRECTORY_PATH = mod.directory_path;
            METADATA_DIRECTORY_NAME = mod.directory_name;
            MOD_JSON_PATH = $@"{mod.directory_path}{Global.s}{MOD_JSON_NAME}";
            metadata_request = new Dictionary<string, Metadata>();
            metadata_update = new Dictionary<string, Metadata>();
        }
        public async Task<bool> InitMetadataAsync(Mod mod)
        {
            if (mod.exist_mods_json)
            {
                metadata = JsonSerializer.Deserialize<Metadata>(await File.ReadAllTextAsync($"{mod.mods_json_path}"));
                return true;
            }
            else
            {
                metadata = new Metadata();
                return false;
            }
        }
        // DownloadextractorCallPatternの派生クラスをコンストラクタにした場合は、型に応じたコンストラクタを呼び出す
        public MetadataManager(ExtractInfo extractorCallPattern)
        {
            metadata = extractorCallPattern switch
            {
                GameBananaAPIV4 api => new(api),
                GameBananaRecord api => new(api),
                DivaModArchivePost api => new(api),
                _ => new(),
            };
        }
    }
}
