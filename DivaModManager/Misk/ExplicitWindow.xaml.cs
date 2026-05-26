using DivaModManager.Models;
using System;
using System.Windows;

namespace DivaModManager
{
    public partial class ExplicitWindow : Window
    {
        public bool YesNo = false;
        public ExplicitWindow(DivaModArchivePost post)
        {
            InitializeComponent();
            ExplicitReasonText.Text = ViewStr(post.Explicit_Reason, 1000);
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
