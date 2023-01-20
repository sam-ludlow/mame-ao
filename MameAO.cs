using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using System.IO;
using System.Data;
using System.IO.Compression;
using System.Diagnostics;
using System.Xml.Linq;
using System.Reflection;
using System.Web;
using System.Security.Policy;

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

		HashSet<string> _AvailableDownloadMachines;
		HashSet<string> _AvailableDownloadSoftwareLists;

		public MameAOProcessor(string rootDirectory)
		{
			_RootDirectory = rootDirectory;

			_HttpClient = new HttpClient();
			_HttpClient.Timeout = TimeSpan.FromMinutes(15);
		}

		private HashSet<string> AvailableFilesInMetadata(string find, dynamic metadata)
		{
			HashSet<string> result = new HashSet<string>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.StartsWith(find) == true && name.EndsWith(".zip") == true)
				{
					name = name.Substring(find.Length);
					name = name.Substring(0, name.Length - 4);
					result.Add(name);
				}
			}

			return result;
		}

		public void Start()
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;

			Console.WriteLine($"Welcome to Spludlow MAME Shell V{version.Major}.{version.Minor} https://github.com/sam-ludlow/mame-ao");
			Console.WriteLine("");
			Console.WriteLine("Give it a moment the first time you run");
			Console.WriteLine("");
			Console.WriteLine("Usage: type the MAME machine name and press enter e.g. \"mrdo\"");
			Console.WriteLine("       or with MAME arguments e.g. \"mrdo -window\"");
			Console.WriteLine("");
			Console.WriteLine("SL usage: type the MAME machine name and the software name and press enter e.g. \"a2600 et\"");
			Console.WriteLine("       or with MAME arguments e.g. \"a2600 et -window\"");
			Console.WriteLine("");
			Console.WriteLine("Use dot to run mame without a machine e.g. \".\", or with paramters \". -window\"");
			Console.WriteLine("If you have alreay loaded a machine (in current MAME version) you can use the MAME UI, filter on avaialable.");
			Console.WriteLine("");
			Console.WriteLine("! NO support for CHD yet !");
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
			// Hash Store
			//

			string storeDirectory = Path.Combine(_RootDirectory, "_STORE");

			if (Directory.Exists(storeDirectory) == false)
				Directory.CreateDirectory(storeDirectory);

			_RomHashStore = new HashStore(storeDirectory, Tools.SHA1HexFile);

			//
			// Archive.org machine meta data
			//

			dynamic machineMetadata = GetArchiveOrgMeteData("machine", "https://archive.org/metadata/mame-merged", Path.Combine(_RootDirectory, "_machine_metadata.json"));
			
			_AvailableDownloadMachines = AvailableFilesInMetadata("mame-merged/", machineMetadata);

			string title = machineMetadata.metadata.title;
			_Version = title.Substring(5, 5).Trim();
			_Version = _Version.Replace(".", "");

			Console.WriteLine($"Metadata Title:\t{title}");
			Console.WriteLine($"Version:\t{_Version}");

			_VersionDirectory = Path.Combine(_RootDirectory, _Version);

			if (Directory.Exists(_VersionDirectory) == false)
			{
				Console.WriteLine($"New MAME version: {_Version}");
				Directory.CreateDirectory(_VersionDirectory);
			}

			//
			// Archive.org software meta data
			//

			dynamic softwareMetadata = GetArchiveOrgMeteData("software", "https://archive.org/metadata/mame-sl", Path.Combine(_RootDirectory, "_software_metadata.json"));

			_AvailableDownloadSoftwareLists = AvailableFilesInMetadata("mame-sl/", softwareMetadata);

			title = softwareMetadata.metadata.title;
			string softwareVersion = title.Substring(8, 5).Trim();
			softwareVersion = _Version.Replace(".", "");

			Console.WriteLine($"Software Metadata Title:\t{title}");
			Console.WriteLine($"Software Version:\t{softwareVersion}");

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
				File.WriteAllBytes(binCacheFilename, Tools.Download(_HttpClient, binUrl));
				Console.WriteLine("...done.");
			}

			if (File.Exists(binFilename) == false)
			{
				Console.Write($"Extracting MAME binaries {binFilename} ...");
				RunSelfExtract(binCacheFilename);
				Console.WriteLine("...done.");
			}

			//
			// MAME Machine XML
			//

			_MachineDoc = ExtractMameXml("machine", "-listxml", binFilename, Path.Combine(_VersionDirectory, "_machine.xml"));

			//
			// MAME Software XML
			//

			_SoftwareDoc = ExtractMameXml("software", "-listsoftware", binFilename, Path.Combine(_VersionDirectory, "_software.xml"));

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


		private void FindAllSoftware(XElement software, XElement softwareList, HashSet<string> requiredSoftwares)
		{
			requiredSoftwares.Add(software.Attribute("name").Value);

			string cloneof = software.Attribute("cloneof")?.Value;

			if (cloneof != null)
			{
				XElement parentSoftware = softwareList.Descendants("software").Where(e => e.Attribute("name").Value == cloneof).FirstOrDefault();
				if (parentSoftware == null)
					throw new ApplicationException($"Did not find software cloneof {cloneof}");
				FindAllSoftware(parentSoftware, softwareList, requiredSoftwares);
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
			// Software ROMSs
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
							++softwareFound;
						}
					}
				}

				if (softwareFound == 0)
					throw new ApplicationException($"Did not find software: {machineName}, {softwareName}");
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

			XElement[] disks = machine.Elements("disk").ToArray();
			
			if (disks.Length > 0)
			{
				Console.WriteLine("Warning, this machine uses CHD you will have to manually obtain and place. CHD is not yet supported.");
				Console.WriteLine();
			}

			XElement[] features = machine.Elements("feature").ToArray();

			Console.WriteLine($"Name:           {machine.Attribute("name").Value}");
			Console.WriteLine($"Description:    {machine.Element("description")?.Value}");
			Console.WriteLine($"Year:           {machine.Element("year")?.Value}");
			Console.WriteLine($"Manufacturer:   {machine.Element("manufacturer")?.Value}");
			Console.WriteLine($"Status:         {machine.Element("driver")?.Attribute("status")?.Value}");
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
					string romName = (string)rom.Attribute("name");

					if (romName == null)
						continue;

					string sha1 = (string)rom.Attribute("sha1");

					Console.WriteLine($"Checking ROM: {_RomHashStore.Exists(sha1)}\t{requiredMachineName}\t{romName}\t{sha1}");

					if (sha1 != null)
					{
						if (_RomHashStore.Exists(sha1) == false)
							missingRoms.Add(sha1);
					}
				}

				//
				// If not then download and import into hash store
				//
				if (missingRoms.Count > 0)
				{
					if (_AvailableDownloadMachines.Contains(requiredMachineName) == true)
					{
						string downloadMachineUrl = $"https://archive.org/download/mame-merged/mame-merged/{requiredMachineName}.zip";

						using (TempDirectory tempDir = new TempDirectory())
						{
							string archiveFilename = Path.Combine(tempDir.Path, Path.GetFileName(downloadMachineUrl));
							string extractDirectory = Path.Combine(tempDir.Path, "OUT");
							Directory.CreateDirectory(extractDirectory);

							Console.Write($"Downloading ROM ZIP {downloadMachineUrl} ...");
							File.WriteAllBytes(archiveFilename, Tools.Download(_HttpClient, downloadMachineUrl));
							Console.WriteLine($"...done");

							Console.Write($"Extracting ROM ZIP {archiveFilename} ...");
							ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
							Console.WriteLine($"...done");

							Tools.ClearAttributes(tempDir.Path);

							foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
							{
								bool imported = _RomHashStore.Add(romFilename);
								Console.WriteLine($"Store Import: {imported} {requiredMachineName}{romFilename.Substring(extractDirectory.Length)}");
							}
						}
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
					string romName = (string)rom.Attribute("name");
					string sha1 = (string)rom.Attribute("sha1");

					if (sha1 != null)
					{
						string romFilename = Path.Combine(_VersionDirectory, "roms", requiredMachineName, romName);
						string romDirectory = Path.GetDirectoryName(romFilename);
						if (Directory.Exists(romDirectory) == false)
							Directory.CreateDirectory(romDirectory);

						if (_RomHashStore.Exists(sha1) == true)
						{
							if (File.Exists(romFilename) == false)
							{
								Console.WriteLine($"Place software ROM: {requiredMachineName}\t{romName}");
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
			}

			return missingCount;
		}

		private int GetRomsSoftware(XElement software, List<string[]> romStoreFilenames)
		{
			XElement softwareList = software.Parent;

			string softwareListName = softwareList.Attribute("name").Value;
			string softwareName = software.Attribute("name").Value;

			Console.WriteLine($"SOFTWARE list:{softwareListName} software:{software.Attribute("name").Value} ");

			if (_AvailableDownloadSoftwareLists.Contains(softwareListName) == false)
				throw new ApplicationException($"Software list not on archive.org {softwareListName}");

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

			//
			// Check ROMs in store on SHA1
			//
			XElement dataarea = requiredSoftware.Element("part")?.Element("dataarea");

			if (dataarea == null)
				throw new ApplicationException($"Did not find dataarea:{dataarea}");

			HashSet<string> missingRoms = new HashSet<string>();

			foreach (XElement rom in dataarea.Elements("rom"))
			{
				string romName = rom.Attribute("name")?.Value;

				if (romName == null)
					continue;

				string sha1 = rom.Attribute("sha1")?.Value;

				bool inStore = _RomHashStore.Exists(sha1);

				Console.WriteLine($"Checking Software ROM: {inStore}\t{softwareListName}\t{requiredSoftwareName}\t{romName}\t{sha1}");

				if (sha1 != null && inStore == false)
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

				using (TempDirectory tempDir = new TempDirectory())
				{
					string archiveFilename = Path.Combine(tempDir.Path, "archive.zip");
					string extractDirectory = Path.Combine(tempDir.Path, "OUT");
					Directory.CreateDirectory(extractDirectory);

					Console.Write($"Downloading ROM ZIP {downloadSoftwareUrl} ...");
					File.WriteAllBytes(archiveFilename, Tools.Download(_HttpClient, downloadSoftwareUrl));
					Console.WriteLine($"...done");

					Console.Write($"Extracting ROM ZIP {archiveFilename} ...");
					ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
					Console.WriteLine($"...done");

					Tools.ClearAttributes(tempDir.Path);

					foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
					{
						bool imported = _RomHashStore.Add(romFilename);
						Console.WriteLine($"Store Import: {imported} {softwareListName}/{requiredSoftwareName}{romFilename.Substring(extractDirectory.Length)}");
					}
				}
			}

			//
			// Check and place ROMs
			//

			int missingCount = 0;
			foreach (XElement rom in software.Descendants("rom"))
			{
				string romName = (string)rom.Attribute("name");
				string sha1 = (string)rom.Attribute("sha1");

				if (sha1 != null)
				{
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
						Console.WriteLine($"Missing machine ROM: {softwareListName}\t{softwareName}\t{romName}\t{sha1}");
						++missingCount;
					}
				}
			}

			return missingCount;
		}


		public static void FindAllMachines(string machineName, XElement machineDoc, HashSet<string> requiredMachines)
		{
			XElement machine = machineDoc.Descendants("machine").Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("FindAllMachines machine not found: " + machineName);

			bool hasRoms = (machine.Descendants("rom").Count() > 0);

			if (hasRoms == false)
				return;

			if (requiredMachines.Add(machineName) == false)
				return;

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
