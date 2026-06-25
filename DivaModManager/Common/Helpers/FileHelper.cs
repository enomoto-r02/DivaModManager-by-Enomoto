using DivaModManager.Common.Config;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Extract;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Common.Helpers
{
    public static class FileHelper
    {
        public enum DIVA_PATH_RESULT
        {
            // NOTでなければ削除、という判定方法はしないように
            NOT = 0,
            ERROR_CONFIG_PATH,
            DOWNLOAD_MOD_FILE,
            TEMP_DIRECTORY,
            MODS_DIRECTORY,
            MODS_FILE,
            DML_DOWNLOAD_FILE,
            DML_TEMP_FILE,
            DML_TEMP_DIRECTORY,
            DML_TOML,
            DMM_TOML,
            DMM_JSON,
            DMM_LOG,
            DMM_SEVEN_ZIP,
            WIN_RAR,
            DMM_CACHE,
        }

        /// <summary>
        /// DivaModManager管理下のファイルを削除する
        /// </summary>
        /// <param name="deleteFilePath"></param>
        /// <returns>DivaModManager管理下のファイルで、ファイルを削除したか元からファイルが存在しなかった場合はtrue</returns>
        public static bool DeleteFile(string deleteFilePath, List<string> skipFileList = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, deleteFilePath:\"{deleteFilePath}\", skipFileList:{Util.GetListToParamString(skipFileList)}";
            //Logger.WriteLine(string.Join(" ", $"{MeInfo}", "Start."), LoggerType.Debug, param: ParamInfo);

            if (string.IsNullOrWhiteSpace(deleteFilePath) || !System.IO.File.Exists(deleteFilePath))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, "End.", $"File.Exists is false. deleteFilePath:\"{deleteFilePath}\""), LoggerType.Debug, param: ParamInfo);
                return true;
            }

            var ret = false;
            try
            {
                var isDivaModDirectory = IsDivaModFileOrDirectory(deleteFilePath);
                if (isDivaModDirectory == DIVA_PATH_RESULT.DOWNLOAD_MOD_FILE
                    || isDivaModDirectory == DIVA_PATH_RESULT.MODS_FILE         // CLEAN UPDATEの時
                    || isDivaModDirectory == DIVA_PATH_RESULT.DMM_TOML
                    || isDivaModDirectory == DIVA_PATH_RESULT.DMM_JSON
                    || isDivaModDirectory == DIVA_PATH_RESULT.DML_TOML
                    || isDivaModDirectory == DIVA_PATH_RESULT.DML_TEMP_FILE
                    || isDivaModDirectory == DIVA_PATH_RESULT.DML_TEMP_DIRECTORY
                    || isDivaModDirectory == DIVA_PATH_RESULT.DMM_LOG
                    || isDivaModDirectory == DIVA_PATH_RESULT.DMM_CACHE)
                {
                    if (TryFindUnsafeFileSystemReference(deleteFilePath, out var unsafeReference))
                    {
                        Logger.WriteLine($"DeleteFile: Cannot delete '{deleteFilePath}' because it is unsafe. {unsafeReference}", LoggerType.Error);
                        return false;
                    }

                    var info = new FileInfo(deleteFilePath);
                    var fullPath = Path.GetFullPath(deleteFilePath);
                    Logger.WriteLine($"DeleteFile: File info for '{deleteFilePath}': fullPath='{fullPath}', Attributes={info.Attributes}, LinkTarget={info.LinkTarget ?? "null"}", LoggerType.Debug);

                    ret = Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Global.IsWine)
                        {
                            System.IO.File.Delete(deleteFilePath);
                        }
                        else
                        {
                            // Windows native keeps the previous recycle-bin behavior.
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                deleteFilePath,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
                        }

                        // 成功したら true を返す
                        return true;
                    });
                }
            }
            catch (Exception ex)
            {
                ret = false;
                ParamInfo += $"ex.Message:\n{ex.Message}\nex.StackTrace:\n{ex.StackTrace}";
                Logger.WriteLine($"Error deleting temporary directory. Path:\"{deleteFilePath}\", Return:{ret}", LoggerType.Error, param: ParamInfo);
            }
            //Logger.WriteLine(string.Join(" ", MeInfo, "End."), LoggerType.Debug, param: ParamInfo);

            return ret;
        }

        /// <summary>
        /// DivaModManager管理下のフォルダを削除する
        /// </summary>
        /// <param name="deleteDirectoryPath"></param>
        /// <returns></returns>
        public static bool DeleteDirectory(string deleteDirectoryPath, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, deleteDirectoryPath:{deleteDirectoryPath}";
            //Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug, param: ParamInfo);

            if (string.IsNullOrWhiteSpace(deleteDirectoryPath) || !Directory.Exists(deleteDirectoryPath))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, "End.", $"Directory.Exists is false. deleteDirectoryPath:{deleteDirectoryPath}"), LoggerType.Debug, param: ParamInfo);
                return true;
            }

            var ret = false;

            try
            {
                var isDivaModDirectory = IsDivaModFileOrDirectory(deleteDirectoryPath);
                if (isDivaModDirectory == DIVA_PATH_RESULT.TEMP_DIRECTORY
                    || isDivaModDirectory == DIVA_PATH_RESULT.MODS_DIRECTORY
                    || isDivaModDirectory == DIVA_PATH_RESULT.DML_TEMP_DIRECTORY
                    || isDivaModDirectory == DIVA_PATH_RESULT.DMM_CACHE)
                {
                    if (TryFindUnsafeFileSystemReference(deleteDirectoryPath, out var unsafeReference))
                    {
                        var info = new DirectoryInfo(deleteDirectoryPath);
                        var fullPath = Path.GetFullPath(deleteDirectoryPath);
                        Logger.WriteLine($"DeleteDirectory: Directory info for '{deleteDirectoryPath}': fullPath='{fullPath}', Attributes={info.Attributes}, LinkTarget={info.LinkTarget ?? "null"}", LoggerType.Debug);

                        Logger.WriteLine($"DeleteDirectory: Cannot delete '{deleteDirectoryPath}' because it contains unsafe file system references. {unsafeReference}", LoggerType.Error);
                        return false;
                    }
                    var delDirSize = GetDirectoriesSize(deleteDirectoryPath);
                    if (delDirSize > Global.ConfigToml.WarningDeleteDirectorySize * 1000000)   // MB
                    {
                        List<string> replaceList = new List<string>()
                        {
                            GetDirectorySizeView2(Global.ConfigToml.WarningDeleteDirectorySize * 1000000),
                            new DirectoryInfo(deleteDirectoryPath).Name,
                            GetDirectorySizeView2(delDirSize),
                        };
                        var warnMsg = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(50, replaceList, path: deleteDirectoryPath));
                        if (warnMsg == WindowHelper.WindowCloseStatus.Yes)
                        {
                            ret = DeleteDirectoryRecursiveSafe(deleteDirectoryPath);
                        }
                    }
                    else
                    {
                        ret = DeleteDirectoryRecursiveSafe(deleteDirectoryPath);
                    }
                }
                else
                {
                    Logger.WriteLine($"DeleteDirectory Failed. Path:{deleteDirectoryPath}, isDivaModDirectory:{isDivaModDirectory}", LoggerType.Error);
                }
            }
            catch (Exception ex)
            {
                ret = false;
                ParamInfo += $"Path:{deleteDirectoryPath}, Return:{ret}, ex.Message:{ex.Message}, ex.StackTrace:{ex.StackTrace}";
                Logger.WriteLine($"{MeInfo} Exception.", LoggerType.Debug, param: ParamInfo);
            }

            //Logger.WriteLine($"{MeInfo} End. Path:{deleteDirectoryPath}, Return:{ret}", LoggerType.Debug);

            return ret;
        }

        /// <summary>
        /// CLEAN UPDATE先のディレクトリ削除処理
        /// </summary>
        /// <param name="deletePath"></param>
        /// <param name="skipFileList"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        public static bool DeleteDirectoryForCleanUpdate(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            var ret = false;

            // ここで必ずModのアップデートであることを確認
            var check_type =
                (extract.Type == ExtractInfo.TYPE.CLEAN_UPDATE
                && extract.Site != ExtractInfo.SITE.DML);
            if (!check_type)
            {
                ret = true;
                Logger.WriteLine($"{MeInfo} End. Return:{ret}, ExtractInfo.Type:{extract.Type}", LoggerType.Debug);
                return ret;
            }

            var mv = extract.MoveInfoList.LastOrDefault();
            var isDivaModDirectory = IsDivaModFileOrDirectory(mv.FullPathResult);
            if (isDivaModDirectory == DIVA_PATH_RESULT.MODS_DIRECTORY)
            {
                if (TryFindUnsafeFileSystemReference(mv.FullPathResult, out var unsafeReference))
                {
                    var info = new DirectoryInfo(mv.FullPathResult);
                    var fullPath = Path.GetFullPath(mv.FullPathResult);
                    Logger.WriteLine($"DeleteDirectoryForCleanUpdate: Directory info for '{mv.FullPathResult}': fullPath='{fullPath}', Attributes={info.Attributes}, LinkTarget={info.LinkTarget ?? "null"}", LoggerType.Debug);

                    Logger.WriteLine($"DeleteDirectoryForCleanUpdate: Cannot delete '{mv.FullPathResult}' because it contains unsafe file system references. {unsafeReference}", LoggerType.Error);
                    ret = false;
                    return ret;
                }
                Logger.WriteLine($"Directory Deleting for Clean Update... Path:{mv.FullPathResult}", LoggerType.Info);
                var skipSize = DeleteDirectoryForCleanUpdateRecursion(mv.FullPathResult, extract.SkipFilePathList);
                ret = skipSize.Count == 0;
                if (!ret)
                {
                    List<FileInfo> fileList = skipSize.OfType<FileInfo>().ToList();
                    List<DirectoryInfo> directoryList = skipSize.OfType<DirectoryInfo>().ToList();
                    extract.MoveInfoList.LastOrDefault().FullPathSkip.AddRange(fileList);
                    extract.MoveInfoList.LastOrDefault().FullPathSkip.AddRange(directoryList);
                }

                Logger.WriteLine($"Directory Deleting for Clean Update Complete! Path:{mv.FullPathResult}", LoggerType.Info, dump: ObjectDumper.Dump(extract, $"{MeInfo} End."));
            }

            //Logger.WriteLine($"{MeInfo} End. Return:{ret}", LoggerType.Debug);

            return ret;
        }

        /// <summary>
        /// CLEAN UPDATE先のディレクトリ削除処理(回帰的削除)
        /// </summary>
        /// <param name="deleteDirectoryPath"></param>
        /// <param name="skipPathList"></param>
        /// memo: 返却をList<FileSystemInfo>にしたけど、基本CLEAN UPDATEだからskipFileListはCount==0のはずだし、そうそう失敗しないはず…。
        private static List<object> DeleteDirectoryForCleanUpdateRecursion(string deleteDirectoryPath, List<string> skipFileList)
        {
            List<object> ret = new();
            var inDirectoryFilePathList = Directory.GetFiles(deleteDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly);
            foreach (var childDirectorie in Directory.GetDirectories(deleteDirectoryPath, "*", System.IO.SearchOption.TopDirectoryOnly))
            {
                ret.AddRange(DeleteDirectoryForCleanUpdateRecursion(childDirectorie, skipFileList));
            }
            foreach (var inDirectoryFilePath in inDirectoryFilePathList)
            {
                var skip = false;
                if (skipFileList != null)
                {
                    foreach (var skipFile in skipFileList)
                    {
                        if (FileHelper.PathStartsWith(inDirectoryFilePath, skipFile))
                        {
                            skip = true;
                            break;
                        }
                    }
                }
                if (skip)
                {
                    ret.Add(new FileInfo(inDirectoryFilePath));
                    continue;
                }
                if (!FileHelper.DeleteFile(inDirectoryFilePath))
                {
                    ret.Add(new FileInfo(inDirectoryFilePath));
                }
            }
            if (!Directory.EnumerateFileSystemEntries(deleteDirectoryPath).Any())
            {
                if (!FileHelper.DeleteDirectory(deleteDirectoryPath))
                {
                    ret.Add(new DirectoryInfo(deleteDirectoryPath));
                }
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath">コピー元のファイルパス</param>
        /// <param name="oldVersion">コピー時にファイル名に付与するバージョン文字列</param>
        /// <param name="IsOriginalFileDelete">コピー後にオリジナルのファイルを削除するか</param>
        /// <param name="caller"></param>
        /// <returns>コピー元（退避後）ファイルのフルパス</returns>
        public static string CopyFile(string filePath, string oldVersion = "", bool IsOriginalFileDelete = false, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            var ret = string.Empty;

            try
            {
                if (FileExists(filePath))
                {
                    var directoryFullPath = Path.GetDirectoryName(filePath) ?? string.Empty;
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    var extension = Path.GetExtension(filePath);
                    var version = string.IsNullOrEmpty(oldVersion) ? string.Empty : $"_v{oldVersion}";
                    var backupFileName = Path.Combine(directoryFullPath, $"{fileNameWithoutExtension}{version}_{Global.STARTED_DATETIME:yyyyMMddHHmmssfff}{extension}");
                    System.IO.File.Copy(filePath, backupFileName);
                    if (IsOriginalFileDelete) { DeleteFile(filePath); }
                    ret = backupFileName;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"{MeInfo} Exception.", LoggerType.Debug);
            }

            //Logger.WriteLine($"{MeInfo} End, Return:{ret}", LoggerType.Debug);

            return ret;
        }

        /// <summary>
        /// DivaModManagerの一時フォルダにフォルダをコピー
        /// </summary>
        /// <param name="copyPath"></param>
        /// <returns></returns>
        public static bool CopyDirectory(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            var ret = false;
            var mv = extract.MoveInfoList.LastOrDefault();

            try
            {
                var isDivaModDirectory = IsDivaModFileOrDirectory(mv.FullPathResult);
                if (isDivaModDirectory == DIVA_PATH_RESULT.TEMP_DIRECTORY
                    || (extract.Site == ExtractInfo.SITE.DML && isDivaModDirectory == DIVA_PATH_RESULT.DML_DOWNLOAD_FILE))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(mv.FullPath, mv.FullPathResult, true);
                    ret = true;
                }
                else
                {
                    ret = false;
                    Logger.WriteLine($"{MeInfo} Unexpected. mv.FullPathResult:{mv.FullPathResult}, Return:{ret}", LoggerType.Debug);
                }
            }
            catch (Exception ex)
            {
                ret = false;
                ParamInfo += $"mv.FullPathResult:{mv.FullPathResult}, Return:{ret}, ex.Message:{ex.Message}, ex.StackTrace:{ex.StackTrace}";
                Logger.WriteLine($"{MeInfo} Exception.", LoggerType.Debug);
            }

            //Logger.WriteLine($"{MeInfo} End, Return:{ret}", LoggerType.Debug);

            return ret;
        }

        /// <summary>
        /// DivaModManagerが管理するファイル、ディレクトリか判定する
        /// </summary>
        /// <param name="targetPath">チェック対象パス</param>
        /// <returns></returns>
        public static DIVA_PATH_RESULT IsDivaModFileOrDirectory(string targetPath, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, targetPath:{targetPath}";
            // Logger.WriteLine($"{MeInfo} Start.", LoggerType.Debug);

            var ret = DIVA_PATH_RESULT.NOT;
            var targetFullPath = Path.GetFullPath(targetPath);

            // 設定ファイルでDMM/DownloadsフォルダとMM+/modsフォルダの両方が設定されている
            var isDownloadDir = !string.IsNullOrWhiteSpace(Path.GetFullPath(Global.downloadBaseLocation))
                && Directory.Exists(Path.GetFullPath(Global.downloadBaseLocation));
            var isModsDir = !string.IsNullOrEmpty(Global.ModsFolder)
                && !string.IsNullOrWhiteSpace(Path.GetFullPath(Global.ModsFolder))
                && Directory.Exists(Path.GetFullPath(Global.ModsFolder));

            // 設定に不備があったら一切処理しない
            if (!isDownloadDir || !isModsDir)
            {
                ret = DIVA_PATH_RESULT.ERROR_CONFIG_PATH;
                ParamInfo += $", isDownloadDir:{isDownloadDir}, isModsDir:{isModsDir}, Return:{ret}";
                Logger.WriteLine($"Error! Directory not exists.", LoggerType.Error, param: ParamInfo);
                return ret;
            }

            try
            {
                var attributes = new DirectoryInfo(targetPath).Attributes;
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // シンボリックリンクは拒否
                    Logger.WriteLine($"Error! Directory is resolve link. targetPath:{targetPath}", LoggerType.Error, param: ParamInfo);
                    return DIVA_PATH_RESULT.NOT;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // Proton のサンドボックスでアクセスできない場合はログに残し、削除対象をシンボリックリンクとして扱わない
                Console.Error.WriteLine($"権限エラー: {ex.Message}");
                return DIVA_PATH_RESULT.NOT;
            }

            // tmp directory
            var checkTmpDirectoryPath = Path.GetFullPath(Path.Combine(Global.assemblyLocation, "Downloads", "temp_"));
            var checkTmpDirectoryResult =
                StartsWith(targetFullPath, checkTmpDirectoryPath);

            // chech directory
            var checkCacheGbApiDirectoryPath = Path.GetFullPath(Path.Combine(Global.assemblyLocation, "cache", "gb_api"));
            var checkGbCacheDirectoryResult =
                StartsWith(targetFullPath, checkCacheGbApiDirectoryPath);

            var checkCacheDmaApiDirectoryPath = Path.GetFullPath(Path.Combine(Global.assemblyLocation, "cache", "dma_api"));
            var checkDmaCacheDirectoryResult =
                StartsWith(targetFullPath, checkCacheDmaApiDirectoryPath);

            var checkCacheImageDirectoryPath = Path.GetFullPath(Path.Combine(Global.assemblyLocation, "cache", "images"));
            var checkImageCacheDirectoryResult =
                StartsWith(targetFullPath, checkCacheImageDirectoryPath);

            // download file
            var checkDownloadFilePath = Path.Combine(Global.assemblyLocation, "Downloads");
            var checkDownloadFileResult =
                PathStartsWith(targetFullPath, Path.GetFullPath(checkDownloadFilePath));

            // mods
            var checkModsResult =
                FileHelper.PathStartsWith(targetFullPath, Path.GetFullPath(Global.ModsFolder))
                // Config.jsonの"ModsFolder"は最後にGlobal.sが付与されていないので、パス長で判定
                && targetFullPath.Length > TrimEndingDirectorySeparators(Path.GetFullPath(Global.ModsFolder)).Length;

            // setting
            var checkSettingResult = PathsEqual(targetFullPath, Path.GetFullPath(ConfigTomlDmm.CONFIG_E_TOML_PATH));

            // log
            var checkDmmLogResult =
                PathsEqual(targetFullPath, Path.GetFullPath(Global.textLogLocation))
                || PathsEqual(targetFullPath, Path.GetFullPath(Global.textLogBackgroundLocation));

            // 7z.exe
            var checkDmmSevenZipResult = PathsEqual(targetFullPath, Path.GetFullPath(Extractor.SEVENZIP_CONSOLE_EXE_LOCAL_PATH));

            // Rar.exe
            //var checkWinRarResult = _targetFullPath == (!string.IsNullOrEmpty(Global.ConfigJson?.WinRarConsolePath) ? Path.GetFullPath(Global.ConfigJson.WinRarConsolePath).ToLowerInvariant() : null);

            // DML Download File
            var checkDmlDownloadResult = FileHelper.PathStartsWith(targetFullPath, Path.GetFullPath(Global.ConfigJson.GetGameLocation()));

            // DML Temp File
            var checkDmlTempFileResult = FileHelper.PathStartsWith(targetFullPath, Path.GetFullPath(Global.temporaryLocationDML));

            // DML Temp Directory
            var checkDmlTmpDirectoryResult = StartsWith(targetFullPath,
                Path.GetFullPath(Path.Combine(Global.temporaryLocationDML, "temp_")));

            // 削除判定の順番は重要なので注意(上位のフォルダほどチェックは後に！)
            if (checkTmpDirectoryResult)
                ret = Directory.Exists(targetFullPath) ? DIVA_PATH_RESULT.TEMP_DIRECTORY : DIVA_PATH_RESULT.NOT;
            if (checkDmaCacheDirectoryResult || checkGbCacheDirectoryResult || checkImageCacheDirectoryResult)
                ret = DIVA_PATH_RESULT.DMM_CACHE;
            else if (checkDmlTmpDirectoryResult)
                ret = Directory.Exists(targetFullPath) ? DIVA_PATH_RESULT.DML_TEMP_DIRECTORY : DIVA_PATH_RESULT.DML_TEMP_FILE;
            else if (checkModsResult)
                ret = Directory.Exists(targetFullPath) ? DIVA_PATH_RESULT.MODS_DIRECTORY : DIVA_PATH_RESULT.MODS_FILE;
            else if (checkDmlTempFileResult)
                ret = File.Exists(targetFullPath) ? DIVA_PATH_RESULT.DML_TEMP_DIRECTORY : DIVA_PATH_RESULT.NOT;
            else if (checkSettingResult)
                ret = DIVA_PATH_RESULT.DMM_TOML;
            else if (checkDmmLogResult)
                ret = DIVA_PATH_RESULT.DMM_LOG;
            else if (checkDmmSevenZipResult)
                ret = DIVA_PATH_RESULT.DMM_SEVEN_ZIP;
            //else if (checkWinRarResult)
            //    ret = DIVA_PATH_RESULT.WIN_RAR;
            else if (checkDmlDownloadResult)
                ret = Directory.Exists(targetFullPath) ? DIVA_PATH_RESULT.DML_DOWNLOAD_FILE : DIVA_PATH_RESULT.NOT;
            else if (checkDownloadFileResult)
                ret = File.Exists(targetFullPath) ? DIVA_PATH_RESULT.DOWNLOAD_MOD_FILE : DIVA_PATH_RESULT.NOT;

            ParamInfo += $", isDownloadDir:{isDownloadDir}, isModsDir:{isModsDir}, Return:{ret}";
            Logger.WriteLine($"{MeInfo} End. Return:{ret}, Path:\"{targetPath}\"", LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// StartsWithを用いた文字列チェック
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="startPath"></param>
        /// <returns></returns>
        public static bool StartsWith(string targetA, string targetB)
        {
            return targetA.StartsWith(targetB, GetPathComparison());
        }

        /// <summary>
        /// startPath配下のパスか確認する
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="startPath"></param>
        /// <returns></returns>
        public static bool PathStartsWith(string targetPath, string startPath)
        {
            try
            {
                var targetFullPath = TrimEndingDirectorySeparators(Path.GetFullPath(targetPath));
                var startFullPath = TrimEndingDirectorySeparators(Path.GetFullPath(startPath));
                if (PathsEqual(targetFullPath, startFullPath))
                {
                    return true;
                }

                var relativePath = Path.GetRelativePath(startFullPath, targetFullPath);
                return relativePath != "."
                    && !IsParentDirectoryReference(relativePath)
                    && !Path.IsPathRooted(relativePath);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error checking file existence for '{targetPath}': {ex.Message}", LoggerType.Warning);
                return false;
            }
        }

        private static bool PathsEqual(string pathA, string pathB)
        {
            return string.Equals(
                TrimEndingDirectorySeparators(Path.GetFullPath(pathA)),
                TrimEndingDirectorySeparators(Path.GetFullPath(pathB)),
                GetPathComparison());
        }

        private static string TrimEndingDirectorySeparators(string path)
        {
            return Path.TrimEndingDirectorySeparator(path);
        }

        private static bool IsParentDirectoryReference(string relativePath)
        {
            return relativePath == ".."
                || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", GetPathComparison())
                || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", GetPathComparison());
        }

        private static StringComparison GetPathComparison()
        {
            return (OperatingSystem.IsWindows() || Global.IsWine)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        public static bool FileExists(string path)
        {
            try
            {
                return System.IO.File.Exists(path);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error checking file existence for '{path}': {ex.Message}", LoggerType.Warning);
                return false;
            }
        }

        public static async Task<bool> FileExistsAsync(string path)
        {
            bool ret = false;
            if (string.IsNullOrWhiteSpace(path))
            {
                return ret;
            }
            try
            {
                ret = await Task.Run(() => System.IO.File.Exists(path));
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error checking file existence for '{path}': {ex.Message}", LoggerType.Warning);
            }
            return ret;
        }

        public static string TryReadAllText(string path, int retries = 3, int delayMs = 1000, bool eraseCommentLine = false, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"path:{path}, caller:{caller}";

            var ret = string.Empty;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    ret = System.IO.File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(ret) && eraseCommentLine)
                    {
                        // コメント行削除
                        // 特にtomlファイルの場合、コメントが残っている状態で読み込むとコメントが2重で出力されるため暫定対応
                        ret = Regex.Replace(ret, "^\\s*#.*\\r*\\n", "", RegexOptions.Multiline);
                        // 空行削除
                        ret = Regex.Replace(ret, "^\\r*\\n", "", RegexOptions.Multiline);
                    }
                }
                catch (IOException ex)
                {
                    if (i < retries - 1)
                    {
                        Task.Delay(delayMs);
                    }
                    else
                    {
                        Logger.WriteLine($"Failed IOException reading '{path}' after {retries} attempts: {ex.Message}", LoggerType.Error, param: ParamInfo);
                        return null;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.WriteLine($"Permission error reading '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Unexpected error reading '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    return null;
                }
            }
            return ret;
        }

        public static async Task<string> TryReadAllTextAsync(string path, int retries = 3, int delayMs = 1000, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"path:{path}, caller:{caller}";

            string ret = null;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    // System.IO.File.Exists は時間がかかる場合があるので非同期化
                    if (!await Task.Run(() => FileExistsAsync(path)))
                    {
                        string fileName = System.IO.Path.GetFileName(path);
                        if (fileName != ConfigTomlDmm.CONFIG_E_TOML_NAME)
                        {
                            Logger.WriteLine($"File not found (async check): '{path}'", LoggerType.Debug);
                        }
                        break;
                    }
                    ret = await System.IO.File.ReadAllTextAsync(path);
                }
                catch (IOException ex)
                {
                    if (i < retries - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        Logger.WriteLine($"Failed IOException reading '{path}' after {retries} attempts: {ex.Message}", LoggerType.Error, param: ParamInfo);
                        break;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // アクセス許可エラー
                    Logger.WriteLine($"Permission error reading '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Unexpected error reading '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// 追記書き込み（非同期版）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="retries"></param>
        /// <param name="delayMs"></param>
        /// <returns></returns>
        public static async Task<bool> AppendAllTextAsync(string path, string content, int retries = 3, int delayMs = 1000, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"path:{path}, caller:{caller}";

            // UIスレッドから呼ばれた場合はそのまま同期実行
            if (App.Current.Dispatcher.CheckAccess())
            {
                return AppendAllText(path, content, retries, delayMs, caller);
            }
            else
            {
                // 非UIスレッドならDispatcher経由
                return await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    return AppendAllText(path, content, retries, delayMs, caller);
                });
            }
        }

        /// <summary>
        /// 追記書き込み（同期版）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="retries"></param>
        /// <param name="delayMs"></param>
        /// <returns></returns>
        public static bool AppendAllText(string path, string content, int retries = 5, int delayMs = 1000,
            string appendInfo = "", [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    System.IO.File.AppendAllText(path, content);
                    return true;
                }
                catch (IOException ex)
                {
                    if (i < retries - 1)
                    {
                        Task.Delay(delayMs);
                    }
                    else
                    {
                        Logger.WriteLine(string.Join(" ", MeInfo, $"Failed IOException writing to '{path}', appendInfo(LastCaller):{appendInfo} ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
                        return false;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Permission error writing to '{path}'ex.Message:{ex.Message}\""), LoggerType.Error, param: ParamInfo);
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Unexpected error writing to '{path}'ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 新規書き込み（非同期版）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="retries"></param>
        /// <param name="delayMs"></param>
        /// <returns></returns>
        public static async Task<bool> TryWriteAllTextAsync(string path, string content, int retries = 3, int delayMs = 1000, [CallerMemberName] string caller = "")
        {
            var ret = false;

            // UIスレッドから呼ばれた場合はそのまま同期実行
            if (App.Current.Dispatcher.CheckAccess())
            {
                ret = TryWriteAllText(path, content, retries, delayMs, caller);
            }
            else
            {
                // 非UIスレッドならDispatcher経由
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ret = TryWriteAllText(path, content, retries, delayMs, caller);
                });
            }

            return ret;
        }

        /// <summary>
        /// 新規書き込み（同期版）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="retries"></param>
        /// <param name="delayMs"></param>
        /// <returns></returns>
        public static bool TryWriteAllText(string path, string content, int retries = 3, int delayMs = 1000, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            string dir = System.IO.Path.GetDirectoryName(path);
            try
            {
                // ディレクトリ存在チェックと作成
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Failed to create directory '{dir} for '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                return false; // ディレクトリ作成失敗なら書き込みも不可
            }

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    System.IO.File.WriteAllText(path, content);
                    return true;
                }
                catch (IOException ex)
                {
                    if (i < retries - 1)
                    {
                        Task.Delay(delayMs);
                    }
                    else
                    {
                        Logger.WriteLine($"Failed IOException writing to '{path}' after {retries} attempts: {ex.Message}", LoggerType.Error, param: ParamInfo);
                        return false;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.WriteLine($"Permission error writing to '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Unexpected error writing to '{path}': {ex.Message}", LoggerType.Error, param: ParamInfo);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 対象パスのサイズを取得する(ファイル、ディレクトリ不問)
        /// </summary>
        /// <param name="path">対象ディレクトリパス</param>
        /// <param name="SkipFileWhenSizeCheckPathList">ディレクトリサイズのカウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <returns></returns>
        public static long GetFileOrDirectorySize(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            long ret = -1;
            if (string.IsNullOrWhiteSpace(path))
            {
                return ret;
            }
            else if (System.IO.File.Exists(path))
            {
                ret = new FileInfo(path).Length;
            }
            else if (Directory.Exists(path))
            {
                ret = GetDirectorySize(path, SkipFileWhenSizeCheckPathList);
            }
            return ret;
        }

        /// <summary>
        /// 対象ディレクトリパス内のディレクトリサイズを取得する(再帰)
        /// </summary>
        /// <param name="path">対象ディレクトリパス</param>
        /// <param name="SkipFileWhenSizeCheckPathList">ディレクトリサイズのカウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <returns></returns>
        public static long GetDirectorySize(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            long DirectorySize = 0;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return DirectorySize;
            }

            var dirInfo = new DirectoryInfo(path);

            if (dirInfo != null)
            {
                foreach (FileInfo fi in dirInfo.GetFiles())
                {
                    var sumFlg = true;
                    if (SkipFileWhenSizeCheckPathList != null)
                    {
                        sumFlg = !SkipFileWhenSizeCheckPathList.Where(x => FileHelper.PathStartsWith(fi.FullName, x)).Any();
                    }
                    if (sumFlg)
                        DirectorySize += fi.Length;
                }
                foreach (DirectoryInfo di in dirInfo.GetDirectories())
                {
                    DirectorySize += GetDirectorySize(di.FullName, SkipFileWhenSizeCheckPathList);
                }
            }

            return DirectorySize;
        }

        public static async Task<long> GetDirectorySizeAsyncAuto(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            if (App.Current.Dispatcher.CheckAccess())
            {
                return GetDirectorySize(path, SkipFileWhenSizeCheckPathList);
            }
            else
            {
                // 非UIスレッドならDispatcherでUIスレッドに委譲
                return await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    return GetDirectorySize(path, SkipFileWhenSizeCheckPathList);
                });
            }
        }

        /// <summary>
        /// 対象ディレクトリパス内にあるファイル数＋ディレクトリ数を取得する(再帰)
        /// </summary>
        /// <param name="path">対象ディレクトリパス</param>
        /// <param name="SkipFileWhenSizeCheckPathList">カウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <returns></returns>
        public static List<FileSystemInfo> GetFilesAndDirectoriesCount(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            var dirInfo = new DirectoryInfo(path);
            var fileSystemInfo = new List<FileSystemInfo>();

            int ret = 0;

            if (dirInfo != null)
            {
                foreach (FileInfo fi in dirInfo.GetFiles())
                {
                    var sumFlg = true;
                    if (SkipFileWhenSizeCheckPathList != null)
                    {
                        sumFlg = !SkipFileWhenSizeCheckPathList.Where(x => FileHelper.PathStartsWith(fi.FullName, x)).Any();
                    }
                    if (sumFlg)
                    {
                        fileSystemInfo.Add(fi);
                        ret++;
                    }
                }
                foreach (DirectoryInfo di in dirInfo.GetDirectories())
                {
                    fileSystemInfo.Add(di);
                    ret++;
                    var addFileSystemInfo = GetFilesAndDirectoriesCount(di.FullName, SkipFileWhenSizeCheckPathList);
                    ret += addFileSystemInfo.Count;
                    fileSystemInfo.AddRange(addFileSystemInfo);
                }
            }
            return fileSystemInfo;
        }

        //public static int GetFilesAndDirectoriesCount(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        //{
        //    var dirInfo = new DirectoryInfo(path);

        //    int ret = 0;
        //    foreach (FileInfo fi in dirInfo.GetFiles())
        //    {
        //        var sumFlg = true;
        //        if (SkipFileWhenSizeCheckPathList != null)
        //        {
        //            sumFlg = !SkipFileWhenSizeCheckPathList.Where(x => FileHelper.PathStartsWith(fi.FullName, x)).Any();
        //        }
        //        if (sumFlg)
        //        {
        //            ret++;
        //        }
        //    }
        //    foreach (DirectoryInfo di in dirInfo.GetDirectories())
        //    {
        //        ret++;
        //        ret += GetFilesAndDirectoriesCount(di.FullName, SkipFileWhenSizeCheckPathList);
        //    }
        //    return ret;
        //}

        /// <summary>
        /// 対象ディレクトリパス内にあるファイル数＋ディレクトリ数を取得する(再帰)
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="SkipFileWhenSizeCheckPathList">カウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <param name="nextPath">対象ディレクトリパス</param>
        /// <returns></returns>
        /// 
        public static List<FileSystemInfo> GetFilesAndDirectoryList(string basePath, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            List<FileSystemInfo> ret = new();
            List<FileSystemInfo> FilesAndDirectoryList = _GetFilesAndDirectoryList(ret, basePath, SkipFileWhenSizeCheckPathList, basePath);

            return ret.ToList();
        }
        private static List<FileSystemInfo> _GetFilesAndDirectoryList(List<FileSystemInfo> ret, string basePath, List<string> SkipFileWhenSizeCheckPathList = null, string nextPath = "")
        {
            if (string.IsNullOrEmpty(nextPath))
                return null;

            if (ret == null)
                ret = new();

            var directories = new DirectoryInfo(nextPath);
            if (directories != null)
            {
                foreach (FileInfo fi in directories.GetFiles())
                {
                    var sumFlg = true;
                    if (SkipFileWhenSizeCheckPathList != null)
                    {
                        sumFlg = !SkipFileWhenSizeCheckPathList.Where(x => FileHelper.PathStartsWith(fi.FullName, x)).Any();
                    }
                    if (sumFlg)
                        ret.Add(fi);
                }
                foreach (DirectoryInfo di in directories.GetDirectories())
                {
                    ret = _GetFilesAndDirectoryList(ret, basePath, SkipFileWhenSizeCheckPathList, di.FullName);
                }
            }
            return ret;
        }

        /// <summary>
        /// GetFilesAndDirectoriesCountの非同期スレッド版
        /// </summary>
        /// <param name="path">対象ディレクトリパス</param>
        /// <param name="skipFilePathList">カウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <returns></returns> 
        public static async Task<int> GetFilesAndDirectoriesCountAsync(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            return await Task<int>.Run(() => GetFilesAndDirectoriesCount(path, SkipFileWhenSizeCheckPathList).Count);
        }

        /// <summary>
        /// GetFilesAndDirectoriesCountの非同期UIスレッド、非同期スレッド両対応版
        /// </summary>
        /// <param name="path">対象ディレクトリパス</param>
        /// <param name="SkipFileWhenSizeCheckPathList">カウント対象外となるファイルのフルパス(PathStartsWithで比較)</param>
        /// <returns></returns> 
        public static async Task<int> GetFilesAndDirectoriesCountAsyncAuto(string path, List<string> SkipFileWhenSizeCheckPathList = null)
        {
            var ret = 0;

            // UIスレッドから呼ばれた場合はそのまま同期実行
            if (App.Current.Dispatcher.CheckAccess())
            {
                ret = GetFilesAndDirectoriesCount(path, SkipFileWhenSizeCheckPathList).Count;
            }
            else
            {
                // 非UIスレッドならDispatcher経由
                ret = await App.Current.Dispatcher.Invoke(async () =>
                {
                    return await GetFilesAndDirectoriesCountAsync(path, SkipFileWhenSizeCheckPathList);
                });
            }

            return ret;
        }

        /// <summary>
        /// ファイルサイズを取得
        /// Exception時は-1が返却されるので比較に注意
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// todo: いずれGetDirectorySizeAsync、GetFilesAndDirectoriesCountAsyncかそれに近いメソッドを作り置き換える
        [Obsolete("'GetFilesAndDirectoriesCountAsyncを使用するか、新規に実装してください。")]
        public static long GetFileSize(string path, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                return new FileInfo(path).Length;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error getting directories size in {path}: {ex.Message}", LoggerType.Error, param: ParamInfo);
                return -1; // -1を返すことでエラーを示す
            }
        }

        /// <summary>
        /// ディレクトリサイズを非同期で取得
        /// Exception時は-1が返却されるので比較に注意
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// todo: いずれGetDirectorySizeAsync、GetFilesAndDirectoriesCountAsyncかそれに近いメソッドを作り置き換える
        [Obsolete("'GetDirectorySizeAsyncを使用するか、新規に実装してください。")]
        public static int GetFileCount(string path, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                var fullPath = Path.GetFullPath(path);
                return new DirectoryInfo(fullPath).GetFiles("*", SearchOption.AllDirectories).Length;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error getting directories size in {path}: {ex.Message}", LoggerType.Error, param: ParamInfo);
                return -1; // -1を返すことでエラーを示す
            }
        }

        /// <summary>
        /// ディレクトリサイズを非同期で取得
        /// Exception時は-1が返却されるので比較に注意
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("'GetDirectorySizeまたはGetDirectorySizeAsyncを使用するか、新規に実装してください。")]
        public static long GetDirectoriesSize(string path, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                return new DirectoryInfo(path).GetDirectorySize();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error getting directories size in {path}: {ex.Message}", LoggerType.Error, param: ParamInfo);
                return -1; // -1を返すことでエラーを示す
            }
        }

        /// <summary>
        /// 厳密な方のサイズ表記(2の累乗)
        /// </summary>
        /// <param name="_directorySize"></param>
        /// <returns></returns>
        public static string GetDirectorySizeView10(long _directorySize)
        {
            if (_directorySize == -1)
                return string.Empty;
            else if (_directorySize < 1024)
                return $"{_directorySize} B";
            else if (_directorySize < 1048576)
                return $"{Math.Round(_directorySize / 1024.0, 2)} KB";
            else if (_directorySize < 1073741824)
                return $"{Math.Round(_directorySize / 1048576.0, 2)} MB";
            else
                return $"{Math.Round(_directorySize / 1073741824.0, 2)} GB";
        }

        /// <summary>
        /// 厳密ではない方のサイズ表記(10の累乗)
        /// </summary>
        /// <param name="_directorySize"></param>
        /// <returns></returns>
        public static string GetDirectorySizeView2(long _directorySize)
        {
            if (_directorySize == -1)
                return string.Empty;
            else if (_directorySize < 1000)
                return $"{_directorySize} B";
            else if (_directorySize < 1000000)
                return $"{Math.Round(_directorySize / 1000.0, 1)} KB";
            else if (_directorySize < 1000000000)
                return $"{Math.Round(_directorySize / 1000000.0, 1)} MB";
            else
                return $"{Math.Round(_directorySize / 1000000000.0, 1)} GB";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string CalculateSha256(string filePath, [CallerMemberName] string caller = "")
        {
            if (!System.IO.File.Exists(filePath)) return string.Empty;

            var checkHash = string.Empty;

            using (FileStream stream = System.IO.File.OpenRead(filePath))
            {
                using SHA256 sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(stream);

                // ハッシュ値を16進数文字列に変換
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                checkHash = sb.ToString();
            }
            return checkHash;
        }

        /// <summary>
        /// 指定パス以下にシンボリックリンク、ジャンクション、ハードリンクなど、
        /// 他のパスを参照し得るファイルシステム要素が含まれているか再帰チェックする
        /// </summary>
        private static bool TryFindUnsafeFileSystemReference(string path, out string reason, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", $"{MeInfo}", "Start.", $"path:{path}"), LoggerType.Debug, param: ParamInfo);
            bool ret = false;

            reason = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"End. string.IsNullOrWhiteSpace return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }


                if (File.Exists(path))
                {
                    bool IsUnsafeFileSystemInfoResult = IsUnsafeFileSystemInfo(new FileInfo(path), out reason);
                    Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"End. File.Exists IsUnsafeFileSystemInfoResult:{IsUnsafeFileSystemInfoResult}"), LoggerType.Debug, param: ParamInfo);
                    return IsUnsafeFileSystemInfoResult;
                }


                if (!Directory.Exists(path))
                {
                    Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"End. !Directory.Exists return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }

                var rootDirectory = new DirectoryInfo(path);
                if (IsUnsafeFileSystemInfo(rootDirectory, out reason))
                {
                    ret = true;
                    Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"End. IsUnsafeFileSystemInfo return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }

                bool TryFindUnsafeFileSystemReferenceRecursiveResult = TryFindUnsafeFileSystemReferenceRecursive(rootDirectory, 0, out reason);
                Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"End. TryFindUnsafeFileSystemReferenceRecursiveResult:{TryFindUnsafeFileSystemReferenceRecursiveResult}"), LoggerType.Debug, param: ParamInfo);
                return TryFindUnsafeFileSystemReferenceRecursiveResult;
            }
            catch (Exception ex)
            {
                reason = $"Error scanning '{path}': {ex.Message}";
                Logger.WriteLine($"TryFindUnsafeFileSystemReference: {reason}, ex.Message:{ex.Message}, ex.StackTrace:{ex.StackTrace}", LoggerType.Error);
                return true;
            }
        }

        private static bool TryFindUnsafeFileSystemReferenceRecursive(DirectoryInfo directory, int depth, out string reason)
        {
            const int MAX_DEPTH = 10000;
            reason = string.Empty;

            if (depth > MAX_DEPTH)
            {
                reason = $"Max depth exceeded at '{directory.FullName}'";
                Logger.WriteLine($"TryFindUnsafeFileSystemReference: {reason}", LoggerType.Error);
                return true;
            }

            try
            {
                foreach (var entry in directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                {
                    if (IsUnsafeFileSystemInfo(entry, out reason))
                        return true;

                    if (entry is DirectoryInfo childDirectory)
                    {
                        if (TryFindUnsafeFileSystemReferenceRecursive(childDirectory, depth + 1, out reason))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                reason = $"Error scanning '{directory.FullName}': {ex.Message}";
                Logger.WriteLine($"TryFindUnsafeFileSystemReference: {reason}", LoggerType.Error);
                return true;
            }

            return false;
        }

        private static bool IsUnsafeFileSystemInfo(FileSystemInfo fileSystemInfo, out string reason)
        {
            reason = string.Empty;

            try
            {
                fileSystemInfo.Refresh();

                if ((fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    reason = $"Reparse point found: '{fileSystemInfo.FullName}'";
                    return true;
                }

                if (!string.IsNullOrEmpty(fileSystemInfo.LinkTarget))
                {
                    reason = $"Symbolic link found: '{fileSystemInfo.FullName}' -> '{fileSystemInfo.LinkTarget}'";
                    return true;
                }

                if (fileSystemInfo is FileInfo fileInfo && HasMultipleHardLinks(fileInfo.FullName, out var linkCount))
                {
                    reason = $"Hard link found: '{fileInfo.FullName}' has {linkCount} links";
                    return true;
                }
            }
            catch (Exception ex)
            {
                reason = $"Error scanning '{fileSystemInfo.FullName}': {ex.Message}";
                Logger.WriteLine($"IsUnsafeFileSystemInfo: {reason}", LoggerType.Error);
                return true;
            }

            return false;
        }

        private static bool HasMultipleHardLinks(string path, out uint linkCount)
        {
            linkCount = 1;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return HasMultipleHardLinksLinux(path, out linkCount);
            }

            try
            {
                using var handle = CreateFileW(
                    path,
                    FILE_READ_ATTRIBUTES,
                    FileShare.ReadWrite | FileShare.Delete,
                    IntPtr.Zero,
                    FileMode.Open,
                    FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                    return false;

                if (!GetFileInformationByHandle(handle, out var info))
                    return false;

                linkCount = info.NumberOfLinks;
                return linkCount > 1;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"HasMultipleHardLinks: Error scanning '{path}': {ex.Message}", LoggerType.Error);
                return true;
            }
        }

        private static bool HasMultipleHardLinksLinux(string path, out uint linkCount)
        {
            linkCount = 1;
            try
            {
                var stat = new LibcStat();
                if (stat_x64(path, ref stat) == 0)
                {
                    linkCount = (uint)stat.st_nlink;
                    return linkCount > 1;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"HasMultipleHardLinksLinux: Error scanning '{path}': {ex.Message}", LoggerType.Error);
            }
            return false;
        }

        [DllImport("libc", EntryPoint = "stat", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern int stat_x64([MarshalAs(UnmanagedType.LPUTF8Str)] string path, ref LibcStat buf);

        [StructLayout(LayoutKind.Sequential)]
        private struct LibcStat
        {
            public ulong st_dev;
            public ulong st_ino;
            public ulong st_nlink;
            public uint st_mode;
            public uint st_uid;
            public uint st_gid;
            public int st_rdev;
            public long st_size;
            public long st_blksize;
            public long st_blocks;
            public long st_atime;
            public long st_mtime;
            public long st_ctime;
        }

        private const uint FILE_READ_ATTRIBUTES = 0x80;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out ByHandleFileInformation lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        /// <summary>
        /// シンボリックリンクを辿らない安全な再帰的ディレクトリ削除
        /// ファイル個別に IsDivaModFileOrDirectory で検証してから削除する
        /// </summary>
        private static bool DeleteDirectoryRecursiveSafe(string path, int depth = 0, [CallerMemberName] string caller = "")
        {
            const int MAX_DEPTH = 10000;
            if (depth > MAX_DEPTH)
            {
                Logger.WriteLine($"DeleteDirectoryRecursiveSafe: Max depth exceeded at '{path}'", LoggerType.Error);
                return false;
            }

            // 呼び出し元 (DeleteDirectory) で IsDivaModFileOrDirectory + TryFindUnsafeFileSystemReference の事前検証済み
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!DeleteFile(file))
                    {
                        Logger.WriteLine($"DeleteDirectoryRecursiveSafe: Failed to delete file '{file}'", LoggerType.Error);
                        return false;
                    }
                }

                foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!DeleteDirectoryRecursiveSafe(dir, depth + 1, caller))
                        return false;
                }

                if (!Directory.EnumerateFileSystemEntries(path).Any())
                {
                    var info = new DirectoryInfo(path);
                    var fullPath = Path.GetFullPath(path);
                    Logger.WriteLine($"DeleteDirectoryRecursiveSafe: Directory info for '{path}': fullPath='{fullPath}', Attributes={info.Attributes}, LinkTarget={info.LinkTarget ?? "null"}", LoggerType.Debug);

                    Directory.Delete(path);
                    Logger.WriteLine($"DeleteDirectoryRecursiveSafe: Directory info for '{path}'", LoggerType.Debug);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"DeleteDirectoryRecursiveSafe: Error at '{path}': {ex.Message}", LoggerType.Error);
                return false;
            }
        }
    }
}

