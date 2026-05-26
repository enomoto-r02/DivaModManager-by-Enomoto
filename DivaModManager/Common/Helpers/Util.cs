using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;


namespace DivaModManager.Common.Helpers
{
    public class NaturalSort : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int lx = x.Length, ly = y.Length;

            for (int mx = 0, my = 0; mx < lx && my < ly; mx++, my++)
            {
                if (char.IsDigit(x[mx]) && char.IsDigit(y[my]))
                {
                    long vx = 0, vy = 0;

                    for (; mx < lx && char.IsDigit(x[mx]); mx++)
                        vx = vx * 10 + x[mx] - '0';

                    for (; my < ly && char.IsDigit(y[my]); my++)
                        vy = vy * 10 + y[my] - '0';

                    if (vx != vy)
                        return vx > vy ? 1 : -1;
                }

                if (mx < lx && my < ly && x[mx] != y[my])
                    return x[mx] > y[my] ? 1 : -1;
            }

            return lx - ly;
        }
    }

    public static class Util
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static string GetListToParamString<T>(List<T> list)
        {
            if (list == null) { return "null"; }
            if (list.Count == 0) { return "Count=0"; }
            else return string.Join(",", list);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="src"></param>
        /// <returns></returns>
        public static T DeepCopy<T>(this T src)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(src);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// DataGridのスクロールをトップに設定する
        /// </summary>
        /// <param name="dataGrid"></param>
        public static void DataGrid_ScrollToTop(DataGrid dataGrid)
        {
            var border = VisualTreeHelper.GetChild(dataGrid, 0) as Decorator;
            if (border != null)
            {
                var scrollViewer = border.Child as ScrollViewer;
                scrollViewer.ScrollToTop();
            }
        }
    }
}
