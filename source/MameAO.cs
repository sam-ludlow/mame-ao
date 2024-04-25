using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Data;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spludlow.MameAO
{
	public static class Globals
	{
		static Globals()
		{
			Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
			AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

			HttpClient = new HttpClient();
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");

			HttpClient.Timeout = TimeSpan.FromSeconds(180);
		}

		public static string ListenAddress = "http://localhost:12380/";

		public static string AssemblyVersion;

		public static HttpClient HttpClient;

		public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

		public static string RootDirectory;
		public static string MameDirectory;

		public static string MameVersion;

		public static bool LinkingEnabled = false;

		public static Dictionary<ItemType, ArchiveOrgItem[]> ArchiveOrgItems = new Dictionary<ItemType, ArchiveOrgItem[]>();
		public static Dictionary<string, GitHubRepo> GitHubRepos = new Dictionary<string, GitHubRepo>();

		public static HashStore RomHashStore;
		public static HashStore DiskHashStore;

		public static MameAOProcessor AO;

		public static Database Database;
		public static Reports Reports;
		public static MameChdMan MameChdMan;
		public static BadSources BadSources;
		public static Favorites Favorites;
		public static Export Export;
		public static Genre Genre;
		public static Samples Samples;
		public static WebServer WebServer;
	}

	public class TaskInfo
	{
		public string Command = "";
		public long BytesCurrent = 0;
		public long BytesTotal = 0;
	}

	public class MameAOProcessor
	{
		private Task _RunTask = null;
		public TaskInfo _TaskInfo = new TaskInfo();

		private string _DownloadTempDirectory;

		private readonly long _DownloadDotSize = 1024 * 1024;

		private IntPtr _ConsoleHandle;

		private string WelcomeText = @"@VERSION
'##::::'##::::'###::::'##::::'##:'########:::::::'###:::::'#######::
 ###::'###:::'## ##::: ###::'###: ##.....:::::::'## ##:::'##.... ##:
 ####'####::'##:. ##:: ####'####: ##:::::::::::'##:. ##:: ##:::: ##:
 ## ### ##:'##:::. ##: ## ### ##: ######::::::'##:::. ##: ##:::: ##:
 ##. #: ##: #########: ##. #: ##: ##...::::::: #########: ##:::: ##:
 ##:.:: ##: ##.... ##: ##:.:: ##: ##:::::::::: ##.... ##: ##:::: ##:
 ##:::: ##: ##:::: ##: ##:::: ##: ########:::: ##:::: ##:. #######::
..:::::..::..:::::..::..:::::..::........:::::..:::::..:::.......:::

       Please wait the first time it has to prepare the data

         The Web User Interface will pop up when ready

              See the README for more information
             https://github.com/sam-ludlow/mame-ao

";

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
		static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);

		public MameAOProcessor()
		{
			Globals.RootDirectory = Globals.Arguments["DIRECTORY"];
		}

		public void Run()
		{
			try
			{
				Initialize();
				Shell();
			}
			catch (Exception e)
			{
				Tools.ReportError(e, "FATAL ERROR", true);
			}
		}

		public void BringToFront()
		{
			if (_ConsoleHandle == IntPtr.Zero)
				Console.WriteLine("!!! Wanring can't get handle on Console Window.");
			else
				SetForegroundWindow(_ConsoleHandle);
		}

		public void Initialize()
		{
			Console.Title = $"MAME-AO {Globals.AssemblyVersion}";

			Console.Write(WelcomeText.Replace("@VERSION", Globals.AssemblyVersion));
			Tools.ConsoleHeading(1, "Initializing");

			Globals.AO = this;

			//
			// Symbolic Links check
			//

			string linkFilename = Path.Combine(Globals.RootDirectory, @"_LINK_TEST.txt");
			string targetFilename = Path.Combine(Globals.RootDirectory, @"_TARGET_TEST.txt");

			File.Delete(linkFilename);
			File.WriteAllText(targetFilename, "TEST");

			Tools.LinkFiles(new string[][] { new string[] { linkFilename, targetFilename } });

			Globals.LinkingEnabled = File.Exists(linkFilename);

			File.Delete(linkFilename);
			File.Delete(targetFilename);

			if (Globals.LinkingEnabled == false)
				Console.WriteLine("!!! You can save a lot of disk space by enabling symbolic links, see the README.");

			//
			// Archive.Org Items
			//

			// Machine ROM
			Globals.ArchiveOrgItems.Add(ItemType.MachineRom, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-merged", "mame-merged/", null, new int[] { 5, 5 }),
			});

			// Machine DISK
			Globals.ArchiveOrgItems.Add(ItemType.MachineDisk, new ArchiveOrgItem[] {
				new ArchiveOrgItem("MAME_0.225_CHDs_merged", null, null, new int[] { 5, 5 }),
				new ArchiveOrgItem("mame-chds-roms-extras-complete", null, null, new int[] { 5, 5 }),
			});

			// Software ROM
			Globals.ArchiveOrgItems.Add(ItemType.SoftwareRom, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-sl", "mame-sl/", null, new int[] { 8, 5 }),
			});

			// Software DISK
			List<ArchiveOrgItem> items = new List<ArchiveOrgItem>();
			string[] tuffyTDogSoftwareLists = new string[] { "3do_m2", "abc1600_hdd", "abc800_hdd", "amiga_hdd", "amiga_workbench", "archimedes_hdd", "bbc_hdd", "cd32", "cdi", "cdtv", "dc", "fmtowns_cd", "gtfore", "hp9k3xx_cdrom", "hp9k3xx_hdd", "hyperscan", "ibm5150_hdd", "ibm5170_cdrom", "ibm5170_hdd", "interpro", "jazz", "kpython2", "mac_cdrom", "mac_hdd", "megacd", "megacdj", "mtx_hdd", "neocd", "next_cdrom", "next_hdd", "nuon", "pc1512_hdd", "pc1640_hdd", "pc8801_cdrom", "pc98_cd", "pcecd", "pcfx", "pet_hdd", "pico", "pippin", "psx", "saturn", "segacd", "sgi_mips", "sgi_mips_hdd", "snes_vkun", "softbox", "v1050_hdd", "vis", "vsmile_cd" };
			foreach (string softwareList in tuffyTDogSoftwareLists)
			{
				string key = $"mame-sl-chd-{softwareList}";
				items.Add(new ArchiveOrgItem(key, null, softwareList, null));
			}
			items.Add(new ArchiveOrgItem("mame-software-list-chds-2", null, "*", null));

			Globals.ArchiveOrgItems.Add(ItemType.SoftwareDisk, items.ToArray());

			// Support (Artwork & Samples)
			Globals.ArchiveOrgItems.Add(ItemType.Support, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-support", "Support/", null, null),
			});

			//
			// Determine MAME version
			//

			ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineRom][0];
			item.GetFile(null);
			Globals.MameVersion = item.Version;

			if (Globals.MameVersion == null)
				Globals.MameVersion = Mame.LatestLocal();

			if (Globals.MameVersion == null)
				throw new ApplicationException("Unable to determine MAME Version.");

			Globals.MameVersion = Globals.MameVersion.Replace(".", "");

			Globals.MameDirectory = Path.Combine(Globals.RootDirectory, Globals.MameVersion);

			if (Directory.Exists(Globals.MameDirectory) == false)
			{
				Console.WriteLine($"!!! MAME Version Bump: {Globals.MameVersion}");
				Directory.CreateDirectory(Globals.MameDirectory);
			}

			Console.WriteLine($"MameVersion: {Globals.MameVersion}");

			//
			// GitHub Repos
			//

			Globals.GitHubRepos.Add("mame-ao", new GitHubRepo("sam-ludlow", "mame-ao"));

			Globals.GitHubRepos.Add("mame", new GitHubRepo("mamedev", "mame"));

			Globals.GitHubRepos.Add("MAME_Dats", new GitHubRepo("AntoPISA", "MAME_Dats"));
			//	https://raw.githubusercontent.com/AntoPISA/MAME_Dats/main/MAME_dat/MAME_Samples.dat
			//	Hopfully will get Artwork soon?	<driver  requiresartwork="yes"/>

			//Globals.GitHubRepos.Add("MAME_SnapTitles", new GitHubRepo("AntoPISA", "MAME_SnapTitles"));
			////	https://raw.githubusercontent.com/AntoPISA/MAME_SnapTitles/main/snap/005.png

			Globals.GitHubRepos.Add("MAME_SupportFiles", new GitHubRepo("AntoPISA", "MAME_SupportFiles"));
			//	https://raw.githubusercontent.com/AntoPISA/MAME_SupportFiles/main/catver.ini/catver.ini


			//
			// Bits & Bobs
			//

			Globals.Reports = new Reports();
			Globals.BadSources = new BadSources();
			Globals.Favorites = new Favorites();

			_ConsoleHandle = FindWindowByCaption(IntPtr.Zero, Console.Title);

			//
			// MAME Binaries
			//

			string binUrl = Globals.GitHubRepos["mame"].UrlDetails + "/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";
			binUrl = binUrl.Replace("@VERSION@", Globals.MameVersion);

			Tools.ConsoleHeading(2, new string[] {
				"MAME",
				binUrl,
			});

			string binCacheFilename = Path.Combine(Globals.MameDirectory, "_" + Path.GetFileName(binUrl));

			string binFilename = Path.Combine(Globals.MameDirectory, "mame.exe");

			if (Directory.Exists(Globals.MameDirectory) == false)
			{
				Console.WriteLine($"New MAME version: {Globals.MameVersion}");
				Directory.CreateDirectory(Globals.MameDirectory);
			}

			if (File.Exists(binCacheFilename) == false)
			{
				Console.Write($"Downloading MAME binaries {binUrl} ...");
				Tools.Download(binUrl, binCacheFilename, _DownloadDotSize, 10);
				Console.WriteLine("...done.");
			}

			if (File.Exists(binFilename) == false)
			{
				Console.Write($"Extracting MAME binaries {binFilename} ...");
				Mame.RunSelfExtract(binCacheFilename);
				Console.WriteLine("...done.");
			}

			//
			// CHD Manager
			//

			Globals.MameChdMan = new MameChdMan();

			//
			// Hash Stores
			//

			string directory = Path.Combine(Globals.RootDirectory, "_STORE");
			Directory.CreateDirectory(directory);
			Globals.RomHashStore = new HashStore(directory, Tools.SHA1HexFile);

			directory = Path.Combine(Globals.RootDirectory, "_STORE_DISK");
			Directory.CreateDirectory(directory);
			Globals.DiskHashStore = new HashStore(directory, Globals.MameChdMan.Hash);

			directory = Path.Combine(Globals.RootDirectory, "_TEMP");
			Directory.CreateDirectory(directory);
			_DownloadTempDirectory = directory;

			//
			// Database
			//

			Globals.Database = new Database();

			//
			// MAME Machine XML & SQL
			//

			string machineXmlFilename = Path.Combine(Globals.MameDirectory, "_machine.xml");

			if (File.Exists(machineXmlFilename) == false)
			{
				Console.Write($"Extracting MAME machine XML {machineXmlFilename} ...");
				Mame.ExtractXML(binFilename, machineXmlFilename, "-listxml");
				Console.WriteLine("...done.");
			}

			string machineDatabaseFilename = Path.Combine(Globals.MameDirectory, "_machine.sqlite");

			Globals.Database.InitializeMachine(machineXmlFilename, machineDatabaseFilename, Globals.AssemblyVersion);

			GC.Collect();

			//
			// MAME Software XML & SQL
			//

			string softwareXmlFilename = Path.Combine(Globals.MameDirectory, "_software.xml");

			if (File.Exists(softwareXmlFilename) == false)
			{
				Console.Write($"Extracting MAME software XML {softwareXmlFilename} ...");
				Mame.ExtractXML(binFilename, softwareXmlFilename, "-listsoftware");
				Console.WriteLine("...done.");
			}

			string softwareDatabaseFilename = Path.Combine(Globals.MameDirectory, "_software.sqlite");

			Globals.Database.InitializeSoftware(softwareXmlFilename, softwareDatabaseFilename, Globals.AssemblyVersion);

			GC.Collect();

			//
			// Export
			//

			Globals.Export = new Export();

			//
			// Genre
			//

			Globals.Genre = new Genre();
			Globals.Genre.Initialize();

			//
			// Samples
			//
			Globals.Samples = new Samples();
			Globals.Samples.Initialize();

			//
			// New version Check
			//

			string tag_name = Globals.GitHubRepos["mame-ao"].tag_name;
			if (Globals.AssemblyVersion != tag_name)
				Tools.ConsoleHeading(1, new string[] {
					"New MAME-AO version available",
					"",
					$"{Globals.AssemblyVersion} => {tag_name}",
					"",
					"Automatically update with shell command \".up\".",
				});

			Console.WriteLine("");
		}

		public void Shell()
		{
			Globals.WebServer = new WebServer();
			Globals.WebServer.StartListener();

			Tools.ConsoleHeading(1, new string[] {
				"Remote Listener ready for commands",
				Globals.ListenAddress,
				$"e.g. {Globals.ListenAddress}api/command?line=a2600 et -window"

			});
			Console.WriteLine("");

			Process.Start(Globals.ListenAddress);

			Tools.ConsoleHeading(1, "Shell ready for commands");
			Console.WriteLine("");

			while (true)
			{
				Console.Write($"MAME Shell ({Globals.MameVersion})> ");
				string line = Console.ReadLine();
				line = line.Trim();

				if (line.Length == 0)
					continue;

				if (RunLineTask(line) == true)
					_RunTask.Wait();
				else
					Console.WriteLine("BUSY!");
			}
		}

		private HashSet<string> _DontBringToFrontCommands = new HashSet<string>(new string[] { ".favm", ".favmx", ".favs", ".favsx" });

		public bool RunLineTask(string line)
		{
			if (_RunTask != null && _RunTask.Status != TaskStatus.RanToCompletion)
				return false;

			if (line.Length == 0 || _DontBringToFrontCommands.Contains(line.Split(new char[] { ' ' })[0]) == false)
				BringToFront();

			_RunTask = new Task(() => {
				try
				{
					RunLine(line);
				}
				catch (ApplicationException e)
				{
					Console.WriteLine();
					Console.WriteLine("!!! ERROR: " + e.Message);
					Console.WriteLine();
				}
				catch (Exception e)
				{
					Tools.ReportError(e, "WORKER ERROR", false);
				}
				finally
				{
					lock (_TaskInfo)
					{
						_TaskInfo.Command = "";
						_TaskInfo.BytesCurrent = 0;
						_TaskInfo.BytesTotal = 0;
					}
				}
			});

			lock (_TaskInfo)
			{
				_TaskInfo.Command = line;
				_TaskInfo.BytesCurrent = 0;
				_TaskInfo.BytesTotal = 0;
			}

			_RunTask.Start();

			return true;
		}

		public void RunLine(string line)
		{
			string binFilename = Path.Combine(Globals.MameDirectory, "mame.exe");

			string machine;
			string software = "";
			string arguments = "";

			string[] parts = line.Split(new char[] { ' ' });

			machine = parts[0];

			if (machine.StartsWith(".") == true)
			{
				switch (machine)
				{
					case ".":
						if (parts.Length > 1)
							arguments = String.Join(" ", parts.Skip(1));
						break;

					case ".list":
						ListSavedState();
						return;

					case ".import":
						if (parts.Length < 2)
							throw new ApplicationException($"Usage: {parts[0]} <source directory>");

						arguments = String.Join(" ", parts.Skip(1));

						ImportDirectory(arguments);
						return;

					case ".up":
						Update(0);
						return;

					case ".upany":  //	for testing update works
						Update(-1);
						return;

					case ".favm":
					case ".favmx":
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <Machine Name>");

						machine = parts[1].ToLower();
						
						ValidateFavorite(machine, null, null);

						if (parts[0].EndsWith("x") == true)
							Globals.Favorites.RemoveMachine(machine);
						else
							Globals.Favorites.AddMachine(machine);

						return;

					case ".favs":
					case ".favsx":
						if (parts.Length != 4)
							throw new ApplicationException($"Usage: {parts[0]} <Machine Name> <List Name> <Software Name>");

						machine = parts[1].ToLower();
						string list = parts[2].ToLower();
						software = parts[3].ToLower();

						ValidateFavorite(machine, list, software);

						if (parts[0].EndsWith("x") == true)
							Globals.Favorites.RemoveSoftware(machine, list, software);
						else
							Globals.Favorites.AddSoftware(machine, list, software);

						return;

					case ".export":
						if (parts.Length < 3)
							throw new ApplicationException($"Usage: {parts[0]} <type: MR, MD, SR, SD, *> <target directory>");

						arguments = String.Join(" ", parts.Skip(2));

						if (Directory.Exists(arguments) == false)
							throw new ApplicationException($"Export directory does not exist: \"{arguments}\".");

						switch (parts[1].ToUpper())
						{
							case "MR":
								Globals.Export.MachineRoms(arguments);
								break;
							case "MD":
								Globals.Export.MachineDisks(arguments);
								break;
							case "SR":
								Globals.Export.SoftwareRoms(arguments);
								break;
							case "SD":
								Globals.Export.SoftwareDisks(arguments);
								break;

							case "*":
								Globals.Export.MachineRoms(arguments);
								Globals.Export.MachineDisks(arguments);
								Globals.Export.SoftwareRoms(arguments);
								Globals.Export.SoftwareDisks(arguments);
								break;

							default:
								throw new ApplicationException("Export Unknown type not (MR, MD, SR, SD, *).");

						}
						return;

					case ".report":
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <Report Code>" + Environment.NewLine + Environment.NewLine +
								String.Join(Environment.NewLine, Globals.Reports.ReportTypeText()) + Environment.NewLine
								);

						Reports.ReportContext reportContext = new Reports.ReportContext()
						{
							database = Globals.Database,
							romHashStore = Globals.RomHashStore,
							diskHashStore = Globals.DiskHashStore,
							versionDirectory = Globals.MameDirectory,
						};

						if (Globals.Reports.RunReport(parts[1], reportContext) == false)
							throw new ApplicationException("Report Unknown type.");
						return;

					case ".snap":
						if (parts.Length < 2)
							throw new ApplicationException($"Usage: {parts[0]} <target directory>");

						Mame.CollectSnaps(Globals.RootDirectory, String.Join(" ", parts.Skip(1)), Globals.Reports);
						return;

					case ".svg":
						if (parts.Length < 2)
							throw new ApplicationException($"Usage: {parts[0]} <filename or directory>");

						Tools.Bitmap2SVG(String.Join(" ", parts.Skip(1)));
						return;

					case ".ui":
						Process.Start(Globals.ListenAddress);
						return;

					case ".r":
						Globals.WebServer.RefreshAssets();
						return;

					case ".readme":
						Process.Start("https://github.com/sam-ludlow/mame-ao#mame-ao");
						return;

					case ".valid":
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <rom, disk, diskv>");

						switch (parts[1].ToUpper())
						{
							case "ROM":
								HashStore.ValidateHashStore(Globals.RomHashStore, "ROM", Globals.Reports, null);
								break;

							case "DISK":
								HashStore.ValidateHashStore(Globals.DiskHashStore, "DISK", Globals.Reports, null);
								break;

							case "DISKV":
								HashStore.ValidateHashStore(Globals.DiskHashStore, "DISK", Globals.Reports, Globals.MameChdMan);
								break;

							default:
								throw new ApplicationException("Valid Unknown store type (row, disk, diskv).");
						}
						return;

					case ".what":
						Process.Start(Globals.ListenAddress + "api/what");
						return;

					default:
						binFilename = Path.Combine(Globals.RootDirectory, machine.Substring(1), "mame.exe");

						if (File.Exists(binFilename) == false)
							throw new ApplicationException($"Unknown command: {machine}");

						machine = ".";

						if (parts.Length > 1)
							arguments = String.Join(" ", parts.Skip(1));
						break;
				}
			}
			else
			{
				if (parts.Length >= 2)
				{
					if (parts[1].StartsWith("-") == false)
					{
						software = parts[1];

						if (parts.Length > 2)
							arguments = String.Join(" ", parts.Skip(2));
					}
					else
					{
						arguments = String.Join(" ", parts.Skip(1));
					}
				}
			}

			machine = machine.ToLower().Trim();
			software = software.ToLower().Trim();

			if (machine.StartsWith(".") == true)
			{
				Mame.RunMame(binFilename, arguments);
			}
			else
			{
				GetRoms(machine, software);
				Mame.RunMame(binFilename, machine + " " + software + " " + arguments);
			}
		}

		private void ValidateFavorite(string machine, string list, string software)
		{
			DataRow machineRow = Globals.Database.GetMachine(machine);
			if (machineRow == null)
				throw new ApplicationException($"Machine not found: {machine}.");

			if (list == null)
				return;

			DataRow machineListRow = null;
			foreach (DataRow row in Globals.Database.GetMachineSoftwareLists(machineRow))
			{
				if (list == (string)row["name"])
					machineListRow = row;
			}

			if (machineListRow == null)
				throw new ApplicationException($"Machine does not have that software list: {machine}, {list}");

			DataRow softwareListRow = Globals.Database.GetSoftwareList(list);

			if (softwareListRow == null)
				throw new ApplicationException($"Software list not found: {list}");

			DataRow softwareRow = null;
			foreach (DataRow row in Globals.Database.GetSoftwareListsSoftware(softwareListRow))
			{
				if (software == (string)row["name"])
					softwareRow = row;
			}

			if (softwareRow == null)
				throw new ApplicationException($"Software not found in software list: {list}, {software}");
		}

		public void ListSavedState()
		{
			Tools.ConsoleHeading(2, "Saved Games");

			DataTable table = Mame.ListSavedState(Globals.RootDirectory, Globals.Database);

			StringBuilder line = new StringBuilder();

			foreach (DataColumn column in table.Columns)
			{
				if (line.Length > 0)
					line.Append("\t");
				line.Append(column.ColumnName);
			}
			Console.WriteLine(line.ToString());

			foreach (DataRow row in table.Rows)
			{
				line.Length = 0;

				foreach (DataColumn column in table.Columns)
				{
					if (line.Length > 0)
						line.Append("\t");
					line.Append(row[column.ColumnName]);
				}
				Console.WriteLine(line.ToString());
			}
			Console.WriteLine();
		}

		public void Update(int startingPid)
		{
			string updateDirectory = Path.Combine(Globals.RootDirectory, "_TEMP", "UPDATE");

			if (startingPid <= 0)
			{
				GitHubRepo repo = Globals.GitHubRepos["mame-ao"];

				if (repo.tag_name == Globals.AssemblyVersion)
				{
					Console.WriteLine($"MAME-AO is already up to date '{Globals.AssemblyVersion}'.");
					if (startingPid == 0)
						return;
				}

				Console.WriteLine($"Updating MAME-AO '{Globals.AssemblyVersion}' => '{repo.tag_name}'...");

				string archiveUrl = repo.Assets[repo.Assets.First().Key];
				string archiveFilename = Path.Combine(Globals.RootDirectory, $"mame-ao-{repo.tag_name}.zip");

				Tools.Download(archiveUrl, archiveFilename, 0, 5);

				if (Directory.Exists(updateDirectory) == true)
				{
					try
					{
						Directory.Delete(updateDirectory, true);
					}
					catch (UnauthorizedAccessException e)
					{
						throw new ApplicationException("Looks like an update is currently running, please kill all mame-ao processes and try again, " + e.Message, e);
					}
				}

				ZipFile.ExtractToDirectory(archiveFilename, updateDirectory);

				int pid = Process.GetCurrentProcess().Id;

				ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(updateDirectory, "mame-ao.exe"))
				{
					WorkingDirectory = Globals.RootDirectory,
					Arguments = $"UPDATE={pid} DIRECTORY=\"{Globals.RootDirectory}\"",
					UseShellExecute = true,
				};

				Console.Write("Starting update process...");

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;
					process.Start();

					if (process.HasExited == true)
						throw new ApplicationException($"Update process exited imediatly after starting {process.ExitCode}.");
				}

				Console.WriteLine("...done");
				Console.WriteLine("Exiting this process...");
				Thread.Sleep(1000);
				Environment.Exit(0);
			}
			else
			{
				try
				{
					UpdateChild(updateDirectory, startingPid);
				}
				catch (Exception e)
				{
					Tools.ReportError(e, "UPDATE FATAL ERROR", true);
				}
			}
		}

		public void UpdateChild(string updateDirectory, int startingPid)
		{
			Console.WriteLine($"MAME-AO UPDATER {Globals.AssemblyVersion}");
			Console.WriteLine($"Target Directory: {Globals.RootDirectory}, Update From Directory {updateDirectory}.");

			Console.WriteLine("Waiting for starting process to exit...");
			using (Process startingProcess = Process.GetProcessById(startingPid))
			{
				startingProcess.WaitForExit();
			}
			Console.WriteLine("...done");

			try
			{
				File.Delete(Path.Combine(Globals.RootDirectory, "mame-ao.exe"));
			}
			catch (UnauthorizedAccessException e)
			{
				throw new ApplicationException("Looks like the starting process is currently running, please kill all mame-ao processes and try again, " + e.Message, e);
			}

			foreach (string sourceFilename in Directory.GetFiles(updateDirectory))
			{
				string targetFilename = Path.Combine(Globals.RootDirectory, Path.GetFileName(sourceFilename));

				File.Copy(sourceFilename, targetFilename, true);

				Console.WriteLine(targetFilename);
			}

			ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(Globals.RootDirectory, "mame-ao.exe"))
			{
				WorkingDirectory = Globals.RootDirectory,
				UseShellExecute = true,
			};

			Console.Write("Starting updated mame-ao process...");
			Thread.Sleep(1000);

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;
				process.Start();

				if (process.HasExited == true)
					throw new ApplicationException($"New mame-ao process exited imediatly after starting {process.ExitCode}.");
			}

			Console.WriteLine("...done");
			Console.WriteLine("Exiting this process...");
			Thread.Sleep(1000);
			Environment.Exit(0);
		}

		public void GetRoms(string machineName, string softwareName)
		{
			Tools.ConsoleHeading(1, "Asset Acquisition");
			Console.WriteLine();

			//
			// Machine
			//
			DataRow machine = Globals.Database.GetMachine(machineName);
			if (machine == null)
				throw new ApplicationException($"Machine not found: {machineName}");

			DataRow[] softwarelists = Globals.Database.GetMachineSoftwareLists(machine);

			List<string[]> romStoreFilenames = new List<string[]>();

			int missingCount = 0;

			//
			// Machine ROMs
			//
			missingCount += GetRomsMachine(machineName, romStoreFilenames);

			//
			// Machine Disks
			//
			missingCount += GetDisksMachine(machineName, romStoreFilenames);

			//
			// Software ROMSs & Disks
			//
			if (softwareName != "")
			{
				int softwareFound = 0;

				HashSet<string> requiredSoftwareNames = new HashSet<string>();
				requiredSoftwareNames.Add(softwareName);

				foreach (DataRow machineSoftwarelist in softwarelists)
				{
					string softwarelistName = (string)machineSoftwarelist["name"];

					DataRow softwarelist = Globals.Database.GetSoftwareList(softwarelistName);

					if (softwarelist == null)
					{
						Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
						continue;
					}

					foreach (DataRow findSoftware in Globals.Database.GetSoftwareListsSoftware(softwarelist))
					{
						if ((string)findSoftware["name"] == softwareName)
						{
							// Does this need to be recursive ?
							foreach (DataRow sharedFeat in Globals.Database.GetSoftwareSharedFeats(findSoftware))
							{
								if ((string)sharedFeat["name"] == "requirement")
								{
									// some don't have 2 parts
									string[] valueParts = ((string)sharedFeat["value"]).Split(new char[] { ':' });
									string value = valueParts[valueParts.Length - 1];

									requiredSoftwareNames.Add(value);
								}
							}
						}
					}
				}

				foreach (string requiredSoftwareName in requiredSoftwareNames)
				{
					foreach (DataRow machineSoftwarelist in softwarelists)
					{
						string softwarelistName = (string)machineSoftwarelist["name"];

						DataRow softwarelist = Globals.Database.GetSoftwareList(softwarelistName);

						if (softwarelist == null)
						{
							Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
							continue;
						}

						foreach (DataRow findSoftware in Globals.Database.GetSoftwareListsSoftware(softwarelist))
						{
							if ((string)findSoftware["name"] == requiredSoftwareName)
							{
								missingCount += GetRomsSoftware(softwarelist, findSoftware, romStoreFilenames);

								missingCount += GetDiskSoftware(softwarelist, findSoftware, romStoreFilenames);

								++softwareFound;
							}
						}
					}
				}

				if (softwareFound == 0)
					throw new ApplicationException($"Did not find software: {machineName}, {softwareName}");

				if (softwareFound > 1)
				{
					Console.WriteLine();
					Console.WriteLine("!!! Warning more than one software found, not sure which MAME will use. This can happern if the same name apears in different lists e.g. disk & cassette.");
					Console.WriteLine();
				}
			}

			//
			// Place ROMs
			//
			if (Globals.LinkingEnabled == true)
			{
				Tools.LinkFiles(romStoreFilenames.ToArray());
			}
			else
			{
				foreach (string[] romStoreFilename in romStoreFilenames)
					File.Copy(romStoreFilename[1], romStoreFilename[0], true);
			}

			Console.WriteLine();

			//
			// Samples
			//

			Globals.Samples.PlaceSamples(machine);

			//
			// Info
			//
			Tools.ConsoleHeading(1, new string[] {
				"Machine Information",
				"",
				missingCount == 0 ? "Everything looks good to run MAME" : "!!! Missing ROM & Disk files. I doubt MAME will run !!!",
			});
			Console.WriteLine();

			DataRow[] features = Globals.Database.GetMachineFeatures(machine);

			Console.WriteLine($"Name:           {Tools.DataRowValue(machine, "name")}");
			Console.WriteLine($"Description:    {Tools.DataRowValue(machine, "description")}");
			Console.WriteLine($"Year:           {Tools.DataRowValue(machine, "year")}");
			Console.WriteLine($"Manufacturer:   {Tools.DataRowValue(machine, "manufacturer")}");
			Console.WriteLine($"Status:         {Tools.DataRowValue(machine, "ao_driver_status")}");
			Console.WriteLine($"isbios:         {Tools.DataRowValue(machine, "isbios")}");
			Console.WriteLine($"isdevice:       {Tools.DataRowValue(machine, "isdevice")}");
			Console.WriteLine($"ismechanical:   {Tools.DataRowValue(machine, "ismechanical")}");
			Console.WriteLine($"runnable:       {Tools.DataRowValue(machine, "runnable")}");

			foreach (DataRow feature in features)
				Console.WriteLine($"Feature issue: {Tools.DataRowValue(feature, "type")} {Tools.DataRowValue(feature, "status")} {Tools.DataRowValue(feature, "overall")}");

			foreach (DataRow softwarelist in softwarelists)
				Console.WriteLine($"Software list: {Tools.DataRowValue(softwarelist, "name")}");

			Console.WriteLine();
		}

		private int GetRomsMachine(string machineName, List<string[]> romStoreFilenames)
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineRom][0];	//	TODO: Support many

			//
			// Related/Required machines (parent/bios/devices)
			//
			HashSet<string> requiredMachines = new HashSet<string>();

			FindAllMachines(machineName, requiredMachines);

			if (requiredMachines.Count == 0)
				return 0;

			Tools.ConsoleHeading(2, new string[] {
				"Machine ROM",
				$"{machineName}",
				$"required machines: {String.Join(", ", requiredMachines.ToArray())}",
			});

			foreach (string requiredMachineName in requiredMachines)
			{
				DataRow requiredMachine = Globals.Database.GetMachine(requiredMachineName);
				if (requiredMachine == null)
					throw new ApplicationException("requiredMachine not found: " + requiredMachineName);

				//
				// See if ROMS are in the hash store
				//
				HashSet<string> missingRoms = new HashSet<string>();

				foreach (DataRow romRow in Globals.Database.GetMachineRoms(requiredMachine))
				{
					string name = Tools.DataRowValue(romRow, "name");
					string sha1 = Tools.DataRowValue(romRow, "sha1");

					if (name == null || sha1 == null)
						continue;

					bool inStore = Globals.RomHashStore.Exists(sha1);

					Console.WriteLine($"Checking machine ROM: {inStore}\t{sha1}\t{requiredMachineName}\t{name}");

					if (inStore == false)
						missingRoms.Add(sha1);
				}

				//
				// If not then download and import into hash store
				//
				if (missingRoms.Count > 0)
				{
					ArchiveOrgFile file = sourceItem.GetFile(requiredMachineName);
					if (file != null)
						ImportRoms(sourceItem.DownloadLink(file), $"machine rom: '{requiredMachineName}'", file.size, missingRoms.ToArray());
				}
			}

			//
			// Check and place ROMs
			//
			int missingCount = 0;

			foreach (string requiredMachineName in requiredMachines)
			{
				DataRow requiredMachine = Globals.Database.GetMachine(requiredMachineName);
				if (requiredMachine == null)
					throw new ApplicationException("requiredMachine not found: " + requiredMachineName);

				foreach (DataRow romRow in Globals.Database.GetMachineRoms(requiredMachine))
				{
					string name = Tools.DataRowValue(romRow, "name");
					string sha1 = Tools.DataRowValue(romRow, "sha1");

					if (name == null || sha1 == null)
						continue;

					string romFilename = Path.Combine(Globals.MameDirectory, "roms", requiredMachineName, name);
					string romDirectory = Path.GetDirectoryName(romFilename);
					if (Directory.Exists(romDirectory) == false)
						Directory.CreateDirectory(romDirectory);

					bool have = Globals.RomHashStore.Exists(sha1);

					if (have == true)
					{
						if (File.Exists(romFilename) == false)
							romStoreFilenames.Add(new string[] { romFilename, Globals.RomHashStore.Filename(sha1) });
					}
					else
					{
						++missingCount;
					}
					Console.WriteLine($"Place machine ROM: {have}\t{sha1}\t{requiredMachineName}\t{name}");
				}
			}

			return missingCount;
		}

		public static ArchiveOrgFile MachineDiskAvailableSourceFile(DataRow machineRow, DataRow diskRow, ArchiveOrgItem sourceItem, Database database)
		{
			string machineName = Tools.DataRowValue(machineRow, "name");

			string diskName = Tools.DataRowValue(diskRow, "name");
			string merge = Tools.DataRowValue(diskRow, "merge");

			List<string> machineNames = new List<string>();

			machineNames.Add(machineName);

			DataRow currentRow = machineRow;
			while (currentRow.IsNull("romof") == false)
			{
				string romof = (string)currentRow["romof"];
				machineNames.Add(romof);

				currentRow = database.GetMachine(romof);
			}

			string availableDiskName = diskName;

			if (merge != null)
				availableDiskName = merge;

			foreach (string availableMachineName in machineNames)
			{
				string key = $"{availableMachineName}/{availableDiskName}";

				ArchiveOrgFile file = sourceItem.GetFile(key);

				if (file != null)
					return file;
			}

			return null;
		}

		private int GetDisksMachine(string machineName, List<string[]> romStoreFilenames)
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineDisk][0]; //	TODO: Support many

			DataRow machineRow = Globals.Database.GetMachine(machineName);
			if (machineRow == null)
				throw new ApplicationException("GetDisksMachine machine not found: " + machineName);

			DataRow[] diskRows = Globals.Database.GetMachineDisks(machineRow);

			if (diskRows.Length == 0)
				return 0;

			Tools.ConsoleHeading(2, new string[] {
				"Machine Disk",
				$"{machineName}",
			});

			//
			// See if Disks are in the hash store
			//
			List<DataRow> missingDiskRows = new List<DataRow>();

			foreach (DataRow diskRow in diskRows)
			{
				string name = Tools.DataRowValue(diskRow, "name");
				string sha1 = Tools.DataRowValue(diskRow, "sha1");

				if (name == null || sha1 == null)
					continue;

				bool inStore = Globals.DiskHashStore.Exists(sha1);

				Console.WriteLine($"Checking machine Disk: {inStore}\t{sha1}\t{machineName}\t{name}");

				if (inStore == false)
					missingDiskRows.Add(diskRow);
			}

			//
			// If not then download and import into hash store
			//
			if (missingDiskRows.Count > 0)
			{
				foreach (DataRow diskRow in missingDiskRows)
				{
					string diskName = Tools.DataRowValue(diskRow, "name");
					string sha1 = Tools.DataRowValue(diskRow, "sha1");

					ArchiveOrgFile sourceFile = MachineDiskAvailableSourceFile(machineRow, diskRow, sourceItem, Globals.Database);

					if (sourceFile == null)
					{
						Console.WriteLine($"!!! Available Download Machine Disks not found in source: {machineName}, {diskName}");
					}
					else
					{
						ImportDisk(sourceItem, sourceFile, $"machine disk: '{sourceFile.name}'");
					}
				}
			}

			//
			// Check and place
			//

			int missing = 0;

			foreach (DataRow diskRow in diskRows)
			{
				string name = Tools.DataRowValue(diskRow, "name");
				string sha1 = Tools.DataRowValue(diskRow, "sha1");

				if (name == null || sha1 == null)
					continue;

				string filename = Path.Combine(Globals.MameDirectory, "roms", machineName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				bool have = Globals.DiskHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(filename) == false)
						romStoreFilenames.Add(new string[] { filename, Globals.DiskHashStore.Filename(sha1) });
				}
				else
				{
					++missing;
				}

				Console.WriteLine($"Place machine Disk: {have}\t{sha1}\t{machineName}\t{name}");
			}

			return missing;
		}

		private int GetDiskSoftware(DataRow softwareList, DataRow software, List<string[]> romStoreFilenames)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			ArchiveOrgItem[] sourceItems = ArchiveOrgItem.GetItems(ItemType.SoftwareDisk, softwareListName);

			DataRow[] disks = Globals.Database.GetSoftwareDisks(software);

			if (disks.Length == 0)
				return 0;

			Tools.ConsoleHeading(2, new string[] {
				"Software Disk",
				$"{softwareListName} / {softwareName}",
			});

			//
			// See if Disks are in the hash store
			//
			List<DataRow> missingDisks = new List<DataRow>();

			foreach (DataRow disk in disks)
			{
				string name = Tools.DataRowValue(disk, "name");
				string sha1 = Tools.DataRowValue(disk, "sha1");

				if (name == null || sha1 == null)
					continue;

				bool inStore = Globals.DiskHashStore.Exists(sha1);

				Console.WriteLine($"Checking software Disk: {inStore}\t{sha1}\t{softwareListName}\t{softwareName}\t{name}");

				if (inStore == false)
					missingDisks.Add(disk);
			}

			//
			// If not then download and import into hash store
			//
			if (missingDisks.Count > 0)
			{
				List<string> downloadSoftwareNames = new List<string>(new string[] { softwareName });

				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					downloadSoftwareNames.Add(parentSoftwareName);

				foreach (DataRow disk in missingDisks)
				{
					string diskName = Tools.DataRowValue(disk, "name");
					string sha1 = Tools.DataRowValue(disk, "sha1");

					bool imported = false;

					for (int sourceIndex = 0; sourceIndex < sourceItems.Length && imported == false; ++sourceIndex)
					{
						ArchiveOrgItem sourceItem = sourceItems[sourceIndex];

						foreach (string downloadSoftwareName in downloadSoftwareNames)
						{
							string key = $"{softwareListName}/{downloadSoftwareName}/{diskName}";

							if (sourceItem.Tag != null && sourceItem.Tag != "*")
								key = $"{downloadSoftwareName}/{diskName}";

							ArchiveOrgFile file = sourceItem.GetFile(key);

							if (file == null)
								continue;

							imported = ImportDisk(sourceItem, file, sha1);
						}
					}

					if (imported == false)
						throw new ApplicationException($"Software list disk not on archive.org {softwareListName}/{softwareName}/{diskName}");
				}
			}

			//
			// Check and place
			//

			int missing = 0;

			foreach (DataRow disk in disks)
			{
				string name = Tools.DataRowValue(disk, "name");
				string sha1 = Tools.DataRowValue(disk, "sha1");

				if (name == null || sha1 == null)
					continue;

				string filename = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				bool have = Globals.DiskHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(filename) == false)
						romStoreFilenames.Add(new string[] { filename, Globals.DiskHashStore.Filename(sha1) });
				}
				else
				{
					++missing;
				}

				Console.WriteLine($"Place software Disk: {have}\t{sha1}\t{softwareListName}\t{softwareName}\t{name}");
			}

			return missing;
		}

		private int GetRomsSoftware(DataRow softwareList, DataRow software, List<string[]> romStoreFilenames)
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.SoftwareRom][0];	//	TODO: Support many

			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] roms = Globals.Database.GetSoftwareRoms(software);

			if (roms.Length == 0)
				return 0;

			Tools.ConsoleHeading(2, new string[] {
				"Software ROM",
				$"{softwareListName} / {softwareName}",
			});

			ArchiveOrgFile file = sourceItem.GetFile(softwareListName);

			if (file == null)
				throw new ApplicationException($"Software list not on archive.org {softwareListName}");

			//
			// Check ROMs in store on SHA1
			//
			HashSet<string> missingRoms = new HashSet<string>();

			foreach (DataRow rom in roms)
			{
				string romName = Tools.DataRowValue(rom, "name");
				string sha1 = Tools.DataRowValue(rom, "sha1");

				if (romName == null || sha1 == null)
					continue;

				bool inStore = Globals.RomHashStore.Exists(sha1);

				Console.WriteLine($"Checking Software ROM: {inStore}\t{sha1}\t{softwareListName}\t{softwareName}\t{romName}");

				if (inStore == false)
					missingRoms.Add(sha1);
			}

			//
			// Download ROMs and import to store
			//
			if (missingRoms.Count > 0)
			{
				string requiredSoftwareName = softwareName;
				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					requiredSoftwareName = parentSoftwareName;

				string listEnc = Uri.EscapeDataString(softwareListName);
				string softEnc = Uri.EscapeDataString(requiredSoftwareName);

				string downloadSoftwareUrl = sourceItem.DownloadLink(file) + "/@LIST@%2f@SOFTWARE@.zip";
				downloadSoftwareUrl = downloadSoftwareUrl.Replace("@LIST@", listEnc);
				downloadSoftwareUrl = downloadSoftwareUrl.Replace("@SOFTWARE@", softEnc);

				Dictionary<string, long> softwareSizes = GetSoftwareSizes(softwareListName, sourceItem, file);

				if (softwareSizes.ContainsKey(requiredSoftwareName) == false)
					throw new ApplicationException($"Did GetSoftwareSize {softwareListName}, {requiredSoftwareName} ");

				long size = softwareSizes[requiredSoftwareName];

				ImportRoms(downloadSoftwareUrl, $"software rom: '{softwareListName}/{requiredSoftwareName}'", size, missingRoms.ToArray());
			}

			//
			// Check and place ROMs
			//

			int missingCount = 0;
			foreach (DataRow rom in roms)
			{
				string romName = Tools.DataRowValue(rom, "name");
				string sha1 = Tools.DataRowValue(rom, "sha1");

				if (romName == null || sha1 == null)
					continue;

				string romFilename = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName, romName);
				string romDirectory = Path.GetDirectoryName(romFilename);
				if (Directory.Exists(romDirectory) == false)
					Directory.CreateDirectory(romDirectory);

				bool have = Globals.RomHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(romFilename) == false)
						romStoreFilenames.Add(new string[] { romFilename, Globals.RomHashStore.Filename(sha1) });
				}
				else
				{
					++missingCount;
				}
				Console.WriteLine($"Place software ROM: {have}\t{sha1}\t{softwareListName}\t{softwareName}\t{romName}");
			}

			return missingCount;
		}

		private Dictionary<string, long> GetSoftwareSizes(string listName, ArchiveOrgItem item, ArchiveOrgFile file)
		{
			string cacheDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "SoftwareSizes", item.Version);

			Directory.CreateDirectory(cacheDirectory);

			string filename = Path.Combine(cacheDirectory, listName + ".htm");

			string html;
			if (File.Exists(filename) == false)
			{
				string url = item.DownloadLink(file) + "/";
				html = Tools.Query(Globals.HttpClient, url);
				File.WriteAllText(filename, html, Encoding.UTF8);
			}
			else
			{
				html = File.ReadAllText(filename, Encoding.UTF8);
			}

			Dictionary<string, long> result = new Dictionary<string, long>();

			using (StringReader reader = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (line.StartsWith("<tr><td><a href=\"//archive.org/download/") == false)
						continue;

					string[] parts = line.Split(new char[] { '<' });

					string name = null;
					string size = null;

					foreach (string part in parts)
					{
						int index = part.LastIndexOf(">");
						if (index == -1)
							continue;
						++index;

						if (part.StartsWith("a href=") == true)
						{
							name = part.Substring(index + listName.Length + 1);
							name = name.Substring(0, name.Length - 4);
						}

						if (part.StartsWith("td id=\"size\"") == true)
							size = part.Substring(index);
					}

					if (name == null || size == null)
						throw new ApplicationException($"Bad html line {listName} {line}");

					result.Add(name, Int64.Parse(size));
				}
			}

			return result;
		}

		private long ImportRoms(string url, string name, long expectedSize, string[] requiredSHA1s)
		{
			HashSet<string> required = new HashSet<string>(requiredSHA1s);

			long size = 0;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "archive.zip");
				string extractDirectory = Path.Combine(tempDir.Path, "OUT");
				Directory.CreateDirectory(extractDirectory);

				Console.Write($"Downloading {name} size:{Tools.DataSize(expectedSize)} url:{url} ...");
				DateTime startTime = DateTime.Now;
				size = Tools.Download(url, archiveFilename, _DownloadDotSize, 30);
				TimeSpan took = DateTime.Now - startTime;
				Console.WriteLine("...done");

				decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);
				Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

				if (size != expectedSize)
					Console.WriteLine($"!!! Unexpected downloaded file size expect:{expectedSize} actual:{size}");

				Console.Write($"Extracting {name}, {archiveFilename} ...");
				ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
				Console.WriteLine("...done");

				Tools.ClearAttributes(tempDir.Path);

				foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
				{
					string partFilename = romFilename.Substring(extractDirectory.Length);

					string sha1 = Globals.RomHashStore.Hash(romFilename);

					required.Remove(sha1);

					bool imported = Globals.RomHashStore.Add(romFilename, false, sha1);
					Console.WriteLine($"ROM Store Import: {imported} {sha1} {name} {partFilename}");
				}
			}

			foreach (string sha1 in required)
				Console.WriteLine($"!!! Importing missing sha1 in source it won't work. {name} {sha1}");

			return size;
		}

		private bool ImportDisk(ArchiveOrgItem sourceItem, ArchiveOrgFile sourceFile, string expectedSha1)
		{
			string url = sourceItem.DownloadLink(sourceFile);

			if (Globals.BadSources.AlreadyDownloaded(sourceFile) == true)
			{
				Console.WriteLine($"!!! Already Downloaded before and it didn't work (bad in source) chd-sha1:{expectedSha1} source-sha1: {sourceFile.sha1}");
				return false;
			}

			string tempFilename = Path.Combine(_DownloadTempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + Tools.ValidFileName(sourceFile.name) + ".chd");

			lock (_TaskInfo)
			{
				_TaskInfo.BytesTotal = sourceFile.size;
			}

			Console.Write($"Downloading {sourceFile.name} size:{Tools.DataSize(sourceFile.size)} url:{url} ...");
			DateTime startTime = DateTime.Now;
			long size = Tools.Download(url, tempFilename, _DownloadDotSize, 3 * 60, _TaskInfo);
			TimeSpan took = DateTime.Now - startTime;
			Console.WriteLine("...done");

			decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);
			Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

			if (sourceFile.size != size)
				Console.WriteLine($"!!! Unexpected downloaded file size expect:{sourceFile.size} actual:{size}");

			string sha1 = Globals.DiskHashStore.Hash(tempFilename);

			if (sha1 != expectedSha1)
			{
				Console.WriteLine($"!!! Unexpected downloaded CHD SHA1. It's wrong in the source and will not work. expect:{expectedSha1} actual:{sha1}");
				Globals.BadSources.ReportSourceFile(sourceFile, expectedSha1, sha1);
			}

			if (Globals.Database._AllSHA1s.Contains(sha1) == false)
			{
				Console.WriteLine($"!!! Unkown downloaded CHD SHA1. It will be left in the TEMP directory, {sha1}, {tempFilename}");
				return false;
			}

			bool imported = Globals.DiskHashStore.Add(tempFilename, true, sha1);

			Console.WriteLine($"Disk Store Import: {imported} {sha1} {sourceFile.name}");

			return Globals.DiskHashStore.Exists(expectedSha1);
		}

		public void ImportDirectory(string importDirectory)
		{
			if (Directory.Exists(importDirectory) == false)
				throw new ApplicationException($"Import directory does not exist: {importDirectory}");

			Tools.ConsoleHeading(1, new string[] {
				"Import from Directory",
				importDirectory,
			});

			DataTable reportTable = Tools.MakeDataTable(
				"Filename	Type	SHA1	Action",
				"String		String	String	String"
			);

			ImportDirectory(importDirectory, Globals.Database._AllSHA1s, reportTable);

			Globals.Reports.SaveHtmlReport(reportTable, "Import Directory");
		}
		public void ImportDirectory(string importDirectory, HashSet<string> allSHA1s, DataTable reportTable)
		{
			foreach (string filename in Directory.GetFiles(importDirectory, "*", SearchOption.AllDirectories))
			{
				Console.WriteLine(filename);

				string name = filename.Substring(importDirectory.Length + 1);

				string extention = Path.GetExtension(filename).ToLower();

				string sha1;
				string status;

				switch (extention)
				{
					case ".zip":
						sha1 = "";
						status = "";

						using (TempDirectory tempDir = new TempDirectory())
						{
							ZipFile.ExtractToDirectory(filename, tempDir.Path);

							Tools.ClearAttributes(tempDir.Path);

							reportTable.Rows.Add(name, "ARCHIVE", sha1, status);

							ImportDirectory(tempDir.Path, allSHA1s, reportTable);
						}
						break;

					case ".chd":
						sha1 = Globals.DiskHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = Globals.DiskHashStore.Add(filename, false, sha1) ? "" : "Have";
						else
							status = "Unknown";

						reportTable.Rows.Add(name, "DISK", sha1, status);
						break;

					default:
						sha1 = Globals.RomHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = Globals.RomHashStore.Add(filename, false, sha1) ? "" : "Have";
						else
							status = "Unknown";

						reportTable.Rows.Add(name, "ROM", sha1, status);
						break;
				}
			}
		}

		public void FindAllMachines(string machineName, HashSet<string> requiredMachines)
		{
			if (requiredMachines.Contains(machineName) == true)
				return;

			DataRow machineRow = Globals.Database.GetMachine(machineName);

			if (machineRow == null)
				throw new ApplicationException("FindAllMachines machine not found: " + machineName);

			bool hasRoms = (long)machineRow["ao_rom_count"] > 0;

			if (hasRoms == true)
				requiredMachines.Add(machineName);

			string romof = machineRow.IsNull("romof") ? null : (string)machineRow["romof"];

			if (romof != null)
				FindAllMachines(romof, requiredMachines);

			foreach (DataRow row in Globals.Database.GetMachineDeviceRefs(machineName))
				FindAllMachines((string)row["name"], requiredMachines);
		}

	}
}
