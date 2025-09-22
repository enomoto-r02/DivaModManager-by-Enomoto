using System;
using System.Windows;
using System.Linq;

namespace DivaModManager.UI
{
    /// <summary>
    /// for Debug
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        public MainWindow(StartupEventArgs e) : this()
        {
            var title = this.Title;
            if (Global.DEBUG_MODE == Debug.DEBUG_MODE.DEVELOPER)
            {
                // 初期値をVisibleにしているため、、、
                this.DeveloperTabItem.Visibility = Global.DEBUG_MODE == Debug.DEBUG_MODE.DEVELOPER ? Visibility.Visible : Visibility.Hidden;
                this.Title += $" (Developer)";
            }
            else if (Global.DEBUG_MODE == Debug.DEBUG_MODE.DEBUG)
            {
                this.Title += $" (Debug)";
            }
        }

        private void DeveloperTab_TabSelected(object sender, RoutedEventArgs e)
        {

        }
    }
}
