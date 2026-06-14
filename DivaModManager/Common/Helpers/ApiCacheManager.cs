using DivaModManager.Features.Debug;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DivaModManager.Common.Helpers
{
    public static class ApiCacheManager
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static string GetCacheKey(string url)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(64);
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GetCachePath(string url, string cacheDir)
        {
            return Path.Combine(cacheDir, GetCacheKey(url) + ".json");
        }

        private static bool IsCacheValid(string path, int cacheHours)
        {
            if (cacheHours == -1)
                return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            return age.TotalHours < cacheHours;
        }

        public static async Task<string?> GetCachedStringAsync(string url, int cacheHours, HttpClient client, string cacheDir)
        {
            try
            {
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                var cachePath = GetCachePath(url, cacheDir);

                // キャッシュあり・有効期限内ならローカルから読み込み
                if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
                {
                    Logger.WriteLine($"ApiCacheManager: Cache hit (disk) '{url}'", LoggerType.Debug);
                    return await File.ReadAllTextAsync(cachePath);
                }

                // 期限切れのキャッシュファイルは削除
                if (File.Exists(cachePath))
                {
                    try { FileHelper.DeleteFile(cachePath); }
                    catch { Logger.WriteLine($"ApiCacheManager: 古いキャッシュの削除に失敗 {cachePath}", LoggerType.Warning); }
                }

                var key = GetCacheKey(url);
                var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync();
                try
                {
                    // 別スレッドが既にダウンロードしたかもしれないので再確認
                    if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
                        return await File.ReadAllTextAsync(cachePath);

                    Logger.WriteLine($"ApiCacheManager: Fetching '{url}'", LoggerType.Debug);
                    var response = await client.GetStringAsync(url);

                    var dir = Path.GetDirectoryName(cachePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(cachePath, response);
                    Logger.WriteLine($"ApiCacheManager: Save cache '{cachePath}'", LoggerType.Debug);

                    return response;
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ApiCacheManager: 取得に失敗 '{url}': {ex.Message}", LoggerType.Error);
                return null;
            }
        }

        public static void ClearCache(string cacheDir)
        {
            try
            {
                if (Directory.Exists(cacheDir))
                {
                    FileHelper.DeleteDirectory(cacheDir);
                    Logger.WriteLine($"ApiCacheManager: キャッシュを削除しました '{cacheDir}'", LoggerType.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ApiCacheManager: キャッシュ削除に失敗 '{cacheDir}': {ex.Message}", LoggerType.Error);
            }
        }
    }
}
