using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Misk
{
    public partial class EditWindow : Window
    {
        public string _name;
        public bool _folder;
        public string newName;
        public string loadout = null;
        public EditWindow(string name, bool folder)
        {
            InitializeComponent();
            _folder = folder;
            if (!string.IsNullOrEmpty(name))
            {
                _name = name;
                NameBox.Text = name;
                Title = $"Edit {name}";
            }
            else
                Title = "Create New Loadout";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_folder)
                EditFolderName();
            else
                CreateLoadoutName();
        }
        private void CreateLoadoutName()
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                Logger.WriteLine($"Invalid loadout name", LoggerType.Error);
                return;
            }
            if (!Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.ContainsKey(NameBox.Text))
            {
                loadout = NameBox.Text;
                Close();
            }
            else
                Logger.WriteLine($"{NameBox.Text} already exists", LoggerType.Error);
        }
        private void EditFolderName()
        {
            if (!NameBox.Text.Equals(_name, StringComparison.InvariantCultureIgnoreCase))
            {
                var oldDirectory = $"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{Global.s}{_name}";
                var newDirectory = $"{Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder}{Global.s}{Global.s}{NameBox.Text}";
                if (!Directory.Exists(newDirectory))
                {
                    try
                    {
                        Directory.Move(oldDirectory, newDirectory);
                        Logger.WriteLine($"Renamed \"{_name}\" to \"{NameBox.Text}\"", LoggerType.Info);
                        // Rename in every single loadout
                        foreach (var key in Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts.Keys)
                        {
                            var index = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[key].ToList().FindIndex(x => x.name == _name);
                            if (index == -1)
                            {
                                continue;
                            }
                            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[key][index].name = NameBox.Text;
                        }
                        Global.ModList_All = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout];
                        Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Couldn't rename {oldDirectory} to {newDirectory} ({ex.Message})", LoggerType.Error);
                        Close();
                    }
                }
                else
                    Logger.WriteLine($"{newDirectory} already exists", LoggerType.Error);
            }
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (_folder)
                    EditFolderName();
                else
                    CreateLoadoutName();
            }
        }
    }
}
