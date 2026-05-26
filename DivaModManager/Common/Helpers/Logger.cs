using DivaModManager.Features.Debug;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DivaModManager.Common.Helpers
{
    public enum LoggerType
    {
        Info = 0,
        Warning,
        Error,
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

        public void WriteLine(string text, LoggerType type, string param = "", [CallerMemberName] string caller = "")
        {
            string color = "#f7fcfe";
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
                case LoggerType.Debug:
                    color = "#f7fcfe";
                    header = "DEBUG";
                    break;
                case LoggerType.Developer:
                    color = "#f7fcfe";
                    header = "DEVELOPER";
                    break;
            }

            var nowStr = $"[{DateTime.Now}]";
            var headerStr = string.IsNullOrEmpty(header) ? string.Empty : $"[{header}]";
            var paramStr = string.IsNullOrEmpty(param) ? string.Empty : $"[{param}]";

            var outputWindowValue = string.Empty;
            if (Logger.Mode == Logger.DEBUG_MODE.NORMAL)
            {
                outputWindowValue = string.Join(" ", nowStr, headerStr, text) + Environment.NewLine;
            }
            else
            {
                outputWindowValue = string.Join(" ", nowStr, headerStr, text, paramStr) + Environment.NewLine;
            }

            if ((Logger.Mode == Logger.DEBUG_MODE.NORMAL && type <= LoggerType.Error) || Logger.Mode >= Logger.DEBUG_MODE.DEBUG)
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    outputWindow.AppendText(outputWindowValue, color);
                });
            }
        }

        private static string _RichTextLog = string.Empty;
        public static string RichTextLog
        {
            get { return _RichTextLog; }
            private set { _RichTextLog = value; }
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
            catch (FormatException ex)
            {
                MessageBox.Show($"{ex.Message}, {ex.StackTrace}", "AppendTextFirst Error!");
            }
        }

        public static void AppendTextFirst(RichTextBox box, string text, string color)
        {
            BrushConverter bc = new BrushConverter();

            TextRange tr_ins = new TextRange(box.Document.ContentStart, box.Document.ContentStart);
            tr_ins.Text = text;

            try
            {
                tr_ins.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"{ex.Message}, {ex.StackTrace}", "AppendTextFirst Error!");
            }
        }
    }
}
