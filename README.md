# mame-ao

NOTE: This is early dev. Does not currently find all machines required, only works with parents without depenancies.

TODO: handle all roms depenancies, start mame automaiticaly, installer

Automatically download all MAME assetts.

- Get archive.org item, metadata from https://archive.org/details/mame-merged
- This will be the version used (it looks like it is kept upto date pretty well)
- Download MAME binaries from github
- extract machine XML
- determine roms required
- download from archive.org if required
- store all roms under SHA1 files names
- copy roms to mame roms directory


## Usage

- create an empty directory e.g. "C:\MameAO"
- copy "mame-ao.exe" and "Newtonsoft.Json.dll" to directory
- start shell (cmd is easier)
- cd "C:\MameAO"
- mame-ao.exe mrdo
- wait for it to finish
- dir (see the version directory)
- cd 0251 (or whatever the version is)
- mame.exe mrdo



