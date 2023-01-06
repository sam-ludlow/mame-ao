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


namespace Spludlow.MameAO
{

	public class MameAOProcessor
	{
		private HttpClient _HttpClient;

		private string _RootDirectory;
		private string _VersionDirectory;
		private string _Version;

		private XElement _MachineDoc;

		private Spludlow.HashStore _RomHashStore;

		HashSet<string> _AvailableDownloadMachines = new HashSet<string>();

		public MameAOProcessor(string rootDirectory)
		{
			_RootDirectory = rootDirectory;

			_HttpClient = new HttpClient();
			_HttpClient.Timeout = TimeSpan.FromMinutes(15);
		}

		public void Start()
		{
			Console.WriteLine("Welcome to Spludlow MAME Shell");
			Console.WriteLine("");
			Console.WriteLine("Give it a moment the first time you run");
			Console.WriteLine("");
			Console.WriteLine("Usage: type the MAME machine name and press enter e.g. \"mrdo\"");
			Console.WriteLine("You can also supply MAME arguments e.g. \"mrdo -window\"");
			Console.WriteLine("Use dot to run mame without a machine e.g. \".\", or with paramters \". -window\"");
			Console.WriteLine("");
			Console.WriteLine("! NO support for CHD or SL yet !");
			Console.WriteLine("");
			Console.WriteLine($"Data Directory: {_RootDirectory}");
			Console.WriteLine("");

			//
			// Hash Store
			//

			string storeDirectory = Path.Combine(_RootDirectory, "_STORE");

			if (Directory.Exists(storeDirectory) == false)
				Directory.CreateDirectory(storeDirectory);

			_RomHashStore = new HashStore(storeDirectory, Tools.SHA1HexFile);

			//
			// Archive.org meta data
			//
			string metadataUrl = "https://archive.org/metadata/mame-merged";
			string metadataCacheFilename = Path.Combine(_RootDirectory, "_metadata_mame-merged.json");

			if (File.Exists(metadataCacheFilename) == false || (DateTime.Now - File.GetLastWriteTime(metadataCacheFilename) > TimeSpan.FromHours(3)))
			{
				Console.Write($"Downloading metadata JSON {metadataCacheFilename} ...");
				File.WriteAllText(metadataCacheFilename, Tools.PrettyJSON(Tools.Query(_HttpClient, metadataUrl)), Encoding.UTF8);
				Console.WriteLine("...done.");
			}

			Console.Write($"Loading metadata JSON {metadataCacheFilename} ...");
			dynamic metadata = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(metadataCacheFilename, Encoding.UTF8));
			Console.WriteLine("...done.");


			string find = "mame-merged/";
			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				if (name.StartsWith(find) == true && name.EndsWith(".zip") == true)
				{
					name = name.Substring(find.Length);
					name = name.Substring(0, name.Length - 4);
					_AvailableDownloadMachines.Add(name);
				}
			}

			string title = metadata.metadata.title;
			_Version = title.Substring(5, 5).Trim();
			_Version = _Version.Replace(".", "");

			Console.WriteLine($"Metadata Title:\t{title}");
			Console.WriteLine($"Version:\t{_Version}");

			_VersionDirectory = Path.Combine(_RootDirectory, _Version);

			//
			// MAME Binaries
			//

			string binUrl = "https://github.com/mamedev/mame/releases/download/mame@/mame@b_64bit.exe";
			binUrl = binUrl.Replace("@", _Version);

			string binCacheFilename = Path.Combine(_VersionDirectory, "_" + Path.GetFileName(binUrl));

			string binFilename = Path.Combine(_VersionDirectory, "mame.exe");

			string machineXmlFilename = Path.Combine(_VersionDirectory, "_machine.xml");

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
			// MAME XML
			//

			if (File.Exists(machineXmlFilename) == false)
			{
				Console.Write($"Extracting MAME machine XML {machineXmlFilename} ...");
				ExtractXML(binFilename, machineXmlFilename, "-listxml");
				Console.WriteLine("...done.");
			}
				
			Console.Write($"Loading MAME machine XML {machineXmlFilename} ...");
			_MachineDoc = XElement.Load(machineXmlFilename);
			Console.WriteLine("...done.");

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
				string arguments = "";

				int index = line.IndexOf(" ");

				if (index == -1)
				{
					machine = line;
				}
				else
				{
					machine = line.Substring(0, index);
					arguments = line.Substring(index + 1);
				}

				machine = machine.ToLower();

				try
				{
					if (machine.StartsWith(".") == true)
					{
						RunMame(binFilename, arguments);
					}
					else
					{
						if (machine != "")
							GetRoms(machine);

						RunMame(binFilename, machine + " " + arguments);
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

				if (process.ExitCode == 0)
					Console.WriteLine("...MAME Exit OK.");
				else
					Console.WriteLine($"...MAME Exit BAD: {process.ExitCode}");
			}
		}

		public void GetRoms(string machineName)
		{
			XElement machine = _MachineDoc.Descendants("machine")
					  .Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("machine not found");

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
							string archiveFilename = Path.Combine(tempDir._Path, Path.GetFileName(downloadMachineUrl));
							string extractDirectory = Path.Combine(tempDir._Path, "OUT");
							Directory.CreateDirectory(extractDirectory);

							Console.Write($"Downloading ROM ZIP {downloadMachineUrl} ...");
							File.WriteAllBytes(archiveFilename, Tools.Download(_HttpClient, downloadMachineUrl));
							Console.WriteLine($"...done");

							Console.Write($"Extracting ROM ZIP {archiveFilename} ...");
							ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
							Console.WriteLine($"...done");

							Tools.ClearAttributes(tempDir._Path);

							foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
							{
								bool imported = _RomHashStore.Add(romFilename);
								Console.WriteLine($"Store Import: {imported} {requiredMachineName}/{Path.GetFileName(romFilename)}");
							}
						}
					}
				}
			}

			//
			// Copy ROMs from hash store to MAME rom directory if required
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
								Console.WriteLine($"Place ROM: {requiredMachineName}\t{romName}");
								File.Copy(_RomHashStore.Filename(sha1), romFilename);
							}
						}
						else
						{
							Console.WriteLine($"Missing ROM: {requiredMachineName}\t{romName}\t{sha1}");
							++missingCount;
						}
					}
				}
			}

			if (missingCount == 0)
				Console.WriteLine("Looking good to run MAME.");
			else
				Console.WriteLine("Missing ROMs I doubt MAME will run.");
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
