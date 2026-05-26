using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DivaModManager.Features.DMM;

/// <summary>
/// Interaction logic for UpdateFileBox.xaml
/// </summary>
public partial class UpdateFileBox : Window
{
    public string chosenFileUrl;
    public string chosenFileName;

    public UpdateFileBox(List<GameBananaItemFile> files, string packageName)
    {
        InitializeComponent();
        FileList.ItemsSource = files;
        TitleBox.Text = packageName;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = sender as Button;
        var item = button.DataContext as GameBananaItemFile;
        chosenFileUrl = item.DownloadUrl;
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
