# mame-ao
Easiest way to use MAME & HBMAME. Automatic download and setup of all files from GitHub, BitTorrent, and archive.org on the fly.

![MAME-AO UI](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-ui.png)

## Installation & Usage
- create an empty directory e.g. `C:\MameAO`
- download latest release ZIP from https://github.com/sam-ludlow/mame-ao/releases/latest (Do not use `_x64`)
- put ZIP in empty directory and extract
- double click "mame-ao.exe"
- wait for the MAME-AO to start
- perform option 1 OR 2 below
- click image to start machine

NOTE: First time it has to extract MAME's data, this will take a moment, next time it will start quickly, version bumps in MAME or MAME-AO will trigger further data initializations.

## Virus Detected
This software can be falsely reported as malicious software. 

You have my word that it is Kosher. The binaries are built using GitHub Actions.

Your Web browser, Windows, and other security software may attempt to block downloading and running MAME-AO & DOME-BT, check the pop-ups carefully there will be an option somewhere to allow.

The command `.upany` after initial setup may help with Microsoft Defender notices.

NOTE: Some software list items are infected. MAME-AO will download them if asked, this may trigger alerts if the software is DOS/Windows based. This is not dangerous to your computer, only to the system being emulated within MAME.

## Enter your Archive.org credentials (Option 1)
If you intend on using BitTorrent you can skip this step, press `ENTER` twice. You can enter your credentials later with the command `.creds`.

You will need an archive.org account to use MAME-AO if not using BitTorrent. You should create one here https://archive.org/account/signup

![MAME-AO UI](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-archive.org-credentials.png)

## Enable BitTorrent (Option 2)
You can enable BitTorrent in the UI or use the command `.bt`. Once enabled BitTorrent will start automatically next time.

BitTorrent is recomended as it has the vary latest assets.

![MAME-AO Enable BitTorrent](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-enable-bit-torrent.png)

Bit Torrent is handled by a seperate process the first time in runs you will get a Windows Firewall message, you need to allow the `dome-bt.exe` process. More info here https://github.com/sam-ludlow/dome-bt

To remove BitTorrent use the UI or command `.btx` then use the command `.creds` to enter Archive.org credentials if you have not already.

![MAME-AO Disable BitTorrent](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-disable-bit-torrent.png)

## System requirements
- Windows 64-Bit with .net framework 4.8
- 2 Gb RAM free
- 2 Gb DISK free (absolute minimum)
- CPU with x86-64-v2 functionality (MAME > 0273)

NOTE: MAME-AO application is 32 bit to keep RAM usage down.
 
## Reporting issues
https://github.com/sam-ludlow/mame-ao/issues

## HBMAME - HomeBrew MAME
Enable HBMAME from the about page or use the command `.core hbmame`. Only works with BitTorrent, DOME-BT will restart to enable new core.

![MAME-AO About page Emulator Cores](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-about-emulator-cores.png)

![MAME-AO using HBMAME](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-hbmame.png)

## Symbolic Links - Save disk space
When MAME-AO downloads assets it keeps them in a "hash store", this makes keeping account of them very simple, you don’t have to keep grooming a bunch of ZIP files.

MAME needs the ROMs in the right place, so they need duplicating from the store. The best way to do this is use symbolic links, these use virtually zero disk space and point to the actual file in the store, otherwise MAME-AO will copy the files wasting diskspace.

### Enabling symbolic links in Windows
- Run `secpol` (Local Security Policy) as Administrator. Do a Windows search for `secpol` click `Run as administrator`.
- Go to `Local Policies\User Rights Assignment\Create symbolic links`
- Add the required user or group. Enter your username, click the `Check Names` button to check it is correct.
- You will need to re-start for settings to take effect.

TIP: Run `whoami` in Windows command prompt (Windows search `cmd`) to determine your exact user name.

## MAME Quick usage help
For more detail see the official docs. https://docs.mamedev.org/usingmame/usingmame.html

### MAME Keyboard controls
NOTE: Machines that emulate keyboards will take over, use `Scroll Lock` to toggle between standard MAME controls and full keyboard.

You should use a joystick but you will need a few keyboard commands. Full keyboard docs here https://docs.mamedev.org/usingmame/defaultkeys.html

![MAME-AO Real Man's Joystick](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-joystick.png)

|Key|Description|
|:----|:----|
| 5 6 7 8 | Coin Up Player 1-4 |
| 1 2 3 4 | Start Player 1-4 |
| Cursor keys | Player 1 Joystick |
| Left Ctrl | Player 1 Button 1 |
| Left Alt | Player 1 Button 2 |
| Space bar | Player 1 Button 3 |
| Tab | Configuration menu |
| F2 | Service Mode |
| F6 | Create Saved State |
| F7 | Load Saved State |
| F12 | Snap the screen |
| Escape | Exit MAME |
| Scroll Lock | Keyboard UI controls OR full keyboard |

## Settings - User Preferences
You can configure optional settings from the UI page http://localhost:12380/settings

![MAME-AO Settings](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-settings.png)

## Configuration
You can set advanced configuration options using the file `_config.txt`, each line should be `KEY	VALUE` (TAB separator).

| Name | Description | Default | Example |
| ------------- | ------------- | --- | --- |
| StorePathRom | Override default ROM Store directory | _STORE | `StorePathRom	E:\STORE_ROM` |
| StorePathDisk | Override default DISK Store directory | _STORE_DISK | `StorePathDisk	D:\STORE_DISK` |
| BitTorrentPath | Override default Bit Torrent directory | _BT | `BitTorrentPath	D:\DOME_BT` |
| MameArguments | Pass additional arguments to MAME |  | `MameArguments	-window` |
| MameVersion | Run MAME-AO on a fixed MAME version (for old CPUs use this example) | | `MameVersion	0273` |
| SoftwareListSkip | Skip these software lists when running `.fetch` command for software disks, comma delimited | | `SoftwareListSkip	psx, saturn, dc` |
| BitTorrentRestartMinutes | Minutes to wait until restarting DOME-BT if no asset data has downloaded | 5 | `BitTorrentRestartMinutes	2.5` |

MAME-AO must be restarted for changes to `_config.txt` to take affect.

## AO Shell
From the shell you can enter the short machine name, other options are available for software.

|Command|Description|Example|
|:----|:----|:----|
| \<machine\> | Start machine | `mrdo` |
| \<machine\>@\<core\> | Start machine in core (`mame`, `hbmame`) |`dinos163@hbmame`|
| \<machine\> \<arguments\> | With all place commands you can put arguments at the end | `mrdo -window` |
| \<machine\> \<software\> | Start machine with software | `a2600 et` |
| \<machine\> \<software\>@\<software list\> | Start machine with software, specify software list for correct media | `cpc464p barb2@gx4000` |

There are also commands available they all start with a dot `.`

|Command|Description|Example|
|:----|:----|:----|
|.|Run MAME directly without placing files, use it start MAME's built in UI or pass arguments to MAME|`. a2600 -cart et`|
|.0000|Run a previous version of MAME directly , without placing files, useful for going back to saved state from previous sessions |`.0123 gaunt2`|
|.accdb|Create MS Access databases (machine & software) linked to SQLite, usfull for looking at MAME-AO data|`.accdb`|
|.accdbxml|Create MS Access databases (machine & software) from XML, usfull for looking at complete MAME data|`.accdbxml`|
|.bt|Enable the bit torrent client|`.bt`|
|.btr|Restart the bit torrent client|`.btr`|
|.bts|Stop the bit torrent client|`.bts`|
|.btx|Remove the bit torrent client|`.btx`|
|.core|Change emulation core|`.core hbmame`|
|.creds|Enter archive.org credentials, If you press `ENTER` twice your auth cookie will be deleted.|`.creds`|
|.dbm|Machine database SQL query|`.dbm SELECT rom.* FROM machine INNER JOIN rom ON machine.machine_id = rom.machine_id WHERE machine.name = 'mrdo'`|
|.dbs|Software database SQL query|`.dbs SELECT softwarelist.* FROM softwarelist ORDER BY softwarelist.name`|
|.export|Run the export function, [see export section](#export)|`.export mr C:\EXPORT`|
|.fetch|Fetch all required assets, used for maintaining full sets|`.fetch sr`|
|.import|Run the import function, [see import section](#import)|`.import C:\IMPORT`|
|.list|List saved state and previous MAME versions|`.list`|
|.r|Reload `UI.html` & `_styles.css` usfull when developing the UI|`.r`|
|.readme|Show the mame-ao README on github.com|`.readme`|
|.report|Run reports, [see reports section](#reports)|`.report avsum`|
|.snap|Run the snapshot collection function, [see snapshots section](#snapshots)|`.snap C:\snaps`|
|.softname|Fetch & Export complete software list with friendly filenames, usefull for outside MAME|`.softname electron_cart C:\EXPORT`|
|.softnamed|Fetch & Export complete software list with friendly filenames in directories, usefull for outside MAME|`.softnamed c64_cart C:\EXPORT`|
|.software|Fetch & Place complete software list, you can have them all ready then load inside MAME.|`.software bbcb_flop`|
|.style|Write file `_styles.css` so you can modify UI stlyes, use command `.r` to refresh changes.|`.style`|
|.svg|Convert bitmaps to SVG, [see SVG section](#svg)|`.svg C:\snaps\file.png`|
|.test|Perform asset place tests|`.test everything 100`|
|.ui|Launch the UI in default browser|`.ui`|
|.up|Self update MAME-AO to the latest on GitHub|`.up`|
|.upany|Self update MAME-AO anyway even if up to date, this can be used to clear the Windows Defender warning on first install.|`.upany`|
|.valid|Validate the hash store, [see validate store section](#validate-store)|`.valid rom`|
|.what|View current MAME whatsnew.txt in default browser.|`.what`|

## Saved State and previous MAME versions
Saved state somtimes does not work between MAME versions. If you have started something with saved state you may as well use the same MAME version.

MAME-AO leaves MAME versions isolated in their own directory. You can list all saved state from previous and current MAME versions in the UI or with the command `.list`.

You can start a particular version of MAME with the command `.VVVV` where V is the version e.g. `.0252` or pass arguments e.g. `.0252 mrdo -window`.

MAME-AO only allows placing of assets in current MAME version. Any machine you ran before in previous MAME versions will already have all assets in place.

When starting previous MAME versions use the available filter in the MAME UI.

Sometimes a regression in MAME will break a machine in the current version, so if a machine doesn't work after updating MAME you can run a previous version.

## Import
If you have downloaded files elsewhere you can feed them into MAME-AO with the import function. MAME-AO will still place them as normal.

Use the following command to perform an import

`.import <directory>`

Files are imported based on their filename extension.
- .ZIP – Archives will be extracted and imported with the same rules, this is recursive (ZIPs in ZIPs... will also be imported).
- .CHD – Disk files, will have chdman.exe run against them to determine the SHA1.
- Everything else – Considered a ROM, a hash of the file to determine the SHA1.

Important notes on import
- Filenames are not important, except the extention, the file will be imported based on its SHA1.
- Files will not be imported if its SHA1 is not in the current MAME version.
- Only .ZIP archives will be extracted, other archive formats (.7z, .rar, ...) will be considered ROMs and not work. If you have these extract them manually to the import directory.

## Export
If you want to use MAME-AO's assets (ROMs & DISKs) anywhere else you probably aren't that happy with the way it stores files. Hash stores, no ZIPs, uncompressed ROMs or symbolic links, oh dear what am I supposed to do with those?

Well that's what the export command is for use it like so:

`.export <type> <directory>`

For example `.export mr C:\My MAME ROMs`

Type can be:
- MR : Machine ROM
- MD : Machine DISK
- SR : Software ROM
- SD : Software DISK
- \* : Everything

Everything it can will be exported to the specified directory, based on the current MAME version.
 
Machine ROMs will be in split-set format (separate parent ZIP & child diff ZIPs). If anything is missing the ZIP will not be created.

Machine DISKs that exist in a parent machine will not be exported, as the file would be duplicated.

## Validate Store
You can check the hash store is in good order using the following commands:

- `.valid rom` - Validate the ROM Hash Store, each file will be SHA1 hashed and compared to the filename.
- `.valid disk` - Validate the DISK Hash Store, each file will have the SHA1 verified with chdman.exe and compared to the filename. WARNING: This can take a while, each CHD will have its SHA1 calculated to verify it is correct.

If any issues are found a report will be produced, if all good then no report. Problem files in the report should be manually deleted.

You shouldn't need to do this, only if you are unsure about integrity, maybe after a power cut or recovering from a bad disk.

## Snapshots
Within MAME you can hit `F12` to take a snapshot of the screen these are saved within the MAME `snap` directory.

You can use the snap feature to collect all current MAME snaps moving them to a specified directory. Files are named like so:

`[mame machine].[mame version].[time stamp].[mame filename].png`

You can run from the UI or use the command:

`.snap <target directory>`

## Reports
Some MAME-AO functions will produce HTML reports, so you can see what it's been doing.

You can enable in settings for reporting on all downloaded and placed files.

There are several reports that you can run from the UI or with the command `.report <type>`

Use the `.report` command without any arguments to list available reports.

When reports have finished they will pop up in the browser, they are also saved in the `_REPORTS` directory.

## SVG
Convert bitmaps (from snapshots) into Scalable Vector Graphics format.

![MAME-AO SVG](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-svg.png)

You can run from the UI or use the command, single file or all files in a directory:

`.svg <filename or directory>`

## Phone Home
MAME-AO will send MAME usage data up to the mother ship.

By default, data is sent, if you do not want that you can switch it off.

Options available on the UI settings page
- `Yes` Send data
- `Yes Verbose` Send data and show payload in console.
- `No` Do not send data.

## Data Operations
MAME-AO has the capability to perform various Data operations by passing command line options when starting the program, it will exit immediately when finished.

WARNING: The payload operations use a lot of RAM (MAME 5GB, TOSEC 9GB) you must use the x64 build of MAME-AO and have the RAM free.

These are used by our sister project `Spludlow Data Web` located at https://data.spludlow.co.uk/

These may be used for generating data sets in various formats, you could use it for data processing pipelines or just for looking at the data.

![MAME-AO Data Operations](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-data-operations.png)

You can specify a specific version e.g.`version=0250` otherwise the latest will be used.

For `get` operations if a new version if found the exit code will be set to 1.

### MAME
|Operation|Description|Example|
|:----|:----|:----|
| mame-get | Download and extract MAME binaries. | `mame-ao.exe mame-get directory="C:\ao-data\mame"` |
| mame-xml | Extract XML from MAME. | `mame-ao.exe mame-xml directory="C:\ao-data\mame"` |
| mame-json | Convert XML to JSON. | `mame-ao.exe mame-json directory="C:\ao-data\mame"` |
| mame-sqlite | Convert XML to SQLite. | `mame-ao.exe mame-sqlite directory="C:\ao-data\mame"` |
| mame-msaccess | Convert XML to MS Access. | `mame-ao.exe mame-msaccess directory="C:\ao-data\mame"` |
| mame-zips | Compress things. | `mame-ao.exe mame-zips directory="C:\ao-data\mame"` |
| mame-mssql | Convert XML to Microsoft SQL. | `mame-ao.exe mame-mssql directory="C:\ao-data\mame" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-mame-machine, ao-mame-software"` |
| mame-mssql-payload | Create web payload tables. | `mame-ao.exe mame-mssql-payload directory="C:\ao-data\mame" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-mame-machine, ao-mame-software"` |

### HBMAME
|Operation|Description|Example|
|:----|:----|:----|
| hbmame-get | Download and extract HBMAME binaries. | `mame-ao.exe hbmame-get directory="C:\ao-data\hbmame"` |
| hbmame-xml | Extract XML from HBMAME. | `mame-ao.exe hbmame-xml directory="C:\ao-data\hbmame"` |
| hbmame-json | Convert XML to JSON. | `mame-ao.exe hbmame-json directory="C:\ao-data\hbmame"` |
| hbmame-sqlite | Convert XML to SQLite. | `mame-ao.exe hbmame-sqlite directory="C:\ao-data\hbmame"` |
| hbmame-msaccess | Convert XML to MS Access. | `mame-ao.exe hbmame-msaccess directory="C:\ao-data\mame"` |
| hbmame-zips | Compress things. | `mame-ao.exe hbmame-zips directory="C:\ao-data\mame"` |
| hbmame-mssql | Convert XML to Microsoft SQL. | `mame-ao.exe hbmame-mssql directory="C:\ao-data\hbmame" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-hbmame-machine, ao-hbmame-software"` |
| hbmame-mssql-payload | Create web payload tables. | `mame-ao.exe hbmame-mssql-payload directory="C:\ao-data\hbmame" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-hbmame-machine, ao-hbmame-software"` |

### FBNeo
|Operation|Description|Example|
|:----|:----|:----|
| fbneo-get | Download and extract FBNeo binaries. | `mame-ao.exe fbneo-get directory="C:\ao-data\fbneo"` |
| fbneo-xml | Extract XML from FBNeo. | `mame-ao.exe fbneo-xml directory="C:\ao-data\fbneo"` |
| fbneo-json | Convert XML to JSON. | `mame-ao.exe fbneo-json directory="C:\ao-data\fbneo"` |
| fbneo-sqlite | Convert XML to SQLite. | `mame-ao.exe fbneo-sqlite directory="C:\ao-data\fbneo"` |
| fbneo-msaccess | Convert XML to MS Access. | `mame-ao.exe fbneo-msaccess directory="C:\ao-data\mame"` |
| fbneo-zips | Compress things. | `mame-ao.exe fbneo-zips directory="C:\ao-data\mame"` |
| fbneo-mssql | Convert XML to Microsoft SQL. | `mame-ao.exe fbneo-mssql directory="C:\ao-data\fbneo" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-fbneo"` |
| fbneo-mssql-payload | Create web payload tables. | `mame-ao.exe fbneo-mssql-payload directory="C:\ao-data\fbneo" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-fbneo"` |

### TOSEC
|Operation|Description|Example|
|:----|:----|:----|
| tosec-get | Download TOSEC and extract XML. | `mame-ao.exe tosec-get directory="C:\ao-data\tosec"` |
| tosec-xml | Extract XML from FBNeo. | `mame-ao.exe tosec-xml directory="C:\ao-data\tosec"` |
| tosec-json | Convert XML to JSON. | `mame-ao.exe tosec-json directory="C:\ao-data\tosec"` |
| tosec-sqlite | Convert XML to SQLite. | `mame-ao.exe tosec-sqlite directory="C:\ao-data\tosec"` |
| tosec-msaccess | Convert XML to MS Access. | `mame-ao.exe tosec-msaccess directory="C:\ao-data\mame"` |
| tosec-zips | Compress things. | `mame-ao.exe tosec-zips directory="C:\ao-data\mame"` |
| tosec-mssql | Convert XML to Microsoft SQL. | `mame-ao.exe tosec-mssql directory="C:\ao-data\tosec" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-tosec"` |
| tosec-mssql-payload | Create web payload tables. | `mame-ao.exe tosec-mssql-payload directory="C:\ao-data\tosec" server="Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;" names="ao-tosec"` |

## Internal Workings

### Overall
Assets (ROMs & CHDs) are downloaded to a "Hash Store". Uncompressed individual files are stored with a filename that is the SHA1 of the file. This is completely separate to the MAME rom directories. ZIP files are not used at all.

Each MAME version is kept completely isolated, when a new version of MAME is used a fresh MAME directory is created. Previous versions are left in place, you can go back to them anytime, let’s say you have some saved state.

When you select a machine MAME-AO will download if they are not already in the Hash Store. When in the Hash Store the files are copied (or preferably linked if enabled) to the correct place in the MAME rom directory.

### Data
SQLite databases are generated from the MAME XML output, 2 databases machine & software. This uses quiet a bit of CPU & RAM but once done is quick to load next time. If MAME-AO or MAME have version bumps the database will be re-created.

### GitHub.com Repos
GitHub is used for downloading the MAME release binaries, self updating MAME-AO, and other datasets external to MAME (not in the built in XML)

#### Genres INI
https://raw.githubusercontent.com/AntoPISA/MAME_SupportFiles/main/catver.ini/catver.ini

#### Samples XML
https://raw.githubusercontent.com/AntoPISA/MAME_Dats/main/MAME_dat/MAME_Samples.dat

#### Artwork XML
https://raw.githubusercontent.com/AntoPISA/MAME_Dats/refs/heads/main/Resources/pS_Artwork_Official.dat
https://raw.githubusercontent.com/AntoPISA/MAME_Dats/refs/heads/main/Resources/pS_Artwork_Unofficial_Alternate.dat
https://raw.githubusercontent.com/AntoPISA/MAME_Dats/refs/heads/main/Resources/pS_Artwork_WideScreen.dat

See information on the GitHub Repos in use by MAME-AO by going to the About page. http://localhost:12380/about

![MAME-AO About - GitHub Repos](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-about-github-repos.png)

### BitTorrents
Using BitTorrent is recommended, whoever maintains it is doing an excellent job of keeping it up to date. You can run the latest machines as soon as MAME is released. Archive.org is still used for Artwork & Samples.

![MAME-AO About - DOME-BT Console](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/dome-bt-console.png)

DOME-BT is a separate process it acts like a normal BitTorrent client with its own directory to keep download files separate to MAME-AO.

You can clear down DOME-BT by disabling it and then reenabling it from MAME-AO. This will recover disk space. The whole `_BT` directory is deleted.

DOME-BT will automatically delete old MAME version torrent directories if they are not current. If you’re not too short on disk space, you can just leave it.

DOME-BT will only download files requested. Once downloaded they are available to share with other BitTorrent users so you will get uploads.

Please leave DOME-BT running, if possible, to "Kindly share with others what others so kindly shared with you!"

There is a small web running on http://localhost:12381/ describing the API endpoints used by MAME-AO if you are interested.

### Archive.org Items
Archive.org can be used for downloading MAME assets of these types:

- Machine ROM
- Machine DISK
- Software ROM
- Software DISK
- Support (Artwork & Samples)

See information on the Archive.org Items in use by MAME-AO by going to the About page. http://localhost:12380/about

![MAME-AO About - Archive.org Items](https://raw.githubusercontent.com/sam-ludlow/mame-ao/main/images/mame-ao-about-archive-org-items.png)

You can run `Source Exists` reports to see if the items are missing anything.

## Credits

### MAME
Multi-purpose emulation framework.

https://www.mamedev.org/

https://github.com/mamedev/mame

## HBMAME
Derivative of MAME containing various hacks and homebrews.

https://hbmame.1emulation.com/

https://github.com/Robbbert/hbmame

## FBNeo
Emulator for Arcade Games & Select Consoles.

https://neo-source.com/

https://github.com/finalburnneo/FBNeo

## TOSEC
Cataloguing and preservation of software and other computer resources.

https://www.tosecdev.org/

### Archive.org
Asset preservation

https://archive.org/

### Antonio Paradossi
Images

https://www.progettosnaps.net/snapshots/

https://github.com/AntoPISA/MAME_SnapTitles

Artwork & Samples

https://www.progettosnaps.net/artworks/

https://www.progettosnaps.net/samples/

https://github.com/AntoPISA/MAME_Dats

Genre Data

https://www.progettosnaps.net/catver/

https://github.com/AntoPISA/MAME_SupportFiles

### Pugsy
MAME Cheat files

https://www.mamecheat.co.uk/

### James Newton-King (Newtonsoft)
JSON Library

https://www.newtonsoft.com/json

### SQLite
SQL Database

https://www.sqlite.org/

### Jonathan Magnan (ZZZ Projects)
HTML Parsing (DOME-BT)

https://github.com/zzzprojects/html-agility-pack/

### Alan McGovern
BitTorrent Client Library (DOME-BT)

https://github.com/alanmcgovern/monotorrent

### Spludlow MAME
Image hosting

https://mame.spludlow.co.uk/
