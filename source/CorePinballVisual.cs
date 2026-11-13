using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

using System.Data.SQLite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	internal class CorePinballVisual : ICore
	{
		string ICore.Name => "pinball-visual";

		string ICore.Version { get => _Version; }

		string ICore.Directory { get => _CoreDirectory; }


		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionVisualPinball, _ConnectionPinMAME }; }

		Dictionary<string, string> ICore.SoftwareListDescriptions => throw new NotImplementedException();

		Dictionary<string, string[]> ICore.Filters => throw new NotImplementedException();


		private string _Version = null;
		private string _RootDirectory = null;
		private string _CoreDirectory = null;

		private string _ConnectionVisualPinball = null;
		private string _ConnectionPinMAME = null;

		public PinMame _PinMame = null;

		private string[] _BitTorrentCoreNames = new string[] { "pinball", "pinmame" };


		void ICore.Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			if (version != "0")
				_Version = version;

			_PinMame = new PinMame();
			_PinMame.Initialize(Path.Combine(Path.GetDirectoryName(_RootDirectory), "pinmame"), null);
		}

		private void InitializeConnections()
		{
			_ConnectionVisualPinball = Database.MakeSQLiteConnectionString(Path.Combine(_CoreDirectory, "_pinball-visual.sqlite"));
			_ConnectionPinMAME = Database.MakeSQLiteConnectionString(Path.Combine(_CoreDirectory, "_pinmame.sqlite"));
		}

		public static string LatestLocalVersion(string directory)
		{
			string version = Tools.LatestVersionGitTagName(directory, "VPinballX64.exe");

			if (version == null)
				throw new ApplicationException($"No versions found in '{directory}'.");

			Console.WriteLine($"Version:\t{directory}\t{version}");

			return version;
		}

		int ICore.Get()
		{
			int result = _PinMame.Get();

			string url = _Version == null ?
				"https://api.github.com/repos/vpinball/vpinball/releases/latest" :
				$"https://api.github.com/repos/vpinball/vpinball/releases/tags/mame{_Version}";

			dynamic release = JsonConvert.DeserializeObject<dynamic>(Tools.FetchTextCached(url) ?? throw new ApplicationException("Unable to get vpinball release"));

			if (_Version == null)
				_Version = (string)release.tag_name;

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			JToken[] releaseAssets;

			//
			// Visual Pinball
			//
			if (File.Exists(Path.Combine(_CoreDirectory, "VPinballX64.exe")) == false)
			{
				releaseAssets = ((JArray)release.assets)
					.Where(token => ((string)token["name"]).StartsWith("Developer.VPinballX-") && ((string)token["name"]).EndsWith("Release-win-x64.zip")).ToArray();

				if (releaseAssets.Length != 1)
					throw new ApplicationException($"Did not find single vpinball asset in release: {releaseAssets.Length} {url}");

				string binariesUrl = (string)releaseAssets[0]["browser_download_url"];

				string binariesFilename = Path.Combine(_CoreDirectory, Path.GetFileName(binariesUrl));

				Console.Write($"Downloading {binariesUrl} {binariesFilename} ...");
				Tools.Download(binariesUrl, binariesFilename);
				Console.WriteLine("...done");

				Console.Write($"Extracting {binariesFilename} {_CoreDirectory} ...");
				ZipFile.ExtractToDirectory(binariesFilename, _CoreDirectory);
				Console.WriteLine("...done");

				result = 1;
			}

			//
			//	BitTorrent
			//
			if (BitTorrent.DomeInfo() == null)
				BitTorrent.Start();

			BitTorrent.WaitReady();

			BitTorrent.EnableCore(_BitTorrentCoreNames);

			//
			// DAT
			//
			dynamic info = BitTorrent.DomeInfo();

			var torrents = ((JArray)info.torrents).Where(token => ((string)token["core"]) == "pinball" && ((string)token["type"]) == "visual").ToArray();

			if (torrents.Length != 1)
				throw new ApplicationException($"Did not find single pinball-visual torrent: {torrents.Length}");

			var datUrl = (string)torrents[0]["dat"];
			string datDirectory = Path.Combine(_CoreDirectory, "_dat_" + Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(datUrl)));
			string datZipFilename = Path.Combine(datDirectory, "_dat.zip");

			foreach (string directory in Directory.GetDirectories(_CoreDirectory))
			{
				if (Path.GetFileName(directory).StartsWith("_dat_") && directory != datDirectory)
				{
					Console.WriteLine($"Remove old dat directory: {directory}");
					Directory.Delete(directory);
				}
			}

			if (Directory.Exists(datDirectory) == false || File.Exists(datZipFilename) == false)
			{
				Directory.CreateDirectory(datDirectory);

				if (File.Exists(datZipFilename) == false)
				{
					Console.Write($"Downloading {datUrl} {datZipFilename} ...");
					Tools.Download(datUrl, datZipFilename);
					Console.WriteLine("...done");
				}

				result = 1;
			}

			return result;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			_PinMame.Xml();

			string[] directories = Directory.GetDirectories(_CoreDirectory).Where(dir => Path.GetFileName(dir).StartsWith("_dat_")).ToArray();
			if (directories.Length != 1)
				throw new ApplicationException($"Did not find single _DAT_ directory: {directories.Length} {_CoreDirectory}");

			string datZipFilename = Path.Combine(directories[0], "_dat.zip");
			if (File.Exists(datZipFilename) == false)
				throw new ApplicationException($"Did not find dat zip: {datZipFilename}");

			if (Directory.GetFiles(directories[0], "*.xml").Length == 0)
			{
				Console.Write($"Extracting {datZipFilename} {directories[0]} ...");
				ZipFile.ExtractToDirectory(datZipFilename, directories[0]);
				Console.WriteLine("...done");
			}

		}

		void ICore.MSSql(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			DataSet dataSet;

			string[] directories = Directory.GetDirectories(_CoreDirectory).Where(dir => Path.GetFileName(dir).StartsWith("_dat_") == true).ToArray();
			if (directories.Length != 1)
				throw new ApplicationException($"Did not find one _dat_ directory {directories.Length} {_CoreDirectory}");
			dataSet = PinballVisualDataSet(directories[0]);
			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseNames[0]);
			Database.MakeForeignKeys(serverConnectionString, databaseNames[0]);

			string filename = Path.Combine(_PinMame._CoreDirectory, "_pinmame.xml");
			if (File.Exists(filename) == false)
				throw new ApplicationException($"Did not find one PinMAME XML {filename}");
			dataSet = PinMameDataSet(filename);
			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseNames[1]);
			Database.MakeForeignKeys(serverConnectionString, databaseNames[1]);
		}

		public static DataSet PinballVisualDataSet(string directory)
		{
			XElement subsetsElement = new XElement("subsets");
			XElement subsetElement = new XElement("subset");
			subsetElement.SetAttributeValue("name", "pinball-visual");
			subsetElement.SetAttributeValue("description", "Visual Pinball");
			subsetsElement.Add(subsetElement);

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				XElement datafileElement = XElement.Load(filename);

				//	Move header
				foreach (var itemElement in datafileElement.Element("header").Elements())
					datafileElement.SetAttributeValue(itemElement.Name, itemElement.Value);
				datafileElement.Element("header").Remove();

				subsetElement.Add(datafileElement);
			}

			DataSet dataSet = new DataSet();
			ReadXML.ImportXMLWork(subsetsElement, dataSet, null, null);
			return dataSet;
		}

		public static DataSet PinMameDataSet(string filename)
		{
			XElement subsetsElement = new XElement("subsets");
			XElement subsetElement = new XElement("subset");
			subsetElement.SetAttributeValue("name", "pinmame");
			subsetElement.SetAttributeValue("description", "PinMAME");
			subsetsElement.Add(subsetElement);

			XElement mameElement = XElement.Load(filename);

			foreach (XElement gameElement in mameElement.Elements())
			{
				subsetElement.Add(gameElement);
			}

			DataSet dataSet = new DataSet();
			ReadXML.ImportXMLWork(subsetsElement, dataSet, null, null);
			return dataSet;
		}

		void ICore.SQLite()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Tools.ConsoleHeading(1, new string[] { "SQLite Visual Pinball", _Version, _CoreDirectory });

			DataSet dataSet;
			string name;

			name = "pinball-visual";

			string[] directories = Directory.GetDirectories(_CoreDirectory).Where(dir => Path.GetFileName(dir).StartsWith("_dat_") == true).ToArray();
			if (directories.Length != 1)
				throw new ApplicationException($"Did not find one _dat_ directory {directories.Length} {_CoreDirectory}");
			dataSet = PinballVisualDataSet(directories[0]);

			Database.DataSet2SQLite(name, Database.MakeSQLiteConnectionString(Path.Combine(_CoreDirectory, $"_{name}.sqlite")), dataSet);

			name = "pinmame";

			string filename = Path.Combine(_PinMame._CoreDirectory, "_pinmame.xml");
			if (File.Exists(filename) == false)
				throw new ApplicationException($"Did not find one PinMAME XML {filename}");
			dataSet = PinMameDataSet(filename);

			Database.DataSet2SQLite(name, Database.MakeSQLiteConnectionString(Path.Combine(_CoreDirectory, $"_{name}.sqlite")), dataSet);

		}


		void ICore.SQLiteAo()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Tools.ConsoleHeading(1, new string[] { "SQLite Visual Pinball", _Version, _CoreDirectory });

			DataSet dataSet;
			string name;
			string sqliteFilename;
			string connectionString;

			name = "pinball-visual";
			sqliteFilename = Path.Combine(_CoreDirectory, $"_{name}.sqlite");
			connectionString = Database.MakeSQLiteConnectionString(sqliteFilename);

			if (File.Exists(sqliteFilename) == false || (Cores.GetAoMetaDataAssemblyVersion(connectionString) != Globals.AssemblyVersion))
			{ 
				if (File.Exists(sqliteFilename) == true)
				{
					Console.WriteLine($"Replace Visual Pinball SQLite database old version {sqliteFilename}");
					File.Delete(sqliteFilename);
				}

				string[] directories = Directory.GetDirectories(_CoreDirectory).Where(dir => Path.GetFileName(dir).StartsWith("_dat_") == true).ToArray();
				if (directories.Length != 1)
					throw new ApplicationException($"Did not find one _dat_ directory {directories.Length} {_CoreDirectory}");
				dataSet = PinballVisualDataSet(directories[0]);

				Cores.AddAoMetaData(dataSet, Globals.AssemblyVersion);

				Database.DataSet2SQLite(name, connectionString, dataSet);
			}

			name = "pinmame";
			sqliteFilename = Path.Combine(_CoreDirectory, $"_{name}.sqlite");
			connectionString = Database.MakeSQLiteConnectionString(sqliteFilename);

			if (File.Exists(sqliteFilename) == false || (Cores.GetAoMetaDataAssemblyVersion(connectionString) != Globals.AssemblyVersion))
			{
				if (File.Exists(sqliteFilename) == true)
				{
					Console.WriteLine($"Replace Visual Pinball SQLite database old version {sqliteFilename}");
					File.Delete(sqliteFilename);
				}

				string filename = Path.Combine(_PinMame._CoreDirectory, "_pinmame.xml");
				if (File.Exists(filename) == false)
					throw new ApplicationException($"Did not find one PinMAME XML {filename}");
				dataSet = PinMameDataSet(filename);

				Cores.AddAoMetaData(dataSet, Globals.AssemblyVersion);

				Database.DataSet2SQLite(name, connectionString, dataSet);
			}
		}


		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			InitializeConnections();

			Console.Write($"Load all database SHA1 ...");
			Cores.AllSHA1(hashSet, _ConnectionVisualPinball, new string[] { "rom" });
			Cores.AllSHA1(hashSet, _ConnectionPinMAME, new string[] { "rom" });
			Console.WriteLine("...done");
		}


		public static string PlacePinball(ICore core, string line)
		{
			string[] parts = line.Split('@');
			if (parts.Length != 3)
				throw new ApplicationException("Bad Line");

			string datafile_name = parts[0];
			string machine_name = parts[1];
			string vpxName = parts[2];

			Globals.WorkerTaskReport = Reports.PlaceReportTemplate();

			string[] info;
			long machine_id;
			string machine_description;

			//
			// VisualPinball
			//
			CorePinballVisual corePinballVisual = (CorePinballVisual)core;

			SQLiteConnection connectionVisualPinball = new SQLiteConnection(core.ConnectionStrings[0]);

			using (SQLiteCommand command = new SQLiteCommand(
				"SELECT machine.machine_id, machine.description FROM datafile INNER JOIN machine ON datafile.datafile_id = machine.datafile_id " +
				"WHERE (datafile.name = @datafile_name AND machine.name = @machine_name)", connectionVisualPinball))
			{
				command.Parameters.AddWithValue("@datafile_name", datafile_name);
				command.Parameters.AddWithValue("@machine_name", machine_name);

				DataTable machineTable = Database.ExecuteFill(command);

				if (machineTable.Rows.Count == 0)
					throw new ApplicationException($"Table not found {datafile_name} / {machine_name}");

				machine_id = (long)machineTable.Rows[0]["machine_id"];
				machine_description = (string)machineTable.Rows[0]["description"];
			}

			Tools.ConsoleHeading(1, new string[] { machine_description, core.Directory });

			DataTable romTable = Database.ExecuteFill(connectionVisualPinball, $"SELECT * FROM [rom] WHERE ([rom].[machine_id] = {machine_id}) ORDER BY [name] DESC");

			if (romTable.Rows.Count == 0)
				throw new ApplicationException($"No machine roms found {datafile_name} / {machine_name}");

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

			info = new string[] { "pinball table", datafile_name, machine_name };

			Place._BtStarted = false;   //	TODO tidy up

			if (downloadRequired == true)
			{
				Place.StartBitTorrent(corePinballVisual._BitTorrentCoreNames);    //	TODO tidy up

				var btFile = BitTorrent.SoftwareRom(core.Name, datafile_name, machine_name);
				if (btFile != null)
					Place.DownloadImportFiles(btFile.Filename, btFile.Length, info);
			}

			string tablesDirectory = Path.Combine(core.Directory, "tables", machine_name);

			Place.PlaceAssetFiles(romTable.Rows.Cast<DataRow>().ToArray(), Globals.RomHashStore, tablesDirectory, null, info);

			//
			// PinMAME
			//
			PinMame corePinMAME = ((CorePinballVisual)core)._PinMame;
			corePinMAME.Check();

			SQLiteConnection connectionPinMAME = new SQLiteConnection(core.ConnectionStrings[1]);

			//
			// Extract VBS for PinMAME
			//
			var pinmameGames = new HashSet<string>();

			foreach (DataRow romRow in romTable.Rows)
			{
				string name = (string)romRow["name"];

				if (name.EndsWith(".vpx") == false && name.EndsWith(".vpt") == false)
					continue;

				string vpxFilename = Path.Combine(tablesDirectory, name);
				string vbsFilename = Path.Combine(tablesDirectory, Path.GetFileNameWithoutExtension(name) + ".vbs");

				if (vpxName == null)
					vpxName = name;

				if (File.Exists(vbsFilename) == false)
					ExtractVbs(core.Directory, vpxFilename);

				string pinmameGame = FindPinMameGameName(vbsFilename);
				if (pinmameGame != null)
					pinmameGames.Add(pinmameGame);
			}

			//
			// Place PinMAME
			//
			if (pinmameGames.Count > 0)
			{
				var parentNames = String.Join(", ", pinmameGames.Select(game => $"'{game}'"));

				var cloneTable = Database.ExecuteFill(connectionPinMAME,
					$"SELECT [cloneof] FROM [game] WHERE [name] IN ({parentNames}) AND [cloneof] IS NOT NULL");

				foreach (DataRow row in cloneTable.Rows)
					pinmameGames.Add((string)row["cloneof"]);
			}

			Console.WriteLine($"Required pinmame: {String.Join(", ", pinmameGames)}");

			foreach (string game_name in pinmameGames)
			{
				DataTable gameTable = Database.ExecuteFill(connectionPinMAME, $"SELECT * FROM [game] WHERE ([game].[name] = '{game_name}')");

				if (gameTable.Rows.Count == 0)
					throw new ApplicationException($"Game not found {game_name}");

				long game_id = (long)gameTable.Rows[0]["game_id"];

				romTable = Database.ExecuteFill(connectionPinMAME, $"SELECT * FROM [rom] WHERE ([rom].[game_id] = {game_id})");

				downloadRequired = false;

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

				info = new string[] { "pinmame game", game_name, "" };

				if (downloadRequired == true)
				{
					Place.StartBitTorrent(corePinballVisual._BitTorrentCoreNames);    //	TODO tidy up

					var btFile = BitTorrent.MachineRom("pinmame", game_name);
					if (btFile != null)
						Place.DownloadImportFiles(btFile.Filename, btFile.Length, info);
				}

				string romsDirectory = Path.Combine(corePinMAME._CoreDirectory, "roms", game_name);

				Place.PlaceAssetFiles(romTable.Rows.Cast<DataRow>().ToArray(), Globals.RomHashStore, romsDirectory, null, info);
			}

			return Path.Combine(machine_name, vpxName);
		}

		public static bool ExtractVbs(string exeDirectory, string vpxFilename)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(exeDirectory, "VPinballX64.exe"))
			{
				WorkingDirectory = exeDirectory,
				Arguments = $"-extractvbs \"{vpxFilename}\"",
				UseShellExecute = false,
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.Start();
				process.WaitForExit();

				if (process.ExitCode != 0)
					Console.WriteLine($"VPinballX64 extractvbs Bad exit code {process.ExitCode}");

				return process.ExitCode == 0;
			}
		}

		public static string FindPinMameGameName(string vbsFilename)
		{
			string found = null;

			string[] lines = File.ReadAllLines(vbsFilename);

			string varible = null;

			// pass 1 - find Controller.GameName
			for (int row = 0; row < lines.Length; ++row)
			{
				string line = lines[row].Trim();
				if (line.StartsWith("\'") == true)
					continue;

				if (line.Contains(".GameName") == true)
				{
					if (line.Contains("Controller") || lines[row - 1].Contains("Controller") || lines[row - 2].Contains("Controller"))
					{
						int index = line.IndexOf("=");
						if (index != -1)
						{
							varible = line.Substring(index + 1).Trim();
							break;
						}
					}
				}
			}

			if (varible == null)
				return null;

			if (varible.Contains("\"") == true)
				return varible.Trim('\"').ToLower();

			varible = varible.ToLower();

			// pass 2 - find varible value
			for (int row = 0; row < lines.Length; ++row)
			{
				string line = lines[row].Trim().ToLower();
				if (line.StartsWith("\'") == true)
					continue;

				if ((line.StartsWith($"const {varible}") == true || line.StartsWith(varible) == true) && line.Contains("=") == true)
				{
					int start = line.IndexOf('"') + 1;
					int end = line.IndexOf('"', start);
					found = line.Substring(start, end - start);

					break;
				}
			}

			return found;
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



		void ICore.Json()
		{
			throw new NotImplementedException();
		}

		void ICore.MsAccess()
		{
			throw new NotImplementedException();
		}



		void ICore.MSSqlPayload(string serverConnectionString, string[] databaseNames)
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

		void ICore.Zips()
		{
			throw new NotImplementedException();
		}

		List<DataQueryProfile> ICore.GetDataQueryProfiles()
		{
			throw new NotImplementedException();
		}

		public static void UtilTestFindPinMameGameName()
		{
			string directory = @"C:\tmp\Visual Pinball [VPX08] PinMame Tables";
			string exeDirectory = @"C:\GIT\mame-ao\bin\Debug\pinball-visual\v10.8.0-2051-28dd6c3";

			DataTable table = Tools.MakeDataTable(
				"Found	VPS",
				"String	String");

			foreach (string tableDirectory  in Directory.GetDirectories(directory))
			{
				foreach (string vpxFilename in Directory.GetFiles(tableDirectory, "*.vpx"))
				{
					string vbsFilename = Path.Combine(Path.GetDirectoryName(vpxFilename), Path.GetFileNameWithoutExtension(vpxFilename) + ".vbs");

					Console.WriteLine(vbsFilename);

					if (File.Exists(vbsFilename) == false)
					{
						if (ExtractVbs(exeDirectory, vpxFilename) == false)
							continue;
					}

					string found;

					try
					{
						found = FindPinMameGameName(vbsFilename);
					}
					catch (Exception e)
					{
						Tools.PopText(e.Message + Environment.NewLine + File.ReadAllText(vbsFilename));
						throw;
					}

					table.Rows.Add(found, vbsFilename);

					Console.WriteLine("\t" + found);
				}
			}

			Tools.PopText(table);
		}
	}
}
