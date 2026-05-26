using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DivaModManager.Misk
{
    public partial class DownloadFileBoxDMA : Window
    {
        public Uri chosenFileUrl;
        public string chosenFileName;

        class DMAFileDownload
        {
            public string FileName { get; set; }
            public Uri FileUrl { get; set; }
        }

        public DownloadFileBoxDMA(DivaModArchivePost post)
        {
            InitializeComponent();
            List<DMAFileDownload> files = new();
            for (int i = 0; i < post.Files.Count; i++)
            {
                files.Add(new DMAFileDownload { FileName = post.FileNames[i], FileUrl = post.Files[i] });
            }
            FileList.ItemsSource = files;
            TitleBox.Text = post.Name;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as DMAFileDownload;
            chosenFileUrl = item.FileUrl;
            chosenFileName = item.FileName;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
