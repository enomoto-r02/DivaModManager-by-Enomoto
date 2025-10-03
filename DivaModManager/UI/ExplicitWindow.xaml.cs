using System;
using System.Windows;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for Download.xaml
    /// </summary>
    public partial class ExplicitWindow : Window
    {
        public bool YesNo = false;
        public ExplicitWindow(DivaModArchivePost post)
        {
            InitializeComponent();
            ExplicitReasonText.Text = ViewStr(post.Explicit_Reason, 1000);
        }
        public ExplicitWindow(string text)
        {
            InitializeComponent();
            ExplicitReasonText.Text = text;
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
