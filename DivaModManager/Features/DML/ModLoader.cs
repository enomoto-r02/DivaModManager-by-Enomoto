using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Features.DML
{
    public static class ModLoader
    {
        public static readonly string CONFIG_TOML_NAME = "config.toml";
        public static string CONFIG_TOML_PATH
        {
            get { return $"{Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder)}{Global.s}config.toml"; }
        }

        /// <summary>
        /// DML読み込み用のconfig.tomlを更新
        /// </summary>
        /// <param name="callerMethodName"></param>
        /// <returns></returns>
        public static async Task<bool> BuildAsync([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            if (!File.Exists(CONFIG_TOML_PATH))
            {
                Logger.WriteLine($"BuildAsync Unable to find {CONFIG_TOML_PATH}", LoggerType.Error);
                Logger.WriteLine($"BuildAsync Unable to find {CONFIG_TOML_PATH} id:{Environment.CurrentManagedThreadId}", LoggerType.Debug);
                return false;
            }
            var configString = string.Empty;
            var retryCnt = 0;
            var retryCntOver = false;
            while (string.IsNullOrEmpty(configString) || !retryCntOver)
            {
                try
                {
                    configString = File.ReadAllText(CONFIG_TOML_PATH);
                    break;
                }
                catch (Exception e)
                {
                    // Check if the exception is related to an IO error.
                    if (e.GetType() != typeof(IOException))
                    {
                        Logger.WriteLine($"Couldn't access {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Error);
                        break;
                    }
                    await Task.Delay(500);
                    retryCnt++;
                    retryCntOver = 5 >= retryCnt;
                }
            }
            if (retryCntOver)
            {
                var paramStr = $"caller:{caller} retryCntOver:{retryCntOver} id:{Environment.CurrentManagedThreadId}";
                Logger.WriteLine($"Couldn't read {CONFIG_TOML_PATH}", LoggerType.Error, param: paramStr);
                return false;
            }
            if (!Toml.TryToModel(configString, out TomlTable config, out var diagnostics))
            {
                if (File.Exists(CONFIG_TOML_PATH))
                {
                    Logger.WriteLine($"Unexpected error loading {CONFIG_TOML_NAME}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(CONFIG_TOML_PATH, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_TOML_NAME} and generate a new one. Save file name:{new FileInfo(backupFilePath).Name}", LoggerType.Error);
                }
                // Create a new config if it failed to parse
                config = new()
                {
                    { "enabled", true },
                    { "console", false },
                    { "mods", "mods" },
                };
            }
            var priorityList = new List<string>();
            foreach (var mod in Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout].Where(x => x.enabled).ToList())
                priorityList.Add(mod.name);
            config["priority"] = priorityList.ToArray();
            var isReady = false;
            retryCnt = 0;
            retryCntOver = false;
            while (!isReady || !retryCntOver)
            {
                try
                {
                    File.WriteAllText(CONFIG_TOML_PATH, Toml.FromModel(config));
                    isReady = true;
                }
                catch (Exception e)
                {
                    // Check if the exception is related to an IO error.
                    if (e.GetType() != typeof(IOException))
                    {
                        var paramStr = $"caller:{caller} retryCntOver:{retryCntOver} id:{Environment.CurrentManagedThreadId}";
                        Logger.WriteLine($"Couldn't access {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Error, param: paramStr);
                        break;
                    }
                }
                await Task.Delay(500);
                retryCnt++;
                retryCntOver = 5 >= retryCnt;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Result:{isReady}"), LoggerType.Debug, param: ParamInfo);
            return isReady;
        }
    }
}
