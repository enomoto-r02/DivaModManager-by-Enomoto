using DivaModManager.Common.Helpers;
using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DivaModManager.Structures
{
    public class Metadata
    {
        public static readonly Dictionary<string, string> HOSTS = new()
        {
            { "GB",  "gamebanana.com" },
            { "DMA", "divamodarchive.com" },
        };
        public static JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public int? id { get; set; }
        public Uri preview { get; set; }
        public string submitter { get; set; }
        public Uri avi { get; set; }
        public Uri upic { get; set; }
        public Uri caticon { get; set; }
        public string cat { get; set; } = string.Empty;
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
            submitter = GbApiV4.Owner.Name;
            description = GbApiV4.Description;
            preview = GbApiV4.Image;
            homepage = GbApiV4.Link;
            avi = GbApiV4.Owner.Avatar;
            upic = GbApiV4.Owner.Upic;
            cat = GbApiV4.CategoryName;
            caticon = GbApiV4.Category.Icon;
            lastupdate = GbApiV4.DateUpdated;
        }
        public Metadata(DivaModArchivePost DmaPost)
        {
            id = DmaPost.ID;
            description = DmaPost.Text;
            submitter = DmaPost.Authors[0].Name;
            preview = DmaPost.Images[0];
            homepage = new Uri(Global.DMA_HOMEPAGE_URL_POSTS + DmaPost.ID);
            avi = DmaPost.Authors[0].Avatar;
            cat = DmaPost.PostType;
            lastupdate = DmaPost.Time;
        }
        public Metadata(GameBananaRecord record)
        {
            submitter = record.Owner.Name;
            description = record.Description;
            preview = record.Image;
            homepage = record.Link;
            avi = record.Owner.Avatar;
            upic = record.Owner.Upic;
            cat = record.CategoryName;
            caticon = record.Category.Icon;
            lastupdate = record.DateUpdated;
        }

        public string GetMetadataString()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public static long GetMetadataJsonSize(string mod_json_path)
        {
            return new FileInfo(mod_json_path).Length;
        }

        public bool SaveMetadata(string mod_json_path)
        {
            try
            {
                if (File.Exists(mod_json_path))
                {
                    FileHelper.DeleteFile(mod_json_path);
                }
                File.WriteAllText(mod_json_path, GetMetadataString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
