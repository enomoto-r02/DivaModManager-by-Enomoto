
DivaModManager by Enomoto v1.3.1.35 (beta4)

----- Note (Please read carefully!!) -------------------------

- It includes features that are restricted from the Windows version DivaMoaManager by Enomoto v1.3.1.34 and unstable functions.
- Use at your own risk.
- Operation outside the [verified environments] listed below has not been confirmed.
- Especially on Linux distributions that differ or in environments like macOS, the OS may become non-functional.
- Installing .NET is not required (however, the executable size is very large).
- Since we prioritize implementing features as close to the Windows version as possible, feature requests are generally not accepted.

----- Bugs and Feature Restrictions -------------------------

Launching this tool on Linux or Steam Deck assumes that a Mod operation environment has already been fully set up for users.
Specifically, the following changes have been made from the Windows version DivaMoaManager by Enomoto v1.3.1.34

### Common to Windows and Linux
- UI changes
- Preliminary implementation of the Module tab and Song tab
  Please refer to GitHub for usage instructions.
- Implementation of cache functionality
  For some slower connections or in the GameBanana tab, initial operation may be slower than previous versions.
  Additionally, cache accumulates in the DivaModManager/cache folder (please note the capacity).
  Once a cache has been generated, reduced load times and controlled excessive API access are expected.
  The following times are currently set by the program.
    API cache for GameBanana and DivaModArchive: 3 hours
    Image cache for GameBanana and DivaModArchive: 72 hours
- Error reporting template included.

### Restrictions on Linux
- We have confirmed operation on Wine or Proton (Steam).
  (Please note that it is not native to Linux).
- Stopped the feature of automatically downloading and placing DivaModLoader at startup.
- Disabled the Update Core button functionality.
- Popup windows do not become active (may be made a specification).
- Files are generally moved to the trash when deleted.
  Temporary files are extracted into the DivaModManager/Downloads folder, so please be aware of available space.

----- Verified Environments -------------------------

### beta4
- Windows 11 Home 25H2
- Ubuntu 26.04 (on VMware Workstation) + Proton Experimental 11.0

