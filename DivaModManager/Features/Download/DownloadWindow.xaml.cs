using DivaModManager.Common.Helpers;
using DivaModManager.Features.Extract;
using DivaModManager.Models;
using System.Net.Http;
using System.Threading.Tasks;
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

        public DownloadWindow(GameBananaAPIV4 record)
        {
            InitializeComponent();
            record.Site = ExtractInfo.SITE.GAMEBANANA_API;
            record.Type = ExtractInfo.TYPE.DOWNLOAD;
            DownloadText.Text = $"{record.Title}\nSubmitted by {record.Owner.Name}";
            SizeLabel.Text = record.Files.Count <= 1 ? $"File Size(about) : {FileHelper.GetDirectorySizeView10(record.Files[0].Filesize)}" : $"File Count : {record.Files.Count}";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = record.Image;
            bitmap.EndInit();
            Preview.Source = bitmap;
        }
        public DownloadWindow(GameBananaRecord record)
        {
            InitializeComponent();
            record.Site = ExtractInfo.SITE.GAMEBANANA_BROWSER;
            record.Type = ExtractInfo.TYPE.DOWNLOAD;
            SizeLabel.Text = record.AllFiles.Count <= 1 ? $"File Size(about) : {FileHelper.GetDirectorySizeView10(record.AllFiles[0].Filesize)}" : $"File Count : {record.AllFiles.Count.ToString()}";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = record.Image;
            bitmap.EndInit();
            Preview.Source = bitmap;
        }
        public DownloadWindow(DivaModArchivePost post)
        {
            InitializeComponent();
            post.Site = ExtractInfo.SITE.DIVAMODARCHIVE_API;
            post.Type = ExtractInfo.TYPE.DOWNLOAD;
            DownloadText.Text = $"{post.Name}\nSubmitted by {post.Authors[0].Name}";
            App.Current.Dispatcher.InvokeAsync(async () =>
            {
                SizeLabel.Text = post.Files.Count <= 1 ? $"File Size(about) : {await GetFileSize(Global.DMAclient, post.Files[0].ToString())}" : $"File Count : {post.Files.Count.ToString()}";
            });
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = post.Images[0];
            bitmap.EndInit();
            Preview.Source = bitmap;
        }
        private async Task<string> GetFileSize(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return FileHelper.GetDirectorySizeView2((long)response.Content.Headers.ContentLength);
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
