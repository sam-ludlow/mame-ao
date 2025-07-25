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
	public enum ItemType
	{
		MachineRom,
		MachineDisk,
		SoftwareRom,
		SoftwareDisk,
		Support,
	};

	public class DataQueryProfile
	{
		public string Key;
		public string Text;
		public string Decription;
		public string CommandText;
	}

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

			HttpClient = new HttpClient(new HttpClientHandler { UseCookies = false });
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");
			HttpClient.Timeout = TimeSpan.FromSeconds(180);		// metdata 3 minutes
		}

		public static readonly int AssetDownloadTimeoutMilliseconds = 6 * 60 * 60 * 1000;	// assets 6 hours

		public static string ListenAddress = "http://localhost:12380/";

		public static long DownloadDotSize = 1024 * 1024;

		public static string AssemblyVersion;

		public static HttpClient HttpClient;
		public static string AuthCookie = null;

		public static Dictionary<string, string> Config = new Dictionary<string, string>();

		public static string RootDirectory;
		public static string TempDirectory;
		public static string CacheDirectory;
		public static string ReportDirectory;
		public static string BitTorrentDirectory;

		public static string MameArguments = "";

		public static bool LinkingEnabled = false;
		public static bool BitTorrentAvailable = false;

		public static Dictionary<ItemType, ArchiveOrgItem[]> ArchiveOrgItems = new Dictionary<ItemType, ArchiveOrgItem[]>();
		public static Dictionary<string, GitHubRepo> GitHubRepos = new Dictionary<string, GitHubRepo>();

		public static HashStore RomHashStore;
		public static HashStore DiskHashStore;

		public static HashSet<string> AllSHA1 = new HashSet<string>();

		public static MameAOProcessor AO;

		public static Artwork Artwork;
		public static Favorites Favorites;
		public static Genre Genre;
		public static MameChdMan MameChdMan;
		public static Reports Reports;
		public static Samples Samples;
		public static Settings Settings;
		public static WebServer WebServer = null;

		public static Task WorkerTask = null;
		public static TaskInfo WorkerTaskInfo = new TaskInfo();
		public static DataSet WorkerTaskReport;

		public static PhoneHome PhoneHome;

		public static ICore Core = null;
	}

	public class MameAOProcessor
	{
		private IntPtr ConsoleHandle;

		private readonly string WelcomeText = @"@VERSION

$$\      $$\  $$$$$$\  $$\      $$\ $$$$$$$$\        $$$$$$\   $$$$$$\  
$$$\    $$$ |$$  __$$\ $$$\    $$$ |$$  _____|      $$  __$$\ $$  __$$\ 
$$$$\  $$$$ |$$ /  $$ |$$$$\  $$$$ |$$ |            $$ /  $$ |$$ /  $$ |
$$\$$\$$ $$ |$$$$$$$$ |$$\$$\$$ $$ |$$$$$\          $$$$$$$$ |$$ |  $$ |
$$ \$$$  $$ |$$  __$$ |$$ \$$$  $$ |$$  __|         $$  __$$ |$$ |  $$ |
$$ |\$  /$$ |$$ |  $$ |$$ |\$  /$$ |$$ |            $$ |  $$ |$$ |  $$ |
$$ | \_/ $$ |$$ |  $$ |$$ | \_/ $$ |$$$$$$$$\       $$ |  $$ | $$$$$$  |
\__|     \__|\__|  \__|\__|     \__|\________|      \__|  \__| \______/ 

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

		public MameAOProcessor(string rootDirectory)
		{
			Globals.RootDirectory = rootDirectory;
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

			string configFilename = Path.Combine(Globals.RootDirectory, "_config.txt");
			if (File.Exists(configFilename) == true)
			{
				using (StreamReader reader = new StreamReader(configFilename, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						string[] parts = line.Split('\t');
						if (parts.Length == 2)
							Globals.Config.Add(parts[0], parts[1]);
					}
				}
			}

			Globals.MameArguments = Globals.Config.ContainsKey("MameArguments") == true ? Globals.Config["MameArguments"] : "";

			//
			// Fixes
			//

			//	...
			//	TODO move to "mame"
			//	TODO handle favorites move config files

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
				Tools.ConsoleHeading(3, "!!! You can save a lot of disk space by enabling symbolic links, see the README.");

			//
			// GitHub Repos
			//

			Globals.GitHubRepos.Add("mame-ao", new GitHubRepo("sam-ludlow", "mame-ao"));
			Globals.GitHubRepos.Add("dome-bt", new GitHubRepo("sam-ludlow", "dome-bt"));

			Globals.GitHubRepos.Add("mame", new GitHubRepo("mamedev", "mame"));

			Globals.GitHubRepos.Add("MAME_Dats", new GitHubRepo("AntoPISA", "MAME_Dats"));
			Globals.GitHubRepos.Add("MAME_SupportFiles", new GitHubRepo("AntoPISA", "MAME_SupportFiles"));

			//
			// Bit Torrent
			//

			Globals.BitTorrentDirectory = Globals.Config.ContainsKey("BitTorrentPath") == true ? Globals.Config["BitTorrentPath"] : Path.Combine(Globals.RootDirectory, "_BT");
			Directory.CreateDirectory(Globals.BitTorrentDirectory);

			if (Globals.Config.ContainsKey("BitTorrentRestartMinutes") == true)
				BitTorrent.RestartLimit = TimeSpan.FromMinutes(Double.Parse(Globals.Config["BitTorrentRestartMinutes"]));

			if (BitTorrent.IsInstalled() == true)
				BitTorrent.Initialize();

			//
			//	Archive.Org Credentials
			//

			if (Globals.BitTorrentAvailable == false)
				Globals.AuthCookie = ArchiveOrgAuth.GetCookie();

			//
			// Archive.Org Items
			//

			// Machine ROM
			Globals.ArchiveOrgItems.Add(ItemType.MachineRom, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-merged", "mame-merged/", null),
			});
			// Machine DISK
			Globals.ArchiveOrgItems.Add(ItemType.MachineDisk, new ArchiveOrgItem[] {
				new ArchiveOrgItem("MAME_0.225_CHDs_merged", null, null),
			});
			// Software ROM
			Globals.ArchiveOrgItems.Add(ItemType.SoftwareRom, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-sl", "mame-sl/", null),
			});
			// Software DISK
			Globals.ArchiveOrgItems.Add(ItemType.SoftwareDisk, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-software-list-chds-2", null, "*"),
			});
			// Support (Artwork & Samples)
			Globals.ArchiveOrgItems.Add(ItemType.Support, new ArchiveOrgItem[] {
				new ArchiveOrgItem("mame-support", "Support/", null),
			});

			//
			// Default Core MAME
			//

			Cores.EnableCore("mame", Globals.Config.ContainsKey("MameVersion") == true ? Globals.Config["MameVersion"] : null);

			//
			// CHD Manager
			//

			Globals.MameChdMan = new MameChdMan(Globals.Core.Directory);

			//
			// Hash Stores
			//

			string directory = Globals.Config.ContainsKey("StorePathRom") == true ? Globals.Config["StorePathRom"] : Path.Combine(Globals.RootDirectory, "_STORE");
			Directory.CreateDirectory(directory);
			Console.Write($"Loading Hash Store {directory} ...");
			Globals.RomHashStore = new HashStore(directory, Tools.SHA1HexFile);
			Console.WriteLine("...done.");

			directory = Globals.Config.ContainsKey("StorePathDisk") == true ? Globals.Config["StorePathDisk"] : Path.Combine(Globals.RootDirectory, "_STORE_DISK");
			Directory.CreateDirectory(directory);
			Console.Write($"Loading Hash Store {directory} ...");
			Globals.DiskHashStore = new HashStore(directory, Globals.MameChdMan.Hash);
			Console.WriteLine("...done.");

			//
			// Bits & Bobs
			//

			ConsoleHandle = FindWindowByCaption(IntPtr.Zero, Console.Title);

			Globals.Samples = new Samples();
			Globals.Artwork = new Artwork();

			Globals.Reports = new Reports();

			// TODO

			//Globals.Genre = new Genre();
			//Globals.Genre.Initialize();

			//Globals.Favorites = new Favorites();


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
				Console.Write($"{Globals.Core.Name.ToUpper()} {Globals.Core.Version}> ");
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

		public bool RunLineTask(string line)
		{
			if (Globals.WorkerTask != null && Globals.WorkerTask.Status != TaskStatus.RanToCompletion)
				return false;

			BringToFront();

			Globals.WorkerTask = new Task(() => {
				try
				{
					Globals.PhoneHome = new PhoneHome(line);

					RunLine(line);

					Globals.PhoneHome.Success();
				}
				catch (ApplicationException e)
				{
					Globals.PhoneHome.Error(e);

					Console.WriteLine();
					Console.WriteLine("!!! ERROR: " + e.Message);
					Console.WriteLine();
				}
				catch (Exception e)
				{
					Globals.PhoneHome.Error(e);

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
			LineArguments args = new LineArguments(line);
			string[] parts;

			string machine = "";
			string software = "";
			string arguments = "";

			if (args.First.StartsWith(".") == true)
			{
				switch (args.First)
				{
					case ".list":
						ListSavedState();
						return;

					case ".import":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <source directory>");
						Import.ImportDirectory(parts[1]);
						return;

					case ".up":
						SelfUpdate.Update(0);
						return;

					case ".upany":
						SelfUpdate.Update(-1);
						return;

					case ".favm":
					case ".favmx":
						parts = args.Arguments(2);
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
						parts = args.Arguments(4);
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
						parts = args.Arguments(3);
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <type: MR, MD, SR, SD, *> <target directory>");

						arguments = parts[2];

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
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <Report Code>" + Environment.NewLine + Environment.NewLine +
								String.Join(Environment.NewLine, Globals.Reports.ReportTypeText()) + Environment.NewLine
								);

						if (Globals.Reports.RunReport(parts[1]) == false)
							throw new ApplicationException("Report Unknown type.");
						return;

					case ".snap":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <target directory>");

						Mame.CollectSnaps(Globals.RootDirectory, parts[1], Globals.Reports);
						return;

					case ".svg":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <filename or directory>");

						Tools.Bitmap2SVG(parts[1]);
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
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <rom, disk>");

						switch (parts[1].ToUpper())
						{
							case "ROM":
								HashStore.ValidateHashStore(Globals.RomHashStore, "ROM");
								break;

							case "DISK":
								HashStore.ValidateHashStore(Globals.DiskHashStore, "DISK");
								break;

							default:
								throw new ApplicationException("Valid Unknown store type (row, disk).");
						}
						return;

					case ".what":
						Process.Start(Globals.ListenAddress + "api/what");
						return;

					case ".set":
						parts = args.Arguments(3);
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <key> <value>");
						Globals.Settings.Set(parts[1], parts[2]);
						return;

					case ".dbm":
					case ".dbs":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <command text>");

						Database.ConsoleQuery(Globals.Core, parts[0].Substring(3), parts[1]);
						return;

					case ".upload":
						parts = args.Arguments(5);
						if (parts.Length != 4 && parts.Length != 5)
							throw new ApplicationException($"Usage: {parts[0]} <type> <archive.org item name> <batch size> [asset name]");

						switch (parts[1].ToUpper())
						{
							case "MR":
								Upload.MachineRom(parts[2], Int32.Parse(parts[3]));
								break;
							case "MD":
								throw new NotImplementedException("Coming soon.");
							case "SR":
								throw new NotImplementedException("Coming soon.");
							case "SD":
								throw new NotImplementedException("Coming soon.");

							default:
								throw new ApplicationException("Upload Unknown type not (MR, MD, SR, SD).");
						}
						return;

					case ".aodel":
						parts = args.Arguments(3);
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <archive.org item name> <filename>");

						Upload.DeleteFile(parts[1], parts[2]);
						return;

					case ".bt":
						BitTorrent.Initialize();
						return;

					case ".btx":
						BitTorrent.Remove();
						Tools.ConsoleHeading(1, "To use with archive.org enter the command '.creds' if you have not already entered your credentials");
						return;

					case ".btr":
						BitTorrent.Restart();
						return;

					case ".bts":
						BitTorrent.Stop();
						return;

					case ".creds":
						File.Delete(ArchiveOrgAuth.CacheFilename);
						Globals.AuthCookie = ArchiveOrgAuth.GetCookie();
						return;

					case ".test":
						parts = args.Arguments(3);
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <profile> <count>");
						Test.Run(parts[1], Int32.Parse(parts[2]));
						return;

					case ".fetch":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <type>");

						switch (parts[1].ToUpper())
						{
							case "MR":
								Import.FetchMachineRom();
								break;
							case "MD":
								Import.FetchMachineDisk();
								break;
							case "SR":
								Import.FetchSoftwareRom();
								break;
							case "SD":
								Import.FetchSoftwareDisk();
								break;

							case "*":
								Import.FetchMachineRom();
								Import.FetchMachineDisk();
								Import.FetchSoftwareRom();
								Import.FetchSoftwareDisk();
								break;

							default:
								throw new ApplicationException("Upload Unknown type not (MR, MD, SR, SD, *).");

						}
						return;

					case ".software":
						parts = args.Arguments(2);
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <software list name>");
						Import.PlaceSoftwareList(parts[1], true);
						return;

					case ".softname":
					case ".softnamed":
						parts = args.Arguments(3);
						if (parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <software list name> <target directory>");
						Export.SoftwareListNamedExport(parts[1], parts[2], parts[0].EndsWith("d"));
						return;

					case ".style":
						Globals.WebServer.SaveStyle();
						return;

					case ".accdb":
						LinkMsAccess();
						return;

					case ".core":
						parts = args.Arguments(3);
						if (parts.Length != 2 && parts.Length != 3)
							throw new ApplicationException($"Usage: {parts[0]} <core name> [version]");

						Cores.EnableCore(parts[1], parts.Length == 3 ? parts[2] : null);
						return;

					case ".":
					default:
						parts = args.Arguments(2);
						if (parts.Length > 1)
							arguments = parts[1];

						string version = args.First == "." ? Globals.Core.Version : args.First.Substring(1);

						string binFilename = Path.Combine(Path.GetDirectoryName(Globals.Core.Directory), version, $"{Globals.Core.Name}.exe");

						if (File.Exists(binFilename) == false)
							throw new ApplicationException($"Unknow command or no such version: {binFilename}");

						Mame.RunMame(binFilename, arguments);
						return;
				}
			}

			parts = args.Arguments(3, true);

			machine = parts[0];

			if (parts[parts.Length - 1][0] == '-')
			{
				arguments = parts[parts.Length - 1];
				if (parts.Length == 3)
					software = parts[1];
			}
			else
			{
				if (parts.Length == 2)
					software = parts[1];
			}

			machine = machine.ToLower();
			software = software.ToLower();

			string core = Globals.Core.Name;
			if (machine.Contains("@") == true)
			{
				string[] atParts = machine.Split('@');
				machine = atParts[0];
				core = atParts[1];
			}

			string softwareList = null;
			if (software.Contains("@") == true)
			{
				string[] atParts = software.Split('@');
				software = atParts[0];
				softwareList = atParts[1];
			}

			Cores.EnableCore(core, null);

			Place.PlaceAssets(Globals.Core, machine, software);

			if (softwareList != null)
				software = Globals.Core.GetRequiredMedia(machine, softwareList, software);

			if (Globals.Settings.Options["Cheats"] == "Yes")
				arguments += " -cheat";

			//Globals.PhoneHome.Ready();

			Mame.RunMame(Path.Combine(Globals.Core.Directory, $"{Globals.Core.Name}.exe"), $"{machine} {software} {arguments}");
		}

		public void ListSavedState()
		{
			Tools.ConsoleHeading(2, "Saved Games");

			DataTable table = Mame.ListSavedState(Globals.Core);

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

		public void LinkMsAccess()
		{
			string exeFilename = Path.Combine(Globals.RootDirectory, "access-linker.exe");

			if (File.Exists(exeFilename) == false)
				throw new ApplicationException($"Access Linker not found: {exeFilename}, install from here: https://github.com/sam-ludlow/access-linker/releases/latest");

			Version version = AssemblyName.GetAssemblyName(exeFilename).Version;
			string localVersion = $"{version.Major}.{version.Minor}";

			Tools.ConsoleHeading(1, new string[] { $"Create MS Access databases linked to SQLite", exeFilename, localVersion });

			foreach (string filename in Directory.GetFiles(Globals.Core.Directory, "*.sqlite"))
			{
				string targetFilename = filename + ".accdb";

				ProcessStartInfo startInfo = new ProcessStartInfo(exeFilename)
				{
					Arguments = $"access-link-new filename=\"{targetFilename}\" odbc=\"{filename}\"",
					UseShellExecute = false,
				};

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.Start();
					process.WaitForExit();

					if (process.ExitCode != 0)
						throw new ApplicationException("access-linker.exe Bad exit code");
				}

				Tools.ConsoleHeading(2, new string[] { "MS Access linked database created", filename, "=>", targetFilename });
			}
		}
	}
}
