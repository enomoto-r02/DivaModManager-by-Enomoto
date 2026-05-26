using DivaModManager.Common.ExtendToml;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DivaModManager.Common.ExtendJson;

public static class JsonWithComments
{
    public static readonly JsonSerializerOptions options = new() { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public static string SerializeWithComments<T>(T model)
    {
        string json = JsonSerializer.Serialize(model, options);
        json = json.Insert(0, "\n");

        // 各プロパティ情報を取得
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            var commentAttr = prop.GetCustomAttribute<JsonPropertyComment>();
            var dataAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (commentAttr == null || dataAttr == null) continue;

            string key = dataAttr.Name ?? prop.Name;
            string comment = "// " + commentAttr.Comment.Replace("\r", "").Replace("\n", "\n// ");

            // JSON出力の対応キー行を探す
            string find = $"\"{key}\": ";
            int index = json.IndexOf(find);
            if (index >= 0)
            {
                // 対応行の前にコメントを挿入
                int lineStart = json.LastIndexOf('\n', index);
                if (lineStart < 0) lineStart = 0;
                var insStr = comment;
                // todo: プレフィックスにスペースを追加する処理を入れる(ただし互換性の問題が出たため実装は後日)
                json = json.Insert(lineStart, insStr);
                int lineEnd = json.IndexOf('\n', lineStart + insStr.Length + 1);
            }
        }

        return json;
    }
}
