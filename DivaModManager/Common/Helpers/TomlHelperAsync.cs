using DivaModManager.Common.ExtendToml;
using DivaModManager.Features.Debug;
using DivaModManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Common.Helpers
{
    /// <summary>
    /// 書き直し中
    /// </summary>
    public static class TomlHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>読み込んだTomlTableまたはnull</returns>
        public static TomlTable TryReadToml(string path, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, path:{path}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            string content = string.Empty;

            if (new FileInfo(path).Name == Mod.CONFIG_E_TOML_NAME || !File.Exists(path))
            {
                return null;
            }
            else
            {
                content = FileHelper.TryReadAllText(path, eraseCommentLine: true);
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            try
            {
                // Tomlyn のパースは同期的だが、CPU負荷が高ければ Task.Run でラップ
                if (Toml.TryToModel(content, out TomlTable model, out var diagnostics))
                {
                    return model;
                }
                else
                {
                    // diagnostics をログに出力 (最初の１つ)
                    if (diagnostics != null && diagnostics.Count > 0)
                        Logger.WriteLine($"Failed to parse Toml file {path}: {diagnostics[0].Message}", LoggerType.Warning);
                    else
                        Logger.WriteLine($"Failed to parse Toml file {path} with unknown error.", LoggerType.Warning);
                    return null;
                }
            }
            catch (Exception ex)
            {
                ParamInfo += $"\nex.Message:{ex.Message}\nex.StackTrace:{ex.StackTrace}";
                Logger.WriteLine($"End. Error parsing Toml file {path}", LoggerType.Error, param: ParamInfo);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>読み込んだTomlTableまたはnull</returns>
        public static async Task<TomlTable> TryReadTomlAsync(string path)
        {
            TomlTable ret = null;

            // UIスレッドから呼ばれた場合はそのまま同期実行
            if (App.Current.Dispatcher.CheckAccess())
            {
                ret = TryReadToml(path);
            }
            else
            {
                // 非UIスレッドならDispatcher経由
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ret = TryReadToml(path);
                });
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static bool TryWriteToml(string path, TomlTable model)
        {
            var ret = false;
            try
            {
                // 注：Toml.FromModelでstringに変換できない場合は以下を見直すこと
                //   1. publicのgetter, setterはあるか確認
                //   2. ListやDictionaryのみの場合は変換できない。基本型(int, stringなど)のメンバが必須
                //   3. 必須かはわからないが、【[DataMember(Name = "sevenzip_use")]】の記載を確認
                // Tomlyn のモデルからの変換は同期的
                //string content = Toml.FromModel(model);
                //return FileHelper.TryWriteAllText(path, content);
                var tomlStr = TomlWithComments.SerializeWithComments(model);
                ret = FileHelper.TryWriteAllText(path, tomlStr);
            }
            catch (Exception ex) // Toml.FromModelが例外を投げる場合
            {
                Logger.WriteLine($"Error generating Toml content for {path}: {ex.Message}", LoggerType.Error);
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static async Task<bool> TryWriteTomlAsync(string path, TomlTable model)
        {
            var ret = true;

            if (App.Current.Dispatcher.CheckAccess())
            {
                ret = TryWriteToml(path, model);
            }
            else
            {
                // 非UIスレッドならDispatcher経由
                ret = await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    return TryWriteToml(path, model);
                });
            }

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        /// <returns>項目を追加した場合にtrue</returns>
        public static bool AddInclude(TomlTable config)
        {
            var ret = false;
            if (!config.ContainsKey("include"))
            {
                config.Add("include", new string[1] { "." });
                ret = true;

            }
            return ret;
        }

        public static async Task UpdateModConfigTomlAsync(Mod m)
        {
            var configPath = System.IO.Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, m.name, "config.toml");

            TomlTable config = await TomlHelper.TryReadTomlAsync(configPath);
            bool needsWrite = false;

            if (config != null)
            {
                if (!config.ContainsKey("enabled") || (bool)config["enabled"] != m.enabled)
                {
                    config["enabled"] = m.enabled;
                    needsWrite = true;
                }
                // include がなければ追加
                if (!config.ContainsKey("include"))
                {
                    TomlHelper.AddInclude(config);
                    needsWrite = true;
                }
            }

            if (needsWrite)
            {
                await TomlHelper.TryWriteTomlAsync(configPath, config);

            }
        }
    }
}
