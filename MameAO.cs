using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http;
using Newtonsoft.Json;
using System.IO;
using System.Data;

using System.IO.Compression;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using System.Security.Cryptography;

namespace Spludlow.MameAO
{

	public class MameAOProcessor
	{
		private HttpClient _HttpClient;

		private string _RootDirectory;
		private string _StoreDirectory;
		private string _VersionDirectory;

		private Spludlow.HashStore _RomHashStore;

		private static SHA1Managed _SHA1Managed;


		public static string SHA1HexFile(string filename)
		{
			using (FileStream stream = File.OpenRead(filename))
			{
				byte[] hash = _SHA1Managed.ComputeHash(stream);
				return Hex(hash);
			}
		}

		public static string Hex(byte[] data)
		{
			StringBuilder hex = new StringBuilder();
			foreach (byte b in data)
				hex.Append(b.ToString("x2"));
			return hex.ToString();
		}


		public MameAOProcessor()
		{
			_HttpClient = new HttpClient();
			_SHA1Managed = new SHA1Managed();

		}


		HashSet<string> _AvailableDownloadMachines = new HashSet<string>();


		public void Run(string machineName)
		{

			_RootDirectory = Environment.CurrentDirectory;
			
		//	_RootDirectory = @"D:\MameAO";

			_StoreDirectory = Path.Combine(_RootDirectory, "_STORE");

			_RomHashStore = new HashStore(_StoreDirectory, SHA1HexFile);

			if (Directory.Exists(_StoreDirectory) == false)
			{
				Directory.CreateDirectory(_StoreDirectory);
			}


			//	https://archive.org/metadata/mame-merged

			string metadataCacheFilename = Path.Combine(_RootDirectory, "_metadata_mame-merged.json");

			if (File.Exists(metadataCacheFilename) == false || (DateTime.Now - File.GetLastWriteTime(metadataCacheFilename) > TimeSpan.FromHours(3)))
			{
				File.WriteAllText(metadataCacheFilename, PrettyJSON(Query("https://archive.org/metadata/mame-merged")), Encoding.UTF8);
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
			string version = title.Substring(5, 5).Trim();
			version = version.Replace(".", "");

			Console.WriteLine($"metadata.title:\t{title}");
			Console.WriteLine($"version:\t{version}");

			_VersionDirectory = Path.Combine(_RootDirectory, version);

			string binUrl = "https://github.com/mamedev/mame/releases/download/mame@/mame@b_64bit.exe";
			binUrl = binUrl.Replace("@", version);

			Console.WriteLine($"binUrl:\t{binUrl}");

			string binCacheFilename = Path.Combine(_VersionDirectory, "_" + Path.GetFileName(binUrl));

			string binFilename = Path.Combine(_VersionDirectory, "mame.exe");

			string machineXmlFilename = Path.Combine(_VersionDirectory, "_machine.xml");

			if (Directory.Exists(_VersionDirectory) == false)
			{
				Directory.CreateDirectory(_VersionDirectory);

				if (File.Exists(binCacheFilename) == false)
				{
					File.WriteAllBytes(binCacheFilename, Download(binUrl));
				}

				if (File.Exists(binFilename) == false)
					RunSelfExtract(binCacheFilename);

				if (File.Exists(machineXmlFilename) == false)
					ExtractXML(binFilename, machineXmlFilename, "-listxml");

			}

			Console.Write($"Loading machine XML {machineXmlFilename} ...");

			XElement machineDoc = XElement.Load(machineXmlFilename);

			Console.WriteLine("...done.");






			XElement machine = machineDoc.Descendants("machine")
					  .Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("machine not found");


			HashSet<string> requiredMachines = new HashSet<string>();


			FindAllMachines(machineName, machineDoc, requiredMachines);


			foreach (string requiredMachineName in requiredMachines)
			{
				XElement requiredMachine = machineDoc.Descendants("machine")
					.Where(e => e.Attribute("name").Value == requiredMachineName).FirstOrDefault();

				HashSet<string> requiredRoms = new HashSet<string>();
				HashSet<string> missingRoms = new HashSet<string>();

				foreach (XElement rom in requiredMachine.Descendants("rom"))
				{
					string romName = ((string)rom.Attribute("name")).Trim();
					string sha1 = ((string)rom.Attribute("sha1")).Trim();

					Console.WriteLine($"{romName}\t{sha1}\t{_RomHashStore.Exists(sha1)}");

					if (sha1 != null && sha1.Length == 40)
					{
						requiredRoms.Add(sha1);

						if (_RomHashStore.Exists(sha1) == false)
							missingRoms.Add(sha1);
					}

				}



				if (missingRoms.Count > 0)
				{

					if (_AvailableDownloadMachines.Contains(requiredMachineName) == true)
					{

						//	TODO: cache these against version !!!
						string downloadMachineUrl = $"https://archive.org/download/mame-merged/mame-merged/{requiredMachineName}.zip";

						using (TempDirectory tempDir = new TempDirectory())
						{
							string archiveFilename = Path.Combine(tempDir.Path, Path.GetFileName(downloadMachineUrl));
							string extractDirectory = Path.Combine(tempDir.Path, "OUT");
							Directory.CreateDirectory(extractDirectory);

							File.WriteAllBytes(archiveFilename, Download(downloadMachineUrl));

							ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);

							ClearAttributes(tempDir.Path);

							foreach (string romFilename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
							{

								//	Only load if required in whole on mame ???

								bool imported = _RomHashStore.Add(romFilename);

								Console.WriteLine($"IMPORT: {romFilename}");
							}
						}
					}


				}




			}


			foreach (string requiredMachineName in requiredMachines)
			{
				XElement requiredMachine = machineDoc.Descendants("machine")
					.Where(e => e.Attribute("name").Value == requiredMachineName).FirstOrDefault();

				foreach (XElement rom in requiredMachine.Descendants("rom"))
				{
					string romName = ((string)rom.Attribute("name")).Trim();
					string sha1 = ((string)rom.Attribute("sha1")).Trim();
					//string merge = (string)rom.Attribute("merge");

					if (sha1 != null)	// && sha1.Length == 40 && merge == null)
					{
						string romFilename = Path.Combine(_VersionDirectory, "roms", requiredMachineName, romName);
						string romDirectory = Path.GetDirectoryName(romFilename);
						if (Directory.Exists(romDirectory) == false)
							Directory.CreateDirectory(romDirectory);

						if (_RomHashStore.Exists(sha1) == true)
						{
							if (File.Exists(romFilename) == false)
							{
								Console.WriteLine($"CREATE: {romFilename}");

								File.Copy(_RomHashStore.Filename(sha1), romFilename);
							}
						}
						else
						{
							Console.WriteLine($"MISSING: {romName}");
						}
					}
				}
			}








		}

		public static void FindAllMachines(string machineName, XElement machineDoc, HashSet<string> requiredMachines)
		{
			XElement machine = machineDoc.Descendants("machine").Where(e => e.Attribute("name").Value == machineName).FirstOrDefault();

			if (machine == null)
				throw new ApplicationException("machine not found: " + machineName);

			bool hasRoms = (machine.Descendants("rom").Count() > 0);

			if (hasRoms == false)
				return;

			if (requiredMachines.Add(machineName) == false)
				return;

			Console.WriteLine($"FindAllMachines {machineName}");

			string romof = machine.Attribute("romof")?.Value;

			if (romof != null)
				FindAllMachines(romof, machineDoc, requiredMachines);

			//	<device_ref name="m68000"/>

			foreach (XElement device_ref in machine.Descendants("device_ref"))
				FindAllMachines(device_ref.Attribute("name").Value, machineDoc, requiredMachines);

			//	romof
		}
		public static void ClearAttributes(string directory)
		{
			foreach (string filename in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
				File.SetAttributes(filename, FileAttributes.Normal);
		}

		public static string PrettyJSON(string json)
		{
			dynamic obj = JsonConvert.DeserializeObject<dynamic>(json);
			return JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
		}

		public dynamic QueryObject(string url)
		{
			using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{url}"))
			{
				Task<HttpResponseMessage> requestTask = _HttpClient.SendAsync(requestMessage);
				requestTask.Wait();
				HttpResponseMessage responseMessage = requestTask.Result;

				Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
				responseMessageTask.Wait();
				string responseBody = responseMessageTask.Result;

				responseMessage.EnsureSuccessStatusCode();

				return JsonConvert.DeserializeObject<dynamic>(responseBody);
			}
		}

		public string Query(string url)
		{
			using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{url}"))
			{
				Task<HttpResponseMessage> requestTask = _HttpClient.SendAsync(requestMessage);
				requestTask.Wait();
				HttpResponseMessage responseMessage = requestTask.Result;

				Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
				responseMessageTask.Wait();
				string responseBody = responseMessageTask.Result;

				responseMessage.EnsureSuccessStatusCode();

				return responseBody;
			}
		}
		public byte[] Download(string url)
		{
			using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{url}"))
			{
				Task<HttpResponseMessage> requestTask = _HttpClient.SendAsync(requestMessage);
				requestTask.Wait();
				HttpResponseMessage responseMessage = requestTask.Result;

				Task<byte[]> responseMessageTask = responseMessage.Content.ReadAsByteArrayAsync();
				responseMessageTask.Wait();

				return responseMessageTask.Result;
			}
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
