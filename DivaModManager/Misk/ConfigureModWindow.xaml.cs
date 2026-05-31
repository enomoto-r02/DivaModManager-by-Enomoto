using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Tomlyn;

namespace DivaModManager.Misk
{
    public partial class ConfigureModWindow : Window
    {
        public Mod _mod;
        private string configPath;
        public ConfigureModWindow(Mod mod)
        {
            InitializeComponent();
            if (mod != null)
            {
                _mod = mod;
                configPath = Path.Combine(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder, mod.name, "config.toml");
                var configString = File.ReadAllText(configPath);
                ConfigBox.Text = configString;
                Title = $"Configure {_mod.name}";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                var config = Toml.ToModel(ConfigBox.Text);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Invalid config: {ex.Message}", LoggerType.Error);
                return;
            }
            File.WriteAllText(configPath, ConfigBox.Text);
            Logger.WriteLine($"Successfully saved config!", LoggerType.Info);
            Close();
        }
    }
}
