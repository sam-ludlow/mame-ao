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

		Dictionary<string, string> ICore.SoftwareListDescriptions { get => _SoftwareListDescriptions; }
		Dictionary<string, string[]> ICore.Filters { get => _Filters; }

		private string _RootDirectory = null;
		private string _CoreDirectory = null;

		private string _Version = null;

		private string _ConnectionString = null;

		private Dictionary<string, string> _SoftwareListDescriptions = new Dictionary<string, string>();

		private Dictionary<string, string[]> _Filters = null;

		private List<DataQueryProfile> _DataQueryProfiles = new List<DataQueryProfile>();

		public static Dictionary<string, string> _LookupSystemListInfo;
		public static Dictionary<string, string> _LookupSystemPrefix;
		public static Dictionary<string, string> _LookupPrefixSystem;


		static CoreFbNeo()
		{
			//	https://github.com/finalburnneo/FBNeo/blob/master/src/burner/win32/main.cpp

			_LookupSystemListInfo = new Dictionary<string, string>()	//	19
			{
				{ "arcade",     "" },
				{ "astrocade",  "" },
				{ "channelf",   "" },
				{ "coleco",     "" },
				{ "fds",        "" },
				{ "gamegear",   "gg" },
				{ "gba",        "" },
				{ "megadrive",  "md" },
				{ "msx",        "" },
				{ "neogeo",     "" },	//	not used subset of arcade
				{ "nes",        "" },
				{ "ngp",        "" },
				{ "pce",        "" },
				{ "sg1000",     "" },
				{ "sgx",        "" },
				{ "sms",        "" },
				{ "snes",       "" },
				{ "spectrum",   "" },
				{ "tg16",       "" },
			};
			_LookupSystemListInfo = _LookupSystemListInfo.ToDictionary(x => x.Key, x => string.IsNullOrEmpty(x.Value) ? x.Key : x.Value);


			_LookupSystemPrefix = new Dictionary<string, string>()	//	19
			{
				{ "arcade",     "" },
				{ "astrocade",  "astro_" },
				{ "channelf",   "chf_" },
				{ "coleco",     "cv_" },
				{ "fds",        "fds_" },
				{ "gamegear",   "gg_" },
				{ "gba",        "gba_" },
				{ "megadrive",  "md_" },
				{ "msx",        "msx_" },
				{ "neogeo",     "neogeo_" },	//	not used subset of arcade
				{ "nes",        "nes_" },
				{ "ngp",        "ngp_" },
				{ "pce",        "pce_" },
				{ "sg1000",     "sg1k_" },
				{ "sgx",        "sgx_" },
				{ "sms",        "sms_" },
				{ "snes",       "snes_" },
				{ "spectrum",   "spec_" },
				{ "tg16",       "tg_" },
			};
			_LookupPrefixSystem = _LookupSystemPrefix.ToDictionary(x => x.Value, x => x.Key);
		}

		void ICore.Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			_Version = null;    //	Always use latest
		}

		int ICore.Get()
		{
			//TODO:	dont auto bumnp (new command to bump)

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

			//
			// Extract XML
			//
			string iniFileData = $"nIniVersion 0x7FFFFF{Environment.NewLine}bSkipStartupCheck 1{Environment.NewLine}";
			string configDirectory = Path.Combine(_CoreDirectory, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(Path.Combine(configDirectory, "fbneo64.ini"), iniFileData);

			foreach (var system in _LookupSystemListInfo.Keys)
			{
				string filename = Path.Combine(_CoreDirectory, $"_{system}.xml");
				if (File.Exists(filename) == true)
					continue;

				string arguments = system == "arcade" ? "-listinfo" : $"-listinfo{_LookupSystemListInfo[system]}only";

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

			foreach (string system in _LookupSystemListInfo.Keys)
			{
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

				foreach (string[] rename in new string[][] { new string[] { "game", "machine" }, new string[] { "video", "display" } })
				{
					foreach (DataTable table in dataSet.Tables)
					{
						foreach (DataColumn column in table.Columns.Cast<DataColumn>().Where(col => col.ColumnName == $"{rename[0]}_id"))
							column.ColumnName = $"{rename[1]}_id";

						if (table.TableName == rename[0])
							table.TableName = rename[1];
					}
				}

				Cores.AddAoMetaData(dataSet, Globals.AssemblyVersion);

				Cores.AddExtraAoData(dataSet, Globals.AssemblyVersion);

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Rows)
				{
					string datafile_name = (string)datafileRow["name"];
					foreach (DataRow machineRow in dataSet.Tables["machine"].Select($"datafile_id = {(long)datafileRow["datafile_id"]}"))
						machineRow["ao_type"] = datafile_name;
				}

				var sha1Lookup = HashLookupGetWeb();

				Console.Write("Setting SHA1 ...");

				var rowLookups = Operations.PerformanceDictionaries(dataSet);

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Rows)
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					foreach (DataRow machineRow in rowLookups["machine"][datafile_id])
					{
						long machine_id = (long)machineRow["machine_id"];
						string machine_name = (string)machineRow["name"];
						string romof = machineRow.Field<string>("romof");

						foreach (DataRow romRow in rowLookups["rom"][machine_id])
						{
							if (romRow.IsNull("crc") == true)
								continue;

							string rom_name = (string)romRow["name"];
							string size = Int64.Parse((string)romRow["size"]).ToString();
							string crc = (string)romRow["crc"];
							string merge = romRow.Field<string>("merge");

							string key = $"{datafile_name}\t{machine_name}\t{rom_name}\t{size}\t{crc}".ToLower();

							if (sha1Lookup.ContainsKey(key) == true)
							{
								romRow["sha1"] = sha1Lookup[key];
							}

							//	merge is not in lookup
							if (romof != null && merge != null)
							{
								key = $"{datafile_name}\t{romof}\t{merge}\t{size}\t{crc}".ToLower();
								if (sha1Lookup.ContainsKey(key) == true)
									romRow["sha1"] = sha1Lookup[key];
							}
						}
					}
				}
				Console.WriteLine("... done");

				Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
				Database.DataSet2SQLite("fbneo", _ConnectionString, dataSet);
				Console.WriteLine("... done");
			}

			//
			// Cache softwarelists for description
			//
			_SoftwareListDescriptions = new Dictionary<string, string>();

			foreach (DataRow row in Database.ExecuteFill(_ConnectionString, "SELECT [name], [description] FROM [datafile] ORDER BY [description]").Rows)
				_SoftwareListDescriptions.Add((string)row["name"], (string)row["description"]);


			_Filters = Cores.GetFilters(_ConnectionString);
		}

		List<DataQueryProfile> ICore.GetDataQueryProfiles()
		{
			if (_DataQueryProfiles.Count == 0)
			{
				var table = Database.ExecuteFill(_ConnectionString, "SELECT [name], [description] FROM [datafile] WHERE ([name] != 'neogeo') ORDER BY [name]");

				foreach (DataRow row in table.Rows)
				{
					string name = row.Field<string>("name");
					string description = row.Field<string>("description");

					_DataQueryProfiles.Add(new DataQueryProfile()
					{
						Key = name,
						Text = name.Substring(0, 1).ToUpper() + name.Substring(1),
						Decription = description,
					});
				}

				_DataQueryProfiles.Add(new DataQueryProfile(){ Key = "everything", Text = "Everything", Decription = "Every Machine" });
				_DataQueryProfiles.Add(new DataQueryProfile() { Key = "favorites", Text = "Favorites", Decription = "Favorite Machines" });
			}

			return _DataQueryProfiles;
		}

		public static DataSet GetDatDataSet()
		{
			//Dictionary<string, string> sha1Lookup = new Dictionary<string, string>();

			//var datDataSet = GetDatDataSet();

			//foreach (DataRow datafileRow in datDataSet.Tables["datafile"].Rows)
			//{
			//	long datafile_id = (long)datafileRow["datafile_id"];
			//	string datafile_name = (string)datafileRow["name"];
			//	foreach (DataRow machineRow in datDataSet.Tables["machine"].Select($"datafile_id = {datafile_id}"))
			//	{
			//		long machine_id = (long)machineRow["machine_id"];
			//		string machine_name = (string)machineRow["name"];
			//		foreach (DataRow romRow in datDataSet.Tables["rom"].Select($"machine_id = {machine_id}"))
			//		{
			//			string rom_name = (string)romRow["name"];
			//			string crc = (string)romRow["crc"];
			//			string sha1 = (string)romRow["sha1"];

			//			sha1Lookup.Add($"{datafile_name}\t{machine_name}\t{rom_name}\t{crc}", sha1);
			//		}
			//	}
			//}

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

			if (parts.Length == 3 && parts[2] == "fbneo")
				parts = parts.Take(2).ToArray();

			if (parts.Length != 2)
				throw new ApplicationException("Bad Line");

			Place._BtStarted = false;	//	TODO tidy up

			string datafile_name = parts[1];
			string machine_name = parts[0];

			SQLiteConnection connection = new SQLiteConnection(core.ConnectionStrings[0]);

			Globals.WorkerTaskReport = Reports.PlaceReportTemplate();

			HashSet<string> machine_names = new HashSet<string>();
			string machine_description = null;

			string romof = machine_name;
			while (romof != null)
			{
				using (SQLiteCommand command = new SQLiteCommand(
					"SELECT [machine].[machine_id], [machine].[description], [machine].[romof] FROM [datafile] INNER JOIN [machine] ON [datafile].[datafile_id] = [machine].[datafile_id] " +
					"WHERE ([machine].[name] = @machine_name AND [datafile].[name] = @datafile_name)", connection))
				{
					command.Parameters.AddWithValue("@datafile_name", datafile_name);
					command.Parameters.AddWithValue("@machine_name", romof);

					DataTable machineTable = Database.ExecuteFill(command);

					if (machineTable.Rows.Count == 0)
						throw new ApplicationException($"Machine not found {datafile_name} / {machine_name}");

					machine_names.Add(romof);

					if (romof == machine_name)
						machine_description = (string)machineTable.Rows[0]["description"];

					romof = machineTable.Rows[0].Field<string>("romof");
				}
			}

			Tools.ConsoleHeading(1, new string[] { machine_description, String.Join(", ", machine_names), core.Directory });

			foreach (string name in machine_names)
			{
				using (SQLiteCommand command = new SQLiteCommand(
					"SELECT [rom].* FROM [datafile] INNER JOIN [machine] ON [datafile].[datafile_id] = [machine].[datafile_id] INNER JOIN [rom] ON [machine].[machine_id] = [rom].[machine_id] " +
					"WHERE ([datafile].[name] = @datafile_name AND [machine].[name] = @machine_name);", connection))
				{
					command.Parameters.AddWithValue("@datafile_name", datafile_name);
					command.Parameters.AddWithValue("@machine_name", name);

					DataTable romTable = Database.ExecuteFill(command);

					if (romTable.Rows.Count == 0)
						throw new ApplicationException($"No machine roms found {datafile_name} / {name}");

					if (romTable.Rows.Cast<DataRow>().Count(row => row.IsNull("sha1") == true) > 0)
					{
						Console.WriteLine($"!!! SHA1 not known, game will not run\t{datafile_name}\t{name}");
						continue;
					}

					string[] info = new string[] { "fbneo game ZIP", datafile_name, name };

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

					if (downloadRequired == true)
					{
						Place.StartBitTorrent();	//	TODO tidy up

						var btFile = BitTorrent.SoftwareRom(core.Name, datafile_name, name);
						if (btFile != null)
							Place.DownloadImportFiles(btFile.Filename, btFile.Length, info);
					}

					string romDirectory = Path.Combine(core.Directory, "roms", datafile_name, name);
					string zipFilename = romDirectory + ".zip";
					File.Delete(zipFilename);

					DateTime when = DateTime.Now;
					using (var zipFile = ZipFile.Open(zipFilename, ZipArchiveMode.Create))
					{
						foreach (DataRow row in romTable.Rows)
						{
							if (row.IsNull("sha1") == true)
								continue;

							string rom_name = (string)row["name"];
							string sha1 = (string)row["sha1"];
							bool have = Globals.RomHashStore.Exists(sha1);

							if (have == true)
								zipFile.CreateEntryFromFile(Globals.RomHashStore.Filename(sha1), rom_name);

							Globals.WorkerTaskReport.Tables["Place"].Rows.Add(when, info[0], info[1], info[2], sha1, have, have, name);
						}
					}

				}
			}

			return $"{_LookupSystemPrefix[datafile_name]}{machine_name}";
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
			return Cores.QueryMachines(_ConnectionString, profile, offset, limit, search, manufacturer, status, display, players, control, mechanical, clone, order, sort);
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string publisher, string order, string sort, string favorites_machine)
		{
			throw new NotImplementedException();
		}

		//public static void UtilImportXmlHashLookup(string lookupFilename, string xmlFilename)
		//{
		//	var lookup = new Dictionary<string, string>();

		//	var datafilesElement = XElement.Load(xmlFilename, LoadOptions.None);

		//	foreach (var datafileElement in datafilesElement.Elements())
		//	{
		//		string datafile_name = datafileElement.Attribute("name").Value;

		//		//	TODO: samples
		//		if (datafile_name == "samples")
		//			continue;

		//		Console.WriteLine(datafile_name);

		//		foreach (var machineElement in datafileElement.Elements())
		//		{
		//			string machine_name = machineElement.Attribute("name").Value;

		//			foreach (var romElement in machineElement.Elements("rom"))
		//			{
		//				string rom_name = romElement.Attribute("name").Value;
		//				long rom_size = Int64.Parse(romElement.Attribute("size").Value);
		//				string rom_crc = romElement.Attribute("crc").Value;
		//				string rom_sha1 = romElement.Attribute("sha1").Value;

		//				string key = $"{datafile_name}\t{machine_name}\t{rom_name}\t{rom_size}\t{rom_crc}".ToLower();

		//				lookup.Add(key, rom_sha1);
		//			}
		//		}
		//	}

		//	UtilSaveHashLookup(lookupFilename, lookup);
		//}

		public static Dictionary<string, string> HashLookupGetWeb()
		{
			string version = Tools.FetchTextCached("https://data.spludlow.co.uk/api/fbneo-sha1-lookup/latest.txt");

			if (version == null)
				throw new ApplicationException("Hash Lookup Get Web - Cant download version");

			string url = $"https://data.spludlow.co.uk/api/fbneo-sha1-lookup/{version}.zip";

			string zipCacheFilename = Path.Combine(Globals.CacheDirectory, Tools.ValidFileName(url));

			if (File.Exists(zipCacheFilename) == false)
			{
				Console.Write($"Dowbloading FBNeo SHA1 lookup: {zipCacheFilename} ...");
				Tools.Download(url, zipCacheFilename);
				Console.WriteLine("...done.");
			}

			var lookup = new Dictionary<string, string>();

			using (var zipFile = ZipFile.OpenRead(zipCacheFilename))
			{
				var entry = zipFile.Entries.Single();
				using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						int index = line.LastIndexOf("\t");
						string key = line.Substring(0, index);
						string sha1 = line.Substring(index + 1);

						lookup.Add(key, sha1);
					}
				}
			}

			return lookup;
		}

		public static void HashLookupLoad(string filename)
		{
			var lookup = HashLookupGetDatabase();

			var romIdLookup = HashLookupRomIdLookup();
			var sha1RomIdUpdates = new Dictionary<long[], string>();

			using (var reader = new StreamReader(filename, Encoding.UTF8))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					int index = line.LastIndexOf("\t");
					string key = line.Substring(0, index);
					string sha1 = line.Substring(index + 1);

					if (lookup.ContainsKey(key) == true)
					{
						if (String.IsNullOrEmpty(lookup[key]) == true)
						{
							lookup[key] = sha1;
							sha1RomIdUpdates.Add(romIdLookup[key].ToArray(), sha1);
						}
						else
						{
							if (lookup[key] != sha1)
								throw new ApplicationException($"SHA1 mismatch {key}\t'{lookup[key]}'\t'{sha1}'");
						}
					}
					else
					{
						Console.WriteLine($"Merge existing not in database: {key}");
					}
				}
			}

			HashLookupUpdateDatabase(sha1RomIdUpdates);
		}

		private static Dictionary<string, HashSet<long>> HashLookupRomIdLookup()
		{
			var romIdLookup = new Dictionary<string, HashSet<long>>();
			using (SQLiteConnection connection = new SQLiteConnection(Globals.Core.ConnectionStrings[0]))
			{
				DataTable table = Database.ExecuteFill(connection, @"
					SELECT [datafile].[name], [machine].[name], [rom].[name], [rom].[size], [rom].[crc], [rom].[rom_id]
					FROM [datafile] INNER JOIN [machine] ON [datafile].[datafile_id] = [machine].[datafile_id]
					INNER JOIN [rom] ON [machine].[machine_id] = [rom].[machine_id]
					WHERE ([rom].[crc] IS NOT NULL);
				");

				foreach (DataRow row in table.Rows)
				{
					string key = $"{row[0]}\t{row[1]}\t{row[2]}\t{row[3]}\t{row[4]}".ToLower();
					if (romIdLookup.ContainsKey(key) == false)
						romIdLookup.Add(key, new HashSet<long>());

					romIdLookup[key].Add(row.Field<long>(5));
				}
			}

			return romIdLookup;
		}

		private static void HashLookupUpdateDatabase(Dictionary<long[], string> sha1RomIdUpdates)
		{
			if (sha1RomIdUpdates.Count == 0)
				return;

			Console.Write("update sha1 in database ...");
			using (SQLiteConnection connection = new SQLiteConnection(Globals.Core.ConnectionStrings[0]))
			{
				using (SQLiteCommand command = new SQLiteCommand("UPDATE [rom] SET [sha1] = @sha1 WHERE [rom_id] = @rom_id", connection))
				{
					command.Parameters.Add("@sha1", DbType.String);
					command.Parameters.Add("@rom_id", DbType.Int64);

					connection.Open();

					SQLiteTransaction transaction = connection.BeginTransaction();
					try
					{
						foreach (var pair in sha1RomIdUpdates)
						{
							foreach (var rom_id in pair.Key)
							{
								command.Parameters["@sha1"].Value = pair.Value;
								command.Parameters["@rom_id"].Value = rom_id;
								command.ExecuteNonQuery();
							}
						}

						transaction.Commit();
					}
					catch
					{
						transaction.Rollback();
						throw;
					}
					finally
					{
						connection.Close();
					}
				}
			}
			Console.WriteLine("...done");
		}


		public static void HashLookupLearn(string importDirectory)
		{
			if (Globals.Core.Name != "fbneo")
				throw new ApplicationException("Learn is for fbneo only");

			string datafile_name = Path.GetFileName(importDirectory);

			var romIdLookup = HashLookupRomIdLookup();
			var sha1RomIdUpdates = new Dictionary<long[], string>();

			var lookup = HashLookupGetDatabase();

			int set_count = 0;
			int got_count = 0;
			int not_count = 0;

			foreach (string zipFilename in Directory.GetFiles(importDirectory, "*.zip"))
			{
				string machine_name = Path.GetFileNameWithoutExtension(zipFilename);

				using (var zipArchive = ZipFile.OpenRead(zipFilename))
				{
					foreach (var zipEntry in zipArchive.Entries)
					{
						if (zipEntry.FullName.Contains("/") == true)
							throw new ApplicationException($"Did not expect directory in ZIP {zipFilename} {zipEntry.FullName}");

						byte[] data;
						using (var stream = zipEntry.Open())
						{
							using (var memoryStream = new MemoryStream())
							{
								stream.CopyTo(memoryStream);
								data = memoryStream.ToArray();
							}
						}

						string crc32 = Tools.CRC32Hex(data);
						string sha1 = Tools.SHA1Hex(data);

						string key = $"{datafile_name}\t{machine_name}\t{zipEntry.FullName}\t{data.Length}\t{crc32}".ToLower();

						if (lookup.ContainsKey(key) == true)
						{
							if (String.IsNullOrEmpty(lookup[key]) == true)
							{
								lookup[key] = sha1;
								Console.WriteLine($"Learn\t{key}");
								++set_count;

								sha1RomIdUpdates.Add(romIdLookup[key].ToArray(), sha1);
							}
							else
							{
								if (lookup[key] != sha1)
									throw new ApplicationException($"Learn SHA1 mismatch {key}");

								++got_count;
							}
						}
						else
						{
							++not_count;
						}
					}
				}
			}

			Console.WriteLine($"Learn {datafile_name} SET:{set_count} GOT:{got_count} NOT:{not_count}");

			HashLookupUpdateDatabase(sha1RomIdUpdates);

			if (set_count > 0)
				Globals.Core.AllSHA1(Globals.AllSHA1);
				
			lookup = HashLookupGetDatabase();
			HashLookupReport(lookup);
		}

		public static void HashLookupSave()
		{
			if (Globals.Core.Name != "fbneo")
				throw new ApplicationException("Learn is for fbneo only");

			string targetDirectory = Path.Combine(Globals.TempDirectory, "fbneo-sha1-lookup");
			Directory.CreateDirectory(targetDirectory);

			var lookup = HashLookupGetDatabase();

			HashLookupReport(lookup);

			string filename = Path.Combine(targetDirectory, $"{Globals.Core.Version}.txt");
			string filenameZip = Path.Combine(targetDirectory, $"{Globals.Core.Version}.zip");

			File.Delete(filename);
			File.Delete(filenameZip);

			using (var writer = new StreamWriter(filename, false, Encoding.UTF8))
			{
				foreach (var pair in lookup)
					writer.WriteLine($"{pair.Key}\t{pair.Value}");
			}

			using (var zipFile = ZipFile.Open(filenameZip, ZipArchiveMode.Create))
				zipFile.CreateEntryFromFile(filename, Path.GetFileName(filename));

			File.WriteAllText(Path.Combine(targetDirectory, "latest.txt"), Globals.Core.Version, Encoding.ASCII);

			Console.WriteLine($"FBNeo SHA1 Lookup saved: {targetDirectory}");
		}

		private static Dictionary<string, string> HashLookupGetDatabase()
		{
			var lookup = new Dictionary<string, string>();

			using (SQLiteConnection connection = new SQLiteConnection(Globals.Core.ConnectionStrings[0]))
			{
				DataTable table = Database.ExecuteFill(connection, @"
					SELECT [datafile].[name], [machine].[name], [rom].[name], [rom].[size], [rom].[crc], [rom].[sha1]
					FROM [datafile] INNER JOIN [machine] ON [datafile].[datafile_id] = [machine].[datafile_id] INNER JOIN [rom] ON [machine].[machine_id] = [rom].[machine_id]
					WHERE ([rom].[crc] IS NOT NULL AND [rom].[merge] IS NULL)
					ORDER BY [datafile].[name], [machine].[name], [rom].[name];
				");

				foreach (DataRow row in table.Rows)
				{
					string key = $"{row[0]}\t{row[1]}\t{row[2]}\t{row[3]}\t{row[4]}".ToLower();
					if (lookup.ContainsKey(key) == false)
						lookup.Add(key, row.Field<string>(5) ?? "");
					//else
					//	Console.WriteLine($"Duplicate ROM in data: {key}");
				}
			}

			return lookup;
		}

		public static void HashLookupReport(Dictionary<string, string> lookup)
		{
			var gameDescriptionLookup = new Dictionary<string, string>();
			using (var connection = new SQLiteConnection(Globals.Core.ConnectionStrings[0]))
			{
				using (var adapter = new SQLiteDataAdapter("SELECT datafile.[name], machine.[name], machine.description FROM datafile " +
					"INNER JOIN machine ON datafile.datafile_id = machine.datafile_id ORDER BY datafile.[name], machine.[name];", connection))
				{
					var table = new DataTable();
					adapter.Fill(table);
					foreach (DataRow row in table.Rows)
						gameDescriptionLookup.Add($"{row[0]}\t{row[1]}", row.Field<string>(2));
				}
			}

			StringBuilder result = new StringBuilder();

			var systemCounts = new Dictionary<string, int[]>();

			foreach (var pair in lookup)
			{
				string[] parts = pair.Key.Split('\t');

				string system = parts[0];
				string machine = parts[1];

				if (systemCounts.ContainsKey(system) == false)
					systemCounts.Add(system, new int[] { 0, 0, 0 });

				systemCounts[system][1] += 1;

				if (String.IsNullOrEmpty(pair.Value) == true)
				{
					systemCounts[system][0] += 1;
					result.AppendLine(pair.Key + "\t" + gameDescriptionLookup[$"{system}\t{machine}"]);
				}
			}

			Tools.PopText(result.ToString());

			result.Length = 0;

			foreach (var pair in systemCounts)
				pair.Value[2] = (int)Math.Floor(((decimal)(pair.Value[1] - pair.Value[0]) / pair.Value[1]) * 100.0M);

			foreach (var pair in systemCounts.OrderBy(p => p.Key))
				result.AppendLine($"{pair.Key}\t{(pair.Value[0] == 0 ? "" : pair.Value[0].ToString())}\t{pair.Value[1]}\t{pair.Value[2]}");

			Tools.PopText(result.ToString());
		}

	}
}
