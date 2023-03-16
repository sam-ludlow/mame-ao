# mame-ao

Run MAME easily, just download this and run.

Automatically downloads all MAME binaries (from github.com) and ROMS (from archive.org) on the fly.

Built in web UI just click image and wait.

You can also use the command line just enter machine / software you are looking for the short name/code
- http://adb.arcadeitalia.net/lista_mame.php
- https://mame.spludlow.co.uk/

## Installation & Usage

- create an empty directory e.g. "C:\MameAO"
- download release ZIP from https://github.com/sam-ludlow/mame-ao/releases
- put ZIP in empty directory and extract
- double click "mame-ao.exe"
- wait for the MAME Shell to start
- use the Web UI it will pop up
- or use command line, enter a machine name and press enter e.g. "mrdo"

## Important notes

- The first time you run it will take a while and use a lot of RAM.
- Please be patient, subsequent runs will not.

## UI

- After mame-ao has fully started the web UI will apear in the default browser.
- You can access it at http://127.0.0.1:12380/.
- Just click on image to start the machine.
- If the machine has software it will be listed, click what you want to run.
- Click back on the mame-ao console window to see what it's doing

![MAME-AO UI](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/mame-ao-ui.png)

## System requirements

- Windows with .net framework 4.8
- 32 bit / 64 bit (application is 32 bit keeps RAM usage down)
- 2 Gb RAM free 

## Known issues

- Not all CHD SL (Software list) disks are available in the source & may contains incorect files (sha1 don't match) https://archive.org/download/mame-software-list-chds-2/
- Incorect files (sha1 don't match) in the source will allow you to download on every attempt. You will see a warning in the console for CHDs but not ROMs.
- UI could do with refinement.

## Symbolic Links - Save disk space

To be able to create symbolic links you have to grant permission.

- Run "gpedit"
- Go to "Computer Configuration\Windows Settings\Security Settings\Local Policies\User Rights Assignment\Create symbolic links"
- Add the required user or group. You will need to re-start for settings to take effect.

## Internal Workings

### Overall
Assets (ROMs & CHDs) are downloaded to a “Hash Store”. Uncompressed individual files are stored with a filename that is the SHA1 of the file. This is completely separate to the MAME rom directories. ZIP files are not used at all.

Each MAME version is kept completely isolated, when a new version of MAME is used a fresh MAME directory is created. Previous versions are left in place, you can go back to them anytime, let’s say you have some saved state (these often don’t work with different MAME versions).

When you select a machine MAME-AO will download the files from archive.org if they are not already in the Hash Store. When in the Hash Store the files are copied (or preferably linked if enabled) to the correct place in the MAME rom directory.

### Data
SQLite databases are generated from the MAME XML output, 2 databases machine & software. This uses quiet a bit of CPU & RAM but once done is quick to load next time. If MAME-AO or MAME have version bumps the database will be re-created.

### Sources
There are 4 types of source these each relate to a collection on archive.org.
- Machine ROM (version master)
- Machine DISK
- Software ROM
- Software DISK (not complete or updated as often)

Machine ROM is considered the version master, new MAME binaries will only be used that match the version in this archive.org collection.

Archive.org metadata for the collections are downloaded and cached to know what’s available and the file size.

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
