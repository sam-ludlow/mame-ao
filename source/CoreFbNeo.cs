using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	internal class CoreFbNeo : ICore
	{
		string ICore.Name { get => "fbneo"; }
		string ICore.Version { get => _Version; }
		string ICore.Directory { get => _CoreDirectory; }
		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionString }; }

		Dictionary<string, string> ICore.SoftwareListDescriptions { get => null; }


		private string _RootDirectory = null;
		private string _CoreDirectory = null;

		private string _Version = null;

		private string _ConnectionString = null;

		void ICore.Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			_Version = null;    //	Always use latest
		}

		int ICore.Get()
		{
			string releasesJson = Tools.FetchTextCached("https://api.github.com/repos/finalburnneo/FBNeo/releases") ?? throw new ApplicationException("Unanle to get core's github releases");

			dynamic releases = JsonConvert.DeserializeObject<dynamic>(releasesJson);

			string downloadUrl = null;

			foreach (dynamic release in releases)
			{
				if ((string)release.name == "nightly builds")
				{
					_Version = ((DateTime)release.published_at).ToString("s").Replace(":", "-");

					foreach (dynamic asset in release.assets)
					{
						if ((string)asset.name == "windows-x86_64.zip")
							downloadUrl = (string)asset.browser_download_url;
					}
				}
			}

			if (downloadUrl == null)
				throw new ApplicationException("Did not find download asset");

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			if (File.Exists(Path.Combine(_CoreDirectory, "fbneo64.exe")) == true)
				return 0;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "fbneo.zip");

				Console.Write($"Downloading {downloadUrl} {archiveFilename} ...");
				Tools.Download(downloadUrl, archiveFilename, 1);
				Console.WriteLine("...done");

				Console.Write($"Extract 7-Zip {archiveFilename} {_CoreDirectory} ...");
				ZipFile.ExtractToDirectory(archiveFilename, _CoreDirectory);
				Console.WriteLine("...done");
			}

			return 1;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string iniFileData = $"nIniVersion 0x7FFFFF{Environment.NewLine}bSkipStartupCheck 1{Environment.NewLine}";
			string configDirectory = Path.Combine(_CoreDirectory, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(Path.Combine(configDirectory, "fbneo64.ini"), iniFileData);

			//	https://github.com/finalburnneo/FBNeo/blob/master/src/burner/win32/main.cpp
			string[] listInfos = new string[] { "arcade", "channelf", "coleco", "fds", "gg", "md", "msx", "neogeo", "nes", "ngp", "pce", "sg1000", "sgx", "sms", "snes", "spectrum", "tg16" };

			foreach (string listInfo in listInfos)
			{
				string system = listInfo;
				if (system == "gg")
					system = "gamegear";
				if (system == "md")
					system = "megadrive";

				string filename = Path.Combine(_CoreDirectory, $"_{system}.xml");

				if (File.Exists(filename) == true)
					continue;

				string arguments = listInfo == "arcade" ? "-listinfo" : $"-listinfo{listInfo}only";

				StringBuilder output = new StringBuilder();

				ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(_CoreDirectory, "fbneo64.exe"))
				{
					Arguments = arguments,
					WorkingDirectory = _CoreDirectory,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					StandardOutputEncoding = Encoding.UTF8,
				};

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => output.AppendLine(e.Data));
					process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => Console.WriteLine(e.Data));

					process.Start();
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					process.WaitForExit();

					if (process.ExitCode != 0)
						throw new ApplicationException($"FBNeo Extract bad exit code: {process.ExitCode}");
				}

				File.WriteAllText(filename, output.ToString(), Encoding.UTF8);
			}
		}

		void ICore.Json()
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			foreach (string xmlFilename in Directory.GetFiles(_CoreDirectory, "_*.xml"))
			{
				Console.WriteLine(xmlFilename);

				string jsonFilename = xmlFilename.Substring(0, xmlFilename.Length - 4) + ".json";

				if (File.Exists(jsonFilename) == false)
					Tools.XML2JSON(xmlFilename, jsonFilename);
			}
		}

		void ICore.SQLite()
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string sqlLiteFilename = Path.Combine(_CoreDirectory, "_fbneo.sqlite");

			if (File.Exists(sqlLiteFilename) == true)
				return;

			DataSet dataSet = FBNeoDataSet(_CoreDirectory);

			string connectionString = $"Data Source='{sqlLiteFilename}';datetimeformat=CurrentCulture;";

			Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
			Database.DataSet2SQLite("fbneo", connectionString, dataSet);
			Console.WriteLine("... done");
		}





		public static string FBNeoGetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string version = Path.GetFileName(versionDirectory);

				if (File.Exists(Path.Combine(versionDirectory, "fbneo64.exe")) == false)
					continue;

				versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No FBNeo versions found in '{directory}'.");

			versions.Sort();

			return versions.Last();
		}

		public static DataSet FBNeoDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				string name = Path.GetFileNameWithoutExtension(filename);
				if (name.StartsWith("_") == false)
					continue;
				name = name.Substring(1);

				Console.WriteLine(name);

				XElement document = XElement.Load(filename);

				XElement clrmamepro = document.Element("header").Element("clrmamepro");
				if (clrmamepro != null)
					clrmamepro.Remove();

				DataSet fileDataSet = new DataSet();
				ReadXML.ImportXMLWork(document, fileDataSet, null, null);

				Tools.DataFileMoveHeader(fileDataSet);

				DataTable datafileTable = fileDataSet.Tables["datafile"];
				datafileTable.Columns.Add("key", typeof(string));
				datafileTable.Rows[0]["key"] = name;

				foreach (DataTable table in dataSet.Tables)
					foreach (DataColumn column in table.Columns)
						column.AutoIncrement = false;

				Tools.DataFileMergeDataSet(fileDataSet, dataSet);
			}

			return dataSet;
		}






		void ICore.MSSql()
		{
			throw new NotImplementedException();
		}

		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetMachine(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDeviceRefs(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDisks(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineFeatures(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineRoms(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSamples(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSoftwareLists(DataRow machine)
		{
			throw new NotImplementedException();
		}

		HashSet<string> ICore.GetReferencedMachines(string machine_name)
		{
			throw new NotImplementedException();
		}

		string ICore.GetRequiredMedia(string machine_name, string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(DataRow softwarelist, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareDisks(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftwareList(string softwarelist_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareListsSoftware(DataRow softwarelist)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareRoms(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareSharedFeats(DataRow software)
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlHtml()
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlPayload()
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QueryMachines(DataQueryProfile profile, int offset, int limit, string search)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string favorites_machine)
		{
			throw new NotImplementedException();
		}

		void ICore.SQLiteAo()
		{
			throw new NotImplementedException();
		}


	}
}
