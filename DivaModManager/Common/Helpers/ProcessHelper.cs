using DivaModManager.Features.Debug;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace DivaModManager.Common.Helpers
{
    internal class ProcessHelper
    {
        /// <summary>
        /// 指定されたターゲット（URLまたはファイル/フォルダパス）を外部プロセスで開く
        /// </summary>
        /// <param name="target">開くURLまたはパス</param>
        /// <returns>プロセスが正常に開始された場合は true、それ以外は false</returns>
        public static bool TryStartProcess(string target, string fileName = null)
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
                var psi = new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    Verb = "open",
                    Arguments = fileName,
                };

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
    }
}
