using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Misk
{
    public partial class CreateModWindow : Window
    {
        public TomlTable config;
        public CreateModWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";
            var path = string.Join(string.Empty, NameBox.Text.Split(Path.GetInvalidFileNameChars()));
            if (Directory.Exists($"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}"))
            {
                Logger.WriteLine($"{path} already exists in your mod folder, please choose another name", LoggerType.Warning);
                return;
            }
            config = new()
            {
                { "enabled", true },
                { "include", new string[1] { "." } }
            };
            if (!string.IsNullOrWhiteSpace(NameBox.Text))
                config.Add("name", NameBox.Text);
            if (!string.IsNullOrWhiteSpace(AuthorBox.Text))
                config.Add("author", AuthorBox.Text);
            if (!string.IsNullOrWhiteSpace(VersionBox.Text))
                config.Add("version", VersionBox.Text);
            if (!string.IsNullOrWhiteSpace(DateBox.Text))
                config.Add("date", DateBox.Text);
            if (!string.IsNullOrWhiteSpace(DescriptionBox.Text))
                config.Add("description", DescriptionBox.Text);
            Directory.CreateDirectory($"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}");
            var configFile = Toml.FromModel(config);
            File.WriteAllText($"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}{Global.s}config.toml", configFile);
            if (!string.IsNullOrEmpty(PreviewBox.Text) && File.Exists(PreviewBox.Text))
                File.Copy(PreviewBox.Text, $"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}{Global.s}Preview{Path.GetExtension(PreviewBox.Text)}", true);
            Logger.WriteLine($"Created {NameBox.Text}!", LoggerType.Info);
            try
            {
                Process process = Process.Start("explorer.exe", $"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}");
                Logger.WriteLine($@"Opened {Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}.", LoggerType.Info);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($@"Couldn't open {Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{path}. ({ex.Message})", LoggerType.Error);
            }
            Close();
        }

        private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NameBox.Text))
                SaveButton.IsEnabled = true;
            else
                SaveButton.IsEnabled = false;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = $"Image Files (*.*)|*.*",
                Title = $"Select Preview",
                Multiselect = false,
                InitialDirectory = Global.assemblyLocation
            };
            dialog.ShowDialog();
            if (!string.IsNullOrEmpty(dialog.FileName) && File.Exists(dialog.FileName))
                PreviewBox.Text = dialog.FileName;
            // Bring Create Package window back to foreground after closing dialog
            Activate();
        }
    }
}
