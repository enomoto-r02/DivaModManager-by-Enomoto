using DivaModManager.Common.Converters;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Download;
using DivaModManager.Misk;
using Octokit;
using Onova;
using Onova.Models;
using Onova.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Features.DMM
{
    public class DMMUpdater
    {
        private static readonly string MODULE_NAME = "DivaModManager by Enomoto";

        private static ProgressBox progressBox;
        private static GitHubClient client;
        private static HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GitHubApiTimeoutSec) };
        private static bool IsInitSetup = false;

        private static void InitSetup()
        {
            if (IsInitSetup) { return; }
            client = new GitHubClient(new ProductHeaderValue("DivaModManager-by-Enomoto"));
            IsInitSetup = true;
        }

        public static async Task<bool> CheckForDMMUpdate(CancellationTokenSource cancellationToken, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            // Get Version Number
            var localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            InitSetup();
            var owner = "enomoto-r02";
            var repo = "DivaModManager-by-Enomoto";
            var timeout = TimeSpan.FromSeconds(Global.ConfigToml.GitHubApiTimeoutSec);
            Logger.WriteLine($"Download DivaModManager by Enomoto from Github. (This timeout is {timeout} seconds.)", LoggerType.Debug);

            Release release = new();
            try
            {
                release = await client.Repository.Release.GetLatest(owner, repo);

                Match onlineVersionMatch = Regex.Match(release.TagName, @"(?<version>([0-9]+\.?)+)[^a-zA-Z]");
                string onlineVersion = null;
                if (onlineVersionMatch.Success)
                {
                    onlineVersion = onlineVersionMatch.Value;
                }
                if (UpdateAvailable(onlineVersion, localVersion))
                {
                    UpdateChangelogBox notification = new(release, "DivaModManager by Enomoto", $"A new version of DivaModManager by Enomoto is available (v{onlineVersion})!", null, false);
                    notification.ShowDialog();
                    notification.Activate();
                    if (notification.YesNo)
                    {
                        string downloadUrl = release.Assets.First().BrowserDownloadUrl;
                        string fileName = release.Assets.First().Name;
                        // Download the update
                        await DownloadDMM(downloadUrl, fileName, onlineVersion, new Progress<DownloadProgress>(ReportUpdateProgress), cancellationToken);
                        // Notify that the update is about to happen
                        MessageBox.Show($"Finished downloading {fileName}!\nDivaModManager by Enomoto will now restart.", "Notification", MessageBoxButton.OK);
                        // Update DMM
                        UpdateManager updateManager = new(AssemblyMetadata.FromAssembly(Assembly.GetEntryAssembly(), Process.GetCurrentProcess().MainModule.FileName),
                            new LocalPackageResolver($"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate"), new ZipExtractor());
                        if (!Version.TryParse(onlineVersion, out Version version))
                        {
                            MessageBox.Show($"Error parsing {onlineVersion}!\nCancelling update.", "Notification", MessageBoxButton.OK);
                            return false;
                        }
                        // Updates and restarts DMM
                        await updateManager.PrepareUpdateAsync(version);
                        updateManager.LaunchUpdater(version);
                        return true;
                    }
                    else
                        Logger.WriteLine($"Update for DivaModManager by Enomoto {onlineVersion} cancelled.", LoggerType.Info);
                }
                else
                    Logger.WriteLine($"No update for DivaModManager by Enomoto {onlineVersion} available.", LoggerType.Info);
            }
            catch (AggregateException)
            {
                List<string> replaceList = new() { MODULE_NAME };
                var resultWindow = WindowHelper.DMMWindowOpenAsync(25, replaceList);
                if (resultWindow.Result == WindowHelper.WindowCloseStatus.Yes)
                {
                    // GitHubClientのどこかにありそうだけど、面倒だからベタ書き
                    ProcessHelper.TryStartProcess("https://github.com/enomoto-r02/DivaModManager-by-Enomoto/releases");
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.WriteLine($"CheckForDMMUpdate Error!\ne.Message: {e.Message},\ne.StackTrace: {e.StackTrace}", LoggerType.Error);
                return false;
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{false}"), LoggerType.Debug, param: ParamInfo);
            return false;
        }
        private static async Task DownloadDMM(string uri, string fileName, string version, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, uri:{uri}, fileName:{fileName}, version:{version}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            try
            {
                // Create the downloads folder if necessary
                if (!Directory.Exists(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate"))
                    Directory.CreateDirectory(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate");
                progressBox = new ProgressBox(cancellationToken);
                progressBox.progressBar.Value = 0;
                progressBox.progressText.Text = $"Downloading {fileName}";
                progressBox.Title = "DivaModManager by Enomoto Update Progress";
                progressBox.finished = false;
                progressBox.Show();
                progressBox.Activate();
                var downloadFilePath = $"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate{Global.s}{fileName}";
                var moveFilePath = $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate{Global.s}{version}.zip";
                // Write and download the file
                using (var fs = new FileStream(
                    downloadFilePath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await Global.GitHubclient.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                // Rename the file
                if (!File.Exists(moveFilePath))
                {
                    File.Move(downloadFilePath, moveFilePath);
                }
                progressBox.Close();
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                FileHelper.DeleteFile(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}DMMeUpdate{Global.s}{fileName}");
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
                Logger.WriteLine($"Error whilst downloading DivaModManager ({e.Message})", LoggerType.Error);
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
        private static bool UpdateAvailable(string onlineVersion, string localVersion)
        {
            if (onlineVersion is null || localVersion is null)
            {
                return false;
            }
            string[] onlineVersionParts = onlineVersion.Split('.');
            string[] localVersionParts = localVersion.Split('.');
            // Pad the version if one has more parts than another (e.g. 1.2.1 and 1.2)
            if (onlineVersionParts.Length > localVersionParts.Length)
            {
                for (int i = localVersionParts.Length; i < onlineVersionParts.Length; i++)
                {
                    localVersionParts = localVersionParts.Append("0").ToArray();
                }
            }
            else if (localVersionParts.Length > onlineVersionParts.Length)
            {
                for (int i = onlineVersionParts.Length; i < localVersionParts.Length; i++)
                {
                    onlineVersionParts = onlineVersionParts.Append("0").ToArray();
                }
            }
            // Decide whether the online version is new than local
            for (int i = 0; i < onlineVersionParts.Length; i++)
            {
                if (!int.TryParse(onlineVersionParts[i], out _))
                {
                    MessageBox.Show($"Couldn't parse {onlineVersion}");
                    return false;
                }
                if (!int.TryParse(localVersionParts[i], out _))
                {
                    MessageBox.Show($"Couldn't parse {localVersion}");
                    return false;
                }
                if (int.Parse(onlineVersionParts[i]) > int.Parse(localVersionParts[i]))
                {
                    return true;
                }
                else if (int.Parse(onlineVersionParts[i]) != int.Parse(localVersionParts[i]))
                {
                    return false;
                }
            }
            return false;
        }
    }
}
