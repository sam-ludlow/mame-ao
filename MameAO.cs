using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Data;
using System.IO.Compression;
using System.Diagnostics;
using System.Xml.Linq;
using System.Reflection;
using System.Web;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class MameAOProcessor
	{
		private HttpClient _HttpClient;

		private string _RootDirectory;
		private string _VersionDirectory;
		private string _Version;

		private bool _LinkingEnabled = false;

		private XElement _MachineDoc;
		private XElement _SoftwareDoc;

		private Spludlow.HashStore _RomHashStore;
		private Spludlow.HashStore _DiskHashStore;

		private string _DownloadTempDirectory;

		Dictionary<string, long> _AvailableDownloadMachines;
		Dictionary<string, long> _AvailableDownloadMachineDisks;
		Dictionary<string, long> _AvailableDownloadSoftwareLists;
		Dictionary<string, long> _AvailableDownloadSoftwareListDisks;

		private MameChdMan _MameChdMan;

		private long _DownloadDotSize = 1024 * 1024;

		public MameAOProcessor(string rootDirectory)
		{
			_RootDirectory = rootDirectory;
			_HttpClient = new HttpClient();
		}

		public void Start()
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;

			Console.WriteLine($"Welcome to Spludlow MAME Shell V{version.Major}.{version.Minor} https://github.com/sam-ludlow/mame-ao");
			Console.WriteLine("");
			Console.WriteLine("Give it a moment the first time you run");
			Console.WriteLine("");
			Console.WriteLine("Usage: type the MAME machine name and press enter e.g. \"mrdo\"");
			Console.WriteLine("       or a CHD e.g. \"gauntleg\"");
			Console.WriteLine("       or with MAME arguments e.g. \"mrdo -window\"");
			Console.WriteLine("");
			Console.WriteLine("SL usage: type the MAME machine name and the software name and press enter e.g. \"a2600 et\"");
			Console.WriteLine("       or a CHD e.g. \"cdimono1 aidsawar\" (Note: Not all CHD SLs are not currently available)");
			Console.WriteLine("       or with MAME arguments e.g. \"a2600 et -window\"");
			Console.WriteLine("");
			Console.WriteLine("Use dot to run mame without a machine e.g. \".\", or with paramters \". -window\"");
			Console.WriteLine("If you have alreay loaded a machine (in current MAME version) you can use the MAME UI, filter on avaialable.");
			Console.WriteLine("");
			Console.WriteLine("WARNING: Large downloads like CHD will take a while, each dot represents 1 MiB (about a floppy disk) you do the maths.");
			Console.WriteLine("");
			Console.WriteLine($"Data Directory: {_RootDirectory}");
			Console.WriteLine("");

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

			Console.WriteLine($"Symbolic Links Enabled: {_LinkingEnabled}");
			if (_LinkingEnabled == false)
				Console.WriteLine("!!! You can save a lot of disk space by enabling symbolic links, see the README.");
			Console.WriteLine();

			//
			// Archive.org machine meta data
			//

			dynamic machineMetadata = GetArchiveOrgMeteData("machine", "https://archive.org/metadata/mame-merged", Path.Combine(_RootDirectory, "_machine_metadata.json"));
			
			_AvailableDownloadMachines = AvailableFilesInMetadata("mame-merged/", machineMetadata);

			string title = machineMetadata.metadata.title;
			_Version = title.Substring(5, 5).Trim();
			_Version = _Version.Replace(".", "");

			Console.WriteLine($"Version:\t{_Version}");

			_VersionDirectory = Path.Combine(_RootDirectory, _Version);

			if (Directory.Exists(_VersionDirectory) == false)
			{
				Console.WriteLine($"New MAME version: {_Version}");
				Directory.CreateDirectory(_VersionDirectory);
			}

			//
			// Archive.org machine CHD meta data
			//

			dynamic machineDiskMetadata = GetArchiveOrgMeteData("machine disk", "https://archive.org/metadata/mame-chds-roms-extras-complete", Path.Combine(_RootDirectory, "_machine_disk_metadata.json"));

			_AvailableDownloadMachineDisks = AvailableDiskFilesInMetadata(machineDiskMetadata);

			title = machineDiskMetadata.metadata.title;
			string machineDiskVersion = title.Substring(5, 5).Trim();
			machineDiskVersion = machineDiskVersion.Replace(".", "");

			Console.WriteLine($"Machine Disk Version:\t{machineDiskVersion}");

			if (_Version != machineDiskVersion)
				Console.WriteLine("!!! Machine disks (CHD) on archive.org version mismatch, you may have problems.");


			//
			// Archive.org software meta data
			//

			dynamic softwareMetadata = GetArchiveOrgMeteData("software", "https://archive.org/metadata/mame-sl", Path.Combine(_RootDirectory, "_software_metadata.json"));

			_AvailableDownloadSoftwareLists = AvailableFilesInMetadata("mame-sl/", softwareMetadata);

			title = softwareMetadata.metadata.title;
			string softwareVersion = title.Substring(8, 5).Trim();
			softwareVersion = softwareVersion.Replace(".", "");

			Console.WriteLine($"Software Version:\t{softwareVersion}");

			if (_Version != softwareVersion)
				Console.WriteLine("!!! Software lists on archive.org version mismatch, you may have problems.");

			//
			// Archive.org software CHD meta data
			//

			dynamic softwareDiskMetadata = GetArchiveOrgMeteData("software disk", "https://archive.org/metadata/mame-software-list-chds-2", Path.Combine(_RootDirectory, "_software_disk_metadata.json"));

			_AvailableDownloadSoftwareListDisks = AvailableDiskFilesInMetadata(softwareDiskMetadata);

			// Not bothering with version for SL disks at the moment !!!


			//
			// MAME Binaries
			//

			string binUrl = "https://github.com/mamedev/mame/releases/download/mame@/mame@b_64bit.exe";
			binUrl = binUrl.Replace("@", _Version);

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
				RunSelfExtract(binCacheFilename);
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
			// MAME Machine XML
			//

			_MachineDoc = ExtractMameXml("machine", "-listxml", binFilename, Path.Combine(_VersionDirectory, "_machine.xml"));

			//
			// MAME Software XML
			//

			_SoftwareDoc = ExtractMameXml("software", "-listsoftware", binFilename, Path.Combine(_VersionDirectory, "_software.xml"));

		}

		private Dictionary<string, long> AvailableFilesInMetadata(string find, dynamic metadata)
		{
			Dictionary<string, long> result = new Dictionary<string, long>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.StartsWith(find) == true && name.EndsWith(".zip") == true)
				{
					name = name.Substring(find.Length);
					name = name.Substring(0, name.Length - 4);
					long size = Int64.Parse((string)file.size);
					result.Add(name, size);
				}
			}

			return result;
		}

		private Dictionary<string, long> AvailableDiskFilesInMetadata(dynamic metadata)
		{
			Dictionary<string, long> result = new Dictionary<string, long>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.EndsWith(".chd") == true)
				{
					long size = Int64.Parse((string)file.size);
					result.Add(name.Substring(0, name.Length - 4), size);
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

		private XElement ExtractMameXml(string name, string argument, string binFilename, string filename)
		{
			if (File.Exists(filename) == false)
			{
				Console.Write($"Extracting MAME {name} XML {filename} ...");
				ExtractXML(binFilename, filename, argument);
				Console.WriteLine("...done.");
			}

			Console.Write($"Loading MAME {name} XML {filename} ...");
			XElement doc = XElement.Load(filename);
			Console.WriteLine("...done.");

			return doc;
		}
		public void Run()
		{
			Console.WriteLine("");

			string binFilename = Path.Combine(_VersionDirectory, "mame.exe");

			while (true)
			{
				Console.Write($"MAME Shell ({_Version})> ");
				string line = Console.ReadLine();
				line = line.Trim();

				if (line.Length == 0)
					continue;

				string machine;
				string software = "";
				string arguments = "";

				string[] parts = line.Split(new char[] {' '});

				machine = parts[0];

				if (machine == ".")
				{
					if (parts.Length > 1)
						arguments = String.Join(" ", parts.Skip(1));
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

				try
				{
					if (machine.StartsWith(".") == true)
					{
						RunMame(binFilename, arguments);
					}
					else
					{
						GetRoms(machine, software);
						RunMame(binFilename, machine + " " + software + " " + arguments);
					}
				}
				catch (ApplicationException ee)
				{
					Console.WriteLine("SHELL ERROR: " + ee.Message);
				}
				catch (Exception ee)
				{
					Console.WriteLine();
					Console.WriteLine("!!! FATAL ERROR: " + ee.Message);
					Console.WriteLine();
					Console.WriteLine(ee.ToString());
					Console.WriteLine();
					Console.WriteLine("Press any key to continue, program has crashed and will exit.");
					Console.WriteLine("If you want to submit an error report please copy and paste the text from here.");
					Console.WriteLine("Select All (Ctrl+A) -> Copy (Ctrl+C) -> notepad -> paste (Ctrl+V)");

					Console.ReadKey();
					Environment.Exit(1);
				}
			}
		}

		public void RunMame(string binFilename, string arguments)
		{
			Console.WriteLine($"Starting MAME: {arguments} {binFilename} ...");
			Console.WriteLine();

			string directory = Path.GetDirectoryName(binFilename);

			ProcessStartInfo startInfo = new ProcessStartInfo(binFilename);
			startInfo.WorkingDirectory = directory;
			startInfo.Arguments = arguments;

			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.StandardOutputEncoding = Encoding.UTF8;

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						Console.WriteLine($"MAME:{e.Data}");
				});

				process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						Console.WriteLine($"MAME ERROR:{e.Data}");
				});

				process.Start();
				process.BeginOutputReadLine();
				process.WaitForExit();

				Console.WriteLine();
				if (process.ExitCode == 0)
					Console.WriteLine("...MAME Exit OK.");
				else
					Console.WriteLine($"...MAME Exit BAD: {process.ExitCode}");
			}
		}

		public void GetRoms(string machineName, string softwareName)
		{
			//
			// Machine
			//
			XElement machine = _MachineDoc.Descendants("machine")
					  .Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("machine not found");

			XElement[] softwarelists = machine.Elements("softwarelist").ToArray();

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

				foreach (XElement machineSoftwarelist in softwarelists)
				{
					string softwarelistName = machineSoftwarelist.Attribute("name").Value;

					XElement softwarelist = _SoftwareDoc.Descendants("softwarelist")
						.Where(e => e.Attribute("name").Value == softwarelistName).FirstOrDefault();

					if (softwarelist == null)
						throw new ApplicationException($"Software list on machine but not in SL {softwarelistName}");

					foreach (XElement findSoftware in softwarelist.Elements("software"))
					{
						if (findSoftware.Attribute("name").Value == softwareName)
						{
							missingCount += GetRomsSoftware(findSoftware, romStoreFilenames);

							missingCount += GetDiskSoftware(findSoftware, romStoreFilenames);

							++softwareFound;
						}
					}
				}

				if (softwareFound == 0)
					throw new ApplicationException($"Did not find software: {machineName}, {softwareName}");

				if (softwareFound > 1)
					Console.WriteLine("! Warning more than one software found, not sure which MAME will use. This can happern if the same name apears in different lists e.g. disk & cassette.");
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
					File.Copy(romStoreFilename[1], romStoreFilename[0]);
			}

			//
			// Info
			//
			Console.WriteLine();

			if (missingCount == 0)
				Console.WriteLine("ROMs look good to run MAME.");
			else
				Console.WriteLine("Missing ROMs I doubt MAME will run.");

			Console.WriteLine();

			XElement[] features = machine.Elements("feature").ToArray();

			Console.WriteLine($"Name:           {machine.Attribute("name").Value}");
			Console.WriteLine($"Description:    {machine.Element("description")?.Value}");
			Console.WriteLine($"Year:           {machine.Element("year")?.Value}");
			Console.WriteLine($"Manufacturer:   {machine.Element("manufacturer")?.Value}");
			Console.WriteLine($"Status:         {machine.Element("driver")?.Attribute("status")?.Value}");
			Console.WriteLine($"isbios:         {machine.Attribute("isbios")?.Value}");
			Console.WriteLine($"isdevice:       {machine.Attribute("isdevice")?.Value}");
			Console.WriteLine($"ismechanical:   {machine.Attribute("ismechanical")?.Value}");
			Console.WriteLine($"runnable:       {machine.Attribute("runnable")?.Value}");

			foreach (XElement feature in features)
				Console.WriteLine($"Feature issue:  {feature.Attribute("type")?.Value} {feature.Attribute("status")?.Value}");
			foreach (XElement softwarelist in softwarelists)
				Console.WriteLine($"Software list:  {softwarelist.Attribute("name")?.Value}");
			Console.WriteLine();

		}

		private int GetRomsMachine(string machineName, List<string[]> romStoreFilenames)
		{
			//
			// Related/Required machines (parent/bios/devices)
			//
			HashSet<string> requiredMachines = new HashSet<string>();

			FindAllMachines(machineName, _MachineDoc, requiredMachines);

			Console.WriteLine($"Required machines (parent/bios/device): {String.Join(", ", requiredMachines.ToArray())}");

			foreach (string requiredMachineName in requiredMachines)
			{
				XElement requiredMachine = _MachineDoc.Descendants("machine")
					.Where(e => e.Attribute("name").Value == requiredMachineName).FirstOrDefault();

				//
				// See if ROMS are in the hash store
				//
				HashSet<string> missingRoms = new HashSet<string>();

				foreach (XElement rom in requiredMachine.Descendants("rom"))
				{
					string romName = rom.Attribute("name")?.Value;
					string sha1 = rom.Attribute("sha1")?.Value;

					if (romName == null || sha1 == null)
						continue;

					bool inStore = _RomHashStore.Exists(sha1);

					Console.WriteLine($"Checking machine ROM: {inStore}\t{requiredMachineName}\t{romName}\t{sha1}");

					if (inStore == false)
						missingRoms.Add(sha1);
				}

				//
				// If not then download and import into hash store
				//
				if (missingRoms.Count > 0)
				{
					if (_AvailableDownloadMachines.ContainsKey(requiredMachineName) == true)
					{
						string downloadMachineUrl = $"https://archive.org/download/mame-merged/mame-merged/{requiredMachineName}.zip";

						long size = _AvailableDownloadMachines[requiredMachineName];

						ImportRoms(downloadMachineUrl, $"machine:{requiredMachineName}", size);
					}
				}
			}

			//
			// Check and place ROMs
			//
			int missingCount = 0;

			foreach (string requiredMachineName in requiredMachines)
			{
				XElement requiredMachine = _MachineDoc.Descendants("machine")
					.Where(e => e.Attribute("name").Value == requiredMachineName).FirstOrDefault();

				foreach (XElement rom in requiredMachine.Descendants("rom"))
				{
					string romName = rom.Attribute("name")?.Value;
					string sha1 = rom.Attribute("sha1")?.Value;

					if (romName == null || sha1 == null)
						continue;

					string romFilename = Path.Combine(_VersionDirectory, "roms", requiredMachineName, romName);
					string romDirectory = Path.GetDirectoryName(romFilename);
					if (Directory.Exists(romDirectory) == false)
						Directory.CreateDirectory(romDirectory);

					if (_RomHashStore.Exists(sha1) == true)
					{
						if (File.Exists(romFilename) == false)
						{
							Console.WriteLine($"Place machine ROM: {requiredMachineName}\t{romName}");
							romStoreFilenames.Add(new string[] { romFilename, _RomHashStore.Filename(sha1) });
						}
					}
					else
					{
						Console.WriteLine($"Missing machine ROM: {requiredMachineName}\t{romName}\t{sha1}");
						++missingCount;
					}
				}
			}

			return missingCount;
		}

		private int GetDisksMachine(string machineName, List<string[]> romStoreFilenames)
		{
			XElement machine = _MachineDoc.Descendants("machine").Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("GetDisksMachine machine not found: " + machineName);

			XElement[] disks = machine.Elements("disk").ToArray();

			if (disks.Length == 0)
				return 0;

			string parentMachineName = machine.Attribute("romof")?.Value;

			//
			// See if Disks are in the hash store
			//
			HashSet<XElement> missingDisks = new HashSet<XElement>();

			foreach (XElement disk in disks)
			{
				string name = disk.Attribute("name")?.Value;
				string sha1 = disk.Attribute("sha1")?.Value;

				if (disk == null || sha1 == null)
					continue;

				bool inStore = _DiskHashStore.Exists(sha1);

				Console.WriteLine($"Checking machine Disk: {inStore}\t{machineName}\t{name}\t{sha1}");

				if (inStore == false)
					missingDisks.Add(disk);
			}

			//
			// If not then download and import into hash store
			//
			if (missingDisks.Count > 0)
			{
				foreach (XElement disk in missingDisks)
				{
					string diskName = disk.Attribute("name")?.Value;
					string sha1 = disk.Attribute("sha1")?.Value;

					string merge = disk.Attribute("merge")?.Value;

					string availableMachineName = machineName;
					string availableDiskName = diskName;

					if (merge != null)
					{
						if (parentMachineName == null)
							throw new ApplicationException($"machine disk merge without parent {machineName}");

						availableMachineName = parentMachineName;
						availableDiskName = merge;
					}

					string key = $"{availableMachineName}/{availableDiskName}";

					if (_AvailableDownloadMachineDisks.ContainsKey(key) == false)
						throw new ApplicationException($"Available Download Machine Disks not found key:{key}");

					long size = _AvailableDownloadMachineDisks[key];

					string diskNameEnc = Uri.EscapeUriString(availableDiskName);
					string diskUrl = $"https://archive.org/download/mame-chds-roms-extras-complete/{availableMachineName}/{diskNameEnc}.chd";

					string tempFilename = Path.Combine(_DownloadTempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + availableDiskName);

					Console.Write($"Downloading {key} Machine Disk. size:{Tools.DataSize(size)} url:{diskUrl} ...");
					DateTime startTime = DateTime.Now;
					long downloadSize = Tools.Download(diskUrl, tempFilename, _DownloadDotSize, 3 * 60);
					TimeSpan took = DateTime.Now - startTime;
					Console.WriteLine($"...done");

					if (size != downloadSize)
						Console.WriteLine($"Unexpected downloaded file size expect:{size} actual:{downloadSize}");

					bool imported = _DiskHashStore.Add(tempFilename, true);

					Console.WriteLine($"Disk Store Import: {imported} {key}");
				}
			}

			//
			// Check and place
			//

			int missing = 0;

			foreach (XElement disk in disks)
			{
				string name = disk.Attribute("name")?.Value;
				string sha1 = disk.Attribute("sha1")?.Value;

				if (disk == null || sha1 == null)
					continue;

				string filename = Path.Combine(_VersionDirectory, "roms", machineName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				if (_DiskHashStore.Exists(sha1) == true)
				{
					if (File.Exists(filename) == false)
					{
						Console.WriteLine($"Place machine Disk: {machineName}\t{name}");
						romStoreFilenames.Add(new string[] { filename, _DiskHashStore.Filename(sha1) });
					}
				}
				else
				{
					Console.WriteLine($"Missing machine Disk: {machineName}\t{name}\t{sha1}");
					++missing;
				}
			}

			return missing;
		}

		private int GetDiskSoftware(XElement software, List<string[]> romStoreFilenames)
		{
			// think don't need to look for parents ?

			XElement softwareList = software.Parent;

			string softwareListName = softwareList.Attribute("name").Value;
			string softwareName = software.Attribute("name").Value;


			XElement diskarea = software.Element("part")?.Element("diskarea");

			if (diskarea == null)
				return 0;

			XElement[] disks = diskarea.Elements("disk").ToArray();

			//
			// See if Disks are in the hash store
			//
			HashSet<XElement> missingDisks = new HashSet<XElement>();

			foreach (XElement disk in disks)
			{
				string name = disk.Attribute("name")?.Value;
				string sha1 = disk.Attribute("sha1")?.Value;

				if (disk == null || sha1 == null)
					continue;

				bool inStore = _DiskHashStore.Exists(sha1);

				Console.WriteLine($"Checking software Disk: {inStore}\t{softwareListName}\t{softwareName}\t{name}\t{sha1}");

				if (inStore == false)
					missingDisks.Add(disk);
			}


			//
			// If not then download and import into hash store
			//
			if (missingDisks.Count > 0)
			{
				foreach (XElement disk in missingDisks)
				{
					string diskName = disk.Attribute("name")?.Value;
					string sha1 = disk.Attribute("sha1")?.Value;

					string key = $"{softwareListName}/{softwareName}/{diskName}";

					if (_AvailableDownloadSoftwareListDisks.ContainsKey(key) == false)
						throw new ApplicationException($"Software list disk not on archive.org {key}");

					long size = _AvailableDownloadSoftwareListDisks[key];

					//check size ????

					string nameEnc = Uri.EscapeUriString(diskName);
					string url = $"https://archive.org/download/mame-software-list-chds-2/{softwareListName}/{softwareName}/{nameEnc}.chd";

					string tempFilename = Path.Combine(_DownloadTempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + diskName);

					Console.Write($"Downloading {key} Software Disk. size:{Tools.DataSize(size)} url:{url} ...");
					DateTime startTime = DateTime.Now;
					long downloadSize = Tools.Download(url, tempFilename, _DownloadDotSize, 3 * 60);
					TimeSpan took = DateTime.Now - startTime;
					Console.WriteLine($"...done");

					if (size != downloadSize)
						Console.WriteLine($"Unexpected downloaded file size expect:{size} actual:{downloadSize}");

					bool imported = _DiskHashStore.Add(tempFilename, true);

					Console.WriteLine($"Disk Store Import: {imported} {key}");
				}
			}


			//
			// Check and place
			//

			int missing = 0;

			foreach (XElement disk in disks)
			{
				string name = disk.Attribute("name")?.Value;
				string sha1 = disk.Attribute("sha1")?.Value;

				if (disk == null || sha1 == null)
					continue;

				string filename = Path.Combine(_VersionDirectory, "roms", softwareListName, softwareName, name + ".chd");
				string directory = Path.GetDirectoryName(filename);

				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);

				if (_DiskHashStore.Exists(sha1) == true)
				{
					if (File.Exists(filename) == false)
					{
						Console.WriteLine($"Place software Disk: {softwareListName}\t{softwareName}\t{name}");
						romStoreFilenames.Add(new string[] { filename, _DiskHashStore.Filename(sha1) });
					}
				}
				else
				{
					Console.WriteLine($"Missing softwsre Disk: {softwareListName}\t{softwareName}\t{name}\t{sha1}");
					++missing;
				}
			}

			return missing;
		}

		private int GetRomsSoftware(XElement software, List<string[]> romStoreFilenames)
		{
			XElement softwareList = software.Parent;

			string softwareListName = softwareList.Attribute("name").Value;
			string softwareName = software.Attribute("name").Value;

			Console.WriteLine($"SOFTWARE list:{softwareListName} software:{software.Attribute("name").Value} ");

			//
			// Find parent software set
			//
			XElement requiredSoftware = software;
			string cloneof = null;
			while ((cloneof = requiredSoftware.Attribute("cloneof")?.Value) != null)
			{
				requiredSoftware = softwareList.Descendants("software").Where(e => e.Attribute("name").Value == cloneof).FirstOrDefault();
				if (requiredSoftware == null)
					throw new ApplicationException($"Did not find software cloneof parent: {cloneof}");
			}
			string requiredSoftwareName = requiredSoftware.Attribute("name").Value;

			Console.WriteLine($"Required software: {softwareName} => {requiredSoftwareName}");

			// !!!!!!! Think the parent should only be used for DL not checking missing ?????

			//
			// Find and check has roms
			//
			XElement dataarea = requiredSoftware.Element("part")?.Element("dataarea");

			if (dataarea == null)
				return 0;

			if (_AvailableDownloadSoftwareLists.ContainsKey(softwareListName) == false)
				throw new ApplicationException($"Software list not on archive.org {softwareListName}");

			//
			// Check ROMs in store on SHA1
			//
			HashSet<string> missingRoms = new HashSet<string>();

			foreach (XElement rom in dataarea.Elements("rom"))
			{
				string romName = rom.Attribute("name")?.Value;
				string sha1 = rom.Attribute("sha1")?.Value;

				if (romName == null || sha1 == null)
					continue;

				bool inStore = _RomHashStore.Exists(sha1);

				Console.WriteLine($"Checking Software ROM: {inStore}\t{softwareListName}\t{requiredSoftwareName}\t{romName}\t{sha1}");

				if (inStore == false)
					missingRoms.Add(sha1);
			}

			//
			// Download ROMs and import to store
			//
			if (missingRoms.Count > 0)
			{
				string listEnc = Uri.EscapeUriString(softwareListName);
				string softEnc = Uri.EscapeUriString(requiredSoftwareName);
				string slashEnc = HttpUtility.UrlEncode("/");

				string downloadSoftwareUrl = $"https://archive.org/download/mame-sl/mame-sl/{listEnc}.zip/{listEnc}{slashEnc}{softEnc}.zip";

				Dictionary<string, long> softwareSizes = GetSoftwareSizes(softwareListName);

				if (softwareSizes.ContainsKey(requiredSoftwareName) == false)
					throw new ApplicationException($"Did GetSoftwareSize {softwareListName}, {requiredSoftwareName} ");

				long size = softwareSizes[requiredSoftwareName];

				ImportRoms(downloadSoftwareUrl, $"software:{softwareListName}, {requiredSoftwareName}", size);
			}

			//
			// Check and place ROMs
			//

			int missingCount = 0;
			foreach (XElement rom in software.Descendants("rom"))
			{
				string romName = rom.Attribute("name")?.Value;
				string sha1 = rom.Attribute("sha1")?.Value;

				if (romName == null || sha1 == null)
					continue;

				string romFilename = Path.Combine(_VersionDirectory, "roms", softwareListName, softwareName, romName);
				string romDirectory = Path.GetDirectoryName(romFilename);
				if (Directory.Exists(romDirectory) == false)
					Directory.CreateDirectory(romDirectory);

				if (_RomHashStore.Exists(sha1) == true)
				{
					if (File.Exists(romFilename) == false)
					{
						Console.WriteLine($"Place software ROM: {softwareListName}\t{softwareName}\t{romName}");
						romStoreFilenames.Add(new string[] { romFilename, _RomHashStore.Filename(sha1) });
					}
				}
				else
				{
					Console.WriteLine($"Missing software ROM: {softwareListName}\t{softwareName}\t{romName}\t{sha1}");
					++missingCount;
				}
			}

			return missingCount;
		}

		private Dictionary<string, long> GetSoftwareSizes(string listName)
		{
			string cacheDirectory = Path.Combine(_VersionDirectory, "_SoftwareSizes");
			if (Directory.Exists(cacheDirectory) == false)
				Directory.CreateDirectory(cacheDirectory);

			string filename = Path.Combine(cacheDirectory, listName + ".htm");

			string html;
			if (File.Exists(filename) == false)
			{
				html = Tools.Query(_HttpClient, $"https://archive.org/download/mame-sl/mame-sl/{listName}.zip/");
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

		private long ImportRoms(string url, string name, long expectedSize)
		{
			long size = 0;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "archive.zip");
				string extractDirectory = Path.Combine(tempDir.Path, "OUT");
				Directory.CreateDirectory(extractDirectory);

				Console.Write($"Downloading {name} ROM ZIP size:{Tools.DataSize(expectedSize)} url:{url} ...");
				DateTime startTime = DateTime.Now;
				size = Tools.Download(url, archiveFilename, _DownloadDotSize, 30);
				TimeSpan took = DateTime.Now - startTime;
				Console.WriteLine($"...done");

				if (size != expectedSize)
					Console.WriteLine($"Unexpected downloaded file size expect:{expectedSize} actual:{size}");

				decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);

				Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

				Console.Write($"Extracting {name} ROM ZIP {archiveFilename} ...");
				ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
				Console.WriteLine($"...done");

				Tools.ClearAttributes(tempDir.Path);

				foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
				{
					bool imported = _RomHashStore.Add(romFilename);
					Console.WriteLine($"Store Import: {imported} {name} {romFilename.Substring(extractDirectory.Length)}");
				}
			}

			return size;
		}

		public static void FindAllMachines(string machineName, XElement machineDoc, HashSet<string> requiredMachines)
		{
			if (requiredMachines.Contains(machineName) == true)
				return;

			XElement machine = machineDoc.Descendants("machine").Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("FindAllMachines machine not found: " + machineName);

			bool hasRoms = (machine.Descendants("rom").Count() > 0);

			if (hasRoms == true)
				requiredMachines.Add(machineName);

			string romof = machine.Attribute("romof")?.Value;

			if (romof != null)
				FindAllMachines(romof, machineDoc, requiredMachines);

			foreach (XElement device_ref in machine.Descendants("device_ref"))
				FindAllMachines(device_ref.Attribute("name").Value, machineDoc, requiredMachines);
		}

		public void RunSelfExtract(string filename)
		{
			string directory = Path.GetDirectoryName(filename);

			ProcessStartInfo startInfo = new ProcessStartInfo(filename);
			startInfo.WorkingDirectory = directory;
			startInfo.Arguments = "-y";

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.Start();
				process.WaitForExit();

				if (process.ExitCode != 0)
					throw new ApplicationException("Bad exit code");
			}
		}

		public void ExtractXML(string binFilename, string outputFilename, string arguments)
		{
			string directory = Path.GetDirectoryName(binFilename);

			using (StreamWriter writer = new StreamWriter(outputFilename, false, Encoding.UTF8))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(binFilename);
				startInfo.WorkingDirectory = directory;
				startInfo.Arguments = arguments;
				startInfo.UseShellExecute = false;
				startInfo.RedirectStandardOutput = true;
				startInfo.StandardOutputEncoding = Encoding.UTF8;

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
					{
						if (e.Data != null)
							writer.WriteLine(e.Data);
					});

					process.Start();
					process.BeginOutputReadLine();
					process.WaitForExit();

					if (process.ExitCode != 0)
						throw new ApplicationException("ExtractXML Bad exit code");
				}
			}
		}
	}
}
