using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DivaModManager.Common.Config
{
    public static class RegistryConfig
    {
        public static void CheckGBHandler()
        {
            if (!OperatingSystem.IsWindows()) { return; }
            using var isRegist = Registry.CurrentUser.OpenSubKey(@"Software\Classes\DivaModManager");
            var flg = isRegist == null;
            isRegist?.Close();
            if (flg)
            {
                InstallGBHandler();
            }
            else
            {
                UnInstallGBHandler();
            }
        }

        private static void InstallGBHandler([CallerMemberName] string caller = "")
        {
            if (!OperatingSystem.IsWindows()) { return; }

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            string AppPath = $"{Global.assemblyLocation}{Global.s}DivaModManager.exe";
            string protocolName = $"divamodmanager";
            using var isRegist = Registry.CurrentUser.OpenSubKey(@"Software\Classes\DivaModManager");
            var flg = isRegist == null;
            isRegist?.Close();
            if (flg)
            {
                var ret = WindowHelper.DMMWindowOpenAsync(38).Result;
                if (ret == WindowHelper.WindowCloseStatus.Yes)
                {
                    var reg = Registry.CurrentUser.CreateSubKey(@"Software\Classes\DivaModManager");
                    reg.SetValue("", $"URL:{protocolName}");
                    reg.SetValue("URL Protocol", "");
                    reg = reg.CreateSubKey(@"shell\open\command");
                    reg.SetValue("", $"\"{AppPath}\" -download \"%1\"{Logger.SetLastStartUpModeRegistry()}");
                    reg.Close();
                    var ret2 = WindowHelper.DMMWindowOpenAsync(52).Result;
                    Logger.WriteLine($"Registration to the registry is complete. RegistoryPath:\"HKEY_CURRENT_USER\\Software\\Classes\\DivaModManage\"", LoggerType.Info);
                }
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        private static void UnInstallGBHandler()
        {
            if (!OperatingSystem.IsWindows()) { return; }
            using var isRegist = Registry.CurrentUser.OpenSubKey(@"Software\Classes\DivaModManager");
            var flg = isRegist != null;
            isRegist?.Close();
            if (flg)
            {
                var ret = WindowHelper.DMMWindowOpenAsync(51).Result;
                if (ret == WindowHelper.WindowCloseStatus.Yes)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\DivaModManager");
                    Logger.WriteLine($"Registry deletion is complete. RegistoryPath:\"HKEY_CURRENT_USER\\Software\\Classes\\DivaModManage\"", LoggerType.Info);
                    var ret2 = WindowHelper.DMMWindowOpenAsync(52).Result;
                }
            }
        }

        /// <summary>
        /// レジストリのパスを更新する
        /// ＊別のパスのDMMを後から起動した場合を考慮し、レジストリが存在した場合は強制上書き
        /// </summary>
        public static void UpdateGBHandler()
        {
            if (!OperatingSystem.IsWindows()) { return; }
            string AppPath = $"{Global.assemblyLocation}{Global.s}DivaModManager.exe";
            string protocolName = $"divamodmanager";
            using var isRegist = Registry.CurrentUser.OpenSubKey(@"Software\Classes\DivaModManager");
            var flg = isRegist != null;
            if (flg)
            {
                var reg = Registry.CurrentUser.CreateSubKey(@"Software\Classes\DivaModManager");
                reg.SetValue("", $"URL:{protocolName}");
                reg.SetValue("URL Protocol", "");
                reg = reg.CreateSubKey(@"shell\open\command");
                reg.SetValue("", $"\"{AppPath}\" -download \"%1\"{Logger.SetLastStartUpModeRegistry()}");
                reg.Close();
            }
            isRegist?.Close();
        }
    }
}
