using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DivaModManager
{
    public class DivaModArchivePost
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("images")]
        public List<Uri> Images { get; set; }
        [JsonPropertyName("files")]
        public List<Uri> Files { get; set; }
        [JsonPropertyName("time")]
        public DateTime Time { get; set; }
        [JsonIgnore]
        public string DateUpdatedAgo => $"Updated {StringConverters.FormatTimeAgo(DateTime.UtcNow - Time)}";
        [JsonPropertyName("post_type")]
        public string PostType { get; set; }
        [JsonIgnore]
        public Uri Link => new Uri($"https://divamodarchive.com/posts/{ID}");
        [JsonPropertyName("download_count")]
        public long Downloads { get; set; }
        //public int Downloads { get; set; }
        [JsonPropertyName("like_count")]
        public long Likes { get; set; }
        //public int Likes { get; set; }
        [JsonIgnore]
        public string DownloadString => Downloads.ToString();
        //public string DownloadString => StringConverters.FormatNumber(Downloads);
        [JsonIgnore]
        public string LikeString => Likes.ToString();
        //public string LikeString => StringConverters.FormatNumber(Likes);
        [JsonPropertyName("authors")]
        public List<DivaModArchiveUser> Authors { get; set; }
        [JsonPropertyName("dependencies")]
        public List<object> Dependencies { get; set; }
        [JsonPropertyName("file_names")]
        public List<String> FileNames { get; set; }
        [JsonPropertyName("private")]
        public bool Private { get; set; }
        [JsonPropertyName("explicit")]
        public bool Explicit { get; set; }
        [JsonPropertyName("explicit_reason")]
        public string Explicit_Reason { get; set; }
    }
    public class DivaModArchiveUser
    {
        [JsonPropertyName("id")]
        public long ID { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("avatar")]
        public Uri Avatar { get; set; }
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }
    public class DivaModArchiveModList
    {
        public ObservableCollection<DivaModArchivePost> Posts { get; set; }
        public double TotalPages { get; set; }
        public DateTime TimeFetched = DateTime.UtcNow;
        public bool IsValid => (DateTime.UtcNow - TimeFetched).TotalMinutes < 15;
    }
    public class ModInfo
    {
        public ModInfo(string modFullPath, string modDirectoryName)
        {
            this.modFullPath = modFullPath;
            this.modDirectoryName = modDirectoryName;
        }
        public string modFullPath { get; set; }
        public string modDirectoryName { get; set; }
    }
}