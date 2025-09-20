using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for Download.xaml
    /// </summary>
    public partial class DownloadWindow : Window
    {
        public bool YesNo = false;
        public DownloadApiBase record;

        public DownloadWindow(DownloadApiBase record)
        {
            this.record = record;
        }
        public DownloadWindow(GameBananaAPIV4 record)
        {
            InitializeComponent();
            this.record = record;
            this.record.SITE = DownloadApiBase.TARGET_TO.GAMEBANANA_API;
            DownloadText.Text = $"{record.Title}\nSubmitted by {record.Owner.Name}";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = record.Image;
            bitmap.EndInit();
            Preview.Source = bitmap;
        }
        public DownloadWindow(GameBananaRecord record)
        {
            InitializeComponent();
            this.record = record;
            this.record.SITE = DownloadApiBase.TARGET_TO.GAMEBANANA_BROWSER;
            DownloadText.Text = $"{record.Title}\nSubmitted by {record.Owner.Name}";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = record.Image;
            bitmap.EndInit();
            Preview.Source = bitmap;
        }
        public DownloadWindow(DivaModArchivePost post)
        {
            InitializeComponent();
            this.record = post;
            this.record.SITE = DownloadApiBase.TARGET_TO.DIVAMODARCHIVE_API;
            DownloadText.Text = $"{post.Name}\nSubmitted by {post.Authors[0].Name}";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = post.Images[0];
            bitmap.EndInit();
            Preview.Source = bitmap;
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
    }
}
