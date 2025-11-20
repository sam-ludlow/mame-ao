using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public interface ICore
	{
		string Name { get; }
		string Version { get; }
		string Directory { get; }
		string[] ConnectionStrings { get; }

		Dictionary<string, string> SoftwareListDescriptions { get; }

		void Initialize(string directory, string version);
		int Get();
		void Xml();
		void Json();
		void SQLiteAo();
		void AllSHA1(HashSet<string> hashSet);

		void SQLite();
		void MSSql();
		void MsAccess();
		void Zips();
		void MSSqlHtml();
		void MSSqlPayload();

		DataRow GetMachine(string machine_name);
		DataRow[] GetMachineRoms(string machine_name);
		DataRow[] GetMachineDisks(DataRow machine);
		DataRow[] GetMachineSamples(DataRow machine);
		DataRow[] GetMachineSoftwareLists(DataRow machine);
		DataRow GetSoftwareList(string softwarelist_name);
		HashSet<string> GetReferencedMachines(string machine_name);
		DataRow[] GetMachineDeviceRefs(string machine_name);
		DataRow GetSoftware(DataRow softwarelist, string software_name);
		DataRow GetSoftware(string softwarelist_name, string software_name);
		DataRow[] GetSoftwareSharedFeats(DataRow software);
		DataRow[] GetSoftwareListsSoftware(DataRow softwarelist);
		DataRow[] GetSoftwareRoms(DataRow software);
		DataRow[] GetSoftwareDisks(DataRow software);
		DataRow[] GetMachineFeatures(DataRow machine);

		string GetRequiredMedia(string machine_name, string softwarelist_name, string software_name);

		DataTable QueryMachines(DataQueryProfile profile, int offset, int limit, string search);
		DataTable QuerySoftware(string softwarelist_name, int offset, int limit, string search, string favorites_machine);
	}
	public class Cores
	{
		public static void EnableCore(string name, string version)
		{
			if (Globals.Core != null && Globals.Core.Name == name && version == null)
				return;

			if (Globals.Core != null && Globals.Core.Name == name && version != null && Globals.Core.Version == version)
				return;

			ICore core;

			switch (name)
			{
				case "mame":
					core = new CoreMame();
					break;

				case "hbmame":
					core = new CoreHbMame();
					break;

				default:
					throw new ApplicationException($"Unknow core: {name}");
			}

			core.Initialize(Path.Combine(Globals.RootDirectory, name), version);

			core.Get();
			core.Xml();
			core.SQLiteAo();
			core.AllSHA1(Globals.AllSHA1);

			Globals.Core = core;

			Globals.Genre.InitializeCore(core);

			Globals.Favorites = new Favorites();

			if (Globals.BitTorrentAvailable == true)
				BitTorrent.EnableCore(core.Name);
		}

		public static void ExtractXML(string exeFilename)
		{
			string coreDirectory = Path.GetDirectoryName(exeFilename);

			string machineXmlFilename = Path.Combine(coreDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(coreDirectory, "_software.xml");

			if (File.Exists(machineXmlFilename) == false)
			{
				Console.Write("Extract machine XML ...");
				Mame.ExtractXML(exeFilename, machineXmlFilename, "-listxml");
				Console.WriteLine("...done");
			}

			if (File.Exists(softwareXmlFilename) == false)
			{
				Console.Write("Extract software XML and combine ...");
				Mame.ExtractXML(exeFilename, softwareXmlFilename, "-listsoftware");
				ReadXML.CombineHashSoftwareLists(softwareXmlFilename);
				Console.WriteLine("...done");
			}
		}

		public static void MakeSQLite(string coreDirectory, HashSet<string> requiredMachineTables, HashSet<string> requiredSoftwareTables, bool overwrite, string assemblyVersion, Action<DataSet, string> aoExtra)
		{
			string machineSqlLiteFilename = Path.Combine(coreDirectory, "_machine.sqlite");
			string softwareSqlLiteFilename = Path.Combine(coreDirectory, "_software.sqlite");

			string machineXmlFilename = Path.Combine(coreDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(coreDirectory, "_software.xml");

			if (overwrite == true)
			{
				File.Delete(machineSqlLiteFilename);
				File.Delete(machineSqlLiteFilename);
			}

			if (File.Exists(machineSqlLiteFilename) == false)
			{
				Console.Write("Convert machine XML to SQLite ...");
				XML2SQLite(machineXmlFilename, machineSqlLiteFilename, requiredMachineTables, assemblyVersion, aoExtra);
				GC.Collect();
				Console.WriteLine("...done");
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				Console.Write("Convert software XML to SQLite ...");
				XML2SQLite(softwareXmlFilename, softwareSqlLiteFilename, requiredSoftwareTables, assemblyVersion, aoExtra);
				GC.Collect();
				Console.WriteLine("...done");
			}
		}

		private static void XML2SQLite(string xmlFilename, string sqliteFilename, HashSet<string> tableNames, string assemblyVersion, Action<DataSet, string> aoExtra)
		{
			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, tableNames);

			if (aoExtra != null)
				aoExtra(dataSet, assemblyVersion);

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			Database.DataSet2SQLite(document.Name.LocalName, connectionString, dataSet);
		}

		public static void MsAccess(string directory)
		{
			foreach (string filename in Directory.GetFiles(directory, "_*.xml"))
				Tools.MsAccessFromXML(filename);
		}

		public static void Zips(string directory)
		{
			HashSet<string> extentions = new HashSet<string>(new string[] { ".accdb", ".json", ".sqlite", ".xml" });
			foreach (string filename in Directory.GetFiles(directory))
			{
				if (Path.GetFileName(filename).StartsWith("_") == false || extentions.Contains(Path.GetExtension(filename)) == false)
					continue;

				Console.WriteLine(filename);

				Tools.CompressSingleFile(filename, filename + ".zip");

				Tools.Compress7Zip(filename, filename + ".7z");
			}
		}

		public static void AllSHA1(HashSet<string> hashSet, string connectionString, string[] tableNames)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				foreach (string tableName in tableNames)
				{
					DataTable table = Database.ExecuteFill(connection, $"SELECT [sha1] FROM [{tableName}] WHERE [sha1] IS NOT NULL");
					foreach (DataRow row in table.Rows)
						hashSet.Add((string)row["sha1"]);
				}
			}
		}

		public static void AddExtraAoData(DataSet dataSet, string assemblyVersion)
		{
			DataTable table = new DataTable("ao_info");
			table.Columns.Add("ao_info_id", typeof(long));
			table.Columns.Add("assembly_version", typeof(string));
			table.Rows.Add(1L, assemblyVersion);
			dataSet.Tables.Add(table);

			if (dataSet.Tables.Contains("machine") == true)
			{
				DataTable machineTable = dataSet.Tables["machine"];
				DataTable romTable = dataSet.Tables["rom"];
				DataTable diskTable = dataSet.Tables["disk"];
				DataTable softwarelistTable = dataSet.Tables["softwarelist"];
				DataTable driverTable = dataSet.Tables["driver"];
				DataTable inputTable = dataSet.Tables["input"];

				machineTable.Columns.Add("ao_rom_count", typeof(int));
				machineTable.Columns.Add("ao_disk_count", typeof(int));
				machineTable.Columns.Add("ao_softwarelist_count", typeof(int));
				machineTable.Columns.Add("ao_driver_status", typeof(string));
				machineTable.Columns.Add("ao_input_coins", typeof(int));

				foreach (DataRow machineRow in machineTable.Rows)
				{
					long machine_id = (long)machineRow["machine_id"];

					DataRow[] romRows = romTable.Select($"machine_id={machine_id}");
					DataRow[] diskRows = diskTable != null ? diskTable.Select($"machine_id={machine_id}") : new DataRow[0];
					DataRow[] softwarelistRows = softwarelistTable.Select($"machine_id={machine_id}");
					DataRow[] driverRows = driverTable.Select($"machine_id={machine_id}");
					DataRow[] inputRows = inputTable.Select($"machine_id={machine_id}");

					machineRow["ao_rom_count"] = romRows.Count(row => row.IsNull("sha1") == false);
					machineRow["ao_disk_count"] = diskRows.Count(row => row.IsNull("sha1") == false);

					machineRow["ao_softwarelist_count"] = softwarelistRows.Length;
					if (driverRows.Length == 1)
						machineRow["ao_driver_status"] = (string)driverRows[0]["status"];

					machineRow["ao_input_coins"] = 0;
					if (inputRows.Length == 1 && inputRows[0].IsNull("coins") == false)
						machineRow["ao_input_coins"] = Int32.Parse((string)inputRows[0]["coins"]);
				}
			}

			if (dataSet.Tables.Contains("software") == true)
			{
				DataTable softwareTable = dataSet.Tables["software"];
				if (softwareTable.Columns.Contains("cloneof") == false)
					softwareTable.Columns.Add("cloneof", typeof(string));
			}
		}

		public static DataRow GetMachine(string connectionString, string name)
		{
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM [machine] WHERE [name] = '{name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public static DataRow[] GetMachineRoms(string connectionString, DataRow machine)
		{
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM [rom] WHERE [machine_id] = {(long)machine["machine_id"]} AND [sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}
		public static DataRow[] GetMachineRoms(string connectionString, string machine_name)
		{
			DataTable table = Database.ExecuteFill(connectionString,
				$"SELECT [rom].* FROM [machine] INNER JOIN rom ON machine.machine_id = rom.machine_id WHERE ([machine].[name] = '{machine_name}') AND ([sha1] IS NOT NULL)");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetMachineDisks(string connectionString, DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM [disk] WHERE [machine_id] = {machine_id} AND [sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetMachineSamples(string connectionString, DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM [sample] WHERE [machine_id] = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetMachineSoftwareLists(string connectionString, DataRow machine, Dictionary<string, string> softwareListDescriptions)
		{
			long machine_id = (long)machine["machine_id"];
			string machineName = (string)machine["name"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM softwarelist WHERE machine_id = {machine_id}");

			table.Columns.Add("description", typeof(string));

			foreach (DataRow row in table.Rows)
			{
				string listName = (string)row["name"];
				if (softwareListDescriptions.ContainsKey(listName) == true)
					row["description"] = softwareListDescriptions[listName];
				else
					row["description"] = $"MAME DATA ERROR machine '{machineName}' software list '{listName}' does not exist";
			}

			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow GetSoftwareList(string connectionString, string softwarelist_name)
		{
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM softwarelist WHERE name = '{softwarelist_name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public static HashSet<string> GetReferencedMachines(ICore core, string machine_name)
		{
			HashSet<string> requiredMachines = new HashSet<string>();

			GetReferencedMachines(core, machine_name, requiredMachines);

			return requiredMachines;
		}
		private static void GetReferencedMachines(ICore core, string machine_name, HashSet<string> requiredMachines)
		{
			if (requiredMachines.Contains(machine_name) == true)
				return;

			DataRow machineRow = core.GetMachine(machine_name) ?? throw new ApplicationException($"Machine not found (GetReferencedMachines): ${machine_name}");

			if ((long)machineRow["ao_rom_count"] > 0)
				requiredMachines.Add(machine_name);

			if (machineRow.IsNull("romof") == false)
				GetReferencedMachines(core, (string)machineRow["romof"], requiredMachines);

			foreach (DataRow row in core.GetMachineDeviceRefs(machine_name))
				GetReferencedMachines(core, (string)row["name"], requiredMachines);
		}

		public static DataRow GetSoftware(string connectionString, DataRow softwarelist, string software_name)
		{
			long softwarelist_id = (long)softwarelist["softwarelist_id"];

			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				string commandText = @"SELECT * FROM software WHERE softwarelist_id = @softwarelist_id AND software.name = @software_name";
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_id", softwarelist_id);
					command.Parameters.AddWithValue("@software_name", software_name);
					table = Database.ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
				return null;

			return table.Rows[0];
		}

		public static DataRow GetSoftware(string connectionString, string softwarelist_name, string software_name)
		{
			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				string commandText = @"
					SELECT software.* FROM softwarelist
					INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id
					WHERE ((softwarelist.name = @softwarelist_name) AND (software.name = @software_name))
				";

				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_name", softwarelist_name);
					command.Parameters.AddWithValue("@software_name", software_name);
					table = Database.ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
				return null;

			return table.Rows[0];
		}

		public static DataRow[] GetSoftwareSharedFeats(string connectionString, DataRow software)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
				if (Database.TableExists(connection, "sharedfeat") == false)
					return new DataRow[0];
			long software_id = (long)software["software_id"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM sharedfeat WHERE software_id = {software_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetSoftwareListsSoftware(string connectionString, DataRow softwarelist)
		{
			long softwarelist_id = (long)softwarelist["softwarelist_id"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM software WHERE softwarelist_id = {softwarelist_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetSoftwareRoms(string connectionString, DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = Database.ExecuteFill(connectionString,
				"SELECT rom.* FROM (part INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				$"WHERE part.software_id = {software_id} AND rom.[name] IS NOT NULL AND rom.[sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetSoftwareDisks(string connectionString, DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = Database.ExecuteFill(connectionString,
				"SELECT disk.* FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				$"WHERE part.software_id = {software_id} AND disk.[name] IS NOT NULL AND disk.[sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static DataRow[] GetMachineFeatures(string connectionString, DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = Database.ExecuteFill(connectionString, $"SELECT * FROM feature WHERE machine_id = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static string GetRequiredMedia(string machineConnectionString, string softwareConnectionString, Dictionary<string, string> softwareListDescriptions, string machine_name, string softwarelist_name, string software_name)
		{
			DataRow softwareListRow = GetSoftwareList(softwareConnectionString, softwarelist_name) ?? throw new ApplicationException($"GetRequiredMedia Software List not found: {softwarelist_name}");
			DataRow softwareRow = GetSoftware(softwareConnectionString, softwareListRow, software_name) ?? throw new ApplicationException($"GetRequiredMedia Software not found: {software_name}");

			List<string> results = new List<string>();

			foreach (DataRow sharedFeatRow in GetSoftwareSharedFeats(softwareConnectionString, softwareRow))
			{
				if ((string)sharedFeatRow["name"] != "requirement")
					continue;

				string[] valueParts = ((string)sharedFeatRow["value"]).Split(':');

				string require_softwarelist_name = null;
				string require_software_name = valueParts[valueParts.Length - 1];
				if (valueParts.Length > 1)
				{
					require_softwarelist_name = valueParts[0];

					if (GetSoftware(softwareConnectionString, require_softwarelist_name, require_software_name) == null)
						require_softwarelist_name = null;
				}

				if (require_softwarelist_name == null && GetSoftware(softwareConnectionString, softwarelist_name, require_software_name) != null)
					require_softwarelist_name = softwarelist_name;

				if (require_softwarelist_name == null)
				{
					DataRow machineRow = GetMachine(machineConnectionString, machine_name);
					foreach (DataRow machineSoftwareListRow in GetMachineSoftwareLists(machineConnectionString, machineRow, softwareListDescriptions))
					{
						string machineSoftwareList = (string)machineSoftwareListRow["name"];

						if (GetSoftware(softwareConnectionString, machineSoftwareList, require_software_name) != null)
						{
							if (require_softwarelist_name == null)
								require_softwarelist_name = machineSoftwareList;
							else
								Console.WriteLine($"!!! Multiple software lists for sharedfeat.value (using first found): {machine_name}, {softwarelist_name}, {software_name} => {machineSoftwareList}, {require_software_name}");
						}
					}
				}

				if (require_softwarelist_name == null)
				{
					Console.WriteLine($"!!! Not found Software list for sharedfeat.value: {machine_name}, {softwarelist_name}, {software_name} => {valueParts[0]}, {valueParts[1]}");
					continue;
				}

				results.Add(GetSoftwareMedia(machineConnectionString, softwareConnectionString, machine_name, require_softwarelist_name, require_software_name));
			}

			results.Add(GetSoftwareMedia(machineConnectionString, softwareConnectionString, machine_name, softwarelist_name, software_name));

			return String.Join(" ", results);
		}

		private static string GetSoftwareMedia(string machineConnectionString, string softwareConnectionString, string machine_name, string softwarelist_name, string software_name)
		{
			string commandText = @"
				SELECT [part].[name], [part].[interface]
				FROM ([softwarelist] INNER JOIN [software] ON [softwarelist].softwarelist_id = [software].softwarelist_id) INNER JOIN [part] ON [software].software_id = [part].software_id
				WHERE (([softwarelist].[name] = @softwarelist_name) AND ([software].[name] = @software_name) AND ([part].[interface] IS NOT NULL))
				ORDER BY [part].[name]
			";

			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(softwareConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_name", softwarelist_name);
					command.Parameters.AddWithValue("@software_name", software_name);
					table = Database.ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
			{
				Console.WriteLine($"!!! Can't find Software Media Interfaces, list:{softwarelist_name}, soft:{software_name}");
				return software_name;
			}

			var softwareMediaInterfaces = table.Rows.Cast<DataRow>().Select(r => (string)r["interface"]);

			using (SQLiteConnection connection = new SQLiteConnection(machineConnectionString))
			{
				commandText = @"
					SELECT device.*, instance.* FROM (machine INNER JOIN device ON machine.machine_id = device.machine_id) INNER JOIN instance ON device.device_id = instance.device_id
					WHERE (machine.name = @machine_name AND [device].[interface] IS NOT NULL)
					ORDER BY device.type, device.tag, instance.name
				";
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@machine_name", machine_name);
					table = Database.ExecuteFill(command);
				}
			}

			Dictionary<string, List<string>> machineInterfaceBriefnames = new Dictionary<string, List<string>>();

			foreach (DataRow row in table.Rows)
			{
				string instance_briefname = (string)row["briefname"];

				foreach (string device_interface in ((string)row["interface"]).Split(',').Select(i => i.Trim()))
				{
					if (machineInterfaceBriefnames.ContainsKey(device_interface) == false)
						machineInterfaceBriefnames.Add(device_interface, new List<string>());

					machineInterfaceBriefnames[device_interface].Add(instance_briefname);
				}
			}

			List<string> results = new List<string>();

			foreach (string mediaInterface in softwareMediaInterfaces)
			{
				if (machineInterfaceBriefnames.ContainsKey(mediaInterface) == true)
				{
					List<string> names = machineInterfaceBriefnames[mediaInterface];
					if (names.Count > 0)
					{
						results.Add($"-{names[0]} {software_name}");
						names.RemoveAt(0);
					}
				}
			}

			if (results.Count == 0)
			{
				Console.WriteLine($"!!! Can't find Machine Device Instances: {machine_name}, {softwarelist_name}, {software_name}");
				return software_name;
			}

			return String.Join(" ", results);
		}

		public static DataTable QueryMachines(string connectionString, DataQueryProfile profile, int offset, int limit, string search)
		{
			string commandText = profile.CommandText;

			if (search == null)
			{
				commandText = commandText.Replace("@SEARCH", "");
			}
			else
			{
				search = "%" + String.Join("%", search.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) + "%";
				commandText = commandText.Replace("@SEARCH",
					" AND (machine.name LIKE @name OR machine.description LIKE @description)");
			}

			if (profile.Key == "favorites")
			{
				string favorites = "machine.machine_id = -1";
				if (Globals.Favorites._Machines.Count > 0)
				{
					StringBuilder text = new StringBuilder();
					foreach (string name in Globals.Favorites._Machines.Keys)
					{
						if (text.Length > 0)
							text.Append(" OR ");
						text.Append($"(name = '{name}')");
					}
					favorites = text.ToString();
				}
				commandText = commandText.Replace("@FAVORITES", $" AND ({favorites})");
			}

			commandText = commandText.Replace("@LIMIT", limit.ToString());
			commandText = commandText.Replace("@OFFSET", offset.ToString());

			DataTable table;

			using (var connection = new SQLiteConnection(connectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					if (search != null)
					{
						command.Parameters.AddWithValue("@name", search);
						command.Parameters.AddWithValue("@description", search);
					}

					table = Database.ExecuteFill(command);
				}
			}

			Globals.Favorites.AddColumnMachines(table, "name", "favorite");

			return table;
		}

		public static DataTable QuerySoftware(string connectionString, string softwarelist_name, int offset, int limit, string search, string favorites_machine)
		{
			string commandText = "SELECT software.*, softwarelist.name AS softwarelist_name, COUNT() OVER() AS ao_total FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_Id = software.softwarelist_Id " +
				$"WHERE (softwarelist.name = '{softwarelist_name}' @SEARCH) ORDER BY software.description COLLATE NOCASE ASC " +
				"LIMIT @LIMIT OFFSET @OFFSET";

			if (softwarelist_name == "@fav")
			{
				commandText = "SELECT software.*, softwarelist.name AS softwarelist_name, COUNT() OVER() AS ao_total FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_Id = software.softwarelist_Id " +
					"WHERE ((@FAVORITES) @SEARCH) ORDER BY software.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET";

				string[][] listSoftwareNames = Globals.Favorites.ListSoftwareUsedByMachine(favorites_machine);

				if (listSoftwareNames.Length == 0)
				{
					commandText = commandText.Replace("@FAVORITES", "software_id = -1");
				}
				else
				{
					StringBuilder text = new StringBuilder();
					foreach (string[] listSoftwareName in listSoftwareNames)
					{
						if (text.Length > 0)
							text.Append(" OR ");

						text.Append($"(softwarelist.name = '{listSoftwareName[0]}' AND software.name = '{listSoftwareName[1]}')");
					}
					commandText = commandText.Replace("@FAVORITES", text.ToString());
				}
			}

			if (search == null)
			{
				commandText = commandText.Replace("@SEARCH", "");
			}
			else
			{
				search = "%" + String.Join("%", search.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) + "%";
				commandText = commandText.Replace("@SEARCH",
					" AND (software.name LIKE @name OR software.description LIKE @description)");
			}

			commandText = commandText.Replace("@LIMIT", limit.ToString());
			commandText = commandText.Replace("@OFFSET", offset.ToString());

			DataTable table;

			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					if (search != null)
					{
						command.Parameters.AddWithValue("@name", search);
						command.Parameters.AddWithValue("@description", search);
					}

					table = Database.ExecuteFill(command);
				}
			}

			if (favorites_machine != null)
				Globals.Favorites.AddColumnSoftware(table, favorites_machine, softwarelist_name, "name", "favorite");

			return table;
		}

	}
}
