using System;
using System.Windows;

namespace DivaModManager
{
    public partial class ExplicitWindow : Window
    {
        public bool YesNo = false;
        public ExplicitWindow(string message)
        {
            InitializeComponent();
            ExplicitReasonText.Text = ViewStr(message, 1000);
        }
        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            YesNo = true;

            Close();
        }
        private void No_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private static string ViewStr(string str, int maxLen)
        {
            var viewStr = str;
            if (viewStr.Length >= maxLen)
            {
                viewStr = string.Concat(viewStr.AsSpan(0, 1000), "...");
            }

            return viewStr;
        }
    }
}
