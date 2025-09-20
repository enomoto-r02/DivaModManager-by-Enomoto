using DivaModManager.UI;
using Microsoft.VisualBasic.FileIO;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager
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
        private HttpClient client = new();
        private CancellationTokenSource cancellationToken = new();
        private GameBananaAPIV4 response = new();
        private DivaModArchivePost DMAresponse = new();
        private ProgressBox progressBox;

        public async void BrowserDownload(string game, GameBananaRecord record)
        {
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
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
                    UpdateFileBox fileBox = new UpdateFileBox(record.AllFiles, record.Title);
                    fileBox.Activate();
                    fileBox.ShowDialog();
                    downloadUrl = fileBox.chosenFileUrl;
                    fileName = fileBox.chosenFileName;
                }
                if (downloadUrl != null && fileName != null)
                {
                    var filePath = await DownloadFile(downloadUrl, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                    if (!cancelled)
                    {
                        //await Task.Run(() => ExtractFile(filePath, game, record));
                        record.Url = downloadUrl;
                        record.ArchiveFilePath = filePath;
                        record.TYPE = DownloadApiBase.CALL_TYPE.DOWNLOAD;
                        await Task.Run(() => Extractor.ExtractLogicAsync(record));
                    }

                }
            }
        }
        public async void DMABrowserDownload(string game, DivaModArchivePost post)
        {
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
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

            DownloadWindow downloadWindow = new DownloadWindow(post);
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
                    UpdateFileBoxDMA fileBox = new UpdateFileBoxDMA(post);
                    fileBox.Activate();
                    fileBox.ShowDialog();
                    if (fileBox.chosenFileUrl == null)
                        return;
                    downloadUrl = fileBox.chosenFileUrl.ToString();
                    fileName = fileBox.chosenFileName;
                }
                if (downloadUrl != null && fileName != null)
                {
                    var filePath = await DownloadFile(downloadUrl, fileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                    if (!cancelled)
                    {
                        post.Url = downloadUrl;
                        post.ArchiveFilePath = filePath;
                        post.TYPE = DownloadApiBase.CALL_TYPE.DOWNLOAD;
                        await Task.Run(() => Extractor.ExtractLogicAsync(post));
                    }
                        
                }
            }
        }
        // Called by OnStartup (One Click Install)
        public async void Download(string line, bool running)
        {
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                MessageBox.Show($"Please click Setup before installing mods!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Global.logger.WriteLine("Please click Setup before installing mods!", LoggerType.Warning);
                return;
            }
            if (ParseProtocol(line))
            {
                if (await GetData())
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
                                //await Task.Run(() => ExtractFile(fileName, response.Game.Name, response));
                                response.Url = URL_TO_ARCHIVE;
                                response.ArchiveFilePath = $"{Global.downloadBaseLocation}{fileName}";
                                response.TYPE = DownloadApiBase.CALL_TYPE.DOWNLOAD;
                                await Task.Run(() => Extractor.ExtractLogicAsync(response));
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
                                UpdateFileBoxDMA fileBox = new UpdateFileBoxDMA(DMAresponse);
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
                                    //await Task.Run(() => ExtractFile(fileName, Global.selected_game, DMAresponse));
                                    DMAresponse.Url = downloadUrl;
                                    DMAresponse.ArchiveFilePath = $"{Global.downloadBaseLocation}{fileName}";
                                    DMAresponse.TYPE = DownloadApiBase.CALL_TYPE.DOWNLOAD;
                                    await Task.Run(() => Extractor.ExtractLogicAsync(DMAresponse));
                                }
                            }
                        }
                    }
                }
            }
            if (running)
                Environment.Exit(0);
        }

        private async Task<bool> GetData()
        {
            try
            {
                if (URL.Contains("gamebanana", StringComparison.CurrentCultureIgnoreCase))
                {
                    string responseString = await client.GetStringAsync(URL);
                    response = JsonSerializer.Deserialize<GameBananaAPIV4>(responseString);
                    fileName = response.Files.Where(x => x.Id == DL_ID).ToArray()[0].FileName;
                    return true;
                }
                else if (URL.Contains("divamodarchive", StringComparison.CurrentCultureIgnoreCase))
                {
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
                    MOD_ID = data[0].Replace("dma/", String.Empty);
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
        private async Task<string> DownloadFile(string uri, string fileName, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken)
        {
            var ret = string.Empty;
            try
            {
                // Create the downloads folder if necessary
                Directory.CreateDirectory($@"{Global.downloadBaseLocation}");
                // Download the file if it doesn't already exist
                ret = $@"{Global.downloadBaseLocation}{fileName}";
                if (File.Exists(ret))
                {
                    try
                    {
                        //FileSystem.DeleteFile($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}", UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                        Extractor.DeleteTemporaryFile(ret);
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
                    $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                progressBox.Close();
            }
            catch (OperationCanceledException)
            {
                // Remove the file is it will be a partially downloaded one and close up
                //FileSystem.DeleteFile($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}", UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                Extractor.DeleteTemporaryFile(ret);
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

            return ret;
        }
    }
}
