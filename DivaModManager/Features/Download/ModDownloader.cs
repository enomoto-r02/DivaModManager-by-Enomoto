using DivaModManager.Common.Converters;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Extract;
using DivaModManager.Misk;
using DivaModManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Features.Download
{
    public class ModDownloader
    {
        private string URL_TO_ARCHIVE;
        private string URL;
        private string DL_ID;
        private string MOD_TYPE;
        private string MOD_ID;
        private string fileName;
        private bool cancelled;
        private HttpClient client = null;
        private CancellationTokenSource cancellationToken = new();
        private GameBananaAPIV4 response = new();
        private DivaModArchivePost DMAresponse = new();
        private ProgressBox progressBox;

        // Download_click call GameBanana tab
        public async Task BrowserDownload(string game, GameBananaRecord record, Func<ExtractInfo, Task> onExtractAsync = null)
        {
            if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }

            var modLocalSameUrl = Global.ModList_All.Where(x => x.metadataManager?.metadata?.homepage?.ToString() == record.Link.ToString());
            var modLocalSameName = Global.ModList_All.Where(x => x.name.ToUpper() == Path.GetFileNameWithoutExtension(record.Title.ToUpper()));
            if (modLocalSameUrl != null && modLocalSameUrl.Any())
            {
                // 同一URLのModが存在
                if (record.DateUpdated > modLocalSameUrl.FirstOrDefault().metadataManager?.metadata?.lastupdate)
                {
                    var res = await WindowHelper.DMMWindowOpenAsync(5, path: modLocalSameUrl.FirstOrDefault().directory_path);
                    if (res == WindowHelper.WindowCloseStatus.No)
                    {
                        // ダウンロード続行
                        ;
                    }
                    else if (res == WindowHelper.WindowCloseStatus.Yes)
                    {
                        await WindowHelper.DMMWindowOpenAsync(27);
                        return;
                    }
                    else if (res == WindowHelper.WindowCloseStatus.Cancel)
                    {
                        return;
                    }
                }
                else
                {
                    var res = await WindowHelper.DMMWindowOpenAsync(4, path: modLocalSameUrl.FirstOrDefault().directory_path);
                    if (res == WindowHelper.WindowCloseStatus.No || res == WindowHelper.WindowCloseStatus.Cancel)
                    {
                        return;
                    }
                }
            }
            // フォルダ名が同じModが存在
            else if (modLocalSameName != null && modLocalSameName.Any())
            {
                var res = await WindowHelper.DMMWindowOpenAsync(30, path: modLocalSameName.FirstOrDefault().directory_path);
                if (res == WindowHelper.WindowCloseStatus.No || res == WindowHelper.WindowCloseStatus.Cancel)
                {
                    return;
                }
            }


            DownloadWindow downloadWindow = new DownloadWindow(record);
            downloadWindow.ShowDialog();
            if (downloadWindow.YesNo)
            {
                string downloadUrl = null;
                string fileName = null;
                if (record.AllFiles.Count == 1)
                {
                    downloadUrl = record.AllFiles[0].DownloadUrl;
                    fileName = record.AllFiles[0].FileName;
                }
                else if (record.AllFiles.Count > 1)
                {
                    DownloadFileBox fileBox = new DownloadFileBox(record.AllFiles, record.Title);
                    fileBox.Activate();
                    fileBox.ShowDialog();
                    downloadUrl = fileBox.chosenFileUrl;
                    fileName = fileBox.chosenFileName;
                }
                if (downloadUrl != null && fileName != null)
                {
                    await WorkManager.RunAsync(async () =>
                    {
                        var filePath = await DownloadFile(downloadUrl, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                        if (!cancelled)
                        {
                            record.Url = downloadUrl;
                            record.Site = ExtractInfo.SITE.GAMEBANANA_BROWSER;
                            record.Type = ExtractInfo.TYPE.DOWNLOAD;
                            MoveInfoData moveInfo = new MoveInfoData()
                            {
                                FullPath = filePath,
                                Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE
                            };
                            record.MoveInfoList.Add(moveInfo);
                            if (onExtractAsync != null)
                                await onExtractAsync(record);
                            else
                                await Extractor.ExtractMain(record);
                        }
                    });
                }
            }
        }

        // Download_click call DivaModArchive tab
        public async Task DMABrowserDownload(string game, DivaModArchivePost post, Func<ExtractInfo, Task> onExtractAsync = null)
        {
            if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }

            if (post.Explicit)
            {
                ExplicitWindow explicitWindow = new(post);
                explicitWindow.ShowDialog();
                if (!explicitWindow.YesNo)
                {
                    return;
                }
            }

            var modLocalSameUrl = Global.ModList_All.Where(x => x.metadataManager?.metadata?.homepage?.ToString() == post.Link.ToString()).FirstOrDefault();
            var modLocalSameName = Global.ModList_All.Where(x => x.name.ToString().ToUpper() == Path.GetFileNameWithoutExtension(post.Name.ToUpper()));
            if (modLocalSameUrl != null)
            {
                // 同一URLのModが存在
                if (post.Time > modLocalSameUrl.metadataManager?.metadata?.lastupdate)
                {
                    var res = await WindowHelper.DMMWindowOpenAsync(5, path: modLocalSameUrl.directory_path);
                    if (res == WindowHelper.WindowCloseStatus.No)
                    {
                        // ダウンロードは続行
                        ;
                    }
                    // todo: UPDATEが実装されたらここを削除して処理が走るようにする
                    // (ボタンの押下状態によってMoveInfoDataの値をUPDATEにするのを忘れないこと！)
                    else if (res == WindowHelper.WindowCloseStatus.Yes)
                    {
                        await WindowHelper.DMMWindowOpenAsync(27);
                        return;
                    }
                    else if (res == WindowHelper.WindowCloseStatus.Cancel)
                    {
                        return;
                    }
                }
                else
                {
                    var res = await WindowHelper.DMMWindowOpenAsync(4, path: modLocalSameUrl.directory_path);
                    if (res == WindowHelper.WindowCloseStatus.No || res == WindowHelper.WindowCloseStatus.Cancel)
                    {
                        return;
                    }
                }
            }
            // フォルダ名が同じModが存在   
            else if (modLocalSameName != null && modLocalSameName.Any())
            {
                var res = await WindowHelper.DMMWindowOpenAsync(30, path: modLocalSameName.FirstOrDefault().directory_path);
                if (res == WindowHelper.WindowCloseStatus.No || res == WindowHelper.WindowCloseStatus.Cancel)
                {
                    return;
                }
            }

            DownloadWindow downloadWindow = new(post);
            downloadWindow.ShowDialog();
            if (downloadWindow.YesNo)
            {
                string downloadUrl = null;
                string fileName = null;
                if (post.Files.Count == 1)
                {
                    downloadUrl = post.Files[0].ToString();
                    fileName = post.FileNames[0];
                }
                else if (post.Files.Count > 1)
                {
                    DownloadFileBoxDMA fileBox = new DownloadFileBoxDMA(post);
                    fileBox.Activate();
                    fileBox.ShowDialog();
                    if (fileBox.chosenFileUrl == null)
                        return;
                    downloadUrl = fileBox.chosenFileUrl.ToString();
                    fileName = fileBox.chosenFileName;
                }
                if (downloadUrl != null && fileName != null)
                {
                    await WorkManager.RunAsync(async () =>
                    {
                        var filePath = await DownloadFile(downloadUrl, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                        if (!cancelled)
                        {
                            MoveInfoData moveInfo = new()
                            {
                                FullPath = filePath,
                                Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE
                            };
                            post.MoveInfoList.Add(moveInfo);
                            if (onExtractAsync != null)
                                await onExtractAsync(post);
                            else
                                await Extractor.ExtractMain(post);
                        }

                    });
                }
            }
        }
        // Called by OnStartup (One Click Install)
        public async void Download(string line)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"line:{line}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);
            if (string.IsNullOrEmpty(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }
            try
            {
                if (ParseProtocol(line))
                {
                    await App.Current.Dispatcher.Invoke((async () =>
                    {
                        if (await GetDataAsync())
                        {
                            if (URL.Contains("gamebanana", StringComparison.CurrentCultureIgnoreCase))
                            {
                                DownloadWindow downloadWindow = new(response);
                                downloadWindow.ShowDialog();
                                if (downloadWindow.YesNo)
                                {
                                    await DownloadFile(URL_TO_ARCHIVE, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                                    if (!cancelled)
                                    {
                                        response.Site = ExtractInfo.SITE.GAMEBANANA_API;
                                        response.Type = ExtractInfo.TYPE.DOWNLOAD;
                                        MoveInfoData moveInfo = new()
                                        {
                                            FullPath = Path.Combine(Global.downloadBaseLocation, fileName),
                                            Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE,
                                            FullPathResult = Path.Combine(Global.downloadBaseLocation, fileName)
                                        };
                                        response.MoveInfoList.Add(moveInfo);
                                        await App.Current.Dispatcher.Invoke(async () => await Extractor.ExtractMain(response));
                                    }
                                }
                            }
                            else if (URL.Contains("divamodarchive", StringComparison.CurrentCultureIgnoreCase))
                            {
                                DownloadWindow downloadWindow = new(DMAresponse);
                                downloadWindow.ShowDialog();
                                if (downloadWindow.YesNo)
                                {
                                    string downloadUrl = null;
                                    string fileName = null;
                                    if (DMAresponse.Files.Count == 1)
                                    {
                                        downloadUrl = DMAresponse.Files[0].ToString();
                                        fileName = DMAresponse.FileNames[0];
                                    }
                                    else if (DMAresponse.Files.Count > 1)
                                    {
                                        DownloadFileBoxDMA fileBox = new DownloadFileBoxDMA(DMAresponse);
                                        fileBox.Activate();
                                        fileBox.ShowDialog();
                                        downloadUrl = fileBox.chosenFileUrl.ToString();
                                        fileName = fileBox.chosenFileName;
                                    }
                                    if (downloadUrl != null && fileName != null)
                                    {
                                        await DownloadFile(downloadUrl, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                                        if (!cancelled)
                                        {
                                            DMAresponse.Url = downloadUrl;
                                            DMAresponse.Site = ExtractInfo.SITE.DIVAMODARCHIVE_API;
                                            DMAresponse.Type = ExtractInfo.TYPE.DOWNLOAD;
                                            MoveInfoData moveInfo = new()
                                            {
                                                FullPath = Path.Combine(Global.downloadBaseLocation, fileName),
                                                Status = ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE,
                                                FullPathResult = Path.Combine(Global.downloadBaseLocation, fileName)
                                            };
                                            DMAresponse.MoveInfoList.Add(moveInfo);
                                            await App.Current.Dispatcher.Invoke(async () => await Extractor.ExtractMain(DMAresponse));
                                        }
                                    }
                                }
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"Exception! ex.Message:{ex.Message}, ex.StackTrace:{ex.StackTrace}"), LoggerType.Debug, param: ParamInfo);
            }
            finally
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
                Logger.WriteOut();
                Environment.Exit(0);
            }
        }

        private async Task<bool> GetDataAsync()
        {
            try
            {
                if (URL.Contains("gamebanana", StringComparison.CurrentCultureIgnoreCase))
                {
                    client = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(Global.ConfigToml.GameBananaApiTimeoutSec),
                    };
                    string responseString = await client.GetStringAsync(URL);
                    response = JsonSerializer.Deserialize<GameBananaAPIV4>(responseString);
                    fileName = response.Files.Where(x => x.Id == DL_ID).ToArray()[0].FileName;
                    return true;
                }
                else if (URL.Contains("divamodarchive", StringComparison.CurrentCultureIgnoreCase))
                {
                    client = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(Global.ConfigToml.DivaModArchiveApiTimeoutSec),
                    };
                    string responseString = await client.GetStringAsync(URL);
                    DMAresponse = JsonSerializer.Deserialize<DivaModArchivePost>(responseString);
                    fileName = DMAresponse.FileNames[0];
                    return true;
                }
                else
                    return false;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error while fetching data {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        private void ReportUpdateProgress(DownloadProgress progress)
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

        private bool ParseProtocol(string line)
        {
            try
            {
                line = line.Replace("divamodmanager:", "");
                string[] data = line.Split(',');
                // GameBanana 1-click install
                if (data.Length > 1)
                {
                    URL_TO_ARCHIVE = data[0];
                    // Used to grab file info from dictionary
                    var match = Regex.Match(URL_TO_ARCHIVE, @"\d*$");
                    DL_ID = match.Value;
                    MOD_TYPE = data[1];
                    MOD_ID = data[2];
                    URL = $"https://gamebanana.com/apiv6/{MOD_TYPE}/{MOD_ID}?_csvProperties=_sName,_aGame,_sProfileUrl,_aPreviewMedia,_sDescription,_aSubmitter,_aCategory,_aSuperCategory,_aFiles,_tsDateUpdated,_aAlternateFileSources,_bHasUpdates,_aLatestUpdates";
                    return true;
                }
                // DivaModArchive 1-click install
                else if (data.Length == 1)
                {
                    MOD_ID = data[0].Replace("dma/", string.Empty);
                    URL = $"{Global.DMA_API_URL_POSTS}{MOD_ID}";
                    return true;
                }
                else
                    return false;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error while parsing {line}: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        // Download function Core ?
        private async Task<string> DownloadFile(string uri, string fileName, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = string.Empty;
            try
            {
                // Create the downloads folder if necessary
                if (!Directory.Exists(Global.downloadBaseLocation))
                    Directory.CreateDirectory(Global.downloadBaseLocation);
                // Download the file if it doesn't already exist
                ret = Path.Combine(Global.downloadBaseLocation, fileName);
                if (File.Exists(ret))
                {
                    try
                    {
                        FileHelper.DeleteFile(ret);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Couldn't delete the already existing {Global.assemblyLocation}/Downloads/{fileName} ({e.Message})",
                            "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return ret;
                    }
                }
                progressBox = new ProgressBox(cancellationToken);
                progressBox.progressBar.Value = 0;
                progressBox.finished = false;
                progressBox.Title = $"Download Progress";
                progressBox.Show();
                progressBox.Activate();
                // Write and download the file
                using (var fs = new FileStream(
                    Path.Combine(Global.assemblyLocation, "Downloads", fileName), FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    client = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(Global.ConfigToml.DivaModArchiveApiTimeoutSec),
                    };
                    await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                progressBox.Close();
                Logger.WriteLine(string.Join(" ", MeInfo, $"HttpClient.DownloadAsync Complete."), LoggerType.Debug, param: ParamInfo);
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                FileHelper.DeleteFile(ret);
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                    cancelled = true;
                }
            }
            catch (Exception e)
            {
                if (progressBox != null)
                {
                    progressBox.finished = true;
                    progressBox.Close();
                }
                MessageBox.Show($"Error whilst downloading {fileName}. {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                cancelled = true;
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }
    }
}
