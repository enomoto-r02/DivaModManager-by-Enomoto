using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DivaModManager.Features.Feed
{
    public enum GameFilter
    {
        MMP
    }
    public enum FeedFilter
    {
        Featured,
        Recent,
        Popular,
        None
    }
    public enum TypeFilter
    {
        Mods,
        WiPs,
        Sounds
    }
    public static class FeedGenerator
    {
        private static Dictionary<string, GameBananaModList> feed;
        public static bool error;
        public static Exception exception;
        public static GameBananaModList CurrentFeed;
        private static readonly string _gbCacheDir = Path.Combine(Global.assemblyLocation, "cache", "gb_api");

        private static string GetCacheKey(string url)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(64);
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GetCachePath(string url)
        {
            return Path.Combine(_gbCacheDir, GetCacheKey(url) + ".txt");
        }

        private static bool IsCacheValid(string path, int cacheHours)
        {
            if (cacheHours == -1)
                return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            return age.TotalHours < cacheHours;
        }

        public static double GetHeader(this HttpResponseMessage request, string key)
        {
            IEnumerable<string> keys = null;
            if (!request.Headers.TryGetValues(key, out keys))
                return -1;
            return double.Parse(keys.First());
        }
        public static void ClearCache()
        {
            if (feed != null)
                feed.Clear();
            try
            {
                if (Directory.Exists(_gbCacheDir))
                    FileHelper.DeleteDirectory(_gbCacheDir);
            }
            catch { }
        }
        public static async Task GetFeed(int page, GameFilter game, TypeFilter type, FeedFilter filter, GameBananaCategory category, GameBananaCategory subcategory, int perPage, bool nsfw, string search, int cacheHours = 24)
        {
            error = false;
            if (feed == null)
                feed = new Dictionary<string, GameBananaModList>();
            // Remove oldest key if more than 15 pages are cached
            if (feed.Count > 15)
                feed.Remove(feed.Aggregate((l, r) => DateTime.Compare(l.Value.TimeFetched, r.Value.TimeFetched) < 0 ? l : r).Key);
            var requestUrl = GenerateUrl(page, game, type, filter, category, subcategory, perPage, nsfw, search);
            if (feed.ContainsKey(requestUrl) && feed[requestUrl].IsValid)
            {
                CurrentFeed = feed[requestUrl];
                return;
            }
            CurrentFeed = new();

            // ディスクキャッシュをチェック
            var cachePath = GetCachePath(requestUrl);
            if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(cachePath);
                    if (lines.Length >= 2 && double.TryParse(lines[0], out var totalPages))
                    {
                        var json = string.Join(Environment.NewLine, lines.Skip(1));
                        var records = JsonSerializer.Deserialize<ObservableCollection<GameBananaRecord>>(json);
                        if (records != null)
                        {
                            CurrentFeed.Records = records;
                            CurrentFeed.TotalPages = totalPages;
                            Logger.WriteLine($"FeedGenerator: Disk cache hit '{requestUrl}'", LoggerType.Debug);
                            if (!feed.ContainsKey(requestUrl))
                                feed.Add(requestUrl, CurrentFeed);
                            else
                                feed[requestUrl] = CurrentFeed;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"FeedGenerator: Disk cache読み込みエラー: {ex.Message}", LoggerType.Warning);
                }
            }

            try
            {
                var response = await Global.GBclient.GetAsync(requestUrl);
                var responseString = await response.Content.ReadAsStringAsync();
                var records = JsonSerializer.Deserialize<ObservableCollection<GameBananaRecord>>(responseString);
                CurrentFeed.Records = records;
                // Get record count from header
                var numRecords = response.GetHeader("X-GbApi-Metadata_nRecordCount");
                if (numRecords != -1)
                {
                    var totalPages = Math.Ceiling(numRecords / Convert.ToDouble(perPage));
                    if (totalPages == 0)
                        totalPages = 1;
                    CurrentFeed.TotalPages = totalPages;
                }

                // ディスクキャッシュに保存 (1行目: totalPages, 2行目以降: JSON)
                try
                {
                    var dir = Path.GetDirectoryName(cachePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(cachePath, $"{CurrentFeed.TotalPages}{Environment.NewLine}{responseString}");
                    Logger.WriteLine($"FeedGenerator: Disk cache saved '{cachePath}'", LoggerType.Debug);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"FeedGenerator: Disk cache保存エラー: {ex.Message}", LoggerType.Warning);
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
        private static string GenerateUrl(int page, GameFilter game, TypeFilter type, FeedFilter filter, GameBananaCategory category, GameBananaCategory subcategory, int perPage, bool nsfw, string search)
        {
            // Base
            var url = "https://gamebanana.com/apiv6/";
            switch (type)
            {
                case TypeFilter.Mods:
                    url += "Mod/";
                    break;
                case TypeFilter.Sounds:
                    url += "Sound/";
                    break;
                case TypeFilter.WiPs:
                    url += "Wip/";
                    break;
            }
            // Different starting endpoint if requesting all mods instead of specific category
            if (!string.IsNullOrEmpty(search))
            {
                url += $"ByName?_sName=*{search}*&_idGameRow=";
                switch (game)
                {
                    case GameFilter.MMP:
                        url += Global.GAME_ID + "&";
                        break;
                }
            }
            else if (category.ID != null)
                url += "ByCategory?";
            else
            {
                url += $"ByGame?_aGameRowIds[]=";
                switch (game)
                {
                    case GameFilter.MMP:
                        url += Global.GAME_ID + "&";
                        break;
                }
            }
            // Consistent args
            url += $"_csvProperties=_sName,_sModelName,_sProfileUrl,_aSubmitter,_tsDateUpdated,_tsDateAdded,_aPreviewMedia,_sText,_sDescription,_aCategory,_aRootCategory,_aGame,_nViewCount," +
                $"_nLikeCount,_nDownloadCount,_aFiles,_aModManagerIntegrations,_bIsNsfw,_aAlternateFileSources&_nPerpage={perPage}";
            if (!nsfw)
                url += "&_aArgs[]=_sbIsNsfw = false";
            // Sorting filter
            switch (filter)
            {
                case FeedFilter.Recent:
                    url += "&_sOrderBy=_tsDateUpdated,DESC";
                    break;
                case FeedFilter.Featured:
                    url += "&_aArgs[]=_sbWasFeatured = true& _sOrderBy=_tsDateAdded,DESC";
                    break;
                case FeedFilter.Popular:
                    url += "&_sOrderBy=_nDownloadCount,DESC";
                    break;
            }
            // Choose subcategory or category
            if (subcategory.ID != null)
                url += $"&_aCategoryRowIds[]={subcategory.ID}";
            else if (category.ID != null)
                url += $"&_aCategoryRowIds[]={category.ID}";

            // Get page number
            url += $"&_nPage={page}";
            return url;
        }
    }
}
