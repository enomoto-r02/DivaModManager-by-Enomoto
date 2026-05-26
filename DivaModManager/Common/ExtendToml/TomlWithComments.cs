using System.Runtime.Serialization;

namespace DivaModManager.Common.ExtendToml
{
    using DivaModManager.Common.Config;
    using System.Reflection;
    using Tomlyn;
    using Tomlyn.Model;

    public static class TomlWithComments
    {
        public static string SerializeWithComments<T>(T model)
        {
            // Tomlynで通常のTOML文字列を作成
            string toml = Toml.FromModel(model);
            toml = toml.Insert(0, "\n");

            // 各プロパティ情報を取得
            var props = typeof(T).GetProperties();

            foreach (var prop in props)
            {
                DataMemberComment commentAttr;
                if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString())
                {
                    commentAttr = prop.GetCustomAttribute<DataMemberCommentEN>();
                }
                else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString())
                {
                    commentAttr = prop.GetCustomAttribute<DataMemberCommentJP>();
                }
                else
                {
                    commentAttr = null;
                }
                var dataAttr = prop.GetCustomAttribute<DataMemberAttribute>();

                if (commentAttr == null || dataAttr == null) continue;

                string key = dataAttr.Name ?? prop.Name;
                string comment = "# " + commentAttr.Comment.Replace("\r", "").Replace("\n", "\n# ");

                // TOML出力の対応キー行を探す
                string find = key + " =";
                int index = toml.IndexOf(find);
                if (index >= 0)
                {
                    // 対応行の前にコメントを挿入
                    int lineStart = toml.LastIndexOf('\n', index);
                    if (lineStart < 0) lineStart = 0;
                    var insStr = comment;
                    toml = toml.Insert(lineStart, insStr);
                    int lineEnd = toml.IndexOf('\n', lineStart + insStr.Length + 1);
                    // データ行の下に2行の改行を挿入
                    toml = toml.Insert(lineEnd, "\n\n");
                }
            }

            return toml;
        }

        /// <summary>
        /// 暫定
        /// </summary>
        /// <param name="tomlTable"></param>
        /// <returns></returns>
        public static string SerializeWithComments<T>(TomlTable tomlTable, T commentModel)
        {
            // Tomlynで通常のTOML文字列を作成
            string toml = Toml.FromModel(tomlTable);
            toml = toml.Insert(0, "\n");

            // 各プロパティ情報を取得
            var props = typeof(T).GetProperties();

            foreach (var prop in props)
            {
                DataMemberComment commentAttr;
                if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.EN.ToString())
                {
                    commentAttr = prop.GetCustomAttribute<DataMemberCommentEN>();
                }
                else if (Global.ConfigToml.Language == ConfigTomlDmm.Lang.JP.ToString())
                {
                    commentAttr = prop.GetCustomAttribute<DataMemberCommentJP>();
                }
                else
                {
                    commentAttr = null;
                }
                var dataAttr = prop.GetCustomAttribute<DataMemberAttribute>();

                if (commentAttr == null || dataAttr == null) continue;

                string key = dataAttr.Name ?? prop.Name;
                string comment = "# " + commentAttr.Comment.Replace("\r", "").Replace("\n", "\n# ");

                // TOML出力の対応キー行を探す
                string find = key + " =";
                int index = toml.IndexOf(find);
                if (index >= 0)
                {
                    // 対応行の前にコメントを挿入
                    int lineStart = toml.LastIndexOf('\n', index);
                    if (lineStart < 0) lineStart = 0;
                    var insStr = comment;
                    toml = toml.Insert(lineStart, insStr);
                    int lineEnd = toml.IndexOf('\n', lineStart + insStr.Length + 1);
                    // データ行の下に2行の改行を挿入
                    toml = toml.Insert(lineEnd, "\n\n");
                }
            }

            return toml;
        }
    }
}
