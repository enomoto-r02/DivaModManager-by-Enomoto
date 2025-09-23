using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DivaModManager
{
    public static class DataGridCaptureHelper
    {
        public static void SaveStyledDataGridAsPng(DataGrid dataGrid, string filePath)
        {
            // 元の親とインデックスを保存
            var parent = VisualTreeHelper.GetParent(dataGrid) as Panel;
            int index = parent.Children.IndexOf(dataGrid);

            //await Task.Factory.StartNew(() =>
            // 一時退避
            parent.Children.Remove(dataGrid);

            // 仮親に配置
            var panel = new StackPanel();
            panel.Children.Add(dataGrid);

            // レイアウトを展開
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height));
            panel.UpdateLayout();

            double width = dataGrid.ActualWidth;
            double height = dataGrid.ActualHeight;

            // 元の親に戻す
            panel.Children.Remove(dataGrid);
            parent.Children.Insert(index, dataGrid);

            if (width == 0 || height == 0)
            {
                return;
            }

            // レンダリング
            var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dataGrid);

            // 保存
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }
    }
}
