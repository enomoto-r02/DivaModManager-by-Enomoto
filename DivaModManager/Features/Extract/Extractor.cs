using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using DivaModManager.Structures;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;
using static DivaModManager.Common.Helpers.WindowHelper;
using static DivaModManager.Features.Extract.ExtractInfo;

namespace DivaModManager.Features.Extract
{
    public static class Extractor
    {
        enum EXTENSION
        {
            ZIP,
            RAR,
            SEVENZIP,
        }
        private static readonly string BIT_OS_DIR_NAME = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        private static readonly string WINRAR_NAME = "WinRAR";
        private static readonly string WINRAR_CONSOLE_Registry_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver";
        private static readonly string WINRAR_CONSOLE_EXE_NAME = "Rar.exe";
        private static readonly string WINRAR_CONSOLE_Registry_INSTALL_LOCATION_KEY = "InstallLocation";
        private static readonly string SEVENZIP_NAME = "7-Zip";
        private static readonly string SEVENZIP_CONSOLE_EXE_LOCAL_NAME = "7z.exe";
        public static readonly string SEVENZIP_CONSOLE_EXE_LOCAL_PATH = Path.Combine(Global.assemblyLocation, BIT_OS_DIR_NAME, SEVENZIP_CONSOLE_EXE_LOCAL_NAME);
        private static readonly string SEVENZIP_CONSOLE_EXE_LOCAL_HASH_SHA256 = "2bff20bd679d45166b8c2d039044a4ca16189e6d69ff9c82345b4c1306986ec4";
        private static readonly int MAX_LOOP_RECURSION = 10000;
        public static int MoveCount = 0;
        private static readonly int PATH_LENGTH_LIMIT_WINDOWS = 260;
        private static readonly int PATH_LENGTH_LIMIT_LINUX = 4096;



        /// <summary>
        /// 解凍処理のInitializer
        /// WinRARの使用を確認する
        /// </summary>
        /// <param name="calledMethod"></param>
        public static void InitWinRar([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            if (Global.ConfigToml.WinRarUse)
            {
                var initWinRarSetting = false;
                if (string.IsNullOrEmpty(Global.ConfigJson?.WinRarConsolePath)
                    || string.IsNullOrEmpty(Global.ConfigJson?.WinRarConsoleVersion))
                {
                    Global.ConfigToml.ExtractorWinRar = ExtractInfo.EXTERNAL_EXTRACTOR.NOT_FOUND;
                    initWinRarSetting = true;
                }
                else
                {
                    var checkRes = VersionHelper.CompareVersions(
                        FileVersionInfo.GetVersionInfo(Global.ConfigJson?.WinRarConsolePath).ProductVersion,
                        ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString());
                    if (checkRes == VersionHelper.Result.VersionA_NOTHING || checkRes == VersionHelper.Result.VersionB_NOTHING)
                    {
                        // 暫定
                        Global.ConfigToml.ExtractorWinRar = ExtractInfo.EXTERNAL_EXTRACTOR.NO_INSTALL;
                        initWinRarSetting = true;
                    }
                    else if (checkRes == VersionHelper.Result.VersionB_AS_LONGER)
                    {
                        Global.ConfigToml.ExtractorWinRar = ExtractInfo.EXTERNAL_EXTRACTOR.OLD_VERSION;
                        initWinRarSetting = true;
                    }
                    else if (string.IsNullOrEmpty(Global.ConfigJson.WinRarConsolePath)
                        || !DivaModManager.Common.Helpers.FileHelper.FileExists(Global.ConfigJson.WinRarConsolePath)
                        || Global.ConfigJson.WinRarConsoleLastUseHashSHA256 != DivaModManager.Common.Helpers.FileHelper.CalculateSha256(Global.ConfigJson.WinRarConsolePath))
                    {
                        Global.ConfigToml.ExtractorWinRar = ExtractInfo.EXTERNAL_EXTRACTOR.NOT_FOUND;
                        initWinRarSetting = true;
                    }
                }
                // 再設定
                if (!Global.ConfigJson.IsNew && initWinRarSetting)
                {
                    Global.ConfigJson.WinRarConsolePath = null;
                    Global.ConfigJson.WinRarConsoleVersion = null;
                    Global.ConfigToml.WinRarCheckDialog = true;
                    Global.ConfigToml.WinRarUse = false;
                    Global.ConfigToml.Update();
                    List<string> replaceList = new() { WINRAR_NAME, ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString() };
                    var ret = WindowHelper.DMMWindowOpenAsync(7, replaceList);
                }
            }

            if (Global.ConfigToml.WinRarCheckDialog && OperatingSystem.IsWindows())
            {
                // レジストリから取得
                var regWinRarPath = Registry.LocalMachine.OpenSubKey(WINRAR_CONSOLE_Registry_PATH);
                if (!string.IsNullOrEmpty(regWinRarPath?.GetValue(WINRAR_CONSOLE_Registry_INSTALL_LOCATION_KEY) as string))
                {
                    var WinRarExePath = Path.Combine(regWinRarPath.GetValue(WINRAR_CONSOLE_Registry_INSTALL_LOCATION_KEY) as string, WINRAR_CONSOLE_EXE_NAME);

                    if (File.Exists(WinRarExePath))
                    {
                        var findWinRarVersionCheck = VersionHelper.CompareVersions(
                            FileVersionInfo.GetVersionInfo(WinRarExePath).ProductVersion,
                            ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString());

                        // 見つけたRar.exeのバージョンと最低バージョンを比較
                        if (findWinRarVersionCheck == VersionHelper.Result.VersionA_AS_LONGER
                            || findWinRarVersionCheck == VersionHelper.Result.SAME)
                        {
                            Global.ConfigToml.ExtractorWinRar = ExtractInfo.EXTERNAL_EXTRACTOR.USE;
                            if (Global.ConfigToml.WinRarCheckDialog)
                            {
                                List<string> replaceList = new() { WINRAR_NAME, FileVersionInfo.GetVersionInfo(WinRarExePath).ProductVersion, "RAR" };
                                var ret = WindowHelper.DMMWindowOpenAsync(8, replaceList);
                                Global.ConfigToml.WinRarUse = (ret.Result == WindowHelper.WindowCloseStatus.Yes || ret.Result == WindowHelper.WindowCloseStatus.YesCheck);
                                Global.ConfigToml.WinRarCheckDialog =
                                    (ret.Result != WindowHelper.WindowCloseStatus.YesCheck && ret.Result != WindowHelper.WindowCloseStatus.NoCheck);

                                if (Global.ConfigToml.WinRarUse)
                                {
                                    Global.ConfigJson.WinRarConsolePath = WinRarExePath;
                                    Global.ConfigJson.WinRarConsoleVersion = FileVersionInfo.GetVersionInfo(WinRarExePath).ProductVersion;
                                    Global.ConfigJson.WinRarConsoleLastUseHashSHA256 = FileHelper.CalculateSha256(WinRarExePath);
                                }
                            }
                        }
                        else
                        {
                            List<string> replaceList = new() { WINRAR_NAME, ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString() };
                            var ret = WindowHelper.DMMWindowOpenAsync(7, replaceList);
                            Global.ConfigJson.WinRarConsolePath = string.Empty;
                            Global.ConfigJson.WinRarConsoleVersion = string.Empty;
                            Global.ConfigJson.WinRarConsoleLastUseHashSHA256 = string.Empty;
                            Global.ConfigToml.WinRarUse = false;
                            Global.ConfigToml.WinRarCheckDialog =
                                (ret.Result == WindowHelper.WindowCloseStatus.YesCheck || ret.Result == WindowHelper.WindowCloseStatus.NoCheck);
                        }
                    }
                    else
                    {
                        List<string> replaceList = new() { WINRAR_NAME, ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString() };
                        var ret = WindowHelper.DMMWindowOpenAsync(7, replaceList);
                        Global.ConfigToml.WinRarUse = false;
                        Global.ConfigToml.WinRarCheckDialog =
                            (ret.Result == WindowHelper.WindowCloseStatus.YesCheck || ret.Result == WindowHelper.WindowCloseStatus.NoCheck);
                    }
                }
                else
                {
                    List<string> replaceList = new() { WINRAR_NAME, ConfigJson.WINRAR_MINIMUM_PRODUCT_VERSION.ToString() };
                    var ret = WindowHelper.DMMWindowOpenAsync(7, replaceList);
                    Global.ConfigToml.WinRarUse = false;
                    Global.ConfigToml.WinRarCheckDialog =
                        (ret.Result == WindowHelper.WindowCloseStatus.YesCheck || ret.Result == WindowHelper.WindowCloseStatus.NoCheck);
                }
            }
            Global.ConfigToml.Update();

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"WinRarUse:{Global.ConfigToml.WinRarUse}"), LoggerType.Debug, param: ParamInfo);
        }

        /// <summary>
        /// 解凍処理のInitializer
        /// 配布ファイル内の7z.exeを確認する
        /// </summary>
        /// <param name="calledMethod"></param>
        public static void InitSevenZipLocal([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var initSevenZipLocalSetting = true;

            // 使用する場合、起動時に毎回チェックして違いがあったら初期化する
            if (Global.ConfigToml.SevenZipUse)
            {
                if (DivaModManager.Common.Helpers.FileHelper.FileExists(SEVENZIP_CONSOLE_EXE_LOCAL_PATH))
                {
                    var local_hash_sha256 = FileHelper.CalculateSha256(SEVENZIP_CONSOLE_EXE_LOCAL_PATH, caller: $"{MethodBase.GetCurrentMethod().Name.Split(".").ToList().Last()}(SevenZipUse)");
                    if (local_hash_sha256 == SEVENZIP_CONSOLE_EXE_LOCAL_HASH_SHA256)
                    {
                        initSevenZipLocalSetting = false;
                        Global.ConfigToml.ExtractorSevenZip = ExtractInfo.EXTERNAL_EXTRACTOR.USE;
                        Global.ConfigJson.SevenZipConsolePath = SEVENZIP_CONSOLE_EXE_LOCAL_PATH;
                        Global.ConfigJson.SevenZipConsoleLastUseHashSHA256 = local_hash_sha256;
                        Global.ConfigJson.SevenZipConsoleVersion = ConfigJson.SEVENZIP_INCLUDE_PRODUCT_VERSION.ToString();
                        Global.ConfigToml.SevenZipUse = true;
                        Global.ConfigToml.Update();
                    }
                }
                // 再設定
                if (initSevenZipLocalSetting)
                {
                    Global.ConfigJson.SevenZipConsolePath = null;
                    Global.ConfigJson.SevenZipConsoleVersion = null;
                    Global.ConfigToml.SevenZipCheckDialog = true;
                    Global.ConfigToml.SevenZipUse = false;
                    Global.ConfigToml.Update();
                }
            }

            // 7z.exeのチェック
            if (Global.ConfigToml.SevenZipCheckDialog)
            {
                initSevenZipLocalSetting = true;
                if (FileHelper.FileExists(SEVENZIP_CONSOLE_EXE_LOCAL_PATH))
                {
                    var local_hash_sha256 = FileHelper.CalculateSha256(SEVENZIP_CONSOLE_EXE_LOCAL_PATH, caller: $"{MethodBase.GetCurrentMethod().Name.Split(".").ToList().Last()}(SevenZipCheckDialog)");
                    if (local_hash_sha256 == SEVENZIP_CONSOLE_EXE_LOCAL_HASH_SHA256)
                    {
                        initSevenZipLocalSetting = false;
                        Global.ConfigToml.SevenZipUse = true;
                        Global.ConfigToml.SevenZipCheckDialog = true;
                        Global.ConfigToml.ExtractorSevenZip = ExtractInfo.EXTERNAL_EXTRACTOR.USE;
                        Global.ConfigToml.SevenZipUse = true;
                        Global.ConfigJson.SevenZipConsolePath = SEVENZIP_CONSOLE_EXE_LOCAL_PATH;
                        Global.ConfigJson.SevenZipConsoleVersion = FileVersionInfo.GetVersionInfo(SEVENZIP_CONSOLE_EXE_LOCAL_PATH).ProductVersion;
                        Global.ConfigJson.SevenZipConsoleLastUseHashSHA256 = local_hash_sha256;
                    }
                    else
                    {
                        initSevenZipLocalSetting = true;
                    }
                }
                else
                {
                    initSevenZipLocalSetting = true;
                }
                // 再設定
                if (initSevenZipLocalSetting)
                {
                    Global.ConfigJson.SevenZipConsolePath = null;
                    Global.ConfigJson.SevenZipConsoleVersion = null;
                    Global.ConfigToml.SevenZipCheckDialog = true;
                    Global.ConfigToml.SevenZipUse = false;
                    Global.ConfigToml.Update();
                    // todo: バージョンクラスを使ってToString()すると"26.1になるので暫定的な修正
                    List<string> replaceList = new() { SEVENZIP_NAME, "26.01" };
                    var ret = WindowHelper.DMMWindowOpenAsync(9, replaceList);
                }
            }
            Global.ConfigToml.Update();

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"SevenZipUse:{Global.ConfigToml.SevenZipUse}"), LoggerType.Debug, param: ParamInfo);
        }

        /// <summary>
        /// 圧縮ファイルまたはディレクトリを解凍し、適切なフォルダに移動する
        /// </summary>
        /// <param name="extract"></param>
        /// extractCallPatternのSite、Type、ArchiveFilePathは必ず設定してください
        /// ↑ MoveInfoに修正予定
        /// MoveDirectoryRootPathはDML、DMMなどModsフォルダ以外に配置する場合は設定してください(強制的に上書きされます)
        /// <returns>成否</returns>
        /// 成功時にMoveDirectoryRootPathに移動完了した場合の移動後フォルダパスが設定されます
        public static async Task<bool> ExtractMain(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            await Task.Delay(1);
            var ret = false;

            var StopWatchExtractMain = new Stopwatch();
            StopWatchExtractMain.Start();

            if (extract == null || extract.MoveInfoList.Count == 0)
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}", $"Invalid argument."), LoggerType.Error, param: ParamInfo);
                return ret;
            }

            // 解凍
            var extractRes = Extract(extract);
            if (!extractRes)
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{extract.MoveInfoList.LastOrDefault().Result.ToString()}", $"Extract Failed."), LoggerType.Error, param: ParamInfo);
                return extractRes;
            }

            Logger.WriteLine($"Extract After Check... \"{extract.WindowLoggerViewFileName}\"", LoggerType.Info);

            // ダミーファイルチェック
            if (!CheckDummyModAsync(extract))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            // 正しいルートフォルダを確認
            if (!CheckArchiveRootDirectory(extract))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            // 移動先パス取得
            GetMoveDirectoryName(extract);

            // アップデートチェック、config.toml更新
            if (!UpdateModFolderConfigToml(extract))
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            // Modsフォルダパスが決まってからSkipPathを生成(特にSetSkipFileWhenSizeCheckPathList)
            extract.SetSkipPathList();
            extract.SetSkipFileWhenSizeCheckPathList();

            // Updateの場合にコピー先のmodsフォルダから一部設定ファイルを除いて削除する
            var modDirDeleteResult = FileHelper.DeleteDirectoryForCleanUpdate(extract);

            // modsフォルダに移動
            Logger.WriteLine($"Move to Directory... \"{extract.WindowLoggerViewFileName}\"", LoggerType.Info);
            if (!MoveDirectory(extract)) { return ret; }
            Logger.WriteLine($"Move to Directory... End! \"{extract.WindowLoggerViewFileName}\"", LoggerType.Debug);

            // 一時フォルダの削除
            DeleteTemporaryDirectory(extract);

            // mod.json生成
            CreateModJson(extract);

            StopWatchExtractMain.Stop();
            TimeSpan ts = StopWatchExtractMain.Elapsed;
            extract.ExtractMainTime = ts;

            ret = true;
            Logger.WriteLine($"ExtractMainTime: {extract.ExtractMainTime.Minutes}分(Minutes) {extract.ExtractMainTime.Seconds}秒(Seconds) {extract.ExtractMainTime.Milliseconds}ミリ秒(Milliseconds)", LoggerType.Debug);
            var dumpStr = ObjectDumper.Dump(extract, "ExtractInfo");
            Logger.WriteLine($"Extract Complete! \"{extract.WindowLoggerViewFileName}\"", LoggerType.Info);
            Logger.WriteLine(string.Join(" ", MeInfo, $"End", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo, dump: dumpStr);

            return ret;
        }

        /// <summary>
        /// 圧縮ファイルを展開する
        /// </summary>
        /// <param name="extract"></param>
        /// <returns>処理の成否</returns>
        /// 成功時はtrueと共に展開後のModフォルダのルートパス(C:/....../temp_xxxx/MOD_NAME)
        private static bool Extract(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";

            var ret = false;

            // ファイル、ディレクトリの存在チェック
            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start.", $"Name:{mv.FullPath}"), LoggerType.Debug, param: ParamInfo);

            if (string.IsNullOrEmpty(mv.FullPath)
                || (!Directory.Exists(mv.FullPath) && !File.Exists(mv.FullPath)))
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NOT_FOUND_ARCHIVE_PATH;
                var dumpStr = ObjectDumper.Dump(extract, "ExtractInfo");
                Logger.WriteLine($"{MeInfo} End. Return:{ret} Invalid argument.", LoggerType.Error, dump: dumpStr);
                return ret;
            }

            // ディレクトリの場合
            if (Directory.Exists(mv.FullPath))
            {
                if (FileHelper.GetDirectorySize(mv.FullPath, extract.SkipFileWhenSizeCheckPathList) <= 0)
                {
                    extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.SIZE_ZERO;
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }
                else
                {
                    if (extract.Type == ExtractInfo.TYPE.DROP)
                    {

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                            WindowHelper.DMMWindowOpen(27);
                        });

                        // ----- いったん凍結(1.3.1.34 / 2026.5.22) -------------------------
                        //// modsフォルダのドロップは受け付けない
                        //if (FileHelper.PathStartsWith(mv.FullPath, Global.ModsFolder))
                        //{
                        //    Logger.WriteLine($"Sorry, This directory is already in the mods folder. \"{mv.FullPath}\"", LoggerType.Info);
                        //    Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                        //    return ret;
                        //}

                        //var tempPath = string.Empty;
                        //var isDMLInstallResult = IsDMLInstall(extract);
                        //if (isDMLInstallResult)
                        //{
                        //    tempPath = $"{Global.downloadBaseLocation}temp_{DateTime.Now:yyyyMMddHHmmssfff}";
                        //}
                        //else
                        //{
                        //    if (extract.MoveInfoList.LastOrDefault().Result == ExtractInfo.EXTRACT_RESULT.NO_INSTALL_DML)
                        //    {
                        //        Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                        //        return ret;
                        //    }
                        //    tempPath = $"{Global.downloadBaseLocation}temp_{DateTime.Now:yyyyMMddHHmmssfff}";
                        //}

                        //Logger.WriteLine($"Drop Directory Check... \"{mv.FullPath}\"", LoggerType.Info);
                        //extract.WindowLoggerViewFileName = new DirectoryInfo(mv.FullPath).Name;

                        //if (!isDMLInstallResult && !Global.ConfigJson.CurrentConfig.FirstOpen)
                        //{
                        //    App.Current.Dispatcher.Invoke(() =>
                        //    {
                        //        extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NO_INSTALL_DML;
                        //        WindowHelper.DMMWindowOpen(71);
                        //    });
                        //    return ret;
                        //}
                        //Directory.CreateDirectory(tempPath);
                        //extract.MoveInfoList.LastOrDefault().FullPathResult = tempPath;
                        //extract.MoveInfoList.LastOrDefault().Status = ExtractInfo.EXTRACT_STATUS.DIRECTORY_DROP;

                        //FileHelper.CopyDirectory(extract);

                        ////extract.MoveInfoList.LastOrDefault().FileAndDirectoryCount = FileHelper.GetFilesAndDirectoriesCount(mv.FullPath);
                        //extract.MoveInfoList.LastOrDefault().FullPathInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPath);
                        ////extract.MoveInfoList.LastOrDefault().DirectorySize = FileHelper.GetDirectorySize(mv.FullPath, extract.SkipFileWhenSizeCheckPathList);
                        ////extract.MoveInfoList.LastOrDefault().FileAndDirectoryCountResult = extract.MoveInfoList.LastOrDefault().FileAndDirectoryCount;
                        ////extract.MoveInfoList.LastOrDefault().DirectorySizeResult = extract.MoveInfoList.LastOrDefault().DirectorySize;

                        //extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXTRACT_SUCCESS;

                        //ret = true;
                        Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    }
                    else
                    {
                        // 想定しないロジック
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                            WindowHelper.DMMWindowOpen(39);
                        });
                    }
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }
            }
            // 圧縮ファイルの場合
            else
            {
                var dmlVersion = DMLUpdater.CheckDMLHash256Archive(FileHelper.CalculateSha256(mv.FullPath));
                if (!string.IsNullOrWhiteSpace(dmlVersion))
                {
                    extract.Site = ExtractInfo.SITE.DML;
                }
                else
                {
                    if (!Global.ConfigJson.CurrentConfig.FirstOpen)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NO_INSTALL_DML;
                            WindowHelper.DMMWindowOpen(71);
                        });
                        Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                        return ret;
                    }
                }
                if (extract.Site != ExtractInfo.SITE.DML)
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Archive File Check..."), LoggerType.Debug, param: ParamInfo);
                    extract.WindowLoggerViewFileName = new FileInfo(mv.FullPath).Name;

                    var tempPath = Path.Combine(Global.downloadBaseLocation, $"temp_{DateTime.Now:yyyyMMddHHmmssfff}");
                    extract.MoveInfoList.LastOrDefault().FullPathResult = tempPath;
                    extract.MoveInfoList.LastOrDefault().Status = ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT;
                }
                else
                {
                    // なんとなくフォルダ分ける
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Archive File Check..."), LoggerType.Debug, param: ParamInfo);

                    var tempPath = Path.Combine(Global.downloadBaseLocation, extract.Site.ToString(), $"temp_{DateTime.Now:yyyyMMddHHmmssfff}");
                    extract.MoveInfoList.LastOrDefault().FullPathResult = tempPath;
                    extract.MoveInfoList.LastOrDefault().Status = ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT;
                }

                // ExtractCore
                ret = ExtractCore(extract);
                var extractCoreResult = extract.MoveInfoList.LastOrDefault().Result;

                if (extractCoreResult != ExtractInfo.EXTRACT_RESULT.EXTRACT_SUCCESS)
                {
                    var resultWindow = WindowHelper.WindowCloseStatus.None;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        resultWindow = extractCoreResult switch
                        {
                            // ファイルサイズがゼロの場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.SIZE_ZERO =>
                                WindowHelper.DMMWindowOpenAsync(10).Result,
                            // サイズが一致しない場合、処理の継続を確認する
                            ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH =>
                                WindowHelper.DMMWindowOpenAsync(11, path: extract.MoveInfoList.LastOrDefault().FullPathResult).Result,
                            // パス長が超過の場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.PATH_LENGTH_OVER_LIMIT =>
                                WindowHelper.DMMWindowOpenAsync(79, 
                                    path: extract.MoveInfoList.LastOrDefault().FullPathResult,
                                    replaceList: new List<string>() { PATH_LENGTH_LIMIT_WINDOWS.ToString(), extract.MoveInfoList.LastOrDefault().ErrorFilePathList.Max(x => x.Length).ToString()}).Result,
                            // Exceptionの場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.EXCEPTION =>
                                WindowHelper.DMMWindowOpenAsync(12).Result,
                            // config.tomlが無い場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.NO_CONFIG_TOML =>
                                WindowHelper.DMMWindowOpenAsync(13).Result,
                            // config.tomlが複数の場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.MULTI_CONFIG_TOML =>
                                WindowHelper.DMMWindowOpenAsync(14).Result,
                            // サポートしない拡張子の場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.UNSUPPORTED =>
                                WindowHelper.DMMWindowOpenAsync(15).Result,
                            // 危険なファイルの場合、処理を終了する
                            ExtractInfo.EXTRACT_RESULT.DANGEROUS_FILE =>
                                WindowHelper.DMMWindowOpenAsync(16).Result,
                            ExtractInfo.EXTRACT_RESULT.NOT_FOUND_SEVENZIP =>
                                WindowHelper.DMMWindowOpenAsync(9, replaceList: new List<string>() { SEVENZIP_NAME, ConfigJson.SEVENZIP_INCLUDE_PRODUCT_VERSION.ToString() }).Result,
                            // 呼ばれない想定(警告回避のため)
                            _ => throw new Exception($"Unknown Error! {MeInfo}, Name:{extract.WindowLoggerViewFileName}, Return:{extractCoreResult}")
                        };
                    });

                    if (extractCoreResult == ExtractInfo.EXTRACT_RESULT.DANGEROUS_FILE)
                    {
                        FileHelper.DeleteFile(extract.MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.DOWNLOAD_FILE).FirstOrDefault()?.FullPathResult);
                    }

                    Logger.WriteLine(string.Join(" ", $"Failed to extract the compressed file. Path:{mv.FullPath}"), LoggerType.Error, param: ParamInfo);

                    if (extractCoreResult == ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH && resultWindow == WindowCloseStatus.Yes)
                    {
                        ret = true;
                        extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXTRACT_SUCCESS;
                        Logger.WriteLine(string.Join(" ", $"Processing continued. Path:{mv.FullPath}"), LoggerType.Info, param: ParamInfo);
                    }
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Name:{extract.WindowLoggerViewFileName}, Return:{ret}"), LoggerType.Debug, param: ParamInfo);

                    return ret;
                }
                else
                {
                    // 正常に展開できていたらファイルを削除する
                    FileHelper.DeleteFile(extract.MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT).FirstOrDefault().FullPath);

                    if (extract.Site == ExtractInfo.SITE.DML)
                    {
                        Global.ConfigJson.CurrentConfig.UpdateModLoaderVersion();
                        ConfigJson.UpdateConfig();
                    }
                }
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// CopyDirectoryRecursionを呼び出す(回帰呼び出し前)
        /// </summary>
        /// <param name="extractDirectoryPath">コピー元ルートディレクトリパス</param>
        /// <param name="moveDirectoryPath">コピー先ルートディレクトリパス</param>
        private static bool MoveDirectory(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);
            TimeSpan ts = default;
            var ret = true;

            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();

            if (extract.Type == ExtractInfo.TYPE.NONE
                || extract.Site == ExtractInfo.SITE.NONE
                || (string.IsNullOrEmpty(extract.MoveInfoList.LastOrDefault().FullPath)
                && !Directory.Exists(extract.MoveInfoList.LastOrDefault().FullPath))
                || (string.IsNullOrEmpty(extract.MoveInfoList.LastOrDefault().FullPathResult)
                && !Directory.Exists(extract.MoveInfoList.LastOrDefault().FullPathResult))
                )
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                var errorStr = $"Invalid argument.\\nType:{{extract.Type}}, Site:{{extract.Site}},"
                    + $"DestDirectoryPath:{extract.MoveInfoList.LastOrDefault().FullPath}, MoveDirectoryPath:{extract.MoveInfoList.LastOrDefault().FullPathResult}";
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}", $"{errorStr}"), LoggerType.Error, param: ParamInfo);
            }
            else
            {
                // modsフォルダへ移動
                List<long> directorySizeOffset = new();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                // add
                extract.MoveInfoList.LastOrDefault().FullPathInfo = FileHelper.GetFilesAndDirectoriesCount(extract.MoveInfoList.LastOrDefault().FullPath, extract.SkipFileWhenSizeCheckPathList);
                List<FileInfo> fiList = new List<FileInfo>();
                foreach (string skipFilePath in extract.SkipFilePathList)
                {
                    fiList.Add(new FileInfo(skipFilePath));
                }

                MoveCount++;
                MoveDirectoryRecursion(0, extract.MoveInfoList.LastOrDefault().FullPath, extract.MoveInfoList.LastOrDefault().FullPathResult, fiList);
                MoveCount--;

                sw.Stop();
                ts = sw.Elapsed;
                extract.MoveTime = ts;

                //extract.MoveInfoList.LastOrDefault().DirectorySizeOffset = directorySizeOffset.Sum();

                if (extract.Site == ExtractInfo.SITE.DML)
                {
                    // DMLで厳密な移動チェック、行わなくてよくない？
                    if (extract.Type == ExtractInfo.TYPE.DOWNLOAD)
                        Global.ConfigJson.CurrentConfig.ModLoaderVersion = Path.GetFileNameWithoutExtension(extract.MoveInfoList.FirstOrDefault().FullPath);
                    ConfigJson.UpdateConfig();
                    ret = true;
                }
                else
                {
                    // modsフォルダに移動した場合はファイル数とディレクトリサイズを取得
                    extract.MoveInfoList.LastOrDefault().FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(extract.MoveInfoList.LastOrDefault().FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    MoveInfoData updateMv = new(extract.MoveInfoList.LastOrDefault());

                    if (extract.Type == ExtractInfo.TYPE.DOWNLOAD)
                    {
                        ret = extract.MoveInfoList.LastOrDefault().CheckFilesCountAndSize();
                    }
                    else if (extract.Type == ExtractInfo.TYPE.CLEAN_UPDATE)
                    {
                        updateMv = new()
                        {
                            Status = ExtractInfo.EXTRACT_STATUS.MOVE_DIRECTORY,
                            FullPath = mv.FullPath,
                            DirectorySize = FileHelper.GetDirectorySize(mv.FullPath, extract.SkipFileWhenSizeCheckPathList),
                            FileAndDirectoryCount = FileHelper.GetFilesAndDirectoriesCount(mv.FullPath, extract.SkipFileWhenSizeCheckPathList),
                            FullPathResult = mv.FullPathResult,
                            DirectorySizeResult = FileHelper.GetDirectorySize(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList),
                            FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList)
                        };
                        // UPDATEの場合に比較するのはMoveInfoWhenUpdateList(config.toml、mod.jsonなどを除いたもの)
                        extract.MoveInfoWhenUpdateList.Add(updateMv);
                        ret = extract.MoveInfoWhenUpdateList.LastOrDefault().CheckFilesCountAndSize();
                    }
                    if (!ret)
                    {
                        extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH;
                        Logger.WriteLine(string.Join(" ", $"Size or Files Unmatch when TemporaryDirectory to Mods Directory Moving..."), LoggerType.Error, param: ParamInfo);

                        App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpenAsync(11));
                        var dumpStr = ObjectDumper.Dump(extract.MoveInfoList.LastOrDefault(), "extract.MoveInfoList.LastOrDefault()");
                        Logger.WriteLine(string.Join(" ", MeInfo, $"Dump:"), LoggerType.Developer, param: ParamInfo, dump: dumpStr);
                    }
                }
            }

            Logger.WriteLine($"MoveTime: {ts.Minutes}分(Minutes) {ts.Seconds}秒(Seconds) {ts.Milliseconds}ミリ秒(Milliseconds)", LoggerType.Debug);
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loop">無限ループ回避用の階層カウンタ。ルートディレクトリの呼び出し時は0を入れてください</param>
        /// <param name="extractDirectoryPath"></param>
        /// <param name="moveDirectoryPath"></param>
        /// <param name="SkipFileList"></param>
        /// <param name="moveSkipPathList"></param>
        /// <exception cref="Exception"></exception>
        private static void MoveDirectoryRecursion(int loop, string extractDirectoryPath, string moveDirectoryPath, List<FileInfo> SkipFileList, List<string> moveSkipPathList = null)
        {
            try
            {
                var moveRootDirectory = moveDirectoryPath;
                Directory.CreateDirectory(moveRootDirectory);
                var inExtractDirectoryFilePathList = Directory.GetFiles(extractDirectoryPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var inExtractDirectoryFilePath in inExtractDirectoryFilePathList)
                {
                    var relativePath = Path.GetRelativePath(extractDirectoryPath, inExtractDirectoryFilePath);
                    var moveFilePath = Path.Combine(moveDirectoryPath, relativePath);
                    var skip = false;
                    if (moveSkipPathList != null)
                    {
                        foreach (var moveSkipPath in moveSkipPathList)
                        {
                            if (FileHelper.PathStartsWith(moveFilePath, moveSkipPath))
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (skip) continue;

                    // todo: FileHelperへ移行
                    if (File.Exists(inExtractDirectoryFilePath) && File.Exists(moveFilePath))
                    {
                        //SkipFileList.Add(new FileInfo(moveFilePath).Length - new FileInfo(inExtractDirectoryFilePath).Length);
                        SkipFileList.Add(new FileInfo(moveFilePath));
                    }
                    File.Copy(inExtractDirectoryFilePath, moveFilePath, true);
                }
                foreach (var childDirectorie in Directory.GetDirectories(extractDirectoryPath, "*", SearchOption.TopDirectoryOnly))
                {
                    var moveRootDirectoryChild = Path.Combine(childDirectorie.Replace(childDirectorie, moveDirectoryPath), Path.GetFileName(childDirectorie));
                    MoveDirectoryRecursion(++loop, childDirectorie, moveRootDirectoryChild, SkipFileList);
                }
            }
            catch (Exception ex)
            {
                // 個々のフォルダ移動エラーログ
                Logger.WriteLine($"Error moving Directory '{extractDirectoryPath}' to '{moveDirectoryPath}': {ex.Message}", LoggerType.Error);
                // Global.logger が null の可能性？ static method なので注意
            }

            if (loop >= MAX_LOOP_RECURSION)
            {
                throw new Exception("MoveDirectoryRecursion Loop over!");
            }
        }

        /// <summary>
        /// MoveDirectoryInTemporaryを呼び出す(回帰呼び出し前)
        /// todo: 説明が古い
        ///   通常はextract.DestDirectoryPathからextract.MoveDirectoryPathへ移動させます。
        ///   newTemporaryPathが与えられた場合のみ、extract.DestDirectoryPath_TempからnewTemporaryPathへ移動し
        ///   そこを正しい移動元フォルダ(extract.DestDirectoryPath)とします
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="newTemporaryPath">テンポラリからテンポラリへ移動させる場合のみ指定すること</param>
        /// <param name="calledMethod"></param>
        private static void MoveDirectoryInTemporary(ExtractInfo extract, string newTemporaryPath = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();

            if (string.IsNullOrWhiteSpace(newTemporaryPath))
            {
                // temp to mods
                MoveDirectoryInTemporaryRecursion(0, mv.FullPath, mv.FullPathResult);

                //mv.DirectorySizeResult = FileHelper.GetDirectorySizeAsyncAuto(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList).Result;
                //mv.FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                mv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                //mv.FileAndDirectoryCountResult = extract.FileAndDirectoryCountResult.Count();
            }
            else
            {
                // temp to temp
                MoveDirectoryInTemporaryRecursion(0, mv.FullPath, newTemporaryPath);
                foreach (var childDirectorie in Directory.GetDirectories(mv.FullPathResult, "*", SearchOption.TopDirectoryOnly))
                {
                    // Download/temp_xxx直下に残ったサイズが0のフォルダは移動済の残りと判断
                    if (FileHelper.GetDirectorySize(childDirectorie, extract.SkipFileWhenSizeCheckPathList) == 0)
                    {
                        FileHelper.DeleteDirectory(childDirectorie);
                        //extract.MoveInfoList.LastOrDefault().FileAndDirectoryCountResult--;
                        var deleteInfo = extract.MoveInfoList.LastOrDefault().FullPathResultInfo.Where(x => x.FullName == childDirectorie).FirstOrDefault();
                        extract.MoveInfoList.LastOrDefault().FullPathResultInfo.Remove(deleteInfo);
                        extract.MoveInfoList.LastOrDefault().FullPathOffset.Add(deleteInfo);
                    }
                }
                extract.MoveInfoList.LastOrDefault().FullPathResult = newTemporaryPath;

                //mv.DirectorySizeResult = FileHelper.GetDirectorySize(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                //mv.FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                //mv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);

                //mv.FileAndDirectoryCountResult = extract.FileAndDirectoryCountResult.Count();
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        /// <summary>
        /// テンポラリフォルダ内でのファイル移動
        /// 二重フォルダ等の場合に使用
        /// </summary>
        /// <param name="extractDirectoryPath">移動元ルートディレクトリパス</param>
        /// <param name="moveDirectoryRootPath">移動先ルートディレクトリパス</param>
        private static void MoveDirectoryInTemporaryRecursion(int loop, string extractDirectoryRootPath, string moveDirectoryRootPath)
        {
            try
            {
                var inExtractDirectoryFileNames = Directory.GetFiles(extractDirectoryRootPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var inExtractDirectoryFileName in inExtractDirectoryFileNames)
                {
                    var moveFilePath = Path.Combine(moveDirectoryRootPath, extractDirectoryRootPath.Replace(extractDirectoryRootPath, string.Empty));
                    File.Move(
                        $"{inExtractDirectoryFileName}",
                        Path.Combine(moveFilePath, Path.GetFileName(inExtractDirectoryFileName))
                    );
                }
                foreach (var childDirectorie in Directory.GetDirectories(extractDirectoryRootPath, "*", SearchOption.TopDirectoryOnly))
                {
                    var childDirectoryName = Path.GetFileName(childDirectorie);
                    if (!FileHelper.PathStartsWith(childDirectoryName, "temp_") && childDirectorie != moveDirectoryRootPath)
                    {
                        var moveNextDirectoryPath = Path.Combine(moveDirectoryRootPath, childDirectoryName, extractDirectoryRootPath.Replace(extractDirectoryRootPath, string.Empty));

                        // 無限ループで同名のネストフォルダを作ってしまうため、暫定(制約)
                        var currentDirectoryName = Path.GetFileName(Path.GetFullPath(moveDirectoryRootPath).TrimEnd(Path.DirectorySeparatorChar));
                        var createDirectoryName = Path.GetFileName(Path.GetFullPath(moveNextDirectoryPath).TrimEnd(Path.DirectorySeparatorChar));
                        if (currentDirectoryName != createDirectoryName)
                        {
                            Directory.CreateDirectory(moveNextDirectoryPath);
                            MoveDirectoryInTemporaryRecursion(loop++, childDirectorie, moveNextDirectoryPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string MeInfo = Logger.GetMeInfo(new StackFrame());
                Logger.WriteLine(string.Join(" ", $"{MeInfo}", $"Error moving Directory '{extractDirectoryRootPath}' to '{moveDirectoryRootPath}': {ex.Message}"), LoggerType.Error);
            }

            if (loop >= MAX_LOOP_RECURSION)
            {
                throw new Exception("MoveDirectoryInTemporaryRecursion Loop over!");
            }
        }

        /// <summary>
        /// mod.jsonを生成する
        /// (extractPatternParameterBase.IsCreateModJsonを設定)
        /// </summary>
        /// <param name="extract"></param>
        /// <returns>生成した場合はtrue</returns>
        private static bool CreateModJson(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;
            var mv = extract.MoveInfoList.LastOrDefault();

            // Modのダウンロード、アップデートのみ生成
            if ((extract.Type == ExtractInfo.TYPE.DOWNLOAD
                || extract.Type == ExtractInfo.TYPE.CLEAN_UPDATE)
                && !string.IsNullOrEmpty(mv.FullPathResult)
                && extract.Site != ExtractInfo.SITE.DML)
            {
                // 新しく作るmod.jsonのパス
                var createModJsonPath = Path.Combine(mv.FullPathResult, "mod.json");
                if (!File.Exists(createModJsonPath))
                {
                    MetadataManager metadataManager = new(extract);
                    metadataManager.metadata.SaveMetadata(createModJsonPath);

                    ret = true;
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Create Metadata.", $"modJsonPath:{createModJsonPath}", $"GetMetadataString():{metadataManager.metadata.GetMetadataString()}"), LoggerType.Debug, param: ParamInfo);
                }
            }

            Logger.WriteLine($"{MeInfo} End. Return:{ret}", LoggerType.Debug);
            return ret;
        }

        /// <summary>
        /// 展開したフォルダがダミーファイルかチェックする
        /// </summary>
        /// <param name="extract"></param>
        /// <returns></returns>
        private static bool CheckDummyModAsync(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            // DROPの場合はチェックしない
            if (extract.Type == ExtractInfo.TYPE.DROP
                || extract.Site == ExtractInfo.SITE.DML
                || string.IsNullOrEmpty(extract.Url))
            {
                ret = true;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            var config_toml_cnt = Directory.GetFiles(extract.MoveInfoList.LastOrDefault().FullPathResult, "config.toml", SearchOption.AllDirectories).Length;

            long size;
            if (File.Exists(extract.MoveInfoList.LastOrDefault().FullPathResult))
            {
                size = new FileInfo(extract.MoveInfoList.LastOrDefault().FullPathResult).Length;
            }
            else if (Directory.Exists(extract.MoveInfoList.LastOrDefault().FullPathResult))
            {
                size = new DirectoryInfo(extract.MoveInfoList.LastOrDefault().FullPathResult).GetDirectorySize();
            }
            else
            {
                size = 0;
            }

            if (config_toml_cnt == 0 && 1024 * 1024 > size)    // 1MB(1048576)
            {
                ret = false;

                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.DUMMY_MOD;
                FileHelper.DeleteFile(extract.MoveInfoList.FirstOrDefault().FullPath);
                FileHelper.DeleteDirectory(extract.MoveInfoList.FirstOrDefault().FullPathResult);

                var resultWindow = WindowHelper.DMMWindowOpenAsync(20);
                if (resultWindow.Result == WindowHelper.WindowCloseStatus.Yes)
                {
                    ProcessHelper.TryStartProcess(extract.Url);
                }
            }
            else
            {
                ret = true;
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 正しいルートフォルダを確認してExtractInfo.DestDirectoryPathに設定する
        /// (ファイルを選択して圧縮していた場合や二重フォルダのチェック)
        /// 前提：呼び出す前にextractPatternParameterBase.DestDirectoryPath_TempRootを設定しておくこと
        /// ＊圧縮ファイル展開後、ドロップされたディレクトリをベースにして正しいルートディレクトリを調査する
        /// </summary>
        /// <param name="extract">解凍したテンポラリディレクトリ(ExtractInfo.DestDirectoryPath_TempRoot)(必須)</param>
        /// <returns></returns>
        //private static async Task<string> GetRootFolderAsync(string ArchiveDestination, string _ArchiveSourcePath = null)
        private static bool CheckArchiveRootDirectory(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = true;
            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();
            MoveInfoData mv_first = extract.MoveInfoList.FirstOrDefault();

            if (extract.Site == ExtractInfo.SITE.DML)
            {
                ret = true;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End. Skip. Return:{ret} DestDirectoryPath:{mv.FullPathResult}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            if (string.IsNullOrEmpty(mv.FullPathResult) || !Directory.Exists(mv.FullPathResult))
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", MeInfo, $"Invalid argument. Return:{ret} DestDirectoryPath:{mv.FullPathResult}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            // 圧縮ファイルを展開していた場合(temp_xxxx/にファイルが生成された)
            // (ディレクトリのドロップの場合も同様)
            else if (!string.IsNullOrEmpty(mv.FullPath))
            {
                var destDirectoryPath_TempRoot_files = Directory.GetFiles(mv.FullPathResult, "*", SearchOption.TopDirectoryOnly);
                var destDirectoryPath_TempRoot_dirs = Directory.GetDirectories(mv.FullPathResult, "*", SearchOption.TopDirectoryOnly);

                // 圧縮ファイルがファイルを直接圧縮していた場合(解凍フォルダにファイルがあるか、フォルダが2つ以上ある)
                if (destDirectoryPath_TempRoot_files.Length > 0 || destDirectoryPath_TempRoot_dirs.Length > 1)
                {
                    // 圧縮ファイル名からフォルダを作成し、そこをルートフォルダとする
                    MoveInfoData nextMv = new(mv);
                    var fileName = Path.GetFileNameWithoutExtension(mv.FullPath);
                    var dirName = new DirectoryInfo(mv.FullPath).Name;
                    var setName = !string.IsNullOrEmpty(fileName) ? fileName : dirName;

                    var newDir = Path.Combine(mv.FullPathResult, setName);
                    Directory.CreateDirectory(newDir);
                    nextMv.FullPathResult = newDir;
                    extract.MoveInfoList.Add(nextMv);

                    MoveDirectoryInTemporary(extract, nextMv.FullPathResult);

                    //nextMv.DirectorySizeResult = FileHelper.GetDirectorySize(nextMv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    //nextMv.FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(nextMv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    nextMv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(nextMv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    //nextMv.FileAndDirectoryCountResult = extract.FileAndDirectoryCountResult.Count();
                    if (!nextMv.CheckFilesCountAndSize())
                    {
                        ret = false;
                        nextMv.Result = ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH;
                        Logger.WriteLine(string.Join(" ", MeInfo, $"The number of files after extraction does not match. File extraction may have failed. extractFileCount: {nextMv.FullPathInfo.Count}, DestFileCount: {nextMv.FullPathResultInfo.Count}, extractSize: {nextMv.GetFullPathSize()}, DestSize: {nextMv.GetFullPathResultSize()}"), LoggerType.Warning, param: ParamInfo);
                        Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                        return false;
                    }
                }
                // フォルダを圧縮している圧縮ファイル(temp_xxxx/Mod_Name/の状態)(普通はこれのはず)
                else
                {
                    // ひとつ下のディレクトリに移動
                    ret = MoveNextDir(extract);
                    if (!ret)
                    {
                        Logger.WriteLine(string.Join(" ", MeInfo, $"End. MoveNextDir Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                        return ret;
                    }

                    // 二重フォルダチェック(解凍したディレクトリのひとつ下にconfig.tomlがあるか)
                    ret = GetConfigTomlDirectory(extract);
                    if (!ret)
                    {
                        // config.tomlが無いか複数ある場合は移動しない
                        ret = false;
                        var checkRes = extract.MoveInfoList.LastOrDefault().Result;
                        WindowHelper.WindowCloseStatus resultWindow = WindowHelper.WindowCloseStatus.None;
                        var tempDirPath = extract.MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT).FirstOrDefault().FullPathResult;
                        if (checkRes == ExtractInfo.EXTRACT_RESULT.NO_CONFIG_TOML)
                        {
                            resultWindow = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(13));
                        }
                        else if (checkRes == ExtractInfo.EXTRACT_RESULT.MULTI_CONFIG_TOML)
                        {
                            resultWindow = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(14, path: tempDirPath));
                            if (resultWindow == WindowHelper.WindowCloseStatus.Yes)
                            {
                                ret = true;
                            }
                        }
                        else
                        {
                            resultWindow = App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(39));
                        }
                        if (!ret && (tempDirPath != null || !string.IsNullOrEmpty(tempDirPath)))
                        {
                            // 圧縮ファイルを解凍していたら削除するか質問
                            if (App.Current.Dispatcher.Invoke(() =>
                                WindowHelper.DMMWindowOpen(19, path: tempDirPath) == WindowHelper.WindowCloseStatus.Yes))
                                FileHelper.DeleteDirectory(tempDirPath);
                        }
                    }
                    else if (extract.MoveInfoList.LastOrDefault().FullPath
                        != extract.MoveInfoList.LastOrDefault().FullPathResult)
                    {
                        // ネストの上位フォルダにファイルがあるチェックを実装し、ある場合は終了する
                        CheckIsFilesConfigTomlDirectoryHiger(extract);
                        if (ret = extract.MoveInfoList.LastOrDefault().Result
                            == ExtractInfo.EXTRACT_RESULT.NEST_DIRECTORY_AND_SIZE_UNMATCH)
                        {
                            ret = false;
                            var tempPath = extract.MoveInfoList.Where(x => x.Status == ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT).FirstOrDefault().FullPathResult;
                            var delWindow = WindowHelper.DMMWindowOpenAsync(18, path: tempPath);
                            if (delWindow.Result == WindowHelper.WindowCloseStatus.Yes)
                            {
                                FileHelper.DeleteDirectory(tempPath);
                            }
                        }
                        else
                        {
                            // 2重フォルダの場合は修正
                            MoveDirectoryInTemporary(extract, extract.MoveInfoList.LastOrDefault().FullPathResult);
                            ret = true;
                        }
                    }
                    else
                    {
                        extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NOT_NEST_DIRECTORY;
                        ret = true;
                    }
                }
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        private static bool MoveNextDir(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = true;

            // ひとつ下のディレクトリに移動
            MoveInfoData mvNext = new(extract.MoveInfoList.LastOrDefault())
            {
                Status = ExtractInfo.EXTRACT_STATUS.CHECK_ROOT_COMPRESS_FILE_DIRECT,
                FullPathResult = Directory.GetDirectories(extract.MoveInfoList.LastOrDefault().FullPathResult, "*", SearchOption.TopDirectoryOnly).FirstOrDefault(),
            };
            extract.MoveInfoList.Add(mvNext);
            MoveDirectoryInTemporary(extract, mvNext.FullPathResult);

            extract.MoveInfoList.LastOrDefault().FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mvNext.FullPathResult, extract.SkipFileWhenSizeCheckPathList);

            if (extract.MoveInfoList.LastOrDefault().CheckFilesCountAndSize())
            {
                ret = false;
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// config.tomlフォルダが存在したフォルダの上位にファイルがあるかチェックする(サイズ比較のみ)
        /// </summary>
        /// <param name="extract"></param>
        private static void CheckIsFilesConfigTomlDirectoryHiger(ExtractInfo extract)
        {
            var tempRootInfo = extract.TempRootInfo();
            var tempRootSize = tempRootInfo.DirectorySize;
            var configPaths = Directory.GetFiles(tempRootInfo.FullPathResult, "config.toml", SearchOption.AllDirectories);
            var configPathSize = FileHelper.GetDirectorySize(Path.GetDirectoryName(configPaths.FirstOrDefault()), extract.SkipFileWhenSizeCheckPathList);
            extract.MoveInfoList.LastOrDefault().Result = configPaths.Length switch
            {
                0 => ExtractInfo.EXTRACT_RESULT.NO_CONFIG_TOML,
                > 1 => ExtractInfo.EXTRACT_RESULT.MULTI_CONFIG_TOML,
                _ => tempRootSize == configPathSize ? ExtractInfo.EXTRACT_RESULT.NEST_DIRECTORY : ExtractInfo.EXTRACT_RESULT.NEST_DIRECTORY_AND_SIZE_UNMATCH
            };
        }

        /// <summary>
        /// config.tomlの存在するフォルダをチェック
        /// </summary>
        /// <param name="extract"></param>
        /// <returns>config.tomlが1つ以外(0または複数)の場合にfalse、それ以外の対応可能なパターンであれば選択肢によってtrue</returns>
        private static bool GetConfigTomlDirectory(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;
            var configTomlDirs = Directory.GetFiles(extract.TempRootInfo().FullPathResult, "config.toml", SearchOption.AllDirectories);

            // config.tomlなし
            if (configTomlDirs.Length == 0)
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NO_CONFIG_TOML;
                Logger.WriteLine($"", LoggerType.Warning);

                Logger.WriteLine(string.Join(" ", MeInfo, $"config.toml is missing .\nThis compressed file cannot be copied to the mods folder."), LoggerType.Warning, param: ParamInfo);
                Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }
            // config.tomlが複数
            else if (configTomlDirs.Length > 1)
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.MULTI_CONFIG_TOML;
                Logger.WriteLine(string.Join(" ", MeInfo, $"config.toml is exists multiple.\nThis compressed file cannot be copied to the mods folder."), LoggerType.Warning, param: ParamInfo);
                Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }
            else
            {
                // config.tomlのあるフォルダを返す
                MoveInfoData mvConfigToml = new(extract.MoveInfoList.LastOrDefault())
                {
                    Status = ExtractInfo.EXTRACT_STATUS.CHECK_CONFIG_TOML_DIRECTORY,
                    FullPathResult = Path.GetDirectoryName(Path.GetFullPath(configTomlDirs.FirstOrDefault()))
                };

                mvConfigToml.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mvConfigToml.FullPathResult, extract.SkipFileWhenSizeCheckPathList);

                extract.MoveInfoList.Add(mvConfigToml);

                ret = true;
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modDirectoryRootPath">Modフォルダパス(MOD_NAME)</param>
        /// <returns>成否</returns>
        /// MoveDirectoryRootPathに移動先ディレクトリパスが設定されます
        /// なお移動先フォルダは作りません
        private static bool GetMoveDirectoryName(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            var ret = false;
            MoveInfoData mvResult = new(extract.MoveInfoList.LastOrDefault())
            {
                Status = ExtractInfo.EXTRACT_STATUS.MOVE_DIRECTORY
            };


            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start. Name:{mvResult.FullPathResult}"), LoggerType.Debug, param: ParamInfo);

            // すでに移動先パスが設定されている場合は設定(DML、UPDATE時など)
            if (extract.Site == ExtractInfo.SITE.DML)
            {
                string directoryRootPath = $"{new DirectoryInfo(Global.ConfigJson.CurrentConfig.Launcher).Parent}";

                mvResult.FullPathResult = directoryRootPath;
                extract.MoveInfoList.Add(mvResult);

                ret = true;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }
            else if (extract.Type == ExtractInfo.TYPE.CLEAN_UPDATE)
            {
                string directoryRootPath = mvResult.FullPath;

                if (!Directory.Exists(directoryRootPath))
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"The extracted directory '{directoryRootPath}' does not exist."), LoggerType.Error);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }

                var directoryName = Path.GetFileName(directoryRootPath);
                mvResult.FullPathResult = Path.Combine(Global.ModsFolder, directoryName);
                extract.MoveInfoList.Add(mvResult);

                ret = true;
            }
            else if (extract.Type == ExtractInfo.TYPE.DOWNLOAD || extract.Type == ExtractInfo.TYPE.DROP)
            {
                string directoryRootPath = mvResult.FullPath;

                if (!Directory.Exists(directoryRootPath))
                {
                    Logger.WriteLine($"The extracted directory '{directoryRootPath}' does not exist.", LoggerType.Error);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                    return ret;
                }

                var directoryName = Path.GetFileName(directoryRootPath);

                // Modsフォルダ直下に同名フォルダが存在する場合、連番を付与
                string moveDirNameBase = Path.Combine(Global.ModsFolder, directoryName);
                string moveDirNameCopyedName = moveDirNameBase;

                if (extract.Type != ExtractInfo.TYPE.CLEAN_UPDATE)
                {
                    int index = 1;
                    while (Directory.Exists(moveDirNameCopyedName))
                    {
                        moveDirNameCopyedName = $@"{moveDirNameBase} ({index})";
                        index += 1;
                    }
                }

                mvResult.FullPathResult = moveDirNameCopyedName;
                extract.MoveInfoList.Add(mvResult);

                ret = true;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End. Name:{mvResult.FullPathResult}, Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            }
            return ret;
        }

        /// <summary>
        /// DMLまたはUPDATEの時、config.tomlの項目をマージする
        /// </summary>
        /// <param name="extract"></param>
        /// <returns>成否</returns>
        /// todo: ロジックが長すぎる気がする
        private static bool UpdateModFolderConfigToml(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;
            var mv = extract.MoveInfoList.LastOrDefault();

            string temporaryDirectoryModRootPath = mv.FullPath;
            string modDirectoryRootPath = mv.FullPathResult;

            // アップデート時かつ新旧のconfig.tomlが存在する場合のみ続行
            if (extract == null
                || (extract.Site != ExtractInfo.SITE.DML && extract.Type != ExtractInfo.TYPE.CLEAN_UPDATE)
                || string.IsNullOrEmpty(temporaryDirectoryModRootPath)
                || string.IsNullOrEmpty(modDirectoryRootPath))
            {
                // それ以外はtrueとして終了
                ret = true;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                return ret;
            }

            var tempConfigPath = Path.Combine(temporaryDirectoryModRootPath, "config.toml");
            var modsConfigPath = Path.Combine(modDirectoryRootPath, "config.toml");

            TomlTable tempConfig = null;
            if (File.Exists(tempConfigPath))
            {
                Toml.TryToModel(File.ReadAllText(tempConfigPath), out tempConfig, out var diagnostics);
            }
            else
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"config.toml in the Mods folder is missing or has an incorrect format. Please download the latest version instead of updating. Path:{tempConfigPath}"), LoggerType.Debug);
                return ret;
            }

            TomlTable modsConfig = null;

            if (File.Exists(modsConfigPath))
            {
                Toml.TryToModel(File.ReadAllText(modsConfigPath), out modsConfig, out var diagnostics);
            }

            var pathDlltemp = Path.Combine(extract.MoveInfoList.LastOrDefault().FullPath, DMLUpdater.MODULE_NAME_DLL);
            var pathDllmods = Path.Combine(Global.ConfigJson.GetGameLocation(), DMLUpdater.MODULE_NAME_DLL);
            var is39error = false;

            if (tempConfig != null)
            {
                // DML Update (メソッド分けた方がよくない？)
                if (extract.Site == ExtractInfo.SITE.DML)
                {
                    if (extract.Type == ExtractInfo.TYPE.DROP || extract.Type == ExtractInfo.TYPE.DOWNLOAD)
                    {
                        if (modsConfig != null)
                        {
                            // 最終更新日でチェック
                            var lastUpdateResult = VersionHelper.CompareLastUpdate(pathDlltemp, pathDllmods);
                            if (lastUpdateResult == VersionHelper.Result.VersionA_AS_LONGER
                                || lastUpdateResult == VersionHelper.Result.VersionB_NOTHING)
                            {
                                foreach (var key in tempConfig.Keys)
                                {
                                    // これらの項目は旧config.tomlの記載を引き継ぐ
                                    if (modsConfig.ContainsKey(key)
                                        && (key.ToLowerInvariant() == "enabled"
                                        || key.ToLowerInvariant() == "console"
                                        || key.ToLowerInvariant() == "priority"))
                                    {
                                        tempConfig[key] = modsConfig[key];
                                    }
                                    else
                                    {
                                        // 旧configに存在して新configに無い項目は全て旧configから引き継ぐ
                                        // （値が消えることは無い想定だけど）
                                        tempConfig[key] = modsConfig[key];
                                    }
                                }
                                // テンポラリフォルダのconfig.tomlを更新(ファイル移動時に上書きする)
                                FileHelper.TryWriteAllText(tempConfigPath, Toml.FromModel(modsConfig));
                                ret = true;
                            }
                        }
                        else
                        {
                            ret = true;
                        }
                    }
                    else
                    {
                        is39error = true;
                    }
                }
                else if (extract.Site == ExtractInfo.SITE.DMM)
                {
                    is39error = true;
                }
                else if (extract.Type == ExtractInfo.TYPE.CLEAN_UPDATE)
                {
                    ret = true;
                    // mod update
                    if (tempConfig == null)
                    {
                        ret = false;
                        mv.Result = ExtractInfo.EXTRACT_RESULT.NO_CONFIG_TOML;
                    }
                    if (ret != false)
                    {
                        var updateConfigToml = false;
                        foreach (var key in tempConfig.Keys)
                        {
                            // これらの項目は旧config.tomlの記載を引き継ぐ
                            if ((modsConfig.ContainsKey(key) && key.Equals("enabled")))
                            {
                                tempConfig[key] = modsConfig[key];
                                updateConfigToml = true;
                            }
                        }
                        if (updateConfigToml)
                        {
                            File.WriteAllText(tempConfigPath, Toml.FromModel(tempConfig));
                        }
                        ret = true;
                    }
                }
                else
                {
                    is39error = true;
                }
            }
            else
            {
                is39error = true;
            }

            if (!ret)
            {
                if (extract.Site == ExtractInfo.SITE.DML)
                {
                    List<string> replaceList = new() { DMLUpdater.MODULE_NAME, new FileInfo(pathDllmods).LastWriteTime.ToString(), new FileInfo(pathDlltemp).LastWriteTime.ToString() };
                    App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpenAsync(48, replaceList));
                    Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
                }
                else
                {
                    App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpenAsync(13));
                }
            }
            else if (is39error)
            {
                ret = false;
                App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpenAsync(39));
                Logger.WriteLine(string.Join(" ", MeInfo, $"Exception End.", $"Return:{ret}"), LoggerType.Error, param: ParamInfo);
            }
            else
            {
                ret = true;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        private static bool ExtractCore(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            var ret = false;
            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start. Name:{mv.FullPath}"), LoggerType.Debug, param: ParamInfo);

            // 解凍元ファイルが存在しない場合はエラー
            if (string.IsNullOrEmpty(mv.FullPath) || !File.Exists(mv.FullPath))
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                Logger.WriteLine(string.Join(" ", $"{MeInfo} End.", $"Name:{mv.FullPath}, Return:{ret}", $"Archive file is not Found! Path:{mv.FullPath}"), LoggerType.Error);
                return ret;
            }
            else if (FileHelper.GetFileOrDirectorySize(mv.FullPath, extract.SkipFilePathList) <= 0)
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.SIZE_ZERO;
                Logger.WriteLine(string.Join(" ", $"{MeInfo} End.", $"Name:{mv.FullPath}, Return:{ret}", $"Archive file size is Zero! Path:{mv.FullPathResult}"), LoggerType.Error);
                return ret;
            }

            var extension = Path.GetExtension(mv.FullPath).ToLowerInvariant();
            var sw = new Stopwatch();

            try
            {
                static bool UnSupportedCall(ExtractInfo x)
                {
                    x.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.UNSUPPORTED;
                    return false;
                }

                // extension が zip/rar/7z 以外なら早期リターン
                if (extension switch { ".zip" => true, ".rar" => true, ".7z" => true, _ => false })
                {
                    // ZipSlipをチェック
                    if (!CheckArchiveFile(extract)) return false;

                    sw.Start();

                    ret = extension switch
                    {
                        ".zip" => ExtractUseSevenZipLocal(extract),
                        ".rar" => ExtractRarCaller(extract),
                        ".7z" => ExtractUseSevenZipLocal(extract),
                        _ => UnSupportedCall(extract)   // ここは到達しないが保険的に
                    };
                }
                else
                {
                    // Unsupported な拡張子の場合、チェックをスキップしてリターン
                    return UnSupportedCall(extract);
                }

            }
            catch (Exception ex)
            {
                sw.Stop();
                ret = false;
                extract.ExtractResult = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                Logger.WriteLine(string.Join(" ", $"{MeInfo} End.", $" Name:{mv.FullPath}, Return:{ret}", $"ex.Message:{ex.Message}", $"ex.StackTrace:{ex.StackTrace}"), LoggerType.Error);

                throw;
            }

            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            extract.ExtractCoreTime = ts;

            Logger.WriteLine($"ExtractCoreTime: {ts.Minutes}分(Minutes) {ts.Seconds}秒(Seconds) {ts.Milliseconds}ミリ秒(Milliseconds)", LoggerType.Debug);
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $" Name:{mv.FullPath}, Return:{ret}"), LoggerType.Debug, param: ParamInfo);

            return ret;
        }

        /// <summary>
        /// RAR解凍を呼び出すメソッドを決定する
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        private static bool ExtractRarCaller(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            if ((bool)!Global.ConfigToml.WinRarUse)
            {
                //ret = ExtractRar(extract);
                ret = ExtractUseSevenZipLocal(extract);
            }
            else
            {
                ret = ExtractRarUseWinRAR(extract);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 解凍前処理
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        private static bool CheckArchiveFile(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;
            extract.UseExtractComponentZipSlipCheck = "SharpCompress (0.49.1)";

            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();
            using var archive = ArchiveFactory.OpenArchive(mv.FullPath);
            mv.FileAndDirectoryCount = new();
            mv.DirectorySize = archive.Entries.Sum(x => x.Size);

            foreach (var e in archive.Entries)
            {
                string entry = e.Key;
                string dest = Path.Combine(mv.FullPathResult, entry.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                string fullDest = Path.GetFullPath(dest);
                string fullRoot = Path.GetFullPath(mv.FullPathResult);

                string ioPath = fullDest;

                // 危険パス、パス長チェック
                var pathResult = IsDangerousPath(fullDest, fullRoot);
                if (pathResult != EXTRACT_RESULT.NONE)
                {
                    extract.MoveInfoList.LastOrDefault().Result = pathResult;
                    extract.MoveInfoList.LastOrDefault().ErrorFilePathList.Add(fullDest);

                    Logger.WriteLine(
                        $"{Path.GetFileName(mv.FullPath)},{entry}," +
                        $"{extract.UseExtractComponentZipSlipCheck}," +
                        $"Blocked File Path,{fullDest}",
                        LoggerType.Info,
                        param: ParamInfo);

                    continue;
                }

                // ReparsePoint拒否
                if (ContainsReparsePoint(Path.GetDirectoryName(fullDest)!, fullRoot))
                {
                    extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.DANGEROUS_FILE;
                    extract.MoveInfoList.LastOrDefault().ErrorFilePathList.Add(fullDest);

                    Logger.WriteLine(
                        $"{Path.GetFileName(mv.FullPath)},{entry}," +
                        $"{extract.UseExtractComponentZipSlipCheck}," +
                        $"Blocked ReparsePoint,{fullDest}",
                        LoggerType.Info,
                        param: ParamInfo);

                    continue;
                }

                var dirToCheck = fullDest;

                mv.FileAndDirectoryCount.Add(new FileInfo(fullDest));
            }

            // 全走査した時点で結果をチェック＠記載漏れからバグになりやすい部分なのでロジック見直し
            if (extract.MoveInfoList.LastOrDefault().Result is ExtractInfo.EXTRACT_RESULT.DANGEROUS_FILE or ExtractInfo.EXTRACT_RESULT.EXCEPTION or ExtractInfo.EXTRACT_RESULT.PATH_LENGTH_OVER_LIMIT)
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}", $"This file cannot be extracted because it may be supported file. File:{mv.FullPath}"), LoggerType.Error, param: ParamInfo);
                return ret;
            }

            ret = true;
            extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.ZIPSLIP_SUCCESS;
            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// ドロップされたファイルがDMLかチェックする
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        private static bool IsDMLInstall(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var isDML = false;
            var isException = true;

            MoveInfoData mv = extract.MoveInfoList.LastOrDefault();

            if (extract.Site == ExtractInfo.SITE.LOCAL && extract.Type == ExtractInfo.TYPE.DROP)
            {
                if (Directory.Exists(mv.FullPath))
                {
                    if (Global.ConfigToml.DropDivaModLoaderCheck && DMLUpdater.CheckDMLDirectory(mv.FullPath))
                    {
                        var replaceList = new List<string> { DMLUpdater.MODULE_NAME };
                        if (App.Current.Dispatcher.Invoke(() =>
                            WindowHelper.DMMWindowOpen(31, replaceList, path: mv.FullPath) == WindowCloseStatus.Yes)
                        )
                        {
                            extract.Site = ExtractInfo.SITE.DML;
                            isDML = true;
                            isException = false;
                        }
                    }
                    else
                    {
                        if (!Global.ConfigJson.CurrentConfig.FirstOpen)
                        {
                            extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.NO_INSTALL_DML;
                            App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(71));
                            isException = false;
                        }
                        // 通常のフォルダ
                        else
                        {
                            isException = false;
                        }
                    }
                } // isException
            }

            if (isException)
            {
                extract.MoveInfoList.LastOrDefault().Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                App.Current.Dispatcher.Invoke(() => WindowHelper.DMMWindowOpen(39));
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{isDML}"), LoggerType.Debug, param: ParamInfo);

            return isDML;
        }

        /// <summary>
        /// Rar解凍(WinRAR)
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        /// 非同期にするとProcess.WaitForExitAsync()でしぬ
        private static bool ExtractRarUseWinRAR(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = true;
            var mv = extract.MoveInfoList.LastOrDefault();

            if (!File.Exists(Global.ConfigJson.WinRarConsolePath)
                    || Global.ConfigJson.WinRarConsoleVersion != FileVersionInfo.GetVersionInfo(Global.ConfigJson.WinRarConsolePath).ProductVersion)
            {
                ret = false;
                mv.Result = ExtractInfo.EXTRACT_RESULT.NOT_FOUND_WINRAR;
                Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"{ExtractInfo.EXTRACT_RESULT.NOT_FOUND_WINRAR.ToString()}"), LoggerType.Error, param: ParamInfo);
                return ret;
            }
            extract.UseExtractComponentExtract = $"{WINRAR_CONSOLE_EXE_NAME}({Global.ConfigJson.WinRarConsoleVersion})";

            var extractCommand = $"x -y \"{mv.FullPath}\" \"{mv.FullPathResult}\"";

            try
            {
                Logger.WriteLine($"Running {WINRAR_CONSOLE_EXE_NAME} param:{extractCommand}\n", LoggerType.Debug);

                using CancellationTokenSource cancelTokenSource = new();
                CancellationToken cancelToken = cancelTokenSource.Token;

                ProcessStartInfo processInfo = new()
                {
                    FileName = Global.ConfigJson.WinRarConsolePath,
                    WorkingDirectory = Global.assemblyLocation,
                    UseShellExecute = false,
                    Arguments = extractCommand,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                using var extractWinRarProcess = new Process
                {
                    StartInfo = processInfo,
                };

                try
                {
                    if (!Directory.Exists(mv.FullPathResult))
                        Directory.CreateDirectory(mv.FullPathResult);

                    Logger.WriteLine(string.Join(" ", $"Extract Start.", $"(File:{new FileInfo(mv.FullPath).Name})", $"(using {WINRAR_NAME}({Global.ConfigJson.WinRarConsoleVersion}))"), LoggerType.Info);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Extract by WinRAR Start...", $"ExtractFiles:{Path.GetFileName(mv.FullPath)}"), LoggerType.Debug, param: ParamInfo);
                    extractWinRarProcess.EnableRaisingEvents = true;
                    //extractWinRarProcess.OutputDataReceived += DataReceivedEvent;
                    //ExtractWinRarProcess.OutputDataReceived += DataReceivedEventToProgressRate;
                    extractWinRarProcess.Exited += new EventHandler(ExtractComplete);

                    // Rar.exe実行(Extract)
                    extractWinRarProcess.Start();
                    extractWinRarProcess.WaitForExit();

                    //mv.FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    mv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    //mv.FileAndDirectoryCountResult = extract.FileAndDirectoryCountResult.Count();
                    //mv.DirectorySizeResult = FileHelper.GetDirectorySize(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);

                    if (!mv.CheckFilesCountAndSize())
                    {
                        ret = false;
                        mv.Result = ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH;
                        Logger.WriteLine(string.Join(" ", MeInfo, $"The number of files after extraction does not match. File extraction may have failed.", $"ExtractFiles: {mv.FullPath.Count()}, DestFiles: {mv.FullPathResult.Count()}"), LoggerType.Warning, param: ParamInfo);
                    }

                    ret = true;
                    mv.Result = ExtractInfo.EXTRACT_RESULT.EXTRACT_SUCCESS;
                    Logger.WriteLine(string.Join(" ", $"Extract End.", $"(File:{new FileInfo(mv.FullPath).Name})", $"(using {WINRAR_NAME}({Global.ConfigJson.WinRarConsoleVersion}))"), LoggerType.Info);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Extract by WinRAR Complete!", $"ExtractFiles: {Path.GetFileName(mv.FullPath)}"), LoggerType.Debug, param: ParamInfo);
                }
                // タスクがキャンセルされた場合の処理
                catch (OperationCanceledException)
                {
                    ret = false;
                    mv.Result = ExtractInfo.EXTRACT_RESULT.CANCELED;
                }
                finally
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Finally WinRar Process Kill Start.", $"{WINRAR_CONSOLE_EXE_NAME} param:{extractCommand}"), LoggerType.Debug, param: ParamInfo);
                    KillChildProcess(extractWinRarProcess);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Finally WinRar Process Kill End.", $"{WINRAR_CONSOLE_EXE_NAME} param:{extractCommand}"), LoggerType.Debug, param: ParamInfo);
                }
            }
            catch (InvalidFormatException ex)
            {
                mv.Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                Logger.WriteLine(string.Join(" ", MeInfo, $"Format error extracting '{mv.FullPath}", $"ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
            }
            catch (IOException ex)
            {
                mv.Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                Logger.WriteLine(string.Join(" ", MeInfo, $"IO error extracting '{mv.FullPath}", $"ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
            }
            catch (Exception ex)
            {
                mv.Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
                Logger.WriteLine(string.Join(" ", MeInfo, $"Error extracting '{mv.FullPath}", $"ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 外部出力を標準出力として受け取る
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void DataReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    await App.Current.Dispatcher.BeginInvoke(() =>
                    {
                        Logger.WriteLine($"{e.Data}", LoggerType.Debug);
                    });
                }
            }
            catch (Exception)
            {
                await App.Current.Dispatcher.BeginInvoke(() =>
                {
                    Logger.WriteLine($"{e.Data}", LoggerType.Debug);
                });
            }
        }

        /// <summary>
        /// 外部出力を標準出力として受け取る
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void DataReceivedEventToProgressRate(object sender, DataReceivedEventArgs e)
        {
            await App.Current.Dispatcher.BeginInvoke(() =>
            {
                var progressBox = new ExtractProgress(0, 100);
                progressBox.Show();
                progressBox.Activate();
                IProgress<double> progress = (IProgress<double>)progressBox.progressBar;
                try
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        //Logger.WriteLine($"{e.Data}", LoggerType.Debug);
                        progressBox.extractinfo.ProgressValue = double.Parse(Regex.Match(e.Data, "(\\d+)%").Value.Replace("%", ""));

                        // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                        progress.Report(progressBox.extractinfo.ProgressValue);
                    }
                }
                catch (Exception)
                {
                    Logger.WriteLine($"{e.Data}", LoggerType.Debug);
                }
                ;
            });
        }

        /// <summary>
        /// 外部プロセスでの解凍終了時コールバック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ExtractComplete(object sender, EventArgs e)
        {
            App.Current.Dispatcher.InvokeAsync(() =>
            {
                Logger.WriteLine($"Process Extract Complete!", LoggerType.Debug);
            });
        }

        /// <summary>
        /// 呼び出した外部解凍プロセスを閉じる
        /// </summary>
        /// <param name="process"></param>
        private static void KillChildProcess(Process process)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error killing process: {ex.Message}", LoggerType.Error);
            }
            finally
            {
                process?.Dispose();
            }
        }

        /// <summary>
        /// 7-Zipによる解凍
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        /// 非同期にするとProcess.WaitForExitAsync()でしぬかも
        private static bool ExtractUseSevenZipLocal(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            var ret = true;
            var mv = extract.MoveInfoList.LastOrDefault();

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start. Name:{mv.FullPath}"), LoggerType.Debug, param: ParamInfo);

            if (!File.Exists(Global.ConfigJson.SevenZipConsolePath)
                    || VersionHelper.CompareVersions(Global.ConfigJson.SevenZipConsoleVersion, ConfigJson.SEVENZIP_INCLUDE_PRODUCT_VERSION.ToString()) != VersionHelper.Result.SAME)
            {
                ret = false;
                mv.Result = ExtractInfo.EXTRACT_RESULT.NOT_FOUND_SEVENZIP;
                return ret;
            }
            extract.UseExtractComponentExtract = $"{SEVENZIP_CONSOLE_EXE_LOCAL_NAME}({Global.ConfigJson.SevenZipConsoleVersion})";

            var extractCommand = string.Empty;
            extractCommand = $"x -y -bsp1 \"{mv.FullPath}\" -o\"{mv.FullPathResult}\"";

            Logger.WriteLine(string.Join(" ", MeInfo, $"extractCommand : {extractCommand}"), LoggerType.Debug, param: ParamInfo);

            try
            {
                Logger.WriteLine(string.Join(" ", MeInfo, $"Extracting '{Path.GetFileName(mv.FullPath)}'..."), LoggerType.Debug, param: ParamInfo);
                Logger.WriteLine(string.Join(" ", MeInfo, $"Running {SEVENZIP_CONSOLE_EXE_LOCAL_NAME}", $"extractCommand:{extractCommand}"), LoggerType.Debug, param: ParamInfo);

                using CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
                CancellationToken cancelToken = cancelTokenSource.Token;

                var fileName = Global.ConfigJson.SevenZipConsolePath;
                var processInfo = new ProcessStartInfo()
                {
                    FileName = fileName,
                    WorkingDirectory = Global.assemblyLocation,
                    UseShellExecute = false,
                    Arguments = extractCommand,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var extractSevenZipProcess = new Process
                {
                    StartInfo = processInfo,
                };

                try
                {
                    if (!Directory.Exists(mv.FullPathResult))
                        Directory.CreateDirectory(mv.FullPathResult);

                    Logger.WriteLine(string.Join(" ", $"Extract Start.", $"(File:{new FileInfo(mv.FullPath).Name})", $"(using {SEVENZIP_NAME}({Global.ConfigJson.SevenZipConsoleVersion}))"), LoggerType.Info);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Extract by 7-Zip Start...", $"ExtractFiles: {mv.FileAndDirectoryCount}"), LoggerType.Debug, param: ParamInfo);

                    extractSevenZipProcess.EnableRaisingEvents = true;
                    extractSevenZipProcess.Exited += new EventHandler(ExtractComplete);

                    // 7z.exe実行(Extract)
                    extractSevenZipProcess.Start();
                    Thread.Sleep(500);
                    extractSevenZipProcess.WaitForExit();

                    // Wine実行環境などで反映が遅れる場合があるため、少し待つ
                    // 7z.exe側の問題の可能性が高いので、ログ出力以外不要かも
                    Logger.WriteLine(string.Join(" ", MeInfo, $"7z ExitCode:{extractSevenZipProcess.ExitCode}"), LoggerType.Debug, param: ParamInfo);

                    if (extractSevenZipProcess.ExitCode != 0)
                    {
                        Logger.WriteLine(string.Join(" ", MeInfo, $"7z ErrorMessage:{extractSevenZipProcess.StandardError.ReadToEnd()}"), LoggerType.Debug, param: ParamInfo);
                    }

                    for (int i = 0; i < 20; i++)
                    {
                        Thread.Sleep(1000);

                        if (mv.FullPathResult.Any())
                            break;

                        Logger.WriteLine(string.Join(" ", MeInfo, $"i:{i}, mv.FullPathResult:{mv.FullPathResult}"), LoggerType.Debug, param: ParamInfo);
                        mv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);

                        if (mv.FullPathResultInfo.Any())
                            break;
                    }

                    //mv.FileAndDirectoryCountResult = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    mv.FullPathResultInfo = FileHelper.GetFilesAndDirectoriesCount(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    //mv.FileAndDirectoryCountResult = extract.FileAndDirectoryCountResult.Count();
                    //mv.DirectorySizeResult = FileHelper.GetDirectorySize(mv.FullPathResult, extract.SkipFileWhenSizeCheckPathList);
                    var m = mv.FullPathResultInfo.OfType<FileInfo>().ToList().Sum(x => x.Length);

                    if (!mv.CheckFilesCountAndSize())
                    {
                        ret = false;
                        mv.Result = ExtractInfo.EXTRACT_RESULT.SIZE_UNMATCH;
                        Logger.WriteLine(string.Join(" ", MeInfo, $"The number of files after extraction does not match. File extraction may have failed.", $"ExtractFiles: {mv.FullPathInfo.Count()}, DestFilesCount: {mv.FullPathResultInfo.Count()}"), LoggerType.Warning, param: ParamInfo);
                        return ret;
                    }

                    ret = true;
                    mv.Result = ExtractInfo.EXTRACT_RESULT.EXTRACT_SUCCESS;
                    Logger.WriteLine(string.Join(" ", $"Extract End.", $"(File:{new FileInfo(mv.FullPath).Name})", $"(using {SEVENZIP_NAME}({Global.ConfigJson.SevenZipConsoleVersion}))"), LoggerType.Info);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Extract by 7-Zip Complete!", $"ExtractFiles:{mv.FileAndDirectoryCount}"), LoggerType.Debug, param: ParamInfo);
                }
                // タスクがキャンセルされた場合の処理
                catch (OperationCanceledException)
                {
                    ret = false;
                    Logger.WriteLine(string.Join(" ", MeInfo, $"7z Extract Canceled!"), LoggerType.Debug, param: ParamInfo);
                    mv.Result = ExtractInfo.EXTRACT_RESULT.CANCELED;
                }
                finally
                {
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Finally SevenZip Process Kill Start.", $"{SEVENZIP_CONSOLE_EXE_LOCAL_NAME} extractCommand:{extractCommand}"), LoggerType.Debug, param: ParamInfo);
                    KillChildProcess(extractSevenZipProcess);
                    Logger.WriteLine(string.Join(" ", MeInfo, $"Finally SevenZip Process Kill End.", $"{SEVENZIP_CONSOLE_EXE_LOCAL_NAME} extractCommand:{extractCommand}"), LoggerType.Debug, param: ParamInfo);
                }
            }
            catch (IOException ex)
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", MeInfo, $"IO error extracting {mv.FullPath}", $"ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
                mv.Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
            }
            catch (Exception ex)
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", MeInfo, $"Error extracting {mv.FullPath}", $"ex.Message:{ex.Message}"), LoggerType.Error, param: ParamInfo);
                mv.Result = ExtractInfo.EXTRACT_RESULT.EXCEPTION;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Name:{mv.FullPath}, Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static bool DeleteTemporaryDirectory(ExtractInfo extract, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = true;
            var tempPath = string.Empty;

            try
            {
                tempPath = extract.MoveInfoList.Where(x =>
                    x.Status == ExtractInfo.EXTRACT_STATUS.ARCHIVE_EXTRACT
                    || x.Status == ExtractInfo.EXTRACT_STATUS.DIRECTORY_DROP)
                        .FirstOrDefault().FullPathResult;

                if (FileHelper.DeleteDirectory(tempPath))
                    Logger.WriteLine(string.Join(" ", MeInfo, $"{MeInfo} Complete! Path:{tempPath}"), LoggerType.Debug, param: ParamInfo);
                else
                    Logger.WriteLine(string.Join(" ", MeInfo, $"{MeInfo} Failed! Path:{tempPath}"), LoggerType.Debug, param: ParamInfo);
            }
            catch (Exception ex)
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", MeInfo, $"Exception.", $"ex.Message:{ex.Message}", $"ex.StackTrace:{ex.StackTrace}"), LoggerType.Error, param: ParamInfo);
            }
            Logger.WriteLine($"{MeInfo} End. Path:{tempPath}, Return:{ret}", LoggerType.Debug);
            return ret;
        }

        /// <summary>
        /// パス検査
        /// 親ディレクトリにJunction/Symlinkがないか(ReparsePoint 対策)
        /// </summary>
        /// <param name="fullDest"></param>
        /// <param name="fullRoot"></param>
        /// <returns></returns>
        private static EXTRACT_RESULT IsDangerousPath(string fullDest, string fullRoot)
        {
            // 正規化
            fullDest = Path.GetFullPath(fullDest);
            fullRoot = Path.GetFullPath(fullRoot);

            // ZipSlip
            if (!FileHelper.PathStartsWith(fullDest, fullRoot))
                return EXTRACT_RESULT.DANGEROUS_FILE;

            // 長いパス拒否
            // MAX_PATH を少し余裕持って制限
            var maxPath = OperatingSystem.IsWindows() ? PATH_LENGTH_LIMIT_WINDOWS : PATH_LENGTH_LIMIT_LINUX;
            if (fullDest.Length >= maxPath)
                return EXTRACT_RESULT.PATH_LENGTH_OVER_LIMIT;

            // UNC拒否 (Windowsのみ)
            if (OperatingSystem.IsWindows() && fullDest.StartsWith(@"\\"))
                return EXTRACT_RESULT.DANGEROUS_FILE;

            // NTプレフィックス拒否 (Windowsのみ)
            if (OperatingSystem.IsWindows() && fullDest.StartsWith(@"\\?\"))
                return EXTRACT_RESULT.DANGEROUS_FILE;

            return EXTRACT_RESULT.NONE;
        }

        /// <summary>
        /// 親ディレクトリ検査
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool ContainsReparsePoint(string path, string stopPath)
        {
            var current = new DirectoryInfo(path);
            stopPath = Path.GetFullPath(stopPath).TrimEnd(Path.DirectorySeparatorChar);

            while (current != null)
            {
                var currentPath = current.FullName.TrimEnd(Path.DirectorySeparatorChar);

                // stopPathより上は見ない
                if (string.Equals(currentPath, stopPath, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (current.Exists)
                {
                    var attributes = current.Attributes;

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }

                current = current.Parent;
            }

            return false;
        }
    }
}

