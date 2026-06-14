using DivaModManager.Features.Debug;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace DivaModManager.Common.Helpers
{
    internal class ProcessHelper
    {
        /// <summary>
        /// 指定されたターゲット（URLまたはファイル/フォルダパス）を外部プロセスで開く
        /// </summary>
        /// <param name="target">開くURLまたはパス</param>
        /// <returns>プロセスが正常に開始された場合は true、それ以外は false</returns>
        public static bool TryStartProcess(string target, string fileName = null, string workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                Logger.WriteLine($"Target for Process.Start is empty or null.", LoggerType.Warning);
                return false;
            }

            try
            {
                // UseShellExecute = true を使うと、関連付けられたアプリケーションで開く（URLやフォルダなど）
                // UseShellExecute = false は直接実行ファイルを実行する場合に使うことが多い
                ProcessStartInfo psi;
                psi = new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    Verb = "open",
                    Arguments = fileName,
                    WorkingDirectory = workingDirectory
                };

                //Thread.Sleep(500);
                Process.Start(psi);
                Logger.WriteLine($"Successfully started process for target: '{target}'.", LoggerType.Debug);
                return true;
            }
            catch (Win32Exception ex)
            {
                Logger.WriteLine($"Error starting process for '{target}': {ex.Message} (ErrorCode: {ex.ErrorCode})", LoggerType.Error);
                return false;
            }
            catch (FileNotFoundException ex)
            {
                Logger.WriteLine($"File not found for process start '{target}': {ex.Message}", LoggerType.Error);
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Unexpected error starting process for '{target}': {ex}", LoggerType.Error);
                return false;
            }
        }


        /// <summary>
        /// 指定されたターゲット（URL、Unix パス、Windows スタイルパス）を外部プロセスで開く。
        /// Wine 上でも Linux のファイルマネージャーが起動するよう変換してから実行。
        /// </summary>
        //public static bool TryStartProcess(string target, string fileName = null, string workingDirectory = null)
        //{
        //    if (string.IsNullOrWhiteSpace(target))
        //    {
        //        Logger.WriteLine($"Target for Process.Start is empty or null.", LoggerType.Warning);
        //        return false;
        //    }

        //    try
        //        {
        //        // Windows → Unix パスを変換
        //        string targetForLinux = target;

        //        //if (Global.IsWine)
        //        //    targetForLinux = ConvertWindowsPathToUnix(target);

        //        ProcessStartInfo psi;

        //        if (Global.IsWine)
        //        {
        //            // Linux 上でファイルマネージャーを起動
        //            psi = new ProcessStartInfo("xdg-open", EscapeArgument(targetForLinux))
        //            {
        //                UseShellExecute = false,
        //                CreateNoWindow = true,
        //                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory   // "Environment.CurrentDirectory"では予期しないディレクトリになる場合があるため、コメント解除時は注意
        //            };
        //        }
        //        else
        //        {
        //            // Windows
        //            psi = new ProcessStartInfo(target)
        //            {
        //                UseShellExecute = true,
        //                Verb = "open",
        //                Arguments = fileName,
        //                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory   // "Environment.CurrentDirectory"では予期しないディレクトリになる場合があるため、コメント解除時は注意
        //            };
        //        }

        //        Process.Start(psi);
        //        Logger.WriteLine($"Successfully started process for target: '{target}'.", LoggerType.Debug);
        //        return true;
        //    }
        //    catch (Win32Exception ex)
        //    {
        //        Logger.WriteLine($"Error starting process for '{target}': {ex.Message} (ErrorCode: {ex.ErrorCode})", LoggerType.Error);
        //        return false;
        //    }
        //    catch (FileNotFoundException ex)
        //    {
        //        Logger.WriteLine($"File not found for process start '{target}': {ex.Message}", LoggerType.Error);
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.WriteLine($"Unexpected error starting process for '{target}': {ex}", LoggerType.Error);
        //        return false;
        //    }
        //}

        /// <summary>
        /// Windows パス（例：Z:\フォルダ\file.txt）を Unix パスに変換
        /// 変換できない／Wine が無い場合は元のパスを返す。
        /// </summary>
        private static string ConvertWindowsPathToUnix(string winPath)
        {
            // 既に Unix スタイルならそのまま返す
            if (winPath.StartsWith("/") || winPath.StartsWith("file:"))
                return winPath;

            // Win32 パスかどうかを簡易判定（ドライブレター + :\）
            var m = Regex.Match(winPath, @"^([a-zA-Z]):\\(.*)");
            if (!m.Success)
                return winPath;   // 変換対象外

            try
            {
                var psi = new ProcessStartInfo("winepath",
                                                $"-u \"{winPath}\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Global.assemblyLocation
                };

                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    // winepath が空文字を返すときがあるのでチェック
                    return !string.IsNullOrEmpty(output) ? output : winPath;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"winepath failed: {ex.Message}", LoggerType.Error);
                return winPath;   // 失敗したらオリジナルを返す
            }
        }

        /// <summary>
        /// コマンドライン引数として安全に渡すためのエスケープ（空白・特殊文字含む）
        /// </summary>
        private static string EscapeArgument(string arg)
        {
            // 既にクオートされている場合はそのまま返す
            if (arg.StartsWith("\"") && arg.EndsWith("\""))
                return arg;

            return $"\"{arg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'")}\"";
        }
    }
}
