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

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class MameAOProcessor
	{
		public readonly HttpClient _HttpClient;

		public readonly string _RootDirectory;
		private string _VersionDirectory;

		public string _Version;
		public string _AssemblyVersion;

		public bool _LinkingEnabled = false;

		private Task _RunTask = null;
		public string _RunTaskCommand = null;

		public Database _Database;

		public WebServer _WebServer;

		public HashStore _RomHashStore;
		public HashStore _DiskHashStore;

		private string _DownloadTempDirectory;

		private MameChdMan _MameChdMan;
		private BadSources _BadSources;
		public Favorites _Favorites;
		public Reports _Reports;
		private Export _Export;
		public Genre _Genre;

		private readonly long _DownloadDotSize = 1024 * 1024;

		public readonly string _ListenAddress = "http://localhost:12380/";

		private readonly string _BinariesDownloadUrl = "https://github.com/mamedev/mame/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";

		public dynamic _MameAoLatest;

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


		public MameAOProcessor(string rootDirectory)
		{
			Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
			_AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

			_RootDirectory = rootDirectory;

			_HttpClient = new HttpClient();
			_HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{_AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");
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
				ReportError(e, "FATAL ERROR", true);
			}
		}

		public void ReportError(Exception e, string title, bool fatal)
		{
			Console.WriteLine();
			Console.WriteLine($"!!! {title}: " + e.Message);
			Console.WriteLine();
			Console.WriteLine(e.ToString());
			Console.WriteLine();
			Console.WriteLine("If you want to submit an error report please copy and paste the text from here.");
			Console.WriteLine("Select All (Ctrl+A) -> Copy (Ctrl+C) -> notepad -> paste (Ctrl+V)");
			Console.WriteLine();
			Console.WriteLine("Report issues here https://github.com/sam-ludlow/mame-ao/issues");

			if (fatal == true)
			{
				Console.WriteLine();
				Console.WriteLine("Press any key to continue, program has crashed and will exit.");
				Console.ReadKey();
				Environment.Exit(1);
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
			Console.Title = $"MAME-AO {_AssemblyVersion}";

			Console.Write(WelcomeText.Replace("@VERSION", _AssemblyVersion));

			Tools.ConsoleHeading(1, "Initializing");

			//
			// Symbolic Links check
			//

			string linkFilename = Path.Combine(_RootDirectory, @"_LINK_TEST.txt");
			string targetFilename = Path.Combine(_RootDirectory, @"_TARGET_TEST.txt");

			if (File.Exists(linkFilename) == true)
				File.Delete(linkFilename);
			File.WriteAllText(targetFilename, "TEST");

			Tools.LinkFiles(new string[][] { new string[] { linkFilename, targetFilename } });

			_LinkingEnabled = File.Exists(linkFilename);

			if (File.Exists(linkFilename) == true)
				File.Delete(linkFilename);
			if (File.Exists(targetFilename) == true)
				File.Delete(targetFilename);

			if (_LinkingEnabled == false)
				Console.WriteLine("!!! You can save a lot of disk space by enabling symbolic links, see the README.");

			//
			// Prepare sources
			//

			string metaDataDirectory = Path.Combine(_RootDirectory, "_METADATA");
			if (Directory.Exists(metaDataDirectory) == false)
				Directory.CreateDirectory(metaDataDirectory);

			foreach (Sources.MameSetType setType in Enum.GetValues(typeof(Sources.MameSetType)))
			{
				Tools.ConsoleHeading(2, $"Prepare source: {setType}");

				try
				{
					string setTypeName = setType.ToString();

					foreach (Sources.MameSourceSet sourceSet in Sources.GetSourceSets(setType))
					{
						string metadataFilename = Path.Combine(metaDataDirectory, $"{setTypeName}_{Path.GetFileName(sourceSet.MetadataUrl)}.json");

						dynamic metadata = GetArchiveOrgMeteData(setTypeName, sourceSet.MetadataUrl, metadataFilename);

						string title = metadata.metadata.title;
						string version = "";

						switch (setType)
						{
							case Sources.MameSetType.MachineRom:
								version = title.Substring(5, 5);

								sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata("mame-merged/", metadata);
								break;

							case Sources.MameSetType.MachineDisk:
								version = title.Substring(5, 5);

								sourceSet.AvailableDownloadFileInfos = AvailableDiskFilesInMetadata(metadata);
								break;

							case Sources.MameSetType.SoftwareRom:
								version = title.Substring(8, 5);

								sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata("mame-sl/", metadata);
								break;

							case Sources.MameSetType.SoftwareDisk:
								version = "";

								sourceSet.AvailableDownloadFileInfos = AvailableDiskFilesInMetadata(metadata);
								break;
						}

						version = version.Replace(".", "").Trim();

						sourceSet.Title = title;
						sourceSet.Version = version;

						Console.WriteLine($"Version:\t{version}");

						if (setType == Sources.MameSetType.MachineRom)
						{
							_Version = version;

							_VersionDirectory = Path.Combine(_RootDirectory, _Version);

							if (Directory.Exists(_VersionDirectory) == false)
							{
								Console.WriteLine($"!!! MAME Version Bump: {_Version}");
								Directory.CreateDirectory(_VersionDirectory);
							}
						}
						else
						{
							if (setType != Sources.MameSetType.SoftwareDisk)    //	Source not kept up to date, like others (pot luck)
							{
								if (_Version != version)
									Console.WriteLine($"!!! {setType} on archive.org version mismatch, expected:{_Version} got:{version}. You may have problems.");
							}
						}
					}
				}
				catch (Exception ee)
				{
					ReportError(ee, $"Error in source, you will have problems downloading new things from {setType}.", false);
				}
			}

			//
			// Bits & Bobs
			//

			string reportDirectory = Path.Combine(_RootDirectory, "_REPORTS");
			if (Directory.Exists(reportDirectory) == false)
				Directory.CreateDirectory(reportDirectory);
			_Reports = new Reports(reportDirectory);

			_BadSources = new BadSources(_RootDirectory);

			_Favorites = new Favorites(_RootDirectory);

			_ConsoleHandle = FindWindowByCaption(IntPtr.Zero, Console.Title);

			//
			// MAME Binaries
			//

			string binUrl = _BinariesDownloadUrl.Replace("@VERSION@", _Version);

			Tools.ConsoleHeading(2, new string[] {
				"MAME",
				binUrl,
			});

			string binCacheFilename = Path.Combine(_VersionDirectory, "_" + Path.GetFileName(binUrl));

			string binFilename = Path.Combine(_VersionDirectory, "mame.exe");

			if (Directory.Exists(_VersionDirectory) == false)
			{
				Console.WriteLine($"New MAME version: {_Version}");
				Directory.CreateDirectory(_VersionDirectory);
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

			_MameChdMan = new MameChdMan(_VersionDirectory);

			//
			// Hash Stores
			//

			string directory = Path.Combine(_RootDirectory, "_STORE");
			if (Directory.Exists(directory) == false)
				Directory.CreateDirectory(directory);
			_RomHashStore = new HashStore(directory, Tools.SHA1HexFile);

			directory = Path.Combine(_RootDirectory, "_STORE_DISK");
			
			if (Directory.Exists(directory) == false)
				Directory.CreateDirectory(directory);
			_DiskHashStore = new HashStore(directory, _MameChdMan.Hash);

			directory = Path.Combine(_RootDirectory, "_TEMP");
			if (Directory.Exists(directory) == false)
				Directory.CreateDirectory(directory);
			_DownloadTempDirectory = directory;

			//
			// Database
			//

			_Database = new Database(_Favorites);

			//
			// MAME Machine XML & SQL
			//

			string machineXmlFilename = Path.Combine(_VersionDirectory, "_machine.xml");

			if (File.Exists(machineXmlFilename) == false)
			{
				Console.Write($"Extracting MAME machine XML {machineXmlFilename} ...");
				Mame.ExtractXML(binFilename, machineXmlFilename, "-listxml");
				Console.WriteLine("...done.");
			}

			string machineDatabaseFilename = Path.Combine(_VersionDirectory, "_machine.sqlite");

			_Database.InitializeMachine(machineXmlFilename, machineDatabaseFilename, _AssemblyVersion);

			GC.Collect();

			//
			// MAME Software XML & SQL
			//

			string softwareXmlFilename = Path.Combine(_VersionDirectory, "_software.xml");

			if (File.Exists(softwareXmlFilename) == false)
			{
				Console.Write($"Extracting MAME software XML {softwareXmlFilename} ...");
				Mame.ExtractXML(binFilename, softwareXmlFilename, "-listsoftware");
				Console.WriteLine("...done.");
			}

			string softwareDatabaseFilename = Path.Combine(_VersionDirectory, "_software.sqlite");

			_Database.InitializeSoftware(softwareXmlFilename, softwareDatabaseFilename, _AssemblyVersion);

			GC.Collect();

			//
			// Export
			//

			_Export = new Export(_Database, _RomHashStore, _DiskHashStore, _Reports);

			//
			// Genre
			//

			_Genre = new Genre(_HttpClient, _RootDirectory, _Database);
			_Genre.Initialize();

			//
			// New version Check
			//

			_MameAoLatest = JsonConvert.DeserializeObject<dynamic>(Tools.Query(_HttpClient, "https://api.github.com/repos/sam-ludlow/mame-ao/releases/latest"));

			if (_MameAoLatest.assets.Count != 1)
				throw new ApplicationException("Expected one github release asset." + _MameAoLatest.assets.Count);

			string latestName = Path.GetFileNameWithoutExtension((string)_MameAoLatest.assets[0].name);
			string currentName = $"mame-ao-{_AssemblyVersion}";
			if (latestName != currentName)
				Tools.ConsoleHeading(1, new string[] {
					"New MAME-AO version available",
					"",
					$"{currentName} => {latestName}",
					"",
					"Automatically update with shell command \".up\".",
				});

			Console.WriteLine("");
		}

		private Dictionary<string, Sources.SourceFileInfo> AvailableFilesInMetadata(string find, dynamic metadata)
		{
			var result = new Dictionary<string, Sources.SourceFileInfo>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.StartsWith(find) == true && name.EndsWith(".zip") == true)
				{
					name = name.Substring(find.Length);
					name = name.Substring(0, name.Length - 4);

					result.Add(name, new Sources.SourceFileInfo(file));
				}
			}

			return result;
		}

		private Dictionary<string, Sources.SourceFileInfo> AvailableDiskFilesInMetadata(dynamic metadata)
		{
			var result = new Dictionary<string, Sources.SourceFileInfo>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.EndsWith(".chd") == true)
				{
					name = name.Substring(0, name.Length - 4);

					result.Add(name, new Sources.SourceFileInfo(file));
				}
			}

			return result;
		}

		private dynamic GetArchiveOrgMeteData(string name, string metadataUrl, string metadataCacheFilename)
		{
			if (File.Exists(metadataCacheFilename) == false || (DateTime.Now - File.GetLastWriteTime(metadataCacheFilename) > TimeSpan.FromHours(3)))
			{
				Console.Write($"Downloading {name} metadata JSON {metadataUrl} ...");
				File.WriteAllText(metadataCacheFilename, Tools.PrettyJSON(Tools.Query(_HttpClient, metadataUrl)), Encoding.UTF8);
				Console.WriteLine("...done.");
			}

			Console.Write($"Loading {name} metadata JSON {metadataCacheFilename} ...");
			dynamic metadata = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(metadataCacheFilename, Encoding.UTF8));
			Console.WriteLine("...done.");

			return metadata;
		}

		public void Shell()
		{
			_WebServer = new WebServer(this);
			_WebServer.StartListener();

			Tools.ConsoleHeading(1, new string[] {
				"Remote Listener ready for commands",
				_ListenAddress,
				$"e.g. {_ListenAddress}api/command?line=a2600 et -window"

			});
			Console.WriteLine("");

			Process.Start(_ListenAddress);

			Tools.ConsoleHeading(1, "Shell ready for commands");
			Console.WriteLine("");

			while (true)
			{
				Console.Write($"MAME Shell ({_Version})> ");
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
					ReportError(e, "WORKER ERROR", false);
				}
				finally
				{
					_RunTaskCommand = null;
				}
			});

			_RunTaskCommand = line;
			_RunTask.Start();

			return true;
		}

		public void RunLine(string line)
		{
			string binFilename = Path.Combine(_VersionDirectory, "mame.exe");

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
							_Favorites.RemoveMachine(machine);
						else
							_Favorites.AddMachine(machine);

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
							_Favorites.RemoveSoftware(machine, list, software);
						else
							_Favorites.AddSoftware(machine, list, software);

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
								_Export.MachineRoms(arguments);
								break;
							case "MD":
								_Export.MachineDisks(arguments);
								break;
							case "SR":
								_Export.SoftwareRoms(arguments);
								break;
							case "SD":
								_Export.SoftwareDisks(arguments);
								break;

							case "*":
								_Export.MachineRoms(arguments);
								_Export.MachineDisks(arguments);
								_Export.SoftwareRoms(arguments);
								_Export.SoftwareDisks(arguments);
								break;

							default:
								throw new ApplicationException("Export Unknown type not (MR, MD, SR, SD, *).");

						}
						return;

					case ".report":
						if (parts.Length != 2)
							throw new ApplicationException($"Usage: {parts[0]} <Report Code>" + Environment.NewLine + Environment.NewLine +
								String.Join(Environment.NewLine, _Reports.ReportTypeText()) + Environment.NewLine
								);

						Reports.ReportContext reportContext = new Reports.ReportContext()
						{
							database = _Database,
							romHashStore = _RomHashStore,
							diskHashStore = _DiskHashStore,
							versionDirectory = _VersionDirectory,
						};

						if (_Reports.RunReport(parts[1], reportContext) == false)
							throw new ApplicationException("Report Unknown type.");
						return;

					case ".snap":
						if (parts.Length < 2)
							throw new ApplicationException($"Usage: {parts[0]} <target directory>");

						Mame.CollectSnaps(_RootDirectory, String.Join(" ", parts.Skip(1)), _Reports);
						return;

					case ".svg":
						if (parts.Length < 2)
							throw new ApplicationException($"Usage: {parts[0]} <filename or directory>");

						Tools.Bitmap2SVG(String.Join(" ", parts.Skip(1)));
						return;

					case ".ui":
						Process.Start(_ListenAddress);
						return;

					case ".r":
						_WebServer.RefreshAssets();
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
								HashStore.ValidateHashStore(_RomHashStore, "ROM", _Reports, null);
								break;

							case "DISK":
								HashStore.ValidateHashStore(_DiskHashStore, "DISK", _Reports, null);
								break;

							case "DISKV":
								HashStore.ValidateHashStore(_DiskHashStore, "DISK", _Reports, _MameChdMan);
								break;

							default:
								throw new ApplicationException("Valid Unknown store type (row, disk, diskv).");
						}
						return;

					case ".what":
						Process.Start(_ListenAddress + "api/what");
						return;

					default:
						binFilename = Path.Combine(_RootDirectory, machine.Substring(1), "mame.exe");

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
			DataRow machineRow = _Database.GetMachine(machine);
			if (machineRow == null)
				throw new ApplicationException($"Machine not found: {machine}.");

			if (list == null)
				return;

			DataRow machineListRow = null;
			foreach (DataRow row in _Database.GetMachineSoftwareLists(machineRow))
			{
				if (list == (string)row["name"])
					machineListRow = row;
			}

			if (machineListRow == null)
				throw new ApplicationException($"Machine does not have that software list: {machine}, {list}");

			DataRow softwareListRow = _Database.GetSoftwareList(list);

			if (softwareListRow == null)
				throw new ApplicationException($"Software list not found: {list}");

			DataRow softwareRow = null;
			foreach (DataRow row in _Database.GetSoftwareListsSoftware(softwareListRow))
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

			DataTable table = Mame.ListSavedState(_RootDirectory, _Database);

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
			string currentName = $"mame-ao-{_AssemblyVersion}";

			string updateDirectory = Path.Combine(_RootDirectory, "_TEMP", "UPDATE");

			if (startingPid <= 0)
			{
				string latestName = Path.GetFileNameWithoutExtension((string)_MameAoLatest.assets[0].name);

				if (latestName == currentName)
				{
					Console.WriteLine($"MAME-AO is already up to date '{currentName}'.");
					if (startingPid == 0)
						return;
				}

				Console.WriteLine($"Updating MAME-AO '{currentName}' => '{latestName}'...");

				string archiveUrl = (string)_MameAoLatest.assets[0].browser_download_url;
				string archiveFilename = Path.Combine(_RootDirectory, latestName + ".zip");

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
					WorkingDirectory = _RootDirectory,
					Arguments = $"UPDATE={pid} DIRECTORY=\"{_RootDirectory}\"",
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
					UpdateChild(currentName, updateDirectory, startingPid);
				}
				catch (Exception e)
				{
					ReportError(e, "UPDATE FATAL ERROR", true);
				}
			}
		}

		public void UpdateChild(string currentName, string updateDirectory, int startingPid)
		{
			Console.WriteLine($"MAME-AO UPDATER {currentName}");
			Console.WriteLine($"Target Directory: {_RootDirectory}, Update From Directory {updateDirectory}.");

			Console.WriteLine("Waiting for starting process to exit...");
			using (Process startingProcess = Process.GetProcessById(startingPid))
			{
				startingProcess.WaitForExit();
			}
			Console.WriteLine("...done");

			try
			{
				File.Delete(Path.Combine(_RootDirectory, "mame-ao.exe"));
			}
			catch (UnauthorizedAccessException e)
			{
				throw new ApplicationException("Looks like the starting process is currently running, please kill all mame-ao processes and try again, " + e.Message, e);
			}

			foreach (string sourceFilename in Directory.GetFiles(updateDirectory))
			{
				string targetFilename = Path.Combine(_RootDirectory, Path.GetFileName(sourceFilename));
				if (File.Exists(targetFilename) == true)
					File.Delete(targetFilename);
				File.Copy(sourceFilename, targetFilename);

				Console.WriteLine(targetFilename);
			}

			ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(_RootDirectory, "mame-ao.exe"))
			{
				WorkingDirectory = _RootDirectory,
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
			DataRow machine = _Database.GetMachine(machineName);
			if (machine == null)
				throw new ApplicationException($"Machine not found: {machineName}");

			DataRow[] softwarelists = _Database.GetMachineSoftwareLists(machine);

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

					DataRow softwarelist = _Database.GetSoftwareList(softwarelistName);

					if (softwarelist == null)
					{
						Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
						continue;
					}

					foreach (DataRow findSoftware in _Database.GetSoftwareListsSoftware(softwarelist))
					{
						if ((string)findSoftware["name"] == softwareName)
						{
							// Does this need to be recursive ?
							foreach (DataRow sharedFeat in _Database.GetSoftwareSharedFeats(findSoftware))
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

						DataRow softwarelist = _Database.GetSoftwareList(softwarelistName);

						if (softwarelist == null)
						{
							Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
							continue;
						}

						foreach (DataRow findSoftware in _Database.GetSoftwareListsSoftware(softwarelist))
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
			if (_LinkingEnabled == true)
			{
				Tools.LinkFiles(romStoreFilenames.ToArray());
			}
			else
			{
				foreach (string[] romStoreFilename in romStoreFilenames)
				{
					//	TODO: Prevent duplicates in here
					if (File.Exists(romStoreFilename[0]) == true)
					{
						Console.WriteLine($"WARNING: Place file already exists {romStoreFilename[0]}");
						File.Delete(romStoreFilename[0]);
					}
					File.Copy(romStoreFilename[1], romStoreFilename[0]);
				}
			}

			Console.WriteLine();

			//
			// Info
			//
			Tools.ConsoleHeading(1, new string[] {
				"Machine Information",
				"",
				missingCount == 0 ? "Everything looks good to run MAME" : "!!! Missing ROM & Disk files. I doubt MAME will run !!!",
			});
			Console.WriteLine();

			DataRow[] features = _Database.GetMachineFeatures(machine);

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
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineRom)[0];

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
				DataRow requiredMachine = _Database.GetMachine(requiredMachineName);
				if (requiredMachine == null)
					throw new ApplicationException("requiredMachine not found: " + requiredMachineName);

				//
				// See if ROMS are in the hash store
				//
				HashSet<string> missingRoms = new HashSet<string>();

				foreach (DataRow romRow in _Database.GetMachineRoms(requiredMachine))
				{
					string name = Tools.DataRowValue(romRow, "name");
					string sha1 = Tools.DataRowValue(romRow, "sha1");

					if (name == null || sha1 == null)
						continue;

					bool inStore = _RomHashStore.Exists(sha1);

					Console.WriteLine($"Checking machine ROM: {inStore}\t{sha1}\t{requiredMachineName}\t{name}");

					if (inStore == false)
						missingRoms.Add(sha1);
				}

				//
				// If not then download and import into hash store
				//
				if (missingRoms.Count > 0)
				{
					if (soureSet.AvailableDownloadFileInfos.ContainsKey(requiredMachineName) == true)
					{
						string downloadMachineUrl = soureSet.DownloadUrl;
						downloadMachineUrl = downloadMachineUrl.Replace("@MACHINE@", requiredMachineName);

						long size = soureSet.AvailableDownloadFileInfos[requiredMachineName].size;

						ImportRoms(downloadMachineUrl, $"machine rom: '{requiredMachineName}'", size, missingRoms.ToArray());
					}
				}
			}

			//
			// Check and place ROMs
			//
			int missingCount = 0;

			foreach (string requiredMachineName in requiredMachines)
			{
				DataRow requiredMachine = _Database.GetMachine(requiredMachineName);
				if (requiredMachine == null)
					throw new ApplicationException("requiredMachine not found: " + requiredMachineName);

				foreach (DataRow romRow in _Database.GetMachineRoms(requiredMachine))
				{
					string name = Tools.DataRowValue(romRow, "name");
					string sha1 = Tools.DataRowValue(romRow, "sha1");

					if (name == null || sha1 == null)
						continue;

					string romFilename = Path.Combine(_VersionDirectory, "roms", requiredMachineName, name);
					string romDirectory = Path.GetDirectoryName(romFilename);
					if (Directory.Exists(romDirectory) == false)
						Directory.CreateDirectory(romDirectory);

					bool have = _RomHashStore.Exists(sha1);

					if (have == true)
					{
						if (File.Exists(romFilename) == false)
							romStoreFilenames.Add(new string[] { romFilename, _RomHashStore.Filename(sha1) });
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

		public static Sources.SourceFileInfo MachineDiskAvailableSourceFile(DataRow machineRow, DataRow diskRow, Sources.MameSourceSet soureSet, Database database)
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

				if (soureSet.AvailableDownloadFileInfos.ContainsKey(key) == true)
				{
					Sources.SourceFileInfo fileInfo = soureSet.AvailableDownloadFileInfos[key];

					if (fileInfo.url == null)   //	Do at init not here.
					{
						string diskNameEnc = Uri.EscapeDataString(availableDiskName);

						string diskUrl = soureSet.DownloadUrl;
						diskUrl = diskUrl.Replace("@MACHINE@", availableMachineName);
						diskUrl = diskUrl.Replace("@DISK@", diskNameEnc);

						fileInfo.url = diskUrl;
					}

					return fileInfo;
				}
			}

			return null;
		}

		private int GetDisksMachine(string machineName, List<string[]> romStoreFilenames)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineDisk)[0];

			DataRow machineRow = _Database.GetMachine(machineName);
			if (machineRow == null)
				throw new ApplicationException("GetDisksMachine machine not found: " + machineName);

			DataRow[] diskRows = _Database.GetMachineDisks(machineRow);

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

				bool inStore = _DiskHashStore.Exists(sha1);

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

					Sources.SourceFileInfo sourceFile = MachineDiskAvailableSourceFile(machineRow, diskRow, soureSet, _Database);

					if (sourceFile == null)
					{
						Console.WriteLine($"!!! Available Download Machine Disks not found in source: {machineName}, {diskName}");
					}
					else
					{
						ImportDisk(sourceFile.url, $"machine disk: '{sourceFile.name}'", sha1, sourceFile);
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

				string filename = Path.Combine(_VersionDirectory, "roms", machineName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				bool have = _DiskHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(filename) == false)
						romStoreFilenames.Add(new string[] { filename, _DiskHashStore.Filename(sha1) });
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

			Sources.MameSourceSet[] soureSets = Sources.GetSourceSets(Sources.MameSetType.SoftwareDisk, softwareListName);

			DataRow[] disks = _Database.GetSoftwareDisks(software);

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

				bool inStore = _DiskHashStore.Exists(sha1);

				Console.WriteLine($"Checking software Disk: {inStore}\t{sha1}\t{softwareListName}\t{softwareName}\t{name}");

				if (inStore == false)
					missingDisks.Add(disk);
			}

			//
			// If not then download and import into hash store
			//
			if (missingDisks.Count > 0)
			{
				string downloadSoftwareName = softwareName;
				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					downloadSoftwareName = parentSoftwareName;

				foreach (DataRow disk in missingDisks)
				{
					string diskName = Tools.DataRowValue(disk, "name");
					string sha1 = Tools.DataRowValue(disk, "sha1");

					bool imported = false;
					for (int sourceIndex = 0; sourceIndex < soureSets.Length && imported == false; ++sourceIndex)
					{
						Sources.MameSourceSet sourceSet = soureSets[sourceIndex];

						string key = $"{softwareListName}/{downloadSoftwareName}/{diskName}";

						if (sourceSet.ListName != null && sourceSet.ListName != "*")
							key = $"{downloadSoftwareName}/{diskName}";

						if (sourceSet.AvailableDownloadFileInfos.ContainsKey(key) == false)
							continue;

						string nameEnc = Uri.EscapeDataString(diskName);

						string url = sourceSet.DownloadUrl;
						url = url.Replace("@LIST@", softwareListName);
						url = url.Replace("@SOFTWARE@", downloadSoftwareName);
						url = url.Replace("@DISK@", nameEnc);

						imported = ImportDisk(url, $"software disk: '{key}'", sha1, sourceSet.AvailableDownloadFileInfos[key]);
					}

					if (imported == false)
						throw new ApplicationException($"Software list disk not on archive.org {softwareListName}/{downloadSoftwareName}/{diskName}");
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

				string filename = Path.Combine(_VersionDirectory, "roms", softwareListName, softwareName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				bool have = _DiskHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(filename) == false)
						romStoreFilenames.Add(new string[] { filename, _DiskHashStore.Filename(sha1) });
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
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.SoftwareRom)[0];

			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] roms = _Database.GetSoftwareRoms(software);

			if (roms.Length == 0)
				return 0;

			Tools.ConsoleHeading(2, new string[] {
				"Software ROM",
				$"{softwareListName} / {softwareName}",
			});

			if (soureSet.AvailableDownloadFileInfos.ContainsKey(softwareListName) == false)
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

				bool inStore = _RomHashStore.Exists(sha1);

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

				string downloadSoftwareUrl = soureSet.DownloadUrl;
				downloadSoftwareUrl = downloadSoftwareUrl.Replace("@LIST@", listEnc);
				downloadSoftwareUrl = downloadSoftwareUrl.Replace("@SOFTWARE@", softEnc);

				Dictionary<string, long> softwareSizes = GetSoftwareSizes(softwareListName, soureSet.HtmlSizesUrl, soureSet.Version);

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

				string romFilename = Path.Combine(_VersionDirectory, "roms", softwareListName, softwareName, romName);
				string romDirectory = Path.GetDirectoryName(romFilename);
				if (Directory.Exists(romDirectory) == false)
					Directory.CreateDirectory(romDirectory);

				bool have = _RomHashStore.Exists(sha1);

				if (have == true)
				{
					if (File.Exists(romFilename) == false)
						romStoreFilenames.Add(new string[] { romFilename, _RomHashStore.Filename(sha1) });
				}
				else
				{
					++missingCount;
				}
				Console.WriteLine($"Place software ROM: {have}\t{sha1}\t{softwareListName}\t{softwareName}\t{romName}");
			}

			return missingCount;
		}

		private Dictionary<string, long> GetSoftwareSizes(string listName, string htmlSizesUrl, string version)
		{
			string cacheDirectory = Path.Combine(_RootDirectory, "_METADATA", "SoftwareSizes", version);

			Directory.CreateDirectory(cacheDirectory);

			string filename = Path.Combine(cacheDirectory, listName + ".htm");

			string html;
			if (File.Exists(filename) == false)
			{
				string url = htmlSizesUrl.Replace("@LIST@", listName);
				html = Tools.Query(_HttpClient, url);
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

					string sha1 = _RomHashStore.Hash(romFilename);

					required.Remove(sha1);

					bool imported = _RomHashStore.Add(romFilename, false, sha1);
					Console.WriteLine($"ROM Store Import: {imported} {sha1} {name} {partFilename}");
				}
			}

			foreach (string sha1 in required)
				Console.WriteLine($"!!! Importing missing sha1 in source it won't work. {name} {sha1}");

			return size;
		}

		private bool ImportDisk(string url, string name, string expectedSha1, Sources.SourceFileInfo sourceInfo)
		{
			if (_BadSources.AlreadyDownloaded(sourceInfo) == true)
			{
				Console.WriteLine($"!!! Already Downloaded before and it didn't work (bad in source) chd-sha1:{expectedSha1} source-sha1: {sourceInfo.sha1}");
				return false;
			}

			string tempFilename = Path.Combine(_DownloadTempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + Tools.ValidFileName(name) + ".chd");

			Console.Write($"Downloading {name} size:{Tools.DataSize(sourceInfo.size)} url:{url} ...");
			DateTime startTime = DateTime.Now;
			long size = Tools.Download(url, tempFilename, _DownloadDotSize, 3 * 60);
			TimeSpan took = DateTime.Now - startTime;
			Console.WriteLine("...done");

			decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);
			Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

			if (sourceInfo.size != size)
				Console.WriteLine($"!!! Unexpected downloaded file size expect:{sourceInfo.size} actual:{size}");

			string sha1 = _DiskHashStore.Hash(tempFilename);

			if (sha1 != expectedSha1)
			{
				Console.WriteLine($"!!! Unexpected downloaded CHD SHA1. It's wrong in the source and will not work. expect:{expectedSha1} actual:{sha1}");
				_BadSources.ReportSourceFile(sourceInfo, expectedSha1, sha1);
			}

			if (_Database._AllSHA1s.Contains(sha1) == false)
			{
				Console.WriteLine($"!!! Unkown downloaded CHD SHA1. It will be left in the TEMP directory, {sha1}, {tempFilename}");
				return false;
			}

			bool imported = _DiskHashStore.Add(tempFilename, true, sha1);

			Console.WriteLine($"Disk Store Import: {imported} {sha1} {name}");

			return _DiskHashStore.Exists(expectedSha1);
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

			ImportDirectory(importDirectory, _Database._AllSHA1s, reportTable);

			_Reports.SaveHtmlReport(reportTable, "Import Directory");
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
						sha1 = _DiskHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = _DiskHashStore.Add(filename, false, sha1) ? "" : "Have";
						else
							status = "Unknown";

						reportTable.Rows.Add(name, "DISK", sha1, status);
						break;

					default:
						sha1 = _RomHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = _RomHashStore.Add(filename, false, sha1) ? "" : "Have";
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

			DataRow machineRow = _Database.GetMachine(machineName);

			if (machineRow == null)
				throw new ApplicationException("FindAllMachines machine not found: " + machineName);

			bool hasRoms = (long)machineRow["ao_rom_count"] > 0;

			if (hasRoms == true)
				requiredMachines.Add(machineName);

			string romof = machineRow.IsNull("romof") ? null : (string)machineRow["romof"];

			if (romof != null)
				FindAllMachines(romof, requiredMachines);

			foreach (DataRow row in _Database.GetMachineDeviceRefs(machineName))
				FindAllMachines((string)row["name"], requiredMachines);
		}

	}
}
