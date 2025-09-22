using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using static DivaModManager.Debug;

namespace DivaModManager
{
    public partial class App : Application
    {
        private void App_Exit(object sender, EventArgs e)
        {
            if (Global.DEBUG_MODE == DEBUG_MODE.DEBUG || Global.DEBUG_MODE == DEBUG_MODE.DEVELOPER)
            {
                FileLogger.FileOpenAndWrite(TextLogger.Log);
                Global.TryStartProcess(Global.textLogLocation);
            }
        }
    }

    /// <summary>
    /// Debug
    /// </summary>
    public static class Debug
    {
        public enum DEBUG_MODE
        {
            NORMAL,
            DEBUG,
            DEVELOPER,
        }
    }

    /// <summary>
    /// for Debug
    /// </summary>
    /// 
    public static partial class Global
    {
        public static Debug.DEBUG_MODE DEBUG_MODE = DEBUG_MODE.NORMAL;
    }

    /// <summary>
    /// for Debug add parameter
    /// </summary>
    public static partial class TextLogger
    {
        private static string _Log = string.Empty;
        public static string Log
        {
            get { return _Log; }
            set { if (Global.DEBUG_MODE == DEBUG_MODE.DEBUG || Global.DEBUG_MODE == DEBUG_MODE.DEVELOPER) _Log = value; }
        }

        public static void WriteLine(string text, LoggerType type)
        {
            _Log += $"[{DateTime.Now}] [{type.ToString().ToUpper()}] {text}\n";
        }
    }

    /// <summary>
    /// for Debug add parameter
    /// </summary>
    public static class FileLogger
    {
        public static void FileOpenAndWrite(string text)
        {
            if (Global.DEBUG_MODE == DEBUG_MODE.DEBUG || Global.DEBUG_MODE == DEBUG_MODE.DEVELOPER)
            {
                // 前回のログは削除
                if (File.Exists(Global.textLogLocation))
                {
                    File.Delete(Global.textLogLocation);
                }
                using var fs = new FileStream(
                    Global.textLogLocation, FileMode.Create, FileAccess.Write, FileShare.Read);
                byte[] info = new UTF8Encoding(true).GetBytes(text);
                fs.Write(info, 0, info.Length);
            }
        }
    }

    public static class ObjectDumper
    {
        public static string Dump(object obj, [CallerMemberName] string callerMethodName = "")
        {
            StringBuilder sb = new();
            if (Global.DEBUG_MODE == DEBUG_MODE.DEVELOPER)
            {
                Type type = obj.GetType();
                var caller = new System.Diagnostics.StackFrame(1, false);
                string callerClassName = caller.GetMethod().DeclaringType.FullName;

                sb.AppendLine($"Called : {callerClassName}#{callerMethodName}");
                sb.AppendLine($"\tType : {type.Name}");
                sb.AppendLine($"\tParam :");

                DumpInternal(obj, callerMethodName, sb);
            }
            return sb.ToString();
        }

        private static void DumpInternal(object obj, string path, StringBuilder sb, string prefix = "  ")
        {
            if (obj == null)
            {
                sb.AppendLine($"{prefix}{path} : null");
                return;
            }

            Type type = obj.GetType();

            // string はそのまま出力
            if (type == typeof(string))
            {
                sb.AppendLine($"{prefix}{path} : \"{obj}\"");
                return;
            }

            // 値型はそのまま出力
            if (type.IsValueType)
            {
                sb.AppendLine($"{prefix}{path} : {obj}");
                return;
            }

            // Dictionary 特別扱い
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var dict = (IDictionary)obj;
                foreach (var key in dict.Keys)
                {
                    DumpInternal(dict[key]!, $"{prefix}{path}[\"{key}\"]", sb, prefix);
                }
                return;
            }

            // IEnumerable (配列や List など)
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                int index = 0;
                foreach (var item in (IEnumerable)obj)
                {
                    DumpInternal(item!, $"{prefix} {path}[{index}]", sb);
                    index++;
                }
                return;
            }

            // プロジェクト内で定義された型以外はスキップ
            if (type.Assembly != Assembly.GetExecutingAssembly())
            {
                sb.AppendLine($"{prefix}{path} : (skipped {type.FullName})");
                return;
            }

            // フィールドを展開（BackingField はスキップ）
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.Name.Contains("k__BackingField"))
                    continue;

                DumpInternal(field.GetValue(obj)!, $"{prefix}{path}.{field.Name}", sb);
            }

            // プロパティを展開（public / private 両方）
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue; // インデクサは無視
                if (!prop.CanRead) continue;

                object? value = null;
                try
                {
                    value = prop.GetValue(obj);
                }
                catch { /* getter で例外が出る場合は無視 */ }

                DumpInternal(value!, $"{prefix}{path}.{prop.Name}", sb);
            }
        }
    }
}