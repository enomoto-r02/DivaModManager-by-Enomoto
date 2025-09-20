using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DivaModManager
{
    public enum LoggerType
    {
        Info,
        Warning,
        Error,
        Debug,
        Critical,
    }
    public class Logger
    {
        RichTextBox outputWindow;
        public Logger(RichTextBox textBox)
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
            }
            // Call on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                outputWindow.AppendText($"[{DateTime.Now}] [{header}] {text}\n", color);
            });
        }
    }

    public static class FileLogger
    {
        public static void FileOpenAndWrite(string value)
        {
            if (File.Exists(Global.textLogLocation))
            {
                File.Delete(Global.textLogLocation);
            }
            using (var fs = new FileStream(
                    Global.textLogLocation, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte[] info = new UTF8Encoding(true).GetBytes(value);
                fs.Write(info, 0, info.Length);
            }
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
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFromString(color));
            }
            catch (FormatException) { }
        }
    }
}
