using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DivaModManager.Features.Debug;

public static class ObjectDumper
{
    /// <summary>
    /// objのダンプを取得
    /// 注意：Developerモードの時のみ
    /// </summary>
    /// <param name="obj">Dump対象オブジェクト</param>
    /// <param name="instanceName">Dump対象オブジェクトのインスタンス名(取得できなさそうなため手動で)</param>
    /// <param name="callerMethodName">呼び出しメソッド名(自動で入る)</param>
    /// <returns></returns>
    public static string Dump(object obj, string instanceName, [CallerMemberName] string caller = "")
    {
        string MeInfo = Logger.GetMeInfo(new StackFrame());

        StringBuilder sb = new();
        sb.AppendLine(MeInfo);

        if (Logger.Mode == Logger.DEBUG_MODE.DEVELOPER)
        {
            var prefix = "  ";
            Type type = obj.GetType();
            var callerMethod = new System.Diagnostics.StackFrame(1, false);
            string callerClassName = callerMethod.GetMethod().DeclaringType.FullName;

            sb.AppendLine($"Called : {callerClassName}#{caller}");
            sb.AppendLine($"{prefix}Type : {type.Name}");
            sb.AppendLine($"{prefix}Param :");

            DumpInternal(obj, instanceName, sb, prefix);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="instanceName"></param>
    /// <param name="sb"></param>
    /// <param name="prefix"></param>
    private static void DumpInternal(object obj, string instanceName, StringBuilder sb, string prefix = "  ")
    {
        if (obj == null)
        {
#if DEBUG
#else
            sb.AppendLine($"{prefix}{instanceName} : null");
#endif
            return;
        }

        Type type = obj.GetType();

        // string はそのまま出力
        if (type == typeof(string))
        {
#if DEBUG
            if (!string.IsNullOrEmpty(obj.ToString())) { sb.AppendLine($"{prefix}{instanceName} : \"{obj}\""); }
#else
            sb.AppendLine($"{prefix}{instanceName} : \"{obj}\"");
#endif

            return;
        }

        // 値型はそのまま出力
        if (type.IsValueType)
        {
            sb.AppendLine($"{prefix}{instanceName} : {obj}");
            return;
        }

        // Dictionary 特別扱い
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            var dict = (IDictionary)obj;
            foreach (var key in dict.Keys)
            {
                DumpInternal(dict[key]!, $"{prefix}{instanceName}[\"{key}\"]", sb, prefix);
            }
            return;
        }

        // IEnumerable (配列や List など)
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            int index = 0;
            foreach (var item in (IEnumerable)obj)
            {
                DumpInternal(item!, $"{prefix} {instanceName}[{index}]", sb);
                index++;
            }
            return;
        }

        // プロジェクト内で定義された型以外はスキップ
        if (type.Assembly != Assembly.GetExecutingAssembly())
        {
            sb.AppendLine($"{prefix}{instanceName} : (skipped {type.FullName})");
            return;
        }

        // フィールドを展開（BackingField はスキップ）
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.Name.Contains("k__BackingField"))
                continue;

            DumpInternal(field.GetValue(obj)!, $"{prefix}{instanceName}.{field.Name}", sb);
        }

        // プロパティを展開（public / private 両方）
#if DEBUG
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
#else
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
#endif
        {
            if (prop.GetIndexParameters().Length > 0) continue; // インデクサは無視
            if (!prop.CanRead) continue;

            object value = null;
            try
            {
                value = prop.GetValue(obj);
            }
            catch { /* getter で例外が出る場合は無視 */ }

            DumpInternal(value!, $"{prefix}{instanceName}.{prop.Name}", sb);
        }
    }
}