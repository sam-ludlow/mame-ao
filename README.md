# mame-ao

Run MAME easily, just download this and run.

Automatically downloads all MAME binaries (from github.com) and ROMS (from archive.org) on the fly.

Built in web UI just click image.

Find other machines / software (not currently available in UI) you are looking for the short name/code
- http://adb.arcadeitalia.net/lista_mame.php
- https://mame.spludlow.co.uk/

## Installation & Usage

- create an empty directory e.g. "C:\MameAO"
- download release ZIP from https://github.com/sam-ludlow/mame-ao/releases
- put ZIP in empty directory and extract
- double click "mame-ao.exe"
- wait for the MAME Shell to start
- enter a machine name and press enter e.g. "mrdo", or use the UI.

## UI

After mame-ao has fully started browse to http://127.0.0.1:12380/. Just click on image.

Notes
- still work in progress
- not currently complete machine list
- images may be slow to load

## System requirements

- Windows with .net framework 4.8
- 32 bit / 64 bit (application is 32 bit keeps RAM usage down)
- 1.4 Gb RAM free 

## Known issues

- UI needs improvement
- Uses about 1.4 Gb RAM - TODO: Just use SQLite don't keep XML in memory
- Not all CHD SL (Software list) disks are available in the source https://archive.org/download/mame-software-list-chds-2/

## Symbolic Links - Save disk space

To be able to create symbolic links you have to grant permission.

- Run "gpedit"
- Go to "Computer Configuration\Windows Settings\Security Settings\Local Policies\User Rights Assignment\Create symbolic links"
- Add the required user or group. You will need to re-start for settings to take effect.

## Credits

### MAME
Emulator software
https://www.mamedev.org/

### Archive.org
ROM & DISK download
https://archive.org/

### Progetto Snaps
UI Images
https://www.progettosnaps.net/snapshots/

UI Genres
https://www.progettosnaps.net/catver/

### Newtonsoft
JSON Library
https://www.newtonsoft.com/json

### SQLite
SQL Database
https://www.sqlite.org/
