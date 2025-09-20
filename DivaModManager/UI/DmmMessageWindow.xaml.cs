using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for DmmMessageWindow.xaml
    /// </summary>
    public partial class DmmMessageWindow : Window
    {
        public bool YesNo = false;

        public DmmMessageWindow(string strInfo, string strText, string title)
        {
            InitializeComponent();

            MessageInfo.Text = strInfo;
            MessageText.Text = strText;
            this.Title = title;
            //var bitmap = new BitmapImage();
            //bitmap.BeginInit();
            //bitmap.UriSource = record.Image;
            //bitmap.EndInit();
            //Preview.Source = bitmap;

            this.Activate();
        }
        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            YesNo = true;
            Close();
        }
        private void No_Click(object sender, RoutedEventArgs e)
        {
            YesNo = false;
            Close();
        }
    }
}
