using DivaModManager.Common.Converters;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Extract;
using DivaModManager.Misk;
using DivaModManager.Models;
using DivaModManager.Structures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Features.Download
{
    public static class ModUpdater
    {
        private static ProgressBox progressBox;
        private static int updateCounter;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="selectedMods"></param>
        /// <param name="isSelectedUpdate">自動アップデートを実装したら、falseで呼ぶ予定</param>
        /// <returns></returns>
        public async static Task CheckForUpdates(string path, List<Mod> selectedMods, bool isSelectedUpdate, Func<ExtractInfo, Task> onExtractAsync = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            updateCounter = 0;
            if (!Directory.Exists(path) || isSelectedUpdate && selectedMods.Count == 0)
            {
                return;
            }
            var cancellationToken = new CancellationTokenSource();
            var requestUrls = new Dictionary<string, List<string>>();
            var mods = new List<string>();
            if (isSelectedUpdate)
            {
                foreach (var mod in selectedMods)
                {
                    var m = mod;
                    mods.Add(path + Global.s.ToString() + m.name);
                }
            }
            else
            {
                // isSelectedUpdateがfalseの場合は現状ではこのロジックに入らないはずなので、そもそも不要か？
                // 確実にmod.jsonがあるフォルダだけを対象にしてしまうのは問題がある（mod.jsonが無いことを認知できない）
                // modフォルダ内を全てアップデートの対象にするため、レスポンスが返ってこない可能性がある
                mods = Directory.GetDirectories(path).Where(x => File.Exists($"{x}{Global.s}mod.json")).ToList();
            }
            var GBmodList = new Dictionary<string, List<string>>();
            var DMAmodList = new Dictionary<string, Metadata>();
            var modInfoDict = new Dictionary<string, ModInfo>();
            var urlCounts = new Dictionary<string, int>();
            foreach (var mod in mods)
            {
                if (!File.Exists($"{mod}{Global.s}mod.json"))
                {
                    Logger.WriteLine($"mod.json is not found in \"{Path.GetFileName(mod)}\"", LoggerType.Warning);
                    continue;
                }
                Metadata metadata;
                try
                {
                    var metadataString = File.ReadAllText($"{mod}{Global.s}mod.json");
                    metadata = JsonSerializer.Deserialize<Metadata>(metadataString);
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Error occurred while getting metadata for {Path.GetFileName(mod)} ({e.Message})", LoggerType.Error);
                    continue;
                }
                Uri url = null;
                if (metadata.homepage != null)
                {
                    url = CreateUriGB(metadata.homepage.ToString());
                    // gamebanana update list
                    if (url != null)
                    {
                        var MOD_TYPE = char.ToUpper(url.Segments[1][0]) + url.Segments[1][1..^2];
                        var MOD_ID = url.Segments[2];
                        if (!urlCounts.ContainsKey(MOD_TYPE))
                            urlCounts.Add(MOD_TYPE, 0);
                        int index = urlCounts[MOD_TYPE];
                        if (!GBmodList.ContainsKey(MOD_TYPE))
                            GBmodList.Add(MOD_TYPE, new());
                        GBmodList[MOD_TYPE].Add(mod);

                        if (!requestUrls.ContainsKey(MOD_TYPE))
                            requestUrls.Add(MOD_TYPE, new string[] { $"https://gamebanana.com/apiv6/{MOD_TYPE}/Multi?_csvProperties=_sName,_aSubmitter,_aCategory,_aSuperCategory,_sProfileUrl,_sDescription,_bHasUpdates,_aLatestUpdates,_aFiles,_aPreviewMedia,_aAlternateFileSources,_tsDateUpdated&_csvRowIds=" }.ToList());
                        else if (requestUrls[MOD_TYPE].Count == index)
                            requestUrls[MOD_TYPE].Add($"https://gamebanana.com/apiv6/{MOD_TYPE}/Multi?_csvProperties=_sName,_aSubmitter,_aCategory,_aSuperCategory,_sProfileUrl,_sDescription,_bHasUpdates,_aLatestUpdates,_aFiles,_aPreviewMedia,_aAlternateFileSources,_tsDateUpdated&_csvRowIds=");
                        requestUrls[MOD_TYPE][index] += $"{MOD_ID},";
                        if (requestUrls[MOD_TYPE][index].Length > 1990)     // todo: この値が何なのか確認
                            urlCounts[MOD_TYPE]++;
                    }
                    // divamodarchive update list
                    else if (metadata.id != null)
                    {
                        var mod_name = Path.GetFileName(mod);
                        var dma_url = Global.DMA_API_URL_POSTS + metadata.id;
                        DMAmodList.Add(mod_name, metadata);
                        ModInfo modInfo = new(path + Global.s + mod_name, mod_name);
                        modInfoDict.Add(mod_name, modInfo);
                    }
                    else
                    {
                        Logger.WriteLine($"id is not found in mod.json of {Path.GetFileName(mod)}", LoggerType.Error);
                        continue;
                    }
                }
            }
            // Remove extra comma(gamebanana)
            foreach (var key in requestUrls.Keys)
            {
                var counter = 0;
                foreach (var requestUrl in requestUrls[key].ToList())
                {
                    if (requestUrl.EndsWith(","))
                        requestUrls[key][counter] = requestUrl[..^1];
                    counter++;
                }

            }
            // none update gamebanana and divamodarchive
            if (requestUrls.Count == 0 && DMAmodList.Count == 0)
            {
                Logger.WriteLine($"No mod updates available.", LoggerType.Info);
                return;
            }
            List<GameBananaAPIV4> response = new();
            Dictionary<string, DivaModArchivePost> DMAresponse = new();
            // gamebanana updates
            foreach (var type in requestUrls)
            {
                foreach (var requestUrl in type.Value)
                {
                    try
                    {
                        var responseString = await Global.GBclient.GetStringAsync(requestUrl);
                        var partialResponse = JsonSerializer.Deserialize<List<GameBananaAPIV4>>(responseString);
                        response = response.Concat(partialResponse).ToList();
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"e.Message:{e.Message}\ne.StackTrace:{e.StackTrace}", LoggerType.Error);
                        return;
                    }
                }
            }
            // divamodarchive updates
            foreach (string mod_dir_name in DMAmodList.Keys)
            {
                int? dma_id = DMAmodList[mod_dir_name].id;
                try
                {
                    var dma_url = Global.DMA_API_URL_POSTS + dma_id;
                    var responseString = await Global.DMAclient.GetStringAsync(dma_url);
                    DivaModArchivePost res = JsonSerializer.Deserialize<DivaModArchivePost>(responseString);
                    DMAresponse.Add(mod_dir_name, res);
                }
                catch (HttpRequestException e)
                {
                    Logger.WriteLine($"e.Message:{e.Message}\ne.StackTrace:{e.StackTrace}", LoggerType.Error);
                    continue;
                }
            }
            // gamebanana update process
            var convertedModList = new List<string>();
            foreach (var type in GBmodList)
                foreach (var mod in type.Value)
                    convertedModList.Add(mod);
            for (int i = 0; i < convertedModList.Count; i++)
            {
                Metadata metadata;
                try
                {
                    metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText($"{convertedModList[i]}{Global.s}mod.json"));
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Error occurred while getting metadata for {convertedModList[i]} ({e.Message})", LoggerType.Error);
                    continue;
                }
                await ModCleanUpdateGB(response[i], convertedModList[i], metadata, new Progress<DownloadProgress>(ReportUpdateProgress), CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token), onExtractAsync: onExtractAsync);
            }
            //divamodarchive update process
            if (DMAresponse.Count > 0)
            {
                foreach (var mod_dir_name in DMAresponse.Keys)
                {
                    var DMAmod = DMAresponse[mod_dir_name];
                    Metadata metadata;
                    try
                    {
                        metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText($"{modInfoDict[mod_dir_name].modFullPath}{Global.s}mod.json"));
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"Error occurred while getting metadata for {path} ({e.Message})", LoggerType.Error);
                        continue;
                    }
                    if (DMAresponse.ContainsKey(mod_dir_name))
                    {
                        var res = DMAresponse[mod_dir_name];
                        await ModCleanUpdateDMA(res, modInfoDict[mod_dir_name].modFullPath, metadata, new Progress<DownloadProgress>(ReportUpdateProgress), CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token), onExtractAsync: onExtractAsync);
                    }
                    else
                    {
                        Logger.WriteLine($"{Path.GetFileName(path)} was most likely trashed by the creator and cannot receive anymore updates", LoggerType.Warning);
                    }
                }
            }

            if (updateCounter == 0)
                Logger.WriteLine($"No mod updates available.", LoggerType.Info);
            else
                Logger.WriteLine($"Done checking for mod updates!", LoggerType.Info);

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
        // clean update for gamebanana
        private static async Task ModCleanUpdateGB(GameBananaAPIV4 item, string mod, Metadata metadata, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, Func<ExtractInfo, Task> onExtractAsync = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            // If lastupdate doesn't exist, add one
            if (metadata.lastupdate == null)
            {
                if (item.HasUpdates != null && (bool)item.HasUpdates)
                    metadata.lastupdate = item.Updates[0].DateAdded;
                else
                    metadata.lastupdate = new DateTime(1970, 1, 1);
                string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText($@"{mod}{Global.s}mod.json", metadataString);
                return;
            }
            if (item.HasUpdates != null && (bool)item.HasUpdates)
            {

                await WorkManager.RunAsync(async () =>
                {
                    var update = item.Updates[0];
                    // Compares dates of last update to current
#if DEBUG
                    if (true)
                    {
#else
                    if (DateTime.Compare((DateTime)metadata.lastupdate, update.DateAdded) < 0)
                    {
#endif
                        ++updateCounter;
                        // Display the changelog and confirm they want to update
                        Logger.WriteLine($"An update is available for {Path.GetFileName(mod)}!", LoggerType.Info);
                        UpdateChangelogBox changelogBox = new(item, Path.GetFileName(mod), $"A new update is available for {Path.GetFileName(mod)}", item.Image, true);
                        changelogBox.Content.Text = "Would you like to clean update?";
                        changelogBox.Activate();
                        changelogBox.ShowDialog();
                        if (changelogBox.Skip)
                        {
                            if (File.Exists($@"{mod}{Global.s}mod.json"))
                            {
                                Logger.WriteLine($"Skipped update for {Path.GetFileName(mod)}...", LoggerType.Info);
                                metadata.lastupdate = update.DateAdded;
                                string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                                File.WriteAllText($@"{mod}{Global.s}mod.json", metadataString);
                            }
                            return;
                        }
                        if (!changelogBox.YesNo)
                        {
                            Logger.WriteLine($"Declined update for {Path.GetFileName(mod)}...", LoggerType.Info);
                            return;
                        }
                        // Download the update
                        var files = item.Files;
                        string downloadUrl = null, fileName = null;

                        if (files.Count > 1)
                        {
                            DownloadFileBox fileBox = new(files.ToList(), Path.GetFileName(mod));
                            //changelogBox.Owner = App.Current.MainWindow.GetType() != fileBox.GetType() ? App.Current.MainWindow : null;
                            fileBox.Activate();
                            fileBox.ShowDialog();
                            downloadUrl = fileBox.chosenFileUrl;
                            fileName = fileBox.chosenFileName;
                        }
                        else if (files.Count == 1)
                        {
                            downloadUrl = files.ElementAt(0).DownloadUrl;
                            fileName = files.ElementAt(0).FileName;
                        }
                        else
                        {
                            Logger.WriteLine($"An update is available for {Path.GetFileName(mod)} but no downloadable files are available directly from GameBanana.", LoggerType.Info);
                        }
                        if (item.AlternateFileSources != null)
                        {
                            List<string> replaceList = new() { Path.GetFileName(mod) };
                            var choice = await WindowHelper.DMMWindowOpenAsync(54, replaceList);
                            if (choice == WindowHelper.WindowCloseStatus.No)
                            {
                                ProcessHelper.TryStartProcess(item.Url);
                                //new AltLinkWindow(item.AlternateFileSources, Path.GetFileName(mod), Global.ConfigJson.CurrentGame, metadata.homepage.AbsoluteUri, true).ShowDialog();
                                return;
                            }
                        }
                        if (downloadUrl != null && fileName != null)
                        {
                            await DownloadFile(downloadUrl, fileName, mod, item, progress, cancellationToken, onExtractAsync: onExtractAsync);
                        }
                        else
                        {
                            Logger.WriteLine($"Cancelled update for {Path.GetFileName(mod)}", LoggerType.Info);
                        }
                    }
                });
            }
            else if (item.HasUpdates == null)
            {
                Logger.WriteLine($"{Path.GetFileName(mod)} was most likely trashed by the creator and cannot receive anymore updates", LoggerType.Warning);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        // clean update for divamodarchive
        private static async Task ModCleanUpdateDMA(DivaModArchivePost item, string mod, Metadata metadata, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, Func<ExtractInfo, Task> onExtractAsync = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            // If lastupdate doesn't exist, add one
            if (metadata.lastupdate == null)
            {
                metadata.lastupdate = item.Time;
                string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText($@"{mod}{Global.s}mod.json", metadataString);
                return;
            }
            // Compares dates of last update to current
            if (DateTime.Compare((DateTime)metadata.lastupdate, item.Time) < 0)
            {
                ++updateCounter;
                // Display the changelog and confirm they want to update
                Logger.WriteLine($"An update is available for {Path.GetFileName(mod)}!", LoggerType.Info);
                UpdateChangelogBox changelogBox = new(item, Path.GetFileName(mod), $"A new update is available for {Path.GetFileName(mod)}", true);
                changelogBox.Content.Text = "Would you like to clean update?";
                changelogBox.Activate();
                changelogBox.ShowDialog();
                if (changelogBox.Skip)
                {
                    if (File.Exists($@"{mod}{Global.s}mod.json"))
                    {
                        Logger.WriteLine($"Skipped update for {Path.GetFileName(mod)}...", LoggerType.Info);
                        metadata.lastupdate = item.Time;
                        string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText($@"{mod}{Global.s}mod.json", metadataString);
                    }
                    return;
                }
                if (!changelogBox.YesNo)
                {
                    Logger.WriteLine($"Declined update for {Path.GetFileName(mod)}...", LoggerType.Info);
                    return;
                }
                // Download the update
                await DownloadFile(item.Files[0].ToString(), item.FileNames[0], mod, item, progress, cancellationToken, onExtractAsync: onExtractAsync);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
        // Called by ModUpdateGB to download the file
        private static async Task DownloadFile(string uri, string fileName, string mod, GameBananaAPIV4 item, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, Func<ExtractInfo, Task> onExtractAsync = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            try
            {
                if (!Directory.Exists($@"{Global.assemblyLocation}{Global.s}Downloads"))
                    Directory.CreateDirectory($@"{Global.assemblyLocation}{Global.s}Downloads");
                if (File.Exists($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}"))
                {
                    try
                    {
                        FileHelper.DeleteFile($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}");
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"{MeInfo} Couldn't delete the already existing {Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName} ({e.Message})",
                            LoggerType.Error);
                        return;
                    }
                }

                await WorkManager.RunAsync(async () =>
                {
                    progressBox = new ProgressBox(cancellationToken);
                    progressBox.progressBar.Value = 0;
                    progressBox.finished = false;
                    progressBox.Title = $"Download Progress";
                    progressBox.Show();
                    progressBox.Activate();
                    // Write and download the file
                    var filePath = $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}";
                    using (var fs = new FileStream(
                        filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GameBananaApiTimeoutSec) };
                        await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                    }
                    progressBox.Close();

                    item.Site = ExtractInfo.SITE.GAMEBANANA_API;
                    item.Type = ExtractInfo.TYPE.CLEAN_UPDATE;
                    item.Url = uri;
                    MoveInfoData moveInfo = new() { FullPath = filePath, Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE };
                    item.MoveInfoList.Add(moveInfo);
                    if (onExtractAsync != null)
                        await onExtractAsync(item);
                    else
                        await Extractor.ExtractMain(item);
                });
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                FileHelper.DeleteFile(@$"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}");
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
                Logger.WriteLine($"Error whilst downloading {fileName} ({e.Message})", LoggerType.Error);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
        // Called by ModUpdate to download the file
        private static async Task DownloadFile(string uri, string fileName, string mod, DivaModArchivePost item, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, Func<ExtractInfo, Task> onExtractAsync = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            try
            {
                // Create the downloads folder if necessary
                if (!Directory.Exists($@"{Global.assemblyLocation}{Global.s}Downloads"))
                    Directory.CreateDirectory($@"{Global.assemblyLocation}{Global.s}Downloads");
                // Download the file if it doesn't already exist
                var filePath = $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}";
                if (File.Exists(filePath))
                {
                    try
                    {
                        FileHelper.DeleteFile(filePath);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"{MeInfo} Couldn't delete the already existing {filePath} ({e.Message})",
                            LoggerType.Error);
                        return;
                    }
                }
                await WorkManager.RunAsync(async () =>
                {
                    progressBox = new ProgressBox(cancellationToken);
                    progressBox.progressBar.Value = 0;
                    progressBox.finished = false;
                    progressBox.Title = $"Download Progress";
                    progressBox.Show();
                    progressBox.Activate();
                    // Write and download the file
                    using (var fs = new FileStream(
                        $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}", FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(Global.ConfigToml.DivaModArchiveApiTimeoutSec) };
                        await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                    }
                    progressBox.Close();

                    item.Site = ExtractInfo.SITE.DIVAMODARCHIVE_API;
                    item.Type = ExtractInfo.TYPE.CLEAN_UPDATE;
                    MoveInfoData moveInfo = new() { FullPath = filePath, Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE };
                    item.MoveInfoList.Add(moveInfo);
                    if (onExtractAsync != null)
                        await onExtractAsync(item);
                    else
                        await Extractor.ExtractMain(item);
                });
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                FileHelper.DeleteFile($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}");
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
                Logger.WriteLine($"Error whilst downloading {fileName} ({e.Message})", LoggerType.Error);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
        private static Uri CreateUriGB(string url)
        {
            Uri uri;
            if ((Uri.TryCreate(url, UriKind.Absolute, out uri) || Uri.TryCreate("http://" + url, UriKind.Absolute, out uri)) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Use validated URI here
                string host = uri.DnsSafeHost;
                if (uri.Segments.Length != 3)
                    return null;
                switch (host)
                {
                    case "www.gamebanana.com":
                    case "gamebanana.com":
                        return uri;
                }
            }
            return null;
        }
    }
}
