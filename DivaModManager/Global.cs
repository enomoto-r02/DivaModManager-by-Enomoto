using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DivaModManager
{
    public static class Global
    {
        public static Config config;
        public static Logger logger;
        public static char s = Path.DirectorySeparatorChar;
        public static string assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
        public static List<string> games;
        public static string selected_game;
        public static ObservableCollection<Mod> ModList;
        // After applying the filter, for backup purposes
        public static ObservableCollection<Mod> ModList_All;
        public static bool SearchModListFlg;
        public static ObservableCollection<String> LoadoutItems;
        public static ObservableCollection<String> CategoryItems;
        public enum Col
        {
            Enabled = 0,
            Priority,
            Name,
            Category,
            Size,
            Note,
        }
        public static string DMA_HOMEPAGE_URL_POSTS = "https://divamodarchive.com/posts/";
        public static string DMA_API_URL_POSTS = "https://divamodarchive.com/api/v1/posts/";
        public static string DMA_PAGE_URL_BASE = "https://divamodarchive.com/post/";

        public static void UpdateConfig()
        {
            config.Configs[config.CurrentGame].Loadouts[config.Configs[config.CurrentGame].CurrentLoadout] = ModList_All;
            string configString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var isReady = false;
            while (!isReady)
            {
                try
                {
                    File.WriteAllText($@"{assemblyLocation}{s}Config.json", configString);
                    isReady = true;
                }
                catch (Exception e)
                {
                    // Check if the exception is related to an IO error.
                    if (e.GetType() != typeof(IOException))
                    {
                        Global.logger.WriteLine($"Couldn't write to Config.json ({e.Message})", LoggerType.Error);
                        break;
                    }
                }
            }
        }

        #region 外部プロセス起動 共通ヘルパー

        /// <summary>
        /// 指定されたターゲット（URLまたはファイル/フォルダパス）を外部プロセスで安全に開きます。
        /// </summary>
        /// <param name="target">開くURLまたはパス。</param>
        /// <param name="workingDirectory">プロセスの作業ディレクトリ（オプション）。</param>
        /// <returns>プロセスが正常に開始された場合は true、それ以外は false。</returns>
        public static bool TryStartProcess(string target, string workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                Global.logger?.WriteLine($"Target for Process.Start is empty or null.", LoggerType.Warning);
                return false;
            }

            try
            {
                // UseShellExecute = true を使うと、関連付けられたアプリケーションで開く（URLやフォルダなど）
                // UseShellExecute = false は直接実行ファイルを実行する場合に使うことが多い
                var psi = new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    Verb = "open" // Verb は UseShellExecute = true の場合に意味を持つ
                };

                if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                Process.Start(psi);
                Global.logger?.WriteLine($"Successfully started process for target: '{target}'.", LoggerType.Info);
                return true;
            }
            catch (Win32Exception ex) // プロセス開始時の一般的なエラー
            {
                Global.logger?.WriteLine($"Error starting process for '{target}': {ex.Message} (ErrorCode: {ex.ErrorCode})", LoggerType.Error);
                // ユーザーに通知するかどうかはケースバイケース
                // MessageBox.Show($"Could not open '{target}':\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (FileNotFoundException ex) // 実行ファイルが見つからない場合 (UseShellExecute=false の場合など)
            {
                Global.logger?.WriteLine($"File not found for process start '{target}': {ex.Message}", LoggerType.Error);
                return false;
            }
            catch (Exception ex) // その他の予期せぬエラー
            {
                Global.logger?.WriteLine($"Unexpected error starting process for '{target}': {ex}", LoggerType.Error);
                return false;
            }
        }

        #endregion
    }
}
