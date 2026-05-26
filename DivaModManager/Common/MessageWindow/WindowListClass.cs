using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using Tomlyn;

namespace DivaModManager.Common.MessageWindow
{
    public static class WindowListClass
    {
        public static string WINDOW_LIST_NAME { get; set; } = "Message.toml";
        public static string WINDOW_LIST_PATH = $"{Global.assemblyLocation}{WINDOW_LIST_NAME}";
        public static string WINDOW_LIST_RESOURCE_PATH = $"DivaModManager.Resources.Message.toml";

        public static Dictionary<string, string> WINDOW_LIST_NO_58 = new()
        {
            { "https://gamebanana.com/mods/434457", DMLUpdater.MODULE_NAME },   // Eden Module ID Patch
            { "https://gamebanana.com/mods/432449", DMLUpdater.MODULE_NAME },   // Eden Song ID Patch
            { "https://gamebanana.com/mods/397695", DMLUpdater.MODULE_NAME },   // Eden Song Limit Patch
            { "https://gamebanana.com/mods/427167", "Eden Core" },              // Eden Project LITE Version
            { "https://gamebanana.com/mods/438186", Global.GAME_NAME },         // Future Tone Customization - Dynamic Version
        };
        public static List<string> WINDOW_LIST_NO_59 = new()
        {
            "https://gamebanana.com/mods/405848",       // Eden Project [v5.6]
            "https://gamebanana.com/mods/397695",       // Eden Song Limit Patch
        };


        public static Dictionary<string, WindowInfo> WindowList { get; set; } = new();

        public static bool InitWindowList([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            Dictionary<string, WindowInfo> windowListToml = new();
            bool ret = false;

            try
            {
                var content = string.Empty;
                ret = LoadWindowMessageTomlFromResource();
            }
            catch (Exception ex)
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", $"Initialization was performed because {WINDOW_LIST_PATH} could not be read.", $"ex.Message:{ex.Message}", $"ex.StackTrace:{ex.StackTrace}"), LoggerType.Error, param: ParamInfo);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"WindowList.Count:{WindowList.Count()} Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        public static bool LoadWindowMessageTomlFromResource([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            var assenbly = Assembly.GetExecutingAssembly();
            using Stream stream = assenbly.GetManifestResourceStream(WINDOW_LIST_RESOURCE_PATH);
            if (stream != null)
            {
                using StreamReader reader = new(stream);
                var content = reader.ReadToEnd();

                WindowList = Toml.ToModel<Dictionary<string, WindowInfo>>(content);
                ret = true;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        // "none"かreplaceが指定されていない{0}はブランクとして扱う
        public static Regex regex = new(@"^(none|\{\d+\}$)");

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        public static string ReplaceMessage(string message, List<string> replace)
        {
            var text = message;
            if (replace == null || replace.Count == 0)
            {
                text = regex.Replace(text, "");
                return text;
            }

            int numMax = 0;
            var matches = Regex.Matches(text, "{\\d+}");

            var numList = new List<int>();
            foreach (var match in matches)
            {
                numList.Add(int.Parse(match.ToString().Replace("{", "").Replace("}", "")));
            }
            if (numList.Count > 0)
                numMax = numList.ToList().Max(x => x) + 1;
            else
                return message;
            for (var n = 0; n < numMax; n++)
            {
                var rep = "{" + n + "}";
                text = text.Replace(rep, replace[n]);
            }
            return text;
        }

        #region ダミーデータ
        public static string WindowListDummyData()
        {
            Dictionary<string, WindowInfo> dataList = new();
            string key = 1.ToString("MESSAGE-0000");
            var data = new WindowInfo()
            {
                ID = 1,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Config.tomlがない",
                Context_EN = "No Config.toml",
                WindowTitle_JP = "No Config.toml_JP",
                WindowTitle_EN = "No Config.toml_EN",
            };
            dataList.Add(key, data);

            data = new()
            {
                ID = 2,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Context_jp",
                Context_EN = "Context_en",
                WindowTitle_JP = "Multi Config.toml",
                WindowTitle_EN = "Multi Config.toml",
            };
            key = 2.ToString("MESSAGE-0000");
            dataList.Add(key, data);

            data = new()
            {
                ID = 3,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Context_jp",
                Context_EN = "Context_en",
                WindowTitle_JP = "Nest Directory JP",
                WindowTitle_EN = "Nest Directory EN",
            };
            key = 3.ToString("MESSAGE-0000");
            dataList.Add(key, data);

            var ret = Toml.FromModel(dataList);

            return ret;
        }
        #endregion ダミーデータ

        public static WindowInfo MessageWindowNo(int ID)
        {
            var key = ID.ToString("WINDOW-0000");
            return WindowList[key].DeepCopy();    // windowListはstaticなので、コピーして別インスタンスにする
        }

        public static WindowInfo MessageWindowNo(int ID, List<string> replaceList)
        {
            var key = ID.ToString("WINDOW-0000");
            var info = WindowList[key].DeepCopy();    // windowListはstaticなので、コピーして別インスタンスにする
            info.replaceList = replaceList;

            return info;
        }

        public static bool Save()
        {
            var ret = false;
            try
            {
                string tomlString = Toml.FromModel(WindowList);
                ret = FileHelper.TryWriteAllText(WINDOW_LIST_PATH, tomlString);
            }
            catch (Exception ex)
            {
                string MeInfo = Logger.GetMeInfo(new StackFrame());
                Logger.WriteLine(string.Join(" ", $"Unexpected error loading {WINDOW_LIST_PATH}: {ex.Message}. Using default {WINDOW_LIST_PATH} in {AssemblyName.GetAssemblyName}."), LoggerType.Error);
            }
            return ret;
        }
    }
    public class WindowInfo
    {
        public enum TYPE
        {
            NONE = 0,
            INFORMATION,
            WARNING,
            ERROR,
            ATTENTION,
        }
        public enum MESSAGE_WINDOW
        {
            None = 0,
            OK,
            OKCheck,
            YesNo,
            YesNoCheck,
            YesNoOpen,
            Choice,
            Metadata,
            MessageBox,
        }
        [DataMember(Name = "id")]
        public int ID { get; set; }
        [IgnoreDataMember]
        private TYPE _Type { get; set; }
        [DataMember(Name = "type")]
        public TYPE Type
        {
            get { return _Type; }
            set { _Type = Enum.Parse<TYPE>(value.ToString().ToUpper().AsSpan(), true); }
        }
        [IgnoreDataMember]
        public List<string> replaceList { get; set; } = new();
        [DataMember(Name = "info_jp")]
        public string Info_JP { get; set; }
        [DataMember(Name = "info_en")]
        public string Info_EN { get; set; }
        public string Info()
        {
            if (Global.ConfigToml == null) return WindowListClass.ReplaceMessage(Info_EN, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return WindowListClass.ReplaceMessage(Info_JP, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return WindowListClass.ReplaceMessage(Info_EN, replaceList);
            else return string.Empty;
        }
        [DataMember(Name = "context_jp")]
        public string Context_JP { get; set; }
        [DataMember(Name = "context_en")]
        public string Context_EN { get; set; }
        public string Context()
        {
            if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString())
                return WindowListClass.ReplaceMessage(Context_JP, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString())
                return WindowListClass.ReplaceMessage(Context_EN, replaceList);
            else return string.Empty;
        }

        // MessageWindow
        [DataMember(Name = "window_type")]
        public string WindowType { get; set; }
        [DataMember(Name = "window_title_jp")]
        public string WindowTitle_JP { get; set; }
        [DataMember(Name = "window_title_en")]
        public string WindowTitle_EN { get; set; }
        public string WindowTitle()
        {
            if (Global.ConfigToml == null) return WindowListClass.ReplaceMessage(WindowTitle_EN, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return WindowListClass.ReplaceMessage(WindowTitle_JP, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return WindowListClass.ReplaceMessage(WindowTitle_EN, replaceList);
            else return string.Empty;
        }
        // button_1
        [DataMember(Name = "button_1_jp")]
        public string Button_1_JP { get; set; }
        [DataMember(Name = "button_1_en")]
        public string Button_1_EN { get; set; }
        public string Button_1()
        {
            if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return Button_1_JP;
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return Button_1_EN;
            else return string.Empty;
        }
        // button_2
        [DataMember(Name = "button_2_jp")]
        public string Button_2_JP { get; set; }
        [DataMember(Name = "button_2_en")]
        public string Button_2_EN { get; set; }
        public string Button_2()
        {
            if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return Button_2_JP;
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return Button_2_EN;
            else return string.Empty;
        }
        // button_3
        [DataMember(Name = "button_3_jp")]
        public string Button_3_JP { get; set; }
        [DataMember(Name = "button_3_en")]
        public string Button_3_EN { get; set; }
        public string Button_3()
        {
            if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return Button_3_JP;
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return Button_3_EN;
            else return string.Empty;
        }
        // check_1
        [DataMember(Name = "check_1_jp")]
        public string Check_1_JP { get; set; }
        [DataMember(Name = "check_1_en")]
        public string Check_1_EN { get; set; }
        public string Check_1()
        {
            if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString()) return WindowListClass.ReplaceMessage(Check_1_JP, replaceList);
            else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString()) return WindowListClass.ReplaceMessage(Check_1_EN, replaceList);
            else return string.Empty;
        }

        public string WindowId() { return $"WINDOW-{ID.ToString("0000")}"; }

        public override string ToString()
        {
            return $"[{WindowId()}]\n{Info()}\n{Context()}\n\n";
        }
    }
}
