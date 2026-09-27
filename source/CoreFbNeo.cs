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
using Newtonsoft.Json.Linq;
using System.Data.SQLite;

namespace Spludlow.MameAO
{
	internal class CoreFbNeo : ICore
	{
		string ICore.Name { get => "fbneo"; }
		string ICore.Version { get => _Version; }
		string ICore.Directory { get => _CoreDirectory; }
		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionString }; }

		Dictionary<string, string> ICore.SoftwareListDescriptions { get => null; }
		Dictionary<string, string[]> ICore.Filters { get => throw new NotImplementedException(); }

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

			string completeFilename = Path.Combine(_CoreDirectory, "_fbneo.xml");

			if (File.Exists(completeFilename) == true)
				return;

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

			string connectionString = Database.MakeSQLiteConnectionString(sqlLiteFilename);

			Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
			Database.DataSet2SQLite("fbneo", connectionString, dataSet);
			Console.WriteLine("... done");
		}

		void ICore.SQLiteAo()
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string sqlLiteFilename = Path.Combine(_CoreDirectory, "_fbneo.sqlite");

			_ConnectionString = Database.MakeSQLiteConnectionString(sqlLiteFilename);

			if (File.Exists(sqlLiteFilename) == false  || (Cores.GetAoMetaDataAssemblyVersion(_ConnectionString) != Globals.AssemblyVersion))
			{
				if (File.Exists(sqlLiteFilename) == true)
				{
					Console.WriteLine($"Delete SQLite database from old version {sqlLiteFilename}");
					File.Delete(sqlLiteFilename);
				}

				DataSet dataSet = FBNeoDataSet(_CoreDirectory);

				Cores.AddAoMetaData(dataSet, Globals.AssemblyVersion);

				Console.Write("Creating SHA1 lookup ...");
				Dictionary<string, string> sha1Lookup = new Dictionary<string, string>();

				var datDataSet = GetDatDataSet();
				
				foreach (DataRow datafileRow in datDataSet.Tables["datafile"].Rows)
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					foreach (DataRow machineRow in datDataSet.Tables["machine"].Select($"datafile_id = {datafile_id}"))
					{
						long machine_id = (long)machineRow["machine_id"];
						string machine_name = (string)machineRow["name"];
						foreach (DataRow romRow in datDataSet.Tables["rom"].Select($"machine_id = {machine_id}"))
						{
							string rom_name = (string)romRow["name"];
							string crc = (string)romRow["crc"];
							string sha1 = (string)romRow["sha1"];

							sha1Lookup.Add($"{datafile_name}\t{machine_name}\t{rom_name}\t{crc}", sha1);
						}
					}
				}
				Console.WriteLine("... done");

				Console.Write("Setting SHA1 ...");

				//	Maybe just rename game => machine ???

				var rowLookups = Operations.PerformanceDictionaries(dataSet);

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Rows)
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					foreach (DataRow gameRow in rowLookups["game"][datafile_id])
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string romof = gameRow.Field<string>("romof");

						foreach (DataRow romRow in rowLookups["rom"][game_id])
						{
							if (romRow.IsNull("crc") == true)
								continue;

							string rom_name = (string)romRow["name"];
							string crc = (string)romRow["crc"];
							string merge = romRow.Field<string>("merge");

							string key = $"{datafile_name}\t{game_name}\t{rom_name}\t{crc}";

							if (sha1Lookup.ContainsKey(key) == true)
							{
								romRow["sha1"] = sha1Lookup[key];
							}
							else
							{
								if (romof != null)
								{
									key = $"{datafile_name}\t{romof}\t{merge ?? rom_name}\t{crc}";
									if (sha1Lookup.ContainsKey(key) == true)
										romRow["sha1"] = sha1Lookup[key];
								}
							}
						}
					}
				}
				Console.WriteLine("... done");

				Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
				Database.DataSet2SQLite("fbneo", _ConnectionString, dataSet);
				Console.WriteLine("... done");
			}

		}

		public static DataSet GetDatDataSet()
		{
			dynamic info = BitTorrent.DomeInfo();

			var torrents = ((JArray)info.torrents).Where(token => ((string)token["core"]) == "fbneo").ToArray();
			if (torrents.Length != 1)
				throw new ApplicationException($"Did not find single fbneo torrent: {torrents.Length}");

			var datUrl = (string)torrents[0]["dat"];

			string datName = Path.GetFileNameWithoutExtension(datUrl);

			string datCacheFilename = Path.Combine(Globals.CacheDirectory, datName + ".xml");

			XElement datafilesElement;
			if (File.Exists(datCacheFilename) == false)
			{
				using (TempDirectory tempDir = new TempDirectory())
				{
					string datZipFilename = Path.Combine(tempDir.Path, "dat.zip");

					Console.Write($"Downloading {datUrl} {datZipFilename} ...");
					Tools.Download(datUrl, datZipFilename);
					Console.WriteLine("...done");

					string datDirectory = Path.Combine(tempDir.Path, "dats");
					Directory.CreateDirectory(datDirectory);

					Console.Write($"Extracting {datZipFilename} {datDirectory} ...");
					ZipFile.ExtractToDirectory(datZipFilename, datDirectory);
					Console.WriteLine("...done");

					datafilesElement = new XElement("datafiles");

					foreach (string xmlFilename in Directory.GetFiles(datDirectory, "*.dat"))
					{
						var datafileElement = XElement.Load(xmlFilename, LoadOptions.None);

						//	Move header
						foreach (var itemElement in datafileElement.Element("header").Elements())
							datafileElement.SetAttributeValue(itemElement.Name, itemElement.Value);
						datafileElement.Element("header").Remove();

						datafilesElement.Add(datafileElement);
					}

					datafilesElement.Save(datCacheFilename);
				}
			}
			else
			{
				datafilesElement = XElement.Load(datCacheFilename, LoadOptions.None);
			}

			DataSet dataSet = new DataSet();
			ReadXML.ImportXMLWork(datafilesElement, dataSet, null, null);

			return dataSet;
		}

		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			if (_Version == null)
				_Version = FBNeoGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string sqlLiteFilename = Path.Combine(_CoreDirectory, "_fbneo.sqlite");
			_ConnectionString = Database.MakeSQLiteConnectionString(sqlLiteFilename);

			Console.Write($"Load all database SHA1 ...");
			Cores.AllSHA1(hashSet, _ConnectionString, new string[] { "rom" });
			Console.WriteLine("...done");
		}

		public static string PlaceFbNeo(ICore core, string line)
		{
			string[] parts = line.Split('@');
			if (parts.Length != 2)
				throw new ApplicationException("Bad Line");

			string datafile_name = parts[1];
			string game_name = parts[0];

			SQLiteConnection connection = new SQLiteConnection(core.ConnectionStrings[0]);

			Globals.WorkerTaskReport = Reports.PlaceReportTemplate();

			string[] info;
			long game_id;
			string game_description;

			using (SQLiteCommand command = new SQLiteCommand(
				"SELECT [game].[game_id], [game].[description] FROM [datafile] INNER JOIN [game] ON [datafile].[datafile_id] = [game].[datafile_id] " +
				"WHERE ([game].[name] = @game_name AND [datafile].[name] = @datafile_name)", connection))
			{
				command.Parameters.AddWithValue("@datafile_name", datafile_name);
				command.Parameters.AddWithValue("@game_name", game_name);

				DataTable gameTable = Database.ExecuteFill(command);

				if (gameTable.Rows.Count == 0)
					throw new ApplicationException($"Game not found {datafile_name} / {game_name}");

				game_id = (long)gameTable.Rows[0]["game_id"];
				game_description = (string)gameTable.Rows[0]["description"];
			}

			Tools.ConsoleHeading(1, new string[] { game_description, core.Directory });

			DataTable romTable = Database.ExecuteFill(connection, $"SELECT * FROM [rom] WHERE ([rom].[game_id] = {game_id}) ORDER BY [name] DESC");

			if (romTable.Rows.Count == 0)
				throw new ApplicationException($"No game roms found {datafile_name} / {game_id}");

			bool downloadRequired = false;

			foreach (DataRow romRow in romTable.Rows)
			{
				if (romRow.IsNull("sha1") == true)
					continue;
				string sha1 = (string)romRow["sha1"];

				if (Globals.RomHashStore.Exists(sha1) == false)
				{
					downloadRequired = true;
					break;
				}
			}

			info = new string[] { "fbneo game", datafile_name, game_name };

			if (downloadRequired == true)
			{
				var btFile = BitTorrent.SoftwareRom(core.Name, datafile_name, game_name);
				if (btFile != null)
					Place.DownloadImportFiles(btFile.Filename, btFile.Length, info);
			}

			string romDirectory = Path.Combine(core.Directory, "roms", datafile_name, game_name);

			Place.PlaceAssetFiles(romTable.Rows.Cast<DataRow>().ToArray(), Globals.RomHashStore, romDirectory, null, info);


			// TDOO: needs ZIP

			//	TODO: return what will be passed to exe

			//	-w is window


//			arcade = ""
//channelf = "chf_"
//coleco = "cv_"
//gamegear = "gg_"
//megadrive = "md_"
//msx = "msx_"
//nes = "nes_"
//ngp = "ngp_"
//ngpc = "ngpc_"
//spectrum = "spec_"
//sms = "sms_"
//sg1000 = "sg1k_"
//pce = "pce_"
//tg16 = "tg16_"
//sgx = "sgx_"
//neocd = "neocd_"



			return null;
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
			Dictionary<string, string> subsets = new Dictionary<string, string>()
			{
				{ "fbneo", "Emulator for Arcade Games & Select Consoles" },
			};

			HashSet<string> datafileSkipColumns = new HashSet<string>(new string[] { "category", "homepage", "url", "clrmamepro" });

			XElement subsetsElement = new XElement("subsets");

			foreach (var subset in subsets)
			{
				XElement subsetElement = new XElement("subset");
				subsetElement.SetAttributeValue("name", subset.Key);
				subsetElement.SetAttributeValue("description", subset.Value);

				XElement datafilesElement = XElement.Load(Path.Combine(directory, "_fbneo.xml"), LoadOptions.None);

				foreach (var datafileElement in datafilesElement.Elements("datafile"))
				{
					foreach (var itemElement in datafileElement.Element("header").Elements())
						if (datafileSkipColumns.Contains(itemElement.Name.LocalName) == false)
							datafileElement.SetAttributeValue(itemElement.Name, itemElement.Value);
					datafileElement.Element("header").Remove();

					string description = datafileElement.Attribute("name").Value;
					int index = description.IndexOf('-');
					if (index != -1)
						description = description.Substring(index + 1).Trim();

					datafileElement.SetAttributeValue("name", datafileElement.Attribute("key").Value);
					datafileElement.SetAttributeValue("description", description);
					datafileElement.Attribute("key").Remove();

					subsetElement.Add(datafileElement);
				}

				subsetsElement.Add(subsetElement);
			}

			DataSet dataSet = new DataSet();
			ReadXML.ImportXMLWork(subsetsElement, dataSet, null, null);

			foreach (string dummyColumn in new string[] { "sha1", "md5" })
				dataSet.Tables["rom"].Columns.Add(dummyColumn, typeof(string));

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

			OperationsDatish.FBNeoMSSQLPayloads(_RootDirectory, _Version, serverConnectionString, databaseNames[0]);
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

		DataTable ICore.QueryMachines(string profile, int offset, int limit, string search, string manufacturer, string[] status, string[] display, string[] players, string[] control, bool? mechanical, bool? clone, string order, string sort)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string publisher, string order, string sort, string favorites_machine)
		{
			throw new NotImplementedException();
		}




	}
}
