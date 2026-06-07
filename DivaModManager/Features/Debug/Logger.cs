using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace DivaModManager.Features.Debug
{
    public static partial class Logger
    {
        private static string LogPath = Global.textLogLocation;

        public enum DEBUG_MODE
        {
            NORMAL = 0,
            DEBUG,
            DEVELOPER,
        }

        public static DEBUG_MODE Mode = DEBUG_MODE.NORMAL;

        public static void Init(StartupEventArgs e)
        {
            // ワンクリックインストールから動いた場合、MainWindow側がログを上書きしてしまうのでファイル名を変更
            if (e.Args.ToList().Contains("-download"))
            {
                LogPath = Global.textLogBackgroundLocation;
            }
#if DEBUG
            Mode = DEBUG_MODE.DEVELOPER;
#else
            if (e.Args.ToList().Contains("-deb") || e.Args.ToList().Contains("-debug")) Mode = DEBUG_MODE.DEBUG;
            else if (e.Args.ToList().Contains("-dev") || e.Args.ToList().Contains("-developer")) Mode = DEBUG_MODE.DEVELOPER;
#endif
        }

        public static string SetLastStartUpModeRegistry()
        {
            var ret = string.Empty;
            if (Mode == DEBUG_MODE.DEBUG)
            {
                ret = " -debug";
            }
            else if (Mode == DEBUG_MODE.DEVELOPER)
            {
                ret = " -developer";
            }
            return ret;
        }

        public static List<string> MaskAddDropFilePathList { get; set; } = new();

        //private static int LogStackMaxSize = 1073741824;     // 1GB
        private static int LogStackMaxSize = 100 * 1000 * 1000;     // 100MB
        private static string LastCaller { get; set; } = string.Empty;

        private static StringBuilder _TextLog = new(LogStackMaxSize);
        private static string TextLog
        {
            get { return MaskTextLog(_TextLog.ToString()); }
            set
            {
                _TextLog.Append(value + Environment.NewLine);

                if (Global.IsMainWindowLoaded)
                {
                    WriteOut();
                    _TextLog = new(LogStackMaxSize);
                }
            }
        }

        /// <summary>
        /// テキストログ出力
        /// </summary>
        /// <param name="text"></param>
        /// <param name="type"></param>
        /// <param name="param"></param>
        /// <param name="caller"></param>
        public static void WriteLine(string text, LoggerType type, string param = "", string dump = "", [CallerMemberName] string caller = "")
        {
            LastCaller = caller;
            string header = $"[{type.ToString().ToUpper()}]";

            var outputTextValue = string.Empty;
            var now = DateTime.Now;
            var nowStr = $"[{now.ToString()}.{now.Millisecond.ToString("D3")}]";
            var paramStr = string.IsNullOrEmpty(param) ? string.Empty : $"[{param}]";
            var dumpStr = string.IsNullOrEmpty(dump) ? string.Empty : $"[Dump]\n{dump}\n";

            if (Mode == DEBUG_MODE.NORMAL)
            {
                outputTextValue += string.Join(" ", nowStr, header, text);
            }
            else if (Mode == DEBUG_MODE.DEBUG)
            {
                outputTextValue += string.Join(" ", nowStr, header, text, paramStr);
            }
            else if (Mode == DEBUG_MODE.DEVELOPER)
            {
                outputTextValue += string.Join(" ", nowStr, header, text, paramStr) + dumpStr;
            }

            TextLog = outputTextValue;
            Global.WindowLogger?.WriteLine(text, type, param, caller);
        }

        /// <summary>
        /// ログの個人情報をマスクする
        /// ＊DIVA MM+(DML)の配置フォルダ
        /// ＊DMMの配置フォルダ
        /// ＊WinRARの配置フォルダ
        /// ＊7-Zipの配置フォルダ
        /// ＊Dropした圧縮ファイルパス(またはディレクトリ)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string MaskTextLog(string text)
        {
            var ret = string.Empty;
            if (Global.ConfigToml != null)
            {
                ret = text;
                if (Global.ConfigToml.MaskTextLog)
                {
                    foreach (var MaskAddDropFilePath in MaskAddDropFilePathList)
                    {
                        ret = ret.Replace(MaskAddDropFilePath, "(MaskAddDropFilePath)");
                    }
                    if (Directory.Exists(Path.GetDirectoryName(Global.assemblyLocation)))
                        ret = ret.Replace(Path.GetDirectoryName(Global.assemblyLocation), "(Global.assemblyLocationDir)");
                    if (Directory.Exists(Path.GetDirectoryName(Global.ConfigJson.CurrentConfig.Launcher)))
                        ret = ret.Replace(Path.GetDirectoryName(Global.ConfigJson.CurrentConfig.Launcher), "(Global.configJson.CurrentConfig.LauncherDir)");
                    if (Directory.Exists(Path.GetDirectoryName(Global.ConfigJson.WinRarConsolePath)))
                        ret = ret.Replace(Path.GetDirectoryName(Global.ConfigJson.WinRarConsolePath), "(Global.configJson.WinRarConsolePathDir)");
                    if (Directory.Exists(Path.GetDirectoryName(Global.ConfigJson.SevenZipConsolePath)))
                        ret = ret.Replace(Path.GetDirectoryName(Global.ConfigJson.SevenZipConsolePath), "(Global.configJson.SevenZipConsolePathDir)");
                }
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text">出力テキスト</param>
        public static void WriteOut()
        {
            if (string.IsNullOrWhiteSpace(_TextLog.ToString()))
                return;
            FileHelper.AppendAllText(LogPath, TextLog.ToString(), appendInfo: LastCaller);
            _TextLog = new(LogStackMaxSize);
        }

        public static void OpenEditor()
        {
            if (!Global.IsWine && (Mode == DEBUG_MODE.DEBUG || Mode == DEBUG_MODE.DEVELOPER))
            {
                ProcessHelper.TryStartProcess(LogPath, workingDirectory: Global.assemblyLocation);
                Logger.WriteLine($"TryStartProcess LogPath : {LogPath}", LoggerType.Debug);
            }
        }

        // "DivaModManager.Common.MessageWindow.MainWindow+<MainWindow_Loaded>d__20"で最後の"."以降
        public static Regex GetClassNameRegex1 = new Regex(@"(?<=(\.))([^\.]*)$", RegexOptions.Compiled);
        // "MainWindow+<MainWindow_Loaded>d__20"の"+"までと"<>"で囲まれた部分
        public static Regex GetClassNameRegex2 = new Regex(@"(.*)?\+\+?<(.*)?>.*$", RegexOptions.Compiled);
        // "DivaModManager.Common.Config.InitConfigで最後から2つ目の"."以降
        public static Regex GetClassNameRegex3 = new Regex(@"([^\.]*\.[^\.]*)$", RegexOptions.Compiled);
        // 
        public static Regex GetClassNameRegex4 = new Regex(@"\+<.+>.+__\d+", RegexOptions.Compiled);

        private static string _GetClassAndMethodName(StackFrame stackFrame)
        {
            var className = stackFrame.GetMethod().DeclaringType.FullName;
            var methodName = stackFrame.GetMethod().Name;
            if (GetClassNameRegex4.IsMatch(stackFrame.GetMethod().DeclaringType.FullName))
            {
                var match1_Groups = GetClassNameRegex1.Match(className).Groups;
                if (match1_Groups.Count >= 1)
                {
                    var match1 = match1_Groups[match1_Groups.Count - 1].Value;
                    var match2_Groups = GetClassNameRegex2.Match(match1).Groups;
                    if (match2_Groups.Count >= 2)
                    {
                        var match2_1 = match2_Groups[match2_Groups.Count - 2].Value;
                        var match2_2 = match2_Groups[match2_Groups.Count - 1].Value;
                        var match2_2_replace = ConvertMethodName(match2_2);
                        if (!string.IsNullOrEmpty(match2_1) && !string.IsNullOrEmpty(match2_2_replace))
                        {
                            className = string.Join(".", match2_1, match2_2_replace);
                        }
                        else
                        {
                            className = match1;
                        }
                    }
                }
            }
            else
            {
                var match3_Groups = GetClassNameRegex3.Match(className).Groups;
                if (match3_Groups.Count >= 1)
                {
                    className = match3_Groups[0].Value;
                }
            }

            var methodNaneReplace = ConvertMethodName(methodName);
            var retArrays = string.IsNullOrEmpty(methodNaneReplace) ? className.Split(".") : string.Join(".", className, methodNaneReplace).Split(".");
            return string.Join(".", retArrays[retArrays.Length - 2], retArrays[retArrays.Length - 1]);
        }

        private static string ConvertMethodName(string method)
        {
            return method switch
            {
                "MoveNext" => null,
                ".ctor" => "Constructor",
                _ => method
            };
        }

        /// <summary>
        /// StackFrameからクラス名とメソッド名を取得する
        /// todo: Javaではクラスメソッドは"#"、インスタンスメソッドは"."で繋げる(そのうち直す)
        /// </summary>
        /// <param name="stackFrame"></param>
        /// <param name="methodBase"></param>
        /// <returns></returns>
        public static string GetMeInfo(StackFrame stackFrame)
        {
            var ret = _GetClassAndMethodName(stackFrame);
            return ret;
        }
    }
}