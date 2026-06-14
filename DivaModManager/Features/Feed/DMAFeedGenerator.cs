using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DivaModManager.Features.Feed
{
    public enum DMAFeedSort
    {
        Latest,
        Downloads,
        Likes,
    }
    public enum DMAFeedFilter
    {
        None,
        Song,
        Cover,
        Module,
        Ui,
        Plugin,
        Other
    }
    public static class DMAFeedGenerator
    {
        private static Dictionary<string, DivaModArchiveModList> feed;
        public static bool error;
        public static Exception exception;
        public static DivaModArchiveModList CurrentFeed;
        private static readonly string _dmaCacheDir = Path.Combine(Global.assemblyLocation, "cache", "dma_api");

        private static string GetCacheKey(string url)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(64);
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static void DMAClearCache()
        {
            if (feed != null)
                feed.Clear();
            ApiCacheManager.ClearCache(_dmaCacheDir);
        }

        public static async Task GetFeed(int page, DMAFeedSort sort, DMAFeedFilter filter, string search, int limit, int cacheHours = 24)
        {
            error = false;
            if (feed == null)
                feed = new();
            // Remove oldest key if more than 15 pages are cached
            if (feed.Count > 15)
                feed.Remove(feed.Aggregate((l, r) => DateTime.Compare(l.Value.TimeFetched, r.Value.TimeFetched) < 0 ? l : r).Key);
            var requestUrl = GenerateUrl(page, sort, filter, search, limit);
            if (feed.ContainsKey(requestUrl) && feed[requestUrl].IsValid)
            {
                CurrentFeed = feed[requestUrl];
                return;
            }
            CurrentFeed = new();

            var cachePath = Path.Combine(_dmaCacheDir, GetCacheKey(requestUrl) + ".json");
            var countUrl = $"https://divamodarchive.com/api/v1/posts/count?query={search}&limit={limit}";
            var countCachePath = Path.Combine(_dmaCacheDir, GetCacheKey(countUrl) + ".txt");

            // Posts cache check
            if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours)
                && File.Exists(countCachePath) && IsCacheValid(countCachePath, cacheHours))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(cachePath);
                    var posts = JsonSerializer.Deserialize<ObservableCollection<DivaModArchivePost>>(json);
                    if (posts != null)
                    {
                        CurrentFeed.Posts = posts;
                        CurrentFeed.TotalPages = double.Parse(await File.ReadAllTextAsync(countCachePath));
                        Logger.WriteLine($"DMAFeedGenerator: Disk cache hit '{requestUrl}'", LoggerType.Debug);
                        if (!feed.ContainsKey(requestUrl))
                            feed.Add(requestUrl, CurrentFeed);
                        else
                            feed[requestUrl] = CurrentFeed;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"DMAFeedGenerator: Disk cache読み込みエラー: {ex.Message}", LoggerType.Warning);
                }
            }

            try
            {
                // Posts
                {
                    var response = await Global.DMAclient.GetAsync(requestUrl);
                    var responseString = await response.Content.ReadAsStringAsync();
                    var posts = JsonSerializer.Deserialize<ObservableCollection<DivaModArchivePost>>(responseString);
                    CurrentFeed.Posts = posts;
                    // ディスクに保存
                    try
                    {
                        var dir = Path.GetDirectoryName(cachePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        await File.WriteAllTextAsync(cachePath, responseString);
                    }
                    catch { }
                }

                // Count
                {
                    var response = await Global.DMAclient.GetAsync(countUrl);
                    var countString = await response.Content.ReadAsStringAsync();
                    var numPosts = double.Parse(countString);
                    var totalPages = Math.Ceiling(numPosts / limit);
                    if (totalPages == 0)
                        totalPages = 1;
                    CurrentFeed.TotalPages = totalPages;
                    // ディスクに保存
                    try
                    {
                        var dir = Path.GetDirectoryName(countCachePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        await File.WriteAllTextAsync(countCachePath, totalPages.ToString());
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                error = true;
                exception = e;
                return;
            }
            if (!feed.ContainsKey(requestUrl))
                feed.Add(requestUrl, CurrentFeed);
            else
                feed[requestUrl] = CurrentFeed;
        }

        private static bool IsCacheValid(string path, int cacheHours)
        {
            if (cacheHours == -1)
                return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            return age.TotalHours < cacheHours;
        }

        private static string GenerateUrl(int page, DMAFeedSort sort, DMAFeedFilter filter, string search, int limit)
        {
            // Base
            var url = "https://divamodarchive.com/api/v1/posts?sort=";
            switch (sort)
            {
                case DMAFeedSort.Latest:
                    url += "time:desc";
                    break;
                case DMAFeedSort.Downloads:
                    url += "download_count:desc";
                    break;
                case DMAFeedSort.Likes:
                    url += "like_count:desc";
                    break;
            }
            switch (filter)
            {
                case DMAFeedFilter.Song:
                    url += "&filter=post_type=Song";
                    break;
                case DMAFeedFilter.Cover:
                    url += "&filter=post_type=Cover";
                    break;
                case DMAFeedFilter.Module:
                    url += "&filter=post_type=Module";
                    break;
                case DMAFeedFilter.Ui:
                    url += "&filter=post_type=UI";
                    break;
                case DMAFeedFilter.Plugin:
                    url += "&filter=post_type=Plugin";
                    break;
                case DMAFeedFilter.Other:
                    url += "&filter=post_type=Other";
                    break;
            }
            url += $"&query={search}";
            var offset = (page - 1) * limit;
            url += $"&offset={offset}";
            url += $"&limit={limit}";
            return url;
        }
    }
}
