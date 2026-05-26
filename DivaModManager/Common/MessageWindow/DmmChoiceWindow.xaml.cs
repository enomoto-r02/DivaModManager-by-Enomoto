using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DivaModManager.Common.MessageWindow;

/// <summary>
/// 
/// </summary>
public partial class DmmChoiceWindow : Window
{
    public int choice = -1;
    public bool cancel = false;
    public DmmChoiceWindow(List<DmmChoiceModel> choices, string title = null)
    {
        InitializeComponent();
        ChoiceList.ItemsSource = choices;
        if (title != null)
            Title = title;
    }
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = sender as Button;
        var item = button.DataContext as DmmChoiceModel;
        choice = item.Index;
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (choice == -1)
            cancel = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        cancel = true;
        Close();
    }
}

public class DmmChoiceModel
{
    public string MessageInfo { get; set; }
    public string MessageText { get; set; }
    public int Index { get; set; }
}

