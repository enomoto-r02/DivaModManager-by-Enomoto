using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager
{
    public static class Extractor
    {
        /// <summary>
        /// 圧縮ファイルまたはディレクトリを解凍し、Modsフォルダに移動する
        /// </summary>
        /// <param name="apiBase"></param>
        /// <returns>移動後のMODフォルダパス</returns>
        public static async Task<string> ExtractLogicAsync(DownloadApiBase apiBase)
        {
            TextLogger.Log += ObjectDumper.Dump(apiBase, "apiBase");

            if (apiBase == null || !apiBase.SetSkipPathList())
            {
                Global.logger.WriteLine("ExtractLogicAsync Called but Invalid argument return!", LoggerType.Debug);
                return string.Empty;
            }

            // --- 解凍処理 ---
            await Extractor.ExtractAsync(apiBase);
            // 解凍後のModルートパスを取得
            if (string.IsNullOrEmpty(apiBase.TemporaryDirectoryRootPath))
            {
                return string.Empty;
            }

            // --- 移動先パス取得 ---
            GetMoveDirectoryName(apiBase);

            // config.toml更新
            UpdateModFolderConfigToml(apiBase);

            DeleteDirectory(apiBase);
            MoveDirectory(apiBase);

            // サイズチェック
            bool directoryExists = await DirectorySizeMatchAsync(apiBase);
            if (!directoryExists)
            {
                string msg = $"Extracted directory size does not match moved directory size.\nIt may not be processed correctly.";
                MessageBox.Show(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Global.logger.WriteLine(msg, LoggerType.Warning);
            }

            DeleteTemporaryFile(apiBase.ArchiveFilePath);
            DeleteTemporaryDirectory(apiBase.TemporaryDirectoryPath);

            TextLogger.Log += ObjectDumper.Dump(apiBase, "apiBase");
            return apiBase.MoveDirectoryRootPath;
        }

        /// <summary>
        /// 圧縮ファイルを展開する
        /// </summary>
        /// <param name="apiBase"></param>
        /// <returns>展開後のMODフォルダのルートパス(C:/....../temp_xxxx/MOD_NAME)</returns>
        public static async Task<string> ExtractAsync(DownloadApiBase apiBase)
        {
            Global.logger.WriteLine("ExtractAsync Start.", LoggerType.Debug);

            var ret = string.Empty;

            // ディレクトリが指定された場合
            if (Directory.Exists(apiBase.ArchiveFilePath))
            {
                // config.tomlが存在するフォルダパスを確認
                ret = apiBase.TemporaryDirectoryRootPath = GetRootFolderAsync(apiBase.ArchiveFilePath).Result;
                Global.logger.WriteLine("ExtractAsync End. (ディレクトリが指定された)", LoggerType.Debug);
                return ret;
            }

            //var sw = new System.Diagnostics.Stopwatch();
            //sw.Start();

            string _ArchiveType = System.IO.Path.GetExtension(apiBase.ArchiveFilePath);
            apiBase.TemporaryDirectoryPath = $@"{Global.assemblyLocation}Downloads{Global.s}temp_{DateTime.Now:yyyyMMddHHmmssfff}";
            int archiveFileCount = 0;
            int extractedFileCount = 0;

            string extension = System.IO.Path.GetExtension(apiBase.ArchiveFilePath).ToLowerInvariant();
            Global.logger.WriteLine($"Extracting '{System.IO.Path.GetFileName(apiBase.ArchiveFilePath)}'...", LoggerType.Info);
            try
            {
                if (extension == ".7z")
                {
                    Directory.CreateDirectory(apiBase.TemporaryDirectoryPath);

                    // アーカイブ内のファイル数をカウント
                    if (!Global.SevenZipDlllExist)
                    {
                        Global.logger.WriteLine($"Extraction failed because 7z.dll does not exist. Please re-download DivaModManager by Enomoto.", LoggerType.Error);
                        Global.logger.WriteLine($"Path :{Global.SevenZipDlllPath}", LoggerType.Error);
                        return null;
                    }
                    // 展開(処理速度向上のためSevenZipSharp.Interopを使用)
                    using var extractor = new SevenZipExtractor(apiBase.ArchiveFilePath);
                    archiveFileCount = extractor.ArchiveFileData.Count;
                    extractor.ExtractArchive(apiBase.TemporaryDirectoryPath);
                    extractedFileCount = Directory.EnumerateFiles(apiBase.TemporaryDirectoryPath, "*", System.IO.SearchOption.AllDirectories).Count()
                        + Directory.EnumerateDirectories(apiBase.TemporaryDirectoryPath, "*", System.IO.SearchOption.AllDirectories).Count();
                }
                else if (extension == ".zip" || extension == ".rar")
                {
                    Directory.CreateDirectory(apiBase.TemporaryDirectoryPath);

                    using var extractor = ArchiveFactory.Open(apiBase.ArchiveFilePath);
                    archiveFileCount = extractor.Entries.Count(entry => !entry.IsDirectory);
                    extractor.WriteToDirectory(apiBase.TemporaryDirectoryPath,
                        new ExtractionOptions() { Overwrite = true, ExtractFullPath = true, PreserveFileTime = true, PreserveAttributes = false });
                    extractedFileCount = Directory.EnumerateFiles(apiBase.TemporaryDirectoryPath, "*", System.IO.SearchOption.AllDirectories).Count();
                }
                else
                {
                    Global.logger.WriteLine($"Skipping unsupported file type: '{apiBase.ArchiveFilePath}'", LoggerType.Warning);
                    return null;
                }
            }
            catch (InvalidFormatException ex)
            {
                Global.logger.WriteLine($"Format error extracting '{apiBase.ArchiveFilePath}': {ex.Message}", LoggerType.Error);
                // UI スレッドで通知？
                // Dispatcher.InvokeAsync(() => MessageBox.Show($"Could not extract '{Path.GetFileName(fileOrDir)}':\nInvalid archive format.", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            }
            catch (IOException ex)
            {
                Global.logger.WriteLine($"IO error extracting '{apiBase.ArchiveFilePath}': {ex.Message}", LoggerType.Error);
                return null;
            }
            catch (Exception ex) // SharpCompress の他の例外
            {
                Global.logger.WriteLine($"Error extracting '{apiBase.ArchiveFilePath}': {ex}", LoggerType.Error);
                return null;
            }

            // 解凍後にファイル数相違
            if (archiveFileCount == 0 || archiveFileCount != extractedFileCount)
            {
                string msg = $"Extracted file or directory count ({extractedFileCount}) does not match archive file or directory count ({archiveFileCount}).\nIt may not have been extract correctly.";
                MessageBox.Show(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Global.logger.WriteLine(msg, LoggerType.Warning);
                // 処理は継続
            }

            await CheckDummyModAsync(apiBase);

            // config.tomlが存在するフォルダパスを取得し、ルートフォルダを決定(二重フォルダチェック)
            apiBase.TemporaryDirectoryRootPath = await GetRootFolderAsync(apiBase.TemporaryDirectoryPath, apiBase.ArchiveFilePath);

            // mod.json生成
            CreateModJson(apiBase);

            if (string.IsNullOrEmpty(apiBase.TemporaryDirectoryRootPath))
            {
                Global.logger.WriteLine($"Extract Canceled! '{System.IO.Path.GetDirectoryName(apiBase.ArchiveFilePath)}", LoggerType.Info);
            }
            else
            {
                Global.logger.WriteLine($"Extracted! '{System.IO.Path.GetDirectoryName(apiBase.TemporaryDirectoryPath)}", LoggerType.Info);
            }

            DeleteTemporaryFile(apiBase.ArchiveFilePath);

            //sw.Stop();
            //TimeSpan ts = sw.Elapsed;
            //Global.logger.WriteLine($"{ts.Hours}時間 {ts.Minutes}分 {ts.Seconds}秒 {ts.Milliseconds}ミリ秒", LoggerType.Debug);
            
            Global.logger.WriteLine("ExtractAsync End.", LoggerType.Debug);
            return apiBase.TemporaryDirectoryRootPath;
        }

        /// <summary>
        /// ディレクトリのサイズを読み込む
        /// </summary>
        public static async Task TryLoadDirectorySizeAsync(Mod mod, string modDirectoryPath)
        {
            bool isDirectoryPath = await DirectoryExistsAsync(modDirectoryPath);
            if (!isDirectoryPath)
            {
                return; // ファイルがなければ何もしない
            }

            try
            {
                mod._directorySize = await GetDirectoriesSizeAsync(modDirectoryPath);
            }
            catch (Exception ex) // その他の予期せぬエラー
            {
                Global.logger.WriteLine($"Unexpected error processing in TryLoadDirectorySizeAsync at {modDirectoryPath}: {ex.Message}", LoggerType.Error);
            }
        }

        private static async Task<long> GetDirectorySizeAsync(string path, List<string> skipFilePathList = null)
        {
            var dirInfo = new DirectoryInfo(path);

            long DirectorySize = 0;
            var sumFlg = true;
            foreach (FileInfo fi in dirInfo.GetFiles())
            {
                if (skipFilePathList != null)
                    foreach (var skipFilePath in skipFilePathList)
                        if (!skipFilePath.StartsWith(fi.FullName))
                            sumFlg = false;
                if (sumFlg)
                    DirectorySize += fi.Length;
            }
            foreach (DirectoryInfo di in dirInfo.GetDirectories())
                DirectorySize += await GetDirectorySizeAsync(di.FullName, skipFilePathList);
            return DirectorySize;
        }

        public static async Task<bool> DirectoryExistsAsync(string path)
        {
            try
            {
                return await Task.Run(() => Directory.Exists(path));
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Error checking directory existence for '{path}': {ex.Message}", LoggerType.Warning);
                return false;
            }
        }

        public static async Task<string[]> GetDirectoriesAsync(string path)
        {
            try
            {
                return await Task.Run(() => Directory.GetDirectories(path));
            }
            catch (UnauthorizedAccessException ex)
            {
                Global.logger.WriteLine($"Permission error getting directories in '{path}': {ex.Message}", LoggerType.Error);
                return Array.Empty<string>();
            }
            catch (IOException ex)
            {
                Global.logger.WriteLine($"IO error getting directories in '{path}': {ex.Message}", LoggerType.Error);
                return Array.Empty<string>();
            }
            catch (Exception ex) // その他の予期せぬエラー
            {
                Global.logger.WriteLine($"Unexpected error getting directories in '{path}': {ex.Message}", LoggerType.Error);
                return Array.Empty<string>();
            }
        }

        private static async Task<long> GetDirectoriesSizeAsync(string path)
        {
            try
            {
                return await Task.Run(() => GetDirectorySizeAsync(path));
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Error getting directories size in {path}: {ex.Message}", LoggerType.Error);
                return -1; // -1を返すことでエラーを示す
            }
        }

        private static async Task<bool> DirectorySizeMatchAsync(DownloadApiBase apiBase)
        {
            var extractDirectoryPath = apiBase.TemporaryDirectoryRootPath;
            var moveDirectoryPath = apiBase.MoveDirectoryRootPath;
            apiBase.TemporaryDirectoryRootSize = await Extractor.GetDirectorySizeAsync(extractDirectoryPath, apiBase.SkipFilePathList);
            apiBase.MoveDirectoryRootSize = await Extractor.GetDirectorySizeAsync(moveDirectoryPath, apiBase.SkipFilePathList);

            return !apiBase.TemporaryDirectoryRootSize.Equals("0") && apiBase.TemporaryDirectoryRootSize == apiBase.MoveDirectoryRootSize;
        }

        public static bool DeleteTemporaryDirectory(string tempPath)
        {
            var ret = false;
            try
            {
                if (!string.IsNullOrEmpty(tempPath)
                    && Directory.Exists(tempPath)
                    && tempPath.StartsWith($@"{Global.downloadBaseLocation}temp_"))
                {
                    Directory.Delete(tempPath, true);
                    ret = true;
                }
            }
            catch (Exception ex)
            {
                ret = false;
                Global.logger.WriteLine($"Error deleting temporary directory '{tempPath}': {ex.Message}", LoggerType.Error);
            }
            return ret;
        }

        public static bool DeleteTemporaryFile(string tempPath)
        {
            var ret = false;
            try
            {
                if (!string.IsNullOrEmpty(tempPath)
                    && File.Exists(tempPath)
                    && tempPath.StartsWith($@"{Global.downloadBaseLocation}"))
                {
                    File.Delete(tempPath);
                    ret = true;
                }
            }
            catch (Exception ex)
            {
                ret = false;
                Global.logger.WriteLine($"Error deleting temporary directory '{tempPath}': {ex.Message}", LoggerType.Error);
            }
            return ret;
        }

        /// <summary>
        /// CopyDirectoryRecursionを呼び出す(ログ出力用)
        /// </summary>
        /// <param name="extractDirectoryPath">コピー元ルートディレクトリパス</param>
        /// <param name="moveDirectoryPath">コピー先ルートディレクトリパス</param>
        public static void MoveDirectory(DownloadApiBase apiBase)
        {
            Global.logger.WriteLine($"Directory Copying... Path:{apiBase.TemporaryDirectoryRootPath}", LoggerType.Debug);
            if (apiBase.TYPE == DownloadApiBase.CALL_TYPE.NONE)
            {
                return;
            }
            MoveDirectoryRecursion(apiBase.TemporaryDirectoryRootPath, apiBase.MoveDirectoryRootPath, apiBase.SkipFilePathList);
            Global.logger.WriteLine($"Directory Copy Complete! Path:{apiBase.MoveDirectoryRootPath}", LoggerType.Debug);
        }

        /// <summary>
        /// ディレクトリにあるファイルのコピー(子フォルダ含む)
        /// </summary>
        /// <param name="extractDirectoryPath">コピー元ルートディレクトリ</param>
        /// <param name="moveDirectoryPath">コピー先ルートディレクトリ</param>
        private static void MoveDirectoryRecursion(string extractDirectoryPath, string moveDirectoryPath, List<string> moveSkipPathList = null)
        {
            try
            {
                var moveRootDirectory = $"{extractDirectoryPath.Replace(extractDirectoryPath, moveDirectoryPath)}";
                Directory.CreateDirectory(moveRootDirectory);
                var inExtractDirectoryFilePathList = Directory.GetFiles(extractDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly);
                foreach (var inExtractDirectoryFilePath in inExtractDirectoryFilePathList)
                {
                    var moveFilePath = $"{inExtractDirectoryFilePath.Replace(extractDirectoryPath, moveDirectoryPath)}";
                    var skip = false;
                    if (moveSkipPathList != null)
                    {
                        foreach (var moveSkipPath in moveSkipPathList)
                        {
                            if (moveFilePath.StartsWith(moveSkipPath))
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (skip)
                        continue;
                    File.Copy(inExtractDirectoryFilePath, moveFilePath);
                }
                foreach (var childDirectorie in Directory.GetDirectories(extractDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var moveRootDirectoryChild = $"{childDirectorie.Replace(childDirectorie, moveDirectoryPath)}{Global.s}{System.IO.Path.GetFileName(childDirectorie)}";
                    MoveDirectoryRecursion(childDirectorie, moveRootDirectoryChild);
                }
            }
            catch (Exception ex)
            {
                // 個々のフォルダ移動エラーログ
                Global.logger?.WriteLine($"Error moving Directory '{extractDirectoryPath}' to '{moveDirectoryPath}': {ex.Message}", LoggerType.Error);
                // Global.logger が null の可能性？ static method なので注意
                // エラーがあっても続行するか、中断するか？
            }
        }

        /// <summary>
        /// CopyDirectoryRecursionを呼び出す(ログ出力用)
        /// </summary>
        /// <param name="extractDirectoryPath">コピー元ルートディレクトリパス</param>
        /// <param name="moveDirectoryPath">コピー先ルートディレクトリパス</param>
        public static void MoveDirectoryInTemporary(string extractDirectoryPath, string moveDirectoryPath)
        {
            MoveDirectoryInTemporaryRecursion(extractDirectoryPath, moveDirectoryPath);
        }

        /// <summary>
        /// ディレクトリにあるファイルの移動(子フォルダ含む)
        /// </summary>
        /// <param name="extractDirectoryPath">移動元ルートディレクトリパス</param>
        /// <param name="moveDirectoryRootPath">移動先ルートディレクトリパス</param>
        private static void MoveDirectoryInTemporaryRecursion(string extractDirectoryRootPath, string moveDirectoryRootPath)
        {
            try
            {
                var inExtractDirectoryFileNames = Directory.GetFiles(extractDirectoryRootPath, "*", System.IO.SearchOption.TopDirectoryOnly);
                foreach (var inExtractDirectoryFileName in inExtractDirectoryFileNames)
                {
                    var moveFilePath = $"{extractDirectoryRootPath.Replace(extractDirectoryRootPath, $"{moveDirectoryRootPath}{Global.s}")}";
                    File.Move(
                        $"{inExtractDirectoryFileName}",
                        $"{moveFilePath}{System.IO.Path.GetFileName(inExtractDirectoryFileName)}"
                    );
                }
                foreach (var childDirectorie in Directory.GetDirectories(extractDirectoryRootPath, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var childDirectoryName = System.IO.Path.GetFileName(childDirectorie);
                    if (!childDirectoryName.StartsWith("temp_") && childDirectorie != moveDirectoryRootPath)
                    {
                        var moveNextDirectoryPath = $"{extractDirectoryRootPath.Replace(extractDirectoryRootPath, $"{moveDirectoryRootPath}{Global.s}{childDirectoryName}")}";
                        Directory.CreateDirectory($"{moveNextDirectoryPath}");
                        MoveDirectoryInTemporaryRecursion($"{childDirectorie}{Global.s}", moveNextDirectoryPath);
                    }

                }
            }
            catch (Exception ex)
            {
                // 個々のフォルダ移動エラーログ
                Global.logger?.WriteLine($"Error moving Directory '{extractDirectoryRootPath}' to '{moveDirectoryRootPath}': {ex.Message}", LoggerType.Error);
                // エラーがあっても続行
            }
        }

        private static void CreateModJson(DownloadApiBase apiBase)
        {
            // ダウンロード、アップデートの場合は処理を継続
            if ((apiBase.TYPE != DownloadApiBase.CALL_TYPE.DOWNLOAD && apiBase.TYPE != DownloadApiBase.CALL_TYPE.UPDATE)
                || string.IsNullOrEmpty(apiBase.TemporaryDirectoryRootPath))
            {
                return;
            }

            // mod.jsonが存在しない場合、mod.jsonを生成する
            var modJsonPath = $@"{apiBase.TemporaryDirectoryRootPath}{Global.s}mod.json";
            if (!File.Exists(modJsonPath))
            {
                MetadataManager metadataManager = new MetadataManager(apiBase);
                metadataManager.metadata.SaveMetadata(modJsonPath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ArchiveDestination"></param>
        /// <returns></returns>
        private static async Task<string> CheckDummyModAsync(DownloadApiBase apiBase)
        {
            var ret = apiBase.TemporaryDirectoryPath;
            if (apiBase.TYPE == DownloadApiBase.CALL_TYPE.DROP || string.IsNullOrEmpty(apiBase.Url))
            {
                return ret;
            }

            var config_toml_cnt = Directory.GetFiles(apiBase.TemporaryDirectoryPath, "config.toml", System.IO.SearchOption.AllDirectories).Length;
            var file_size = new FileInfo(apiBase.ArchiveFilePath).Length;

            if (config_toml_cnt == 0 && 1024 * 1024 > file_size)
            {
                ret = string.Empty;
                DeleteTemporaryFile(apiBase.ArchiveFilePath);
                DeleteTemporaryDirectory(apiBase.TemporaryDirectoryPath);
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    DmmMessageWindow msgWindow = new(
                        "The MOD file could not be saved.",
                        "This MOD may have alternative file sources.\nWould you like to open the MOD page?",
                        "Information"
                    );
                    msgWindow.ShowDialog();

                    if (msgWindow.YesNo)
                    {
                        Global.TryStartProcess(apiBase.Url);
                    }
                });
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ArchiveDestination"></param>
        /// <param name="_ArchiveSourcePath"></param>
        /// <returns></returns>
        private static async Task<string> GetRootFolderAsync(string ArchiveDestination, string _ArchiveSourcePath = null)
        {
            var ret = ArchiveDestination;

            // 圧縮ファイルを展開していた場合
            if (!string.IsNullOrEmpty(_ArchiveSourcePath))
            {
                // 圧縮ファイルがファイルを直接圧縮していた場合
                if(Directory.GetFiles(ArchiveDestination, "*", System.IO.SearchOption.TopDirectoryOnly).Length > 1)
                {
                    // ファイル名からフォルダを作成し、そこをルートフォルダとする
                    var newDir = $@"{ArchiveDestination}{Global.s}{System.IO.Path.GetFileNameWithoutExtension(_ArchiveSourcePath)}";
                    Directory.CreateDirectory(newDir);
                    MoveDirectoryInTemporary(ArchiveDestination, newDir);
                    ret = newDir;
                }
                // フォルダを圧縮している圧縮ファイル
                else
                {
                    ret = Directory.GetDirectories(ArchiveDestination, "*", System.IO.SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty;
                }
            }

            // 2重フォルダチェック
            var configTomlDestination = GetConfigTomlDirectory(ret);
            if (ret != configTomlDestination)
            {
                // config.tomlがあるフォルダをルートフォルダとして良いか確認
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    DmmMessageWindow dmmMW = new DmmMessageWindow(
                        "Check for the existence of config.toml",
                        $"There is one config.toml file, but it is not in the root folder.\r\nThis may be a duplicate folder.\r\nDo you want to use the folder containing config.toml as the root folder?",
                        "Information"
                    );
                    dmmMW.ShowDialog();
                    ret = dmmMW.YesNo ? configTomlDestination : string.Empty;
                });
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        private static string GetConfigTomlDirectory(string modDirectoryRootPath)
        {
            var ret = string.Empty;
            var dirs = Directory.GetFiles(modDirectoryRootPath, "config.toml", System.IO.SearchOption.AllDirectories);
            if (dirs.Length != 1)
            {
                Global.logger.WriteLine($"There are multiple config.toml files.", LoggerType.Warning);
                ret = modDirectoryRootPath;
            }
            else
            {
                // config.tomlのあるフォルダを返す
                ret = System.IO.Path.GetDirectoryName((System.IO.Path.GetFullPath(dirs.FirstOrDefault())));
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modDirectoryRootPath">MODフォルダパス(MOD_NAME)</param>
        /// <returns>移動先ディレクトリパス</returns>
        private static void GetMoveDirectoryName(DownloadApiBase apiBase)
        {
            // すでに移動先パスが設定されている場合(UPDATE時)
            if (!string.IsNullOrEmpty(apiBase.MoveDirectoryRootPath))
            {
                return;
            }

            string directoryRootPath = apiBase.TemporaryDirectoryRootPath;

            if (!Directory.Exists(directoryRootPath))
            {
                Global.logger.WriteLine($"The extracted directory '{directoryRootPath}' does not exist.", LoggerType.Error);
                return;
            }

            var directoryName = System.IO.Path.GetFileName(directoryRootPath);


            // Modsフォルダ直下に同名フォルダが存在する場合、連番を付与
            string moveDirNameBase = $@"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}{directoryName}";
            string moveDirNameCopyedName = moveDirNameBase;

            if (apiBase.TYPE != DownloadApiBase.CALL_TYPE.UPDATE)
            {
                int index = 1;
                while (Directory.Exists(moveDirNameCopyedName))
                {
                    moveDirNameCopyedName = $@"{moveDirNameBase} ({index})";
                    index += 1;
                }
            }
            
            apiBase.MoveDirectoryRootPath = moveDirNameCopyedName;
        }

        private static string UpdateModFolderConfigToml(DownloadApiBase apiBase)
        {
            string ret = string.Empty;
            bool updateConfigToml = false;

            string temporaryDirectoryModRootPath = apiBase.TemporaryDirectoryRootPath;
            string modDirectoryRootPath = apiBase.MoveDirectoryRootPath;

            // アップデート時かつ新旧のconfig.tomlが存在する場合のみ続行
            if (apiBase == null 
                || apiBase.TYPE != DownloadApiBase.CALL_TYPE.UPDATE
                || string.IsNullOrEmpty(temporaryDirectoryModRootPath)
                || string.IsNullOrEmpty(modDirectoryRootPath))
            {
                return ret;
            }
            var oldConfigPath = $@"{modDirectoryRootPath}{Global.s}config.toml";
            var newConfigPath = $@"{temporaryDirectoryModRootPath}{Global.s}config.toml";

            TomlTable oldConfig = null;
            if (File.Exists(oldConfigPath))
            {
                Toml.TryToModel(File.ReadAllText(oldConfigPath), out oldConfig, out var diagnostics);
            }
            else
            {
                Global.logger.WriteLine($"config.toml in the Mods folder is missing or has an incorrect format. Please download the latest version instead of updating. Path:{oldConfigPath}", LoggerType.Error);
                return ret;
            }
                TomlTable newConfig = null;
            if (File.Exists(newConfigPath))
            {
                Toml.TryToModel(File.ReadAllText(newConfigPath), out newConfig, out var diagnostics);
            }
            else
            {
                Global.logger.WriteLine($"The downloaded mod's config.toml is missing or malformed. Please contact the mod creator. Path:{oldConfigPath}", LoggerType.Error);
                return ret;
            }

            // Use all old config values that aren't metadata to be shown
            if (oldConfig != null && newConfig != null)
            {
                foreach (var key in oldConfig.Keys)
                {
                    // これらの項目以外は旧config.tomlの記載を引き継ぐ
                    if (newConfig.ContainsKey(key)
                        && key.ToLowerInvariant() != "name"
                        && key.ToLowerInvariant() != "description"
                        && key.ToLowerInvariant() != "version"
                        && key.ToLowerInvariant() != "date"
                        && key.ToLowerInvariant() != "author"
                        && key.ToLowerInvariant() != "include"  // 追加
                    )
                    {
                        newConfig[key] = oldConfig[key];
                        updateConfigToml = true;
                    }
                }
                if (updateConfigToml)
                {
                    File.WriteAllText(newConfigPath, Toml.FromModel(newConfig));
                    ret = newConfigPath;
                }
            }
            return ret;
        }

        /// <summary>
        /// DeleteDirectoryRecursionを呼び出す(ログ出力用)
        /// </summary>
        /// <param name="extractDirectoryPath">コピー元ルートディレクトリパス</param>
        /// <param name="moveDirectoryPath">コピー先ルートディレクトリパス</param>
        public static void DeleteDirectory(DownloadApiBase apiBase)
        {
            if (apiBase.TYPE == DownloadApiBase.CALL_TYPE.UPDATE
                && !string.IsNullOrEmpty(apiBase.MoveDirectoryRootPath)
                // Modsフォルダが設定されている
                && apiBase.MoveDirectoryRootPath.StartsWith($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}")
                // Modsフォルダ以降のフォルダ
                && !string.IsNullOrEmpty(apiBase.MoveDirectoryRootPath.Replace($"{Global.config.Configs[Global.config.CurrentGame].ModsFolder}{Global.s}", ""))
            )
            {
                Global.logger.WriteLine($"Directory Deleting... Path:{apiBase.MoveDirectoryRootPath}", LoggerType.Debug);
                DeleteDirectoryRecursion(apiBase.MoveDirectoryRootPath, apiBase.SkipFilePathList);
                Global.logger.WriteLine($"Directory Deleting Complete! Path:{apiBase.MoveDirectoryRootPath}", LoggerType.Info);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deleteDirectoryPath">削除先ルートディレクトリ</param>
        private static void DeleteDirectoryRecursion(string deleteDirectoryPath, List<string> skipPathList = null)
        {
            try
            {
                var inDirectoryFilePathList = Directory.GetFiles(deleteDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly);
                foreach (var childDirectorie in Directory.GetDirectories(deleteDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    DeleteDirectoryRecursion(childDirectorie, skipPathList);
                }
                foreach (var inDirectoryFilePath in inDirectoryFilePathList)
                {
                    var skip = false;
                    if (skipPathList != null)
                    {
                        foreach (var moveSkipPath in skipPathList)
                        {
                            if (inDirectoryFilePath.StartsWith(moveSkipPath))
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (skip)
                        continue;
                    File.Delete(inDirectoryFilePath);
                }
                try
                {
                    Directory.Delete(deleteDirectoryPath);
                }
                catch(IOException)
                {
                    // IOExceptionは表示しない(ディレクトリは空ではない場合を想定)
                }
            }
            catch (Exception ex)
            {
                Global.logger?.WriteLine($"Error deleting Directory  '{deleteDirectoryPath}': {ex.Message}", LoggerType.Error);
            }
        }
    }
}
