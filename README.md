# mame-ao

Run MAME as easily as posibble, just download this and run.

Automatically downloads all MAME binaries (from github.com) and ROMS (from archive.org) on the fly.

Hardest part is finding the machine name (the short name/code) find them here: http://adb.arcadeitalia.net/lista_mame.php

## Installation & Usage

- create an empty directory e.g. "C:\MameAO"
- copy 2 release files "mame-ao.exe" & "Newtonsoft.Json.dll" to the new directory
- double click "mame-ao.exe"
- wait for the MAME Shell to start
- enter a machine name and press enter e.g. "mrdo"

## Known issues

- Uses about 1.3 Gb RAM
- Not all CHD SL (Software list) disks are available in the source https://archive.org/download/mame-software-list-chds-2/

## Symbolic Links - Save disk space

To be able to create symbolic links you have to grant permission.

- Run "gpedit"
- Go to "Computer Configuration\Windows Settings\Security Settings\Local Policies\User Rights Assignment\Create symbolic links"
- Add the required user or group. You will need to re-start for settings to take effect.
