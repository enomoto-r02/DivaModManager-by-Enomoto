using DivaModManager.Features.Debug;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

#nullable enable

namespace DivaModManager.Common.Helpers
{
    public static class ImageCacheManager
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly string _cacheDir = Path.Combine(Global.assemblyLocation, "cache", "images");

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static string GetCacheKey(Uri uri)
        {
            var uriStr = uri.AbsoluteUri;
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(uriStr));
            var sb = new StringBuilder(64);
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GetExtension(Uri uri)
        {
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
                return ext;
            return ".png";
        }

        private static string GetCachePath(Uri uri)
        {
            return Path.Combine(_cacheDir, GetCacheKey(uri) + GetExtension(uri));
        }

        private static bool IsCacheValid(string path, int cacheHours)
        {
            if (cacheHours == -1)
                return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            return age.TotalHours < cacheHours;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    FileHelper.DeleteFile(path);
            }
            catch { }
        }

        /// <summary>
        /// キャッシュがあれば同期的に BitmapImage を返す。なければ null を返す（ダウンロードは行わない）。
        /// </summary>
        public static BitmapImage? GetCachedBitmap(Uri? uri)
        {
            return GetCachedBitmap(uri, -1);
        }

        /// <summary>
        /// キャッシュがあれば同期的に BitmapImage を返す。なければ null を返す（ダウンロードは行わない）。
        /// </summary>
        public static BitmapImage? GetCachedBitmap(Uri? uri, int cacheHours)
        {
            var path = GetCachePathIfExists(uri, cacheHours);
            return path != null ? LoadBitmapFromFile(path) : null;
        }

        /// <summary>
        /// キャッシュファイルのパスを同期的に確認。有効なキャッシュがあればパスを返し、なければ null。
        /// </summary>
        public static string? GetCachePathIfExists(Uri? uri, int cacheHours)
        {
            if (uri == null || string.IsNullOrEmpty(uri.OriginalString))
                return null;
            var cachePath = GetCachePath(uri);
            if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
                return cachePath;
            if (File.Exists(cachePath))
                TryDelete(cachePath);
            return null;
        }

        /// <summary>
        /// キャッシュファイルから同期的に BitmapImage を読み込む。
        /// </summary>
        public static BitmapImage LoadBitmapFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            if (bitmap.CanFreeze)
                bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// 非同期で画像を取得。キャッシュにあればファイルから高速読み込み、なければ WorkManager 経由でダウンロード→保存→読み込み。
        /// </summary>
        public static async Task<BitmapImage?> GetCachedBitmapAsync(Uri? uri, int cacheHours)
        {
            if (uri == null || string.IsNullOrEmpty(uri.OriginalString))
                return null;

            // キャッシュヒット → 同期的に返す
            var cachePath = GetCachePath(uri);
            if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
                return LoadBitmapFromFile(cachePath);

            // 期限切れキャッシュ削除
            if (File.Exists(cachePath))
                TryDelete(cachePath);

            // ダウンロード
            await WorkManager.RunAsync(async () =>
            {
                await DownloadToCacheAsync(uri, cachePath);
            });

            return File.Exists(cachePath) ? LoadBitmapFromFile(cachePath) : null;
        }

        /// <summary>
        /// 画像をバックグラウンドでキャッシュに保存する。UI への反映は行わない。
        /// </summary>
        public static async Task PreCacheAsync(Uri uri, int cacheHours)
        {
            if (uri == null || string.IsNullOrEmpty(uri.OriginalString))
                return;
            var cachePath = GetCachePath(uri);
            if (File.Exists(cachePath) && IsCacheValid(cachePath, cacheHours))
                return;
            await WorkManager.RunAsync(async () =>
            {
                await DownloadToCacheAsync(uri, cachePath);
            });
        }

        /// <summary>
        /// 複数の画像をバックグラウンドで一括キャッシュ保存。
        /// </summary>
        public static async Task PreCacheAsync(IEnumerable<Uri> uris, int cacheHours)
        {
            var tasks = uris
                .Where(u => u != null && !string.IsNullOrEmpty(u.OriginalString))
                .Select(u => PreCacheAsync(u, cacheHours));
            await Task.WhenAll(tasks);
        }

        private static async Task DownloadToCacheAsync(Uri uri, string cachePath)
        {
            var dir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var key = GetCacheKey(uri);
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // 別タスクが既にダウンロードしたかもしれないので再確認
                if (File.Exists(cachePath))
                    return;

                Logger.WriteLine($"ImageCacheManager: Downloading '{uri}'", LoggerType.Debug);
                var bytes = await _httpClient.GetByteArrayAsync(uri);
                File.WriteAllBytes(cachePath, bytes);
                Logger.WriteLine($"ImageCacheManager: Save Cache to '{cachePath}'", LoggerType.Debug);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(_cacheDir))
                {
                    FileHelper.DeleteDirectory(_cacheDir);
                    Logger.WriteLine("ImageCacheManager: 全キャッシュを削除しました", LoggerType.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ImageCacheManager: キャッシュ削除に失敗: {ex.Message}", LoggerType.Error);
            }
        }
    }
}
