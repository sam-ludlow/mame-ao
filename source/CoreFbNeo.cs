using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
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
				if ((string)release.name == "nightly release")
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

			//	https://github.com/finalburnneo/FBNeo/blob/master/src/burner/win32/main.cpp
			string[] listInfos = new string[] { "arcade", "channelf", "coleco", "fds", "gg", "md", "msx", "neogeo", "nes", "ngp", "pce", "sg1000", "sgx", "sms", "snes", "spectrum", "tg16" };

			var fixNames = new Dictionary<string, string>()
            {
                { "gg", "gamegear" },
                { "md", "megadrive" }
            };

			//
			// Extract XML
			//
			string iniFileData = $"nIniVersion 0x7FFFFF{Environment.NewLine}bSkipStartupCheck 1{Environment.NewLine}";
			string configDirectory = Path.Combine(_CoreDirectory, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(Path.Combine(configDirectory, "fbneo64.ini"), iniFileData);

			foreach (string listInfo in listInfos)
			{
				string system = fixNames.ContainsKey(listInfo) == true ? fixNames[listInfo] : listInfo;

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

			//
			// Combine XML
			//
			XmlDocument xmlDocument = new XmlDocument();

			XmlElement datafilesElement = xmlDocument.CreateElement("datafiles");
			xmlDocument.AppendChild(datafilesElement);

			XmlAttribute attribute = xmlDocument.CreateAttribute("version");
			attribute.Value = _Version;
			datafilesElement.Attributes.Append(attribute);

			foreach (string listInfo in listInfos)
			{
				string system = fixNames.ContainsKey(listInfo) == true ? fixNames[listInfo] : listInfo;

				string systemFilename = Path.Combine(_CoreDirectory, $"_{system}.xml");

				XmlDocument systemDocument = new XmlDocument();
				systemDocument.Load(systemFilename);

				foreach (XmlNode sourceNode in systemDocument.GetElementsByTagName("datafile"))
				{
					XmlNode targetNode = xmlDocument.ImportNode(sourceNode, true);

					attribute = xmlDocument.CreateAttribute("key");
					attribute.Value = system;
					targetNode.Attributes.Append(attribute);

					datafilesElement.AppendChild(targetNode);
				}

				File.Delete(systemFilename);
			}

			string completeFilename = Path.Combine(_CoreDirectory, "_fbneo.xml");
			File.Delete(completeFilename);

			XmlWriterSettings settings = new XmlWriterSettings
			{
				OmitXmlDeclaration = false,
				Indent = true,
				IndentChars = "\t",
			};
			using (XmlWriter xmlWriter = XmlWriter.Create(completeFilename, settings))
			{
				xmlDocument.Save(xmlWriter);
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

        void ICore.MsAccess()
        {
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Cores.MsAccess(new string[] { Path.Combine(_CoreDirectory, "_fbneo.xml") });
        }
        void ICore.Zips()
        {
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Cores.Zips(_CoreDirectory);
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

			XElement mainDocument = XElement.Load(Path.Combine(directory, "_fbneo.xml"));

			foreach (var datafileElement in mainDocument.Elements("datafile"))
			{
				XElement clrmamepro = datafileElement.Element("header").Element("clrmamepro");
				if (clrmamepro != null)
					clrmamepro.Remove();

				DataSet fileDataSet = new DataSet();
				ReadXML.ImportXMLWork(datafileElement, fileDataSet, null, null);

				Tools.DataFileMoveHeader(fileDataSet);

				foreach (DataTable table in dataSet.Tables)
					foreach (DataColumn column in table.Columns)
						column.AutoIncrement = false;

				Tools.DataFileMergeDataSet(fileDataSet, dataSet);
			}

			return dataSet;
		}

		void ICore.MSSql(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			DataSet dataSet = CoreFbNeo.FBNeoDataSet(_CoreDirectory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseNames[0]);

			Database.MakeForeignKeys(serverConnectionString, databaseNames[0]);
		}

		void ICore.MSSqlPayload(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);

			OperationsPayload.FBNeoMSSQLPayloads(_RootDirectory, _Version, serverConnectionString, databaseNames[0]);
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

		DataRow[] ICore.GetMachineRoms(string machine_name)
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
