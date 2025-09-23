# DivaModManager-by-Enomoto  
### Overview  
This tool is based on [Tekka's Diva Mod Manager](https://github.com/TekkaGB/DivaModManager) v1.3.1, with bug fixes and minor feature additions made by Enomoto.
Please note that this is not an official successor to DMM.
Depending on future updates to the original DMM, compatibility may be lost.

For an overview and basic operation, please refer to the [original Diva Mod Manager page](https://github.com/TekkaGB/DivaModManager).

### Changes in Version from the Original v1.3.1
- Added support for checking multiple mods
- Added support for dragging and moving multiple mods
- Added a filter function by mod
- Added 'Primary' column and 'Note' column and 'Category' and 'Size' column.
  When you enter information in the Priority or Category or Note column, a file named config_e.toml will be generated in each mod's folder.
  config_e.toml file contains the contents of the Priority and Note columns, so it can be safely deleted when, for example, restoring DMM to its original version.
  When the value of Priority is negative, it will be set below the blank during sorting.
- Fixed so that the dropped files are not deleted when installing the MOD via drag and drop.
- Made it possible to move columns by dragging
- Added a dropdown to show/hide columns
- You can open the corresponding mod folder by double-clicking the name field or by selecting the row and pressing the Enter key.
- Column width can now be changed and retained.
- Improved mod list display and scrolling speed
- Added "Move to Top" and "Move to Bottom" options to the context menu
- Added the AddToTop setting in Config.json, allowing users to choose whether newly added mods appear at the top or bottom of the list
- Disabled update checks on startup to speed up the launch process (press the "Update" button manually if you wish to update)
- Fixed an issue that corrupted the include line in config.toml
- Added a confirmation popup when sorting
- Added a feature to update mods individually (now possible via right-click)
- Fixed an issue where checked states were not correctly reflected in each mod's config.toml when changing loadouts
- Added a loadout copy feature
- Fixed an issue where searching with a single quotation mark (') in the GAMEBANANA tab caused an error
- Fix for the issue where the tool does not start when the contents of config.toml in the MOD folder are blank.
- Fixed an issue where only the first MOD would be selected when multiple versions were available during DMA's one-click installation.
- Changed the minimum window size to 1280x720 (as the layout breaks at 1160x750). If you experience any issues related to resolution, please let us know.
- Added a link to the GitHub page.
- Added an icon to display the DMM folder.
- Major internal processing changes (AI-assisted refactoring including async processing)

### Notes  
- Before use, please back up the original files (exe and Config.json) so that you can revert to the previous version if needed.
- The current version is compatible with the original v1.3.1, but future updates may break compatibility.
- Please refer to Github for the detailed changes in each version.

Please keep this in mind. If you encounter any issues, please let me know.
