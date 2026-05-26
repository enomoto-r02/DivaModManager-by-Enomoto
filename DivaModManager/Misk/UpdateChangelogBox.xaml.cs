using DivaModManager.Common.Helpers;
using DivaModManager.Models;
using Microsoft.Win32;
using Octokit;
using System;
using System.Media;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DivaModManager.Misk
{
    public partial class UpdateChangelogBox : Window
    {
        public bool YesNo = false;
        public bool Skip = false;

        public UpdateChangelogBox(GameBananaAPIV4 item, string packageName, string text, Uri preview, bool skip = false)
        {
            InitializeComponent();
            var update = item.Updates[0];
            if (preview != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = preview;
                bitmap.EndInit();
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewImage.Source = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/noimage.png"));
                PreviewImage.Visibility = Visibility.Visible;
            }
            ChangesGrid.ItemsSource = update.Changes;
            Title = $"{packageName} Changelog";
            VersionLabel.Content = $"Update: {update.Title} {update.Version}";
            Text.Text = text;
            // Format/Remove html tags
            update.Text = update.Text.Replace("<br>", "\n").Replace("&nbsp;", " ");
            UpdateText.Text = Regex.Replace(update.Text, "<.*?>", string.Empty);
            if (UpdateText.Text.Length == 0)
                UpdateText.Visibility = Visibility.Collapsed;
            if (skip)
                SkipButton.Visibility = Visibility.Visible;
            else
            {
                Grid.SetColumnSpan(YesButton, 2);
                Grid.SetColumnSpan(NoButton, 2);
            }
            SizeLabel.Text = $"File Size(about) : {FileHelper.GetDirectorySizeView10(item.Files[0].Filesize)}";
            PlayNotificationSound();
        }
        public UpdateChangelogBox(Release release, string packageName, string text, Uri preview, bool skip = false, bool loader = false)
        {
            InitializeComponent();
            if (preview != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = preview;
                bitmap.EndInit();
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else if (loader)
            {
                PreviewImage.Source = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/dml.png")); ;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewImage.Source = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/noimage.png"));
                PreviewImage.Visibility = Visibility.Visible;
            }
            Title = $"{packageName} Changelog";
            VersionLabel.Content = $"Update: {release.Name}";
            Text.Text = text;
            // Format/Remove html tags
            UpdateText.Text = release.Body;
            if (UpdateText.Text.Length == 0)
                UpdateText.Visibility = Visibility.Collapsed;
            if (skip)
                SkipButton.Visibility = Visibility.Visible;
            else
            {
                Grid.SetColumnSpan(YesButton, 2);
                Grid.SetColumnSpan(NoButton, 2);
            }

            App.Current.Dispatcher.InvokeAsync(async () =>
            {
                var fileSize = await GetFileSize(Global.GitHubclient, release.Url);
                if (fileSize == null) { SizeLabel.Visibility = Visibility.Collapsed; }
                else { SizeLabel.Text = $"File Size(about) : {fileSize}"; }
            });
            PlayNotificationSound();
        }
        public UpdateChangelogBox(DivaModArchivePost post, string packageName, string text, bool skip = false, bool loader = false)
        {
            InitializeComponent();
            if (post.Images[0] != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = post.Images[0];
                bitmap.EndInit();
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else if (loader)
            {
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/dml.png"));
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else
            {
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/DivaModManager;component/Assets/noimage.png"));
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                //PreviewImage.Visibility = Visibility.Collapsed;
            }
            Title = $"{packageName}";
            VersionLabel.Visibility = Visibility.Collapsed;
            Text.Text = text;
            UpdateText.Visibility = Visibility.Collapsed;
            if (skip)
                SkipButton.Visibility = Visibility.Visible;
            else
            {
                Grid.SetColumnSpan(YesButton, 2);
                Grid.SetColumnSpan(NoButton, 2);
            }
            SizeLabel.Text = $"File Size(about) : {GetFileSize(Global.DMAclient, post.Files[0].ToString())}";
            PlayNotificationSound();
        }
        private async Task<string> GetFileSize(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (response.Content.Headers.ContentLength == null)
            {
                return null;
            }
            else
            {
                return FileHelper.GetDirectorySizeView2((long)response.Content.Headers.ContentLength);
            }
        }
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            YesNo = true;
            Close();
        }
        private void Skip_Button_Click(object sender, RoutedEventArgs e)
        {
            Skip = true;
            Close();
        }

        public void PlayNotificationSound()
        {
            bool found = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\Notification.Default\.Current"))
                {
                    if (key != null)
                    {
                        object o = key.GetValue(null); // pass null to get (Default)
                        if (o != null)
                        {
                            SoundPlayer theSound = new((string)o);
                            theSound.Play();
                            found = true;
                        }
                    }
                }
            }
            catch
            { }
            if (!found)
                SystemSounds.Beep.Play(); // consolation prize
        }
    }
}
