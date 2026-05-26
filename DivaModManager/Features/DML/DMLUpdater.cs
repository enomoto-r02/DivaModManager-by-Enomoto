using DivaModManager.Common.Converters;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Download;
using DivaModManager.Features.Extract;
using DivaModManager.Misk;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Features.DML
{
    public class DMLUpdater
    {
        public static readonly string MODULE_NAME = "DivaModLoader";

        private static ProgressBox progressBox;
        private static bool IsInitSetup = false;

        public static readonly string MODULE_NAME_DLL = "dinput8.dll";
        public static readonly string MODULE_NAME_TOML = "config.toml";
        public static readonly string MODULE_NAME_MODS_DIRECTORY = "mods";
        public static readonly string MODULE_NAME_TEMPLATE_DIRECTORY = "Template Mod";
        public static readonly string MODULE_HASH_SHA256_DLL_0014_ARCHIVE = "a4fc01354280608403e33cd6192c33e1826a9e51293e150ab3c05d1b48ac7e2a";
        public static readonly string MODULE_HASH_SHA256_DLL_0014_DLL = "8a0bdc8b0aa74142b1ac1168af3440bfaa6b6fbc68501f96a9473c26c09e46e9";
        public static readonly string MODULE_HASH_SHA256_DLL_0016_ARCHIVE = "71d0c2073b1e42bddbbdd60cb493567e3f3e2023a63fd7299839e7fa7fcaa4a2";
        public static readonly string MODULE_HASH_SHA256_DLL_0016_DLL = "462c31741e72367e56144a4045a64a7713631e02e586d5195bb24f07ba972974";

        public static string CheckDMLHash256DLL(string hash)
        {
            var dict = new Dictionary<string, string>() {
                { "0.0.14", MODULE_HASH_SHA256_DLL_0014_DLL },
                { "0.0.16", MODULE_HASH_SHA256_DLL_0016_DLL },
            };

            return dict.FirstOrDefault(x => x.Value == hash).Key;
        }
        public static string CheckDMLHash256Archive(string hash)
        {
            var dict = new Dictionary<string, string>() {
                { "0.0.14", MODULE_HASH_SHA256_DLL_0014_ARCHIVE },
                { "0.0.16", MODULE_HASH_SHA256_DLL_0016_ARCHIVE },
            };

            return dict.FirstOrDefault(x => x.Value == hash).Key;
        }

        public static bool CheckDMLDirectory(string directoryPath)
        {
            var fileList = new Dictionary<string, bool>() {
                { MODULE_NAME_TOML, false },
                { MODULE_NAME_DLL, false },
                { MODULE_NAME_MODS_DIRECTORY, true },
                { $"{MODULE_NAME_MODS_DIRECTORY}{Global.s}{MODULE_NAME_TEMPLATE_DIRECTORY}", true },
            };

            var count = fileList.Count;
            var DirectoryInfo = new DirectoryInfo(directoryPath);
            count -= DirectoryInfo.GetDirectories(MODULE_NAME_TEMPLATE_DIRECTORY, System.IO.SearchOption.AllDirectories).Length;
            foreach (var files in DirectoryInfo.GetFiles("*", System.IO.SearchOption.AllDirectories))
            {
                if (fileList.ContainsKey(files.Name))
                {
                    if (fileList[files.Name] == false)
                    {
                        count--;
                        if (count == 0) { break; }
                    }
                }
            }

            return count == 0;
        }

        private static void InitSetup()
        {
            if (IsInitSetup) { return; }
            IsInitSetup = true;
        }

        private static GitHubClient client = new GitHubClient(new ProductHeaderValue("DivaModLoader"));

        // call by Update_Check_Click
        public static async Task<bool> CheckForDMLUpdate(CancellationTokenSource cancellationToken, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var gameFolder = Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher);
            if (!Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion = null;
            }
            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].UpdateModLoaderVersion();
            var localVersion = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion;
            InitSetup();
            var owner = "blueskythlikesclouds";
            var repo = "DivaModLoader";
            var timeout = TimeSpan.FromSeconds(Global.ConfigToml.GitHubApiTimeoutSec);
            Logger.WriteLine($"{MeInfo} Download DivaModLoader from Github. (This timeout is {timeout} seconds.)", LoggerType.Debug);

            Release release = new();
            try
            {
                release = await client.Repository.Release.GetLatest(owner, repo);
            }
            catch (AggregateException)
            {
                List<string> replaceList = new() { MODULE_NAME };
                var resultWindow = WindowHelper.DMMWindowOpenAsync(25, replaceList);
                if (resultWindow.Result == WindowHelper.WindowCloseStatus.Yes)
                {
                    // GitHubClientのどこかにありそうだけど、面倒だからベタ書き
                    ProcessHelper.TryStartProcess("https://github.com/blueskythlikesclouds/DivaModLoader/releases");
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.WriteLine($"CheckForDMLUpdate Error! e.Message: {e.Message}, e.StackTrace: {e.StackTrace}", LoggerType.Error);
                return false;
            }
            Match onlineVersionMatch = Regex.Match(release.TagName, @"(?<version>([0-9]+\.?)+)[^a-zA-Z]");
            string onlineVersion = null;
            if (onlineVersionMatch.Success)
            {
                onlineVersion = onlineVersionMatch.Value;
            }
            // バージョンの比較
            if (VersionHelper.CompareVersions(onlineVersion, localVersion) == VersionHelper.Result.VersionA_AS_LONGER
                || VersionHelper.CompareVersions(onlineVersion, localVersion) == VersionHelper.Result.VersionB_NOTHING)
            {
                // localVersionがnull(ファイル存在チェック含む)の場合
                string downloadUrl = release.Assets.First().BrowserDownloadUrl;
                string fileName = release.Assets.First().Name;

                UpdateChangelogBox notification = new(release, "DivaModLoader", $"A new version of DivaModLoader is available (v{onlineVersion})!", null, skip: false, loader: true);
                notification.ShowDialog();
                notification.Activate();
                if (notification.YesNo)
                {
                    await DownloadDML(downloadUrl, fileName, onlineVersion, new Progress<DownloadProgress>(ReportUpdateProgress), cancellationToken);
                    if (!Global.ConfigJson.CurrentConfig.FirstOpen)
                    {
                        Logger.WriteLine($"DivaModLoader failed to install, try setting up again.", LoggerType.Error);
                        return false;
                    }
                    else
                        return true;
                }
            }
            else
            {
                Logger.WriteLine($"No update for DivaModLoader available.", LoggerType.Info);
                return true;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{true}"), LoggerType.Debug, param: ParamInfo);
            return true;
        }
        private static async Task DownloadDML(string uri, string fileName, string version, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var downloadPath = $@"{Global.assemblyLocation}Downloads{Global.s}DML{Global.s}{version}{Path.GetExtension(fileName)}";
            try
            {
                if (!Directory.Exists(Global.temporaryLocationDML))
                    Directory.CreateDirectory(Global.temporaryLocationDML);
                if (File.Exists(downloadPath))
                {
                    try
                    {
                        FileHelper.DeleteFile(downloadPath);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"{MeInfo} Couldn't delete the already existing {downloadPath} ({e.Message})",
                            LoggerType.Error);
                        return;
                    }
                }
                Logger.WriteLine($"Downloading DivaModLoader...", LoggerType.Info);
                progressBox = new ProgressBox(cancellationToken);
                progressBox.progressBar.Value = 0;
                progressBox.finished = false;
                progressBox.Title = $"Download Progress";
                progressBox.Show();
                progressBox.Activate();
                using (var fs = new FileStream(
                    downloadPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GitHubApiTimeoutSec) };
                    await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                progressBox.Close();
                var outputPath = Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher);
                var extract = new ExtractInfo()
                {
                    Site = ExtractInfo.SITE.DML,
                    Type = ExtractInfo.TYPE.DOWNLOAD,
                };
                MoveInfoData moveInfo = new() { FullPath = downloadPath, Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE };
                extract.MoveInfoList.Add(moveInfo);
                await Task.Run(async () => await Extractor.ExtractMain(extract));
            }
            catch (OperationCanceledException)
            {
                FileHelper.DeleteFile(downloadPath);
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                }
                return;
            }
            catch (Exception e)
            {
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                }
                Logger.WriteLine($"Error whilst downloading DivaModLoader ({e.Message})", LoggerType.Error);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
        private static void ReportUpdateProgress(DownloadProgress progress)
        {
            if (progress.Percentage == 1)
            {
                progressBox.finished = true;
            }
            progressBox.progressBar.Value = progress.Percentage * 100;
            progressBox.taskBarItem.ProgressValue = progress.Percentage;
            progressBox.progressTitle.Text = $"Downloading {progress.FileName}...";
            progressBox.progressText.Text = $"{Math.Round(progress.Percentage * 100, 2)}% " +
                $"({StringConverters.FormatSize(progress.DownloadedBytes)} of {StringConverters.FormatSize(progress.TotalBytes)})";
        }
    }
}
