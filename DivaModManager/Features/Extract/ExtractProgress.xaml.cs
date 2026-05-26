using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;

#nullable enable

namespace DivaModManager.Features.Extract
{
    /// <summary>
    /// Interaction logic for ProgressBox.xaml
    /// </summary>
    public partial class ExtractProgress : Window
    {
        public ExtractProgressInfo extractinfo = new();
        private CancellationTokenSource cancellationTokenSource;
        public bool finished = false;

        public ExtractProgress(double start, double end)
        {
            InitializeComponent();
            cancellationTokenSource = new();
            progressBar = new();
            extractinfo.ProgressValue = start;
            extractinfo.ProgressMaxValue = end;
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            progressText.Text = $"{progressBar.Value} %";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }
    }

    public class ExtractProgressInfo : INotifyPropertyChanged
    {
        private double _progressMaxValue;
        public double ProgressMaxValue
        {
            get { return _progressMaxValue; }
            set
            {
                _progressMaxValue = value;
                OnPropertyChanged(nameof(ProgressMaxValue));
            }
        }
        private double _progressValue;
        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
