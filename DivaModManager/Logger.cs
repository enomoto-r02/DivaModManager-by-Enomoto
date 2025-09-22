using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using static DivaModManager.Debug;

namespace DivaModManager
{
    public enum LoggerType
    {
        Info,
        Warning,
        Error,
        Critical,
        Debug,
        Developer,
    }

    public partial class WindowLogger
    {
        RichTextBox outputWindow;
        public WindowLogger(RichTextBox textBox)
        {
            outputWindow = textBox;
        }

        public void WriteLine(string text, LoggerType type)
        {
            string color = "#F2F2F2";
            string header = "";
            switch (type)
            {
                case LoggerType.Info:
                    color = "#52FF00";
                    header = "INFO";
                    break;
                case LoggerType.Warning:
                    color = "#FFFF00";
                    header = "WARNING";
                    break;
                case LoggerType.Error:
                    color = "#FFB0B0";
                    header = "ERROR";
                    break;
                case LoggerType.Critical:
                    color = "#FFB0B0";
                    header = "CRITICAL";
                    break;
                case LoggerType.Debug:
                    color = "#F2F2F2";
                    header = "DEBUG";
                    break;
                case LoggerType.Developer:
                    color = "#F2F2F2";
                    header = "DEVELOPER";
                    break;
            }
            // Call on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var value = $"[{DateTime.Now}] [{header}] {text}\n";
                TextLogger.Log += value;

                // 画面出力はGlobal.DEBUG_MODE、LoggerTypeどちらもDEBUGまで
                if (type != LoggerType.Developer
                    && (DEBUG_MODE.DEBUG >= Global.DEBUG_MODE)
                    || type != LoggerType.Debug)
                {
                    outputWindow.AppendText(value, color);
                }
            });
        }
    }

    // RichTextBox extension to append color
    public static class RichTextBoxExtensions
    {
        public static void AppendText(this RichTextBox box, string text, string color)
        {
            BrushConverter bc = new BrushConverter();

            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
            }
            catch (FormatException) { }
        }
    }
}
