using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

public static class DataGridCaptureHelper
{
    private static void MergeResourceDictionaries(ResourceDictionary target, ResourceDictionary source)
    {
        if (Application.Current?.Resources != null)
        {
            try { target.MergedDictionaries.Add(Application.Current.Resources); } catch { }
        }

        if (source?.MergedDictionaries != null)
        {
            foreach (var md in source.MergedDictionaries)
            {
                try { target.MergedDictionaries.Add(md); } catch { }
            }
        }

        if (source != null)
        {
            foreach (var key in source.Keys)
            {
                try { target[key] = source[key]; } catch { }
            }
        }
    }

    private static ScrollViewer FindScrollViewer(DependencyObject parent)
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }

    // find a descendant whose type name contains "RowsPresenter" or "RowsPanel" or "ItemsPresenter"
    private static FrameworkElement FindRowsPresenterDescendant(DependencyObject parent)
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var tname = child.GetType().Name;
            if (tname.IndexOf("RowsPresenter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                tname.IndexOf("RowsPanel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                tname.IndexOf("ItemsPresenter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child as FrameworkElement;
            }
            var deeper = FindRowsPresenterDescendant(child);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private static double GetActualColumnHeaderHeight(DataGrid grid)
    {
        if (grid.ColumnHeaderHeight > 0 && !double.IsNaN(grid.ColumnHeaderHeight))
            return grid.ColumnHeaderHeight;

        var sv = FindScrollViewer(grid);
        if (sv != null && sv.Content is Panel contentPanel)
        {
            foreach (UIElement child in contentPanel.Children)
            {
                if (child.GetType().Name.IndexOf("ColumnHeadersPresenter", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (child as FrameworkElement)?.ActualHeight ?? 30.0;
            }
        }
        return 30.0;
    }

    private static async Task<List<double>> GetRowHeightsAsync(DataGrid grid)
    {
        var heights = new List<double>();
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            for (int i = 0; i < grid.Items.Count; i++)
            {
                try
                {
                    grid.ScrollIntoView(grid.Items[i]);
                    grid.UpdateLayout();
                    if (grid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row && row.ActualHeight > 0) heights.Add(row.ActualHeight);
                    else
                    {
                        if (grid.RowHeight > 0 && !double.IsNaN(grid.RowHeight)) heights.Add(grid.RowHeight);
                        else heights.Add(25.0);
                    }
                }
                catch
                {
                    heights.Add(25.0);
                }
            }
        }, DispatcherPriority.Background);
        return heights;
    }

    // Pixel-trim fallback (keeps previous behavior)
    private static CroppedBitmap TrimLeftWhitespace(RenderTargetBitmap rtb)
    {
        int w = rtb.PixelWidth;
        int h = rtb.PixelHeight;
        var stride = w * 4;
        var pixels = new byte[h * stride];
        try
        {
            rtb.CopyPixels(pixels, stride, 0);
        }
        catch
        {
            return new CroppedBitmap(rtb, new Int32Rect(0, 0, w, h));
        }

        byte bgB = pixels[0], bgG = pixels[1], bgR = pixels[2], bgA = pixels[3];
        int leftMost = 0;
        bool foundNonBg = false;
        for (int x = 0; x < w; x++)
        {
            bool columnAllBg = true;
            int baseX = x * 4;
            for (int y = 0; y < h; y++)
            {
                int idx = (y * stride) + baseX;
                byte b = pixels[idx + 0], g = pixels[idx + 1], r = pixels[idx + 2], a = pixels[idx + 3];
                if (a != bgA || r != bgR || g != bgG || b != bgB)
                {
                    columnAllBg = false; break;
                }
            }
            if (!columnAllBg) { leftMost = x; foundNonBg = true; break; }
        }

        if (!foundNonBg) return new CroppedBitmap(rtb, new Int32Rect(0, 0, w, h));
        if (leftMost <= 0) return new CroppedBitmap(rtb, new Int32Rect(0, 0, w, h));
        int newWidth = w - leftMost;
        return new CroppedBitmap(rtb, new Int32Rect(leftMost, 0, newWidth, h));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source">キャプチャ対象DataGrid</param>
    /// <param name="baseFilePath">拡張子を除いた出力ファイルパス(このファイルパス+.pngが出力結果になります)</param>
    /// <param name="dpi">DPI(メソッド側で制御していません)</param>
    /// <param name="maxLinesPerPage">ページ遷移する行数(0以下を指定すると指定なしと同じ扱いです)</param>
    /// <param name="maxHeightPx">ページ遷移する最大ピクセル数(0以下を指定すると指定なしと同じ扱いです)</param>
    /// <returns>なし</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// maxLinesPerPageとmaxHeightPxの両方が指定された場合は、
    /// どちらか一方の条件を満たした時点で改ページされます。
    public static async Task CaptureFullDataGridAsync(
        this DataGrid source,
        string baseFilePath,
        int dpi = 200,
        int maxLinesPerPage = 0,
        int maxHeightPx = 0)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (source.ItemsSource == null) return;

        // build exportGrid that copies styles as much as possible
        var exportGrid = new DataGrid
        {
            AutoGenerateColumns = source.AutoGenerateColumns,
            HeadersVisibility = source.HeadersVisibility,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            GridLinesVisibility = source.GridLinesVisibility,
            RowBackground = source.RowBackground,
            AlternatingRowBackground = source.AlternatingRowBackground,
            Background = source.Background,
            Foreground = source.Foreground,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            Style = source.Style,
            CellStyle = source.CellStyle,
            RowStyle = source.RowStyle,
            ColumnHeaderStyle = source.ColumnHeaderStyle,
            AlternationCount = source.AlternationCount,
            Resources = new ResourceDictionary(),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        MergeResourceDictionaries(exportGrid.Resources, source.Resources);

        // copy columns: new instances with pixel width = ActualWidth
        foreach (var col in source.Columns)
        {
            if (col is DataGridTextColumn tx)
            {
                var nc = new DataGridTextColumn
                {
                    Header = tx.Header,
                    Binding = tx.Binding,
                    ElementStyle = tx.ElementStyle,
                    EditingElementStyle = tx.EditingElementStyle,
                    CellStyle = tx.CellStyle,
                    Width = new DataGridLength(col.ActualWidth, DataGridLengthUnitType.Pixel)
                };
                exportGrid.Columns.Add(nc);
            }
            else if (col is DataGridCheckBoxColumn cb)
            {
                var nc = new DataGridCheckBoxColumn
                {
                    Header = cb.Header,
                    Binding = cb.Binding,
                    ElementStyle = cb.ElementStyle,
                    Width = new DataGridLength(col.ActualWidth, DataGridLengthUnitType.Pixel)
                };
                exportGrid.Columns.Add(nc);
            }
            else
            {
                try
                {
                    if (col.GetType().GetConstructor(Type.EmptyTypes)?.Invoke(null) is DataGridColumn clone)
                    {
                        clone.Header = col.Header;
                        clone.Width = new DataGridLength(col.ActualWidth, DataGridLengthUnitType.Pixel);
                        exportGrid.Columns.Add(clone);
                    }
                }
                catch { }
            }
        }

        exportGrid.ItemsSource = source.ItemsSource;

        var host = new StackPanel();
        host.Children.Add(exportGrid);

        // measure/arrange on UI thread with low priority
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, host.DesiredSize.Width, host.DesiredSize.Height));
            host.UpdateLayout();
        }, DispatcherPriority.Background);

        await Task.Yield();

        // compute widths/heights
        double colsWidth = 0;
        foreach (var c in exportGrid.Columns) colsWidth += c.ActualWidth;
        double rowHeaderWidth = (source.HeadersVisibility & DataGridHeadersVisibility.Row) != 0 ? source.RowHeaderWidth : 0;
        double fullWidthDip = Math.Ceiling(colsWidth + rowHeaderWidth); // avoid adding scrollbar here

        var rowHeights = await GetRowHeightsAsync(exportGrid);
        double headerHeight = GetActualColumnHeaderHeight(exportGrid);
        int totalRows = rowHeights.Count;
        if (totalRows == 0) return;

        int startRow = 0;
        int page = 1;

        while (startRow < totalRows)
        {
            //int lines = maxLinesPerPage ?? (totalRows - startRow); // Original
            int lines = maxLinesPerPage == 0 ? (totalRows - startRow) : maxLinesPerPage;

            if (maxHeightPx > 0)
            {
                double acc = headerHeight;
                int count = 0;
                for (int i = startRow; i < totalRows; i++)
                {
                    if (acc + rowHeights[i] > maxHeightPx) break;
                    acc += rowHeights[i];
                    count++;
                    if (maxLinesPerPage > 0 && count >= maxLinesPerPage) break;
                }
                if (count == 0) count = 1;
                lines = Math.Min(lines, count);
            }

            var pageGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                HeadersVisibility = source.HeadersVisibility,
                RowHeaderWidth = source.RowHeaderWidth,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                GridLinesVisibility = source.GridLinesVisibility,
                RowBackground = source.RowBackground,
                AlternatingRowBackground = source.AlternatingRowBackground,
                Background = source.Background,
                Foreground = source.Foreground,
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                Style = source.Style,
                CellStyle = source.CellStyle,
                RowStyle = source.RowStyle,
                ColumnHeaderStyle = source.ColumnHeaderStyle,
                AlternationCount = source.AlternationCount,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            };

            MergeResourceDictionaries(pageGrid.Resources, source.Resources);

            foreach (var sCol in exportGrid.Columns)
            {
                if (sCol is DataGridTextColumn stx)
                {
                    var nc = new DataGridTextColumn
                    {
                        Header = stx.Header,
                        Binding = stx.Binding,
                        ElementStyle = stx.ElementStyle,
                        EditingElementStyle = stx.EditingElementStyle,
                        CellStyle = stx.CellStyle,
                        Width = new DataGridLength(stx.ActualWidth, DataGridLengthUnitType.Pixel)
                    };
                    pageGrid.Columns.Add(nc);
                }
                else if (sCol is DataGridCheckBoxColumn scb)
                {
                    var nc = new DataGridCheckBoxColumn
                    {
                        Header = scb.Header,
                        Binding = scb.Binding,
                        ElementStyle = scb.ElementStyle,
                        Width = new DataGridLength(scb.ActualWidth, DataGridLengthUnitType.Pixel)
                    };
                    pageGrid.Columns.Add(nc);
                }
                else
                {
                    try
                    {
                        if (sCol.GetType().GetConstructor(Type.EmptyTypes)?.Invoke(null) is DataGridColumn nc) { nc.Header = sCol.Header; nc.Width = new DataGridLength(sCol.ActualWidth, DataGridLengthUnitType.Pixel); pageGrid.Columns.Add(nc); }
                    }
                    catch { }
                }
            }

            for (int r = 0; r < lines && (startRow + r) < totalRows; r++)
                pageGrid.Items.Add(exportGrid.Items[startRow + r]);

            var pageHost = new StackPanel();
            pageHost.Children.Add(pageGrid);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                pageHost.Measure(new Size(fullWidthDip, double.PositiveInfinity));
                pageHost.Arrange(new Rect(0, 0, fullWidthDip, pageHost.DesiredSize.Height));
                pageGrid.UpdateLayout();
            }, DispatcherPriority.Background);

            await Task.Yield();

            double pageWidthDip = fullWidthDip;
            double pageHeightDip = pageGrid.ActualHeight;
            if (double.IsNaN(pageHeightDip) || pageHeightDip <= 0) pageHeightDip = pageGrid.DesiredSize.Height;
            if (pageHeightDip <= 0) pageHeightDip = headerHeight + rowHeights.Skip(startRow).Take(lines).Sum();

            var rtb = new RenderTargetBitmap(
                (int)Math.Max(1, Math.Round(pageWidthDip * dpi / 96.0)),
                (int)Math.Max(1, Math.Round(pageHeightDip * dpi / 96.0)),
                dpi, dpi, PixelFormats.Pbgra32);

            // Render on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { rtb.Render(pageGrid); } catch { }
            }, DispatcherPriority.Background);

            // Attempt to find rows presenter and compute crop rect in pixels
            CroppedBitmap cropped = null;
            try
            {
                FrameworkElement rowsPresenter = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var sv = FindScrollViewer(pageGrid);
                    if (sv != null && sv.Content is DependencyObject content)
                    {
                        rowsPresenter = FindRowsPresenterDescendant(content);
                    }
                }, DispatcherPriority.Background);

                if (rowsPresenter != null)
                {
                    // transform to pageGrid coords
                    Point pt = new(0, 0);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var gt = rowsPresenter.TransformToAncestor(pageGrid);
                            pt = gt.Transform(new Point(0, 0));
                        }
                        catch
                        {
                            pt = new Point(0, 0);
                        }
                    }, DispatcherPriority.Background);

                    double xOffsetDip = pt.X;
                    double rowsWidthDip = rowsPresenter.ActualWidth;
                    if (rowsWidthDip <= 0) rowsWidthDip = colsWidth;

                    int xPx = Math.Max(0, (int)Math.Round(xOffsetDip * dpi / 96.0));
                    int wPx = Math.Max(1, (int)Math.Round(rowsWidthDip * dpi / 96.0));
                    int hPx = rtb.PixelHeight;

                    // clamp
                    if (xPx + wPx > rtb.PixelWidth) wPx = rtb.PixelWidth - xPx;
                    if (wPx <= 0) wPx = rtb.PixelWidth;

                    cropped = new CroppedBitmap(rtb, new Int32Rect(xPx, 0, wPx, hPx));
                }
            }
            catch
            {
                cropped = null; // fallback below
            }

            if (cropped == null)
            {
                // fallback to pixel-trim
                try { cropped = TrimLeftWhitespace(rtb); }
                catch { cropped = new CroppedBitmap(rtb, new Int32Rect(0, 0, rtb.PixelWidth, rtb.PixelHeight)); }
            }

            cropped.Freeze();
            var frame = BitmapFrame.Create(cropped);
            frame.Freeze();

            string filePath = $"{baseFilePath}_{page}.png";
            await Task.Run(() =>
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(frame);
                using (var fs = new FileStream(filePath, FileMode.Create))
                    enc.Save(fs);
            });

            startRow += lines;
            page++;
            await Task.Delay(1);
        }

    }
}
