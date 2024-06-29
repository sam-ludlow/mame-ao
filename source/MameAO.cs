using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net;

namespace Spludlow.MameAO
{
	public static class Globals
	{
		public class TaskInfo
		{
			public string Command = "";
			public long BytesCurrent = 0;
			public long BytesTotal = 0;
		}

		static Globals()
		{
			Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
			AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

			HttpClient = new HttpClient();
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");

			HttpClient.Timeout = TimeSpan.FromSeconds(180);		// metdata 3 minutes
		}

		public static readonly int AssetDownloadTimeoutMilliseconds = 6 * 60 * 60 * 1000;	// assets 6 hours

		public static string ListenAddress = "http://localhost:12380/";

		public static long DownloadDotSize = 1024 * 1024;

		public static string AssemblyVersion;

		public static HttpClient HttpClient;

		public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

		public static string RootDirectory;
		public static string TempDirectory;
		public static string CacheDirectory;
		public static string ReportDirectory;

		public static string MameDirectory;

		public static string MameVersion;

		public static bool LinkingEnabled = false;

		public static Dictionary<ItemType, ArchiveOrgItem[]> ArchiveOrgItems = new Dictionary<ItemType, ArchiveOrgItem[]>();
		public static Dictionary<string, GitHubRepo> GitHubRepos = new Dictionary<string, GitHubRepo>();

		public static HashStore RomHashStore;
		public static HashStore DiskHashStore;

		public static MameAOProcessor AO;

		public static Artwork Artwork;
		public static BadSources BadSources;
		public static Database Database;
		public static Favorites Favorites;
		public static Genre Genre;
		public static MameChdMan MameChdMan;
		public static Reports Reports;
		public static Samples Samples;
		public static Settings Settings;
		public static WebServer WebServer;

		public static Task WorkerTask = null;
		public static TaskInfo WorkerTaskInfo = new TaskInfo();
		public static DataSet WorkerTaskReport;
	}

	public class MameAOProcessor
	{
		private IntPtr ConsoleHandle;

		private readonly string WelcomeText = @"@VERSION
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
			Directory.CreateDirectory(Globals.RootDirectory);

			Globals.TempDirectory = Path.Combine(Globals.RootDirectory, "_TEMP");
			Directory.CreateDirectory(Globals.TempDirectory);

			Globals.CacheDirectory = Path.Combine(Globals.TempDirectory, "CACHE");
			Directory.CreateDirectory(Globals.CacheDirectory);

			Globals.ReportDirectory = Path.Combine(Globals.RootDirectory, "_REPORTS");
			Directory.CreateDirectory(Globals.ReportDirectory);
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
			if (ConsoleHandle == IntPtr.Zero)
				Console.WriteLine("!!! Wanring can't get handle on Console Window.");
			else
				SetForegroundWindow(ConsoleHandle);
		}

		public void Initialize()
		{
			Console.Title = $"MAME-AO {Globals.AssemblyVersion}";

			Console.Write(WelcomeText.Replace("@VERSION", Globals.AssemblyVersion));
			Tools.ConsoleHeading(1, "Initializing");

			Globals.AO = this;

			Globals.Settings = new Settings();

			//
			// Fixes
			//

			//	Fixed in 1.88 - this file can have duff data
			string badSourcesFilename = Path.Combine(Globals.RootDirectory, "_BadSources.txt");
			if (File.Exists(badSourcesFilename) == true && File.GetLastWriteTime(badSourcesFilename) < new DateTime(2024, 7, 1))
				File.Delete(badSourcesFilename);

			// Moved in 1.90
			string oldDirectory = Path.Combine(Globals.RootDirectory, "_METADATA");
			if (Directory.Exists(oldDirectory) == true)
				Directory.Delete(oldDirectory, true);

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
			// GitHub Repos
			//

			Globals.GitHubRepos.Add("mame-ao", new GitHubRepo("sam-ludlow", "mame-ao"));

			Globals.GitHubRepos.Add("mame", new GitHubRepo("mamedev", "mame"));

			Globals.GitHubRepos.Add("MAME_Dats", new GitHubRepo("AntoPISA", "MAME_Dats"));
			Globals.GitHubRepos.Add("MAME_SupportFiles", new GitHubRepo("AntoPISA", "MAME_SupportFiles"));

			//
			// Determine MAME version
			//

			if (Globals.Settings.Options["MameVersion"] == "ArchiveOrg")
			{
				ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineRom][0];
				item.GetFile(null);
				Globals.MameVersion = item.Version;
			}
			else
			{
				Globals.MameVersion = Globals.GitHubRepos["mame"].tag_name.Substring(4);
			}

			if (Globals.MameVersion == null)
				Globals.MameVersion = Mame.LatestLocal();

			if (Globals.MameVersion == null)
				throw new ApplicationException("Unable to determine MAME Version.");

			Globals.MameVersion = Globals.MameVersion.Replace(".", "");
			Globals.MameDirectory = Path.Combine(Globals.RootDirectory, Globals.MameVersion);

			Console.WriteLine($"MameVersion: {Globals.MameVersion}");

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
				Console.WriteLine($"!!! New MAME version: {Globals.MameVersion}");
				Directory.CreateDirectory(Globals.MameDirectory);
			}

			if (File.Exists(binCacheFilename) == false)
			{
				Console.Write($"Downloading MAME binaries {binUrl} ...");
				Tools.Download(binUrl, binCacheFilename);
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
			// Bits & Bobs
			//

			ConsoleHandle = FindWindowByCaption(IntPtr.Zero, Console.Title);

			Globals.Reports = new Reports();
			Globals.BadSources = new BadSources();
			Globals.Favorites = new Favorites();

			Globals.Artwork = new Artwork();
			Globals.Samples = new Samples();

			Globals.Genre = new Genre();
			Globals.Genre.Initialize();

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

			try
			{
				Globals.WebServer.StartListener();
			}
			catch (HttpListenerException)
			{
				Tools.ConsoleHeading(1, new string[] { "MAME-AO is already running", "this is not permitted" });
				throw;
			}

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
					Globals.WorkerTask.Wait();
				else
					Console.WriteLine("BUSY!");
			}
		}

		private static void SaveHistory(string line, DateTime startTime)
		{
			DateTime endTime = DateTime.UtcNow;
			string filename = Path.Combine(Globals.TempDirectory, "_history.txt");
			File.AppendAllText(filename, $"{startTime:s}\t{endTime:s}\t{Tools.CleanWhiteSpace(line)}{Environment.NewLine}");
		}

		public bool RunLineTask(string line)
		{
			if (Globals.WorkerTask != null && Globals.WorkerTask.Status != TaskStatus.RanToCompletion)
				return false;

			BringToFront();

			Globals.WorkerTask = new Task(() => {
				try
				{
					//DateTime startTime = DateTime.UtcNow;

					RunLine(line);

					//SaveHistory(line, startTime);
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
					lock (Globals.WorkerTaskInfo)
					{
						Globals.WorkerTaskInfo.Command = "";
						Globals.WorkerTaskInfo.BytesCurrent = 0;
						Globals.WorkerTaskInfo.BytesTotal = 0;
					}
				}
			});

			lock (Globals.WorkerTaskInfo)
			{
				Globals.WorkerTaskInfo.Command = line;
				Globals.WorkerTaskInfo.BytesCurrent = 0;
				Globals.WorkerTaskInfo.BytesTotal = 0;
			}

			Globals.WorkerTask.Start();

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

						Import.ImportDirectory(arguments);
						return;

					case ".up":
						SelfUpdate.Update(0);
						return;

					case ".upany":
						SelfUpdate.Update(-1);
						return;

					case ".favm":
					case ".favmx":
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <Machine Name>");

						machine = parts[1].ToLower();
						
						Favorites.ValidateFavorite(machine, null, null);

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

						Favorites.ValidateFavorite(machine, list, software);

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
								Export.MachineRoms(arguments);
								break;
							case "MD":
								Export.MachineDisks(arguments);
								break;
							case "SR":
								Export.SoftwareRoms(arguments);
								break;
							case "SD":
								Export.SoftwareDisks(arguments);
								break;

							case "*":
								Export.MachineRoms(arguments);
								Export.MachineDisks(arguments);
								Export.SoftwareRoms(arguments);
								Export.SoftwareDisks(arguments);
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

						if (Globals.Reports.RunReport(parts[1]) == false)
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

					case ".set":
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <key> <value>");
						Globals.Settings.Set(parts[1], parts[2]);
						return;

					case ".dbm":
					case ".dbs":
						if (parts.Length == 1)
							throw new ApplicationException($"Usage: {parts[0]} <command text>");

						Database.ConsoleQuery(parts[0].Substring(3), String.Join(" ", parts.Skip(1)));
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
				Place.PlaceAssets(machine, software);
				Mame.RunMame(binFilename, machine + " " + software + " " + arguments);
			}
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
	}
}
