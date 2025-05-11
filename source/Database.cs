using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public class Database
	{
		public class DataQueryProfile
		{
			public string Key;
			public string Text;
			public string Decription;
			public string CommandText;
		}

		public static DataQueryProfile[] DataQueryProfiles = new DataQueryProfile[] {
			new DataQueryProfile(){
				Key = "arcade-good",
				Text = "Arcade Good",
				Decription = "Arcade Machines - Status Good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "arcade-imperfect",
				Text = "Arcade Imperfect",
				Decription = "Arcade Machines - Status Imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "computer-console-good",
				Text = "Computers & Consoles Good",
				Decription = "Computers & Consoles with software - status good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "computer-console-imperfect",
				Text = "Computers & Consoles Imperfect",
				Decription = "Computers & Consoles with software - status imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "other-good",
				Text = "Other Good",
				Decription = "Other Systems without software - status good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count = 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "other-imperfect",
				Text = "Other Imperfect",
				Decription = "Other Systems without software - status imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count = 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "everything",
				Text = "Everything",
				Decription = "Absolutely Everything",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.runnable = 'yes') AND (machine.isdevice = 'no') @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "favorites",
				Text = "Favorites",
				Decription = "Favorites",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.runnable = 'yes') AND (machine.isdevice = 'no') @FAVORITES @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
		};

		public string _MachineConnectionString;
		public string _SoftwareConnectionString;

		private Dictionary<string, DataRow[]> _DevicesRefs;
		private DataTable _SoftwarelistTable;

		public HashSet<string> _AllSHA1s;

		public Database()
		{
		}

		public void InitializeMachine(string xmlFilename, string databaseFilename, string assemblyVersion)
		{
			_MachineConnectionString = Database.DatabaseFromXML(xmlFilename, databaseFilename, assemblyVersion);

			Console.Write("Creating machine performance caches...");

			// Cache device_ref to speed up machine dependancy resolution
			DataTable device_ref_Table = Database.ExecuteFill(_MachineConnectionString, "SELECT * FROM device_ref");

			_DevicesRefs = new Dictionary<string, DataRow[]>();

			DataTable machineTable = Database.ExecuteFill(_MachineConnectionString, "SELECT machine_id, name FROM machine");
			foreach (DataRow row in machineTable.Rows)
			{
				string machineName = (string)row["name"];

				DataRow[] rows = device_ref_Table.Select("machine_id = " + (long)row["machine_id"]);

				_DevicesRefs.Add(machineName, rows);
			}
			
			Console.WriteLine("...done");
		}

		public void InitializeSoftware(string xmlFilename, string databaseFilename, string assemblyVersion)
		{
			_SoftwareConnectionString = Database.DatabaseFromXML(xmlFilename, databaseFilename, assemblyVersion);

			Console.Write("Creating software performance caches...");

			// Cache softwarelists for description
			_SoftwarelistTable = ExecuteFill(_SoftwareConnectionString, "SELECT name, description FROM softwarelist");
			_SoftwarelistTable.PrimaryKey = new DataColumn[] { _SoftwarelistTable.Columns["name"] };

			Console.WriteLine("...done");

			Console.Write("Getting all database SHA1s...");
			_AllSHA1s = GetAllSHA1s();
			Console.WriteLine("...done.");
		}

		public static void AddDataExtras(DataSet dataSet, string name, string assemblyVersion)
		{
			DataTable table = new DataTable("ao_info");
			table.Columns.Add("ao_info_id", typeof(long));
			table.Columns.Add("assembly_version", typeof(string));
			table.Rows.Add(1L, assemblyVersion);
			dataSet.Tables.Add(table);

			if (name == "mame")
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
					DataRow[] diskRows = diskTable.Select($"machine_id={machine_id}");
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

			if (name == "softwarelists")
			{

			}
		}

		public DataRow GetMachine(string name)
		{
			DataTable table = ExecuteFill(_MachineConnectionString, $"SELECT * FROM machine WHERE name = '{name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public DataRow[] GetMachineRoms(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = Database.ExecuteFill(_MachineConnectionString, $"SELECT * FROM rom WHERE machine_id = {machine_id} AND [name] IS NOT NULL AND [sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetMachineDisks(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = Database.ExecuteFill(_MachineConnectionString, $"SELECT * FROM disk WHERE machine_id = {machine_id} AND [name] IS NOT NULL AND [sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetMachineSoftwareLists(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			string machineName = (string)machine["name"];
			DataTable table = ExecuteFill(_MachineConnectionString, $"SELECT * FROM softwarelist WHERE machine_id = {machine_id}");

			table.Columns.Add("description", typeof(string));

			foreach (DataRow row in table.Rows)
			{
				string listName = (string)row["name"];
				DataRow softwarelistRow = _SoftwarelistTable.Rows.Find(listName);

				if (softwarelistRow != null)
					row["description"] = (string)softwarelistRow["description"];
				else
					row["description"] = $"MAME DATA ERROR machine '{machineName}' software list '{listName}' does not exist";
			}

			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetMachineFeatures(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = ExecuteFill(_MachineConnectionString, $"SELECT * FROM feature WHERE machine_id = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}
		public DataRow[] GetMachineDeviceRefs(string machineName)
		{
			return _DevicesRefs[machineName];
		}

		public DataRow[] GetMachineSamples(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = ExecuteFill(_MachineConnectionString, $"SELECT * FROM sample WHERE machine_id = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow GetMachineDriver(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = ExecuteFill(_MachineConnectionString, $"SELECT * FROM driver WHERE machine_id = {machine_id}");

			if (table.Rows.Count == 0)
				return null;

			return table.Rows[0];
		}

		public DataRow[] GetSoftwareListsSoftware(DataRow softwarelist)
		{
			long softwarelist_id = (long)softwarelist["softwarelist_id"];
			DataTable table = ExecuteFill(_SoftwareConnectionString, $"SELECT * FROM software WHERE softwarelist_id = {softwarelist_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow GetSoftware(DataRow softwarelist, string software_name)
		{
			long softwarelist_id = (long)softwarelist["softwarelist_id"];

			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(_SoftwareConnectionString))
			{
				string commandText = @"SELECT * FROM software WHERE softwarelist_id = @softwarelist_id AND software.name = @software_name";
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_id", softwarelist_id);
					command.Parameters.AddWithValue("@software_name", software_name);
					table = ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
				return null;

			return table.Rows[0];
		}

		public DataRow GetSoftware(string softwarelist_name, string software_name)
		{
			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(_SoftwareConnectionString))
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
					table = ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
				return null;

			return table.Rows[0];
		}

		public DataRow[] GetSoftwareListsSoftware(string softwareListName, int offset, int limit, string search, string favorites_machine)
		{
			string commandText = "SELECT software.*, softwarelist.name AS softwarelist_name, COUNT() OVER() AS ao_total FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_Id = software.softwarelist_Id " +
				$"WHERE (softwarelist.name = '{softwareListName}' @SEARCH) ORDER BY software.description COLLATE NOCASE ASC " +
				"LIMIT @LIMIT OFFSET @OFFSET";

			if (softwareListName == "@fav")
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

			using (SQLiteConnection connection = new SQLiteConnection(_SoftwareConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					if (search != null)
					{
						command.Parameters.AddWithValue("@name", search);
						command.Parameters.AddWithValue("@description", search);
					}

					table = ExecuteFill(command);
				}
			}

			if (favorites_machine != null)
				Globals.Favorites.AddColumnSoftware(table, favorites_machine, softwareListName, "name", "favorite");

			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow GetSoftwareList(string name)
		{
			DataTable table = ExecuteFill(_SoftwareConnectionString, $"SELECT * FROM softwarelist WHERE name = '{name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public DataRow[] GetSoftwareRoms(DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = ExecuteFill(_SoftwareConnectionString,
				"SELECT rom.* FROM (part INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				$"WHERE part.software_id = {software_id} AND rom.[name] IS NOT NULL AND rom.[sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetSoftwareDisks(DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = ExecuteFill(_SoftwareConnectionString,
				"SELECT disk.* FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				$"WHERE part.software_id = {software_id} AND disk.[name] IS NOT NULL AND disk.[sha1] IS NOT NULL");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetSoftwareSharedFeats(DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = ExecuteFill(_SoftwareConnectionString, $"SELECT * FROM sharedfeat WHERE software_id = {software_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetSoftwareParts(string softwarelist_name, string software_name)
		{
			string commandText = @"
				SELECT part.*
				FROM (softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id
				WHERE (softwarelist.name = @softwarelist_name AND software.name = @software_name)";

			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(_SoftwareConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_name", softwarelist_name);
					command.Parameters.AddWithValue("@software_name", software_name);

					table = ExecuteFill(command);
				}
			}

			return table.Rows.Cast<DataRow>().ToArray();
		}

		public string GetRequiredMedia(string machine_name, string softwarelist_name, string software_name)
		{
			DataRow softwareListRow = GetSoftwareList(softwarelist_name) ?? throw new ApplicationException($"GetRequiredMedia Software List not found: {softwarelist_name}");
			DataRow softwareRow = GetSoftware(softwareListRow, software_name) ?? throw new ApplicationException($"GetRequiredMedia Software not found: {software_name}");

			List<string> results = new List<string>();

			foreach (DataRow sharedFeatRow in GetSoftwareSharedFeats(softwareRow))
			{
				if ((string)sharedFeatRow["name"] != "requirement")
					continue;

				string[] valueParts = ((string)sharedFeatRow["value"]).Split(':');

				string require_softwarelist_name = null;
				string require_software_name = valueParts[valueParts.Length - 1];
				if (valueParts.Length > 1)
				{
					require_softwarelist_name = valueParts[0];

					if (GetSoftware(require_softwarelist_name, require_software_name) == null)
						require_softwarelist_name = null;
				}

				if (require_softwarelist_name == null && GetSoftware(softwarelist_name, require_software_name) != null)
					require_softwarelist_name = softwarelist_name;

				if (require_softwarelist_name == null)
				{
					DataRow machineRow = GetMachine(machine_name);
					foreach (DataRow machineSoftwareListRow in GetMachineSoftwareLists(machineRow))
					{
						string machineSoftwareList = (string)machineSoftwareListRow["name"];

						if (GetSoftware(machineSoftwareList, require_software_name) != null)
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

				results.Add(GetSoftwareMedia(machine_name, require_softwarelist_name, require_software_name));
			}

			results.Add(GetSoftwareMedia(machine_name, softwarelist_name, software_name));

			return String.Join(" ", results);
		}

		public string GetSoftwareMedia(string machine_name, string softwarelist_name, string software_name)
		{
			string commandText = @"
				SELECT [part].[name], [part].[interface]
				FROM ([softwarelist] INNER JOIN [software] ON [softwarelist].softwarelist_id = [software].softwarelist_id) INNER JOIN [part] ON [software].software_id = [part].software_id
				WHERE (([softwarelist].[name] = @softwarelist_name) AND ([software].[name] = @software_name) AND ([part].[interface] IS NOT NULL))
				ORDER BY [part].[name]
			";

			DataTable table;
			using (SQLiteConnection connection = new SQLiteConnection(_SoftwareConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@softwarelist_name", softwarelist_name);
					command.Parameters.AddWithValue("@software_name", software_name);
					table = ExecuteFill(command);
				}
			}

			if (table.Rows.Count == 0)
			{
				Console.WriteLine($"!!! Can't find Software Media Interfaces, list:{softwarelist_name}, soft:{software_name}");
				return software_name;
			}

			var softwareMediaInterfaces = table.Rows.Cast<DataRow>().Select(r => (string)r["interface"]);

			using (SQLiteConnection connection = new SQLiteConnection(_MachineConnectionString))
			{
				commandText = @"
					SELECT device.*, instance.* FROM (machine INNER JOIN device ON machine.machine_id = device.machine_id) INNER JOIN instance ON device.device_id = instance.device_id
					WHERE (machine.name = @machine_name AND [device].[interface] IS NOT NULL)
					ORDER BY device.type, device.tag, instance.name
				";
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@machine_name", machine_name);
					table = ExecuteFill(command);
				}
			}

			Dictionary<string, List<string>> machineInterfaceBriefnames = new Dictionary<string, List<string>>();

			foreach (DataRow row in table.Rows)
			{
				string device_interface = (string)row["interface"];
				string instance_briefname = (string)row["briefname"];

				if (machineInterfaceBriefnames.ContainsKey(device_interface) == false)
					machineInterfaceBriefnames.Add(device_interface, new List<string>());

				machineInterfaceBriefnames[device_interface].Add(instance_briefname);
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

		public DataQueryProfile GetDataQueryProfile(string key)
		{
			DataQueryProfile found = null;

			if (key.StartsWith("genre") == true)
			{
				long genre_id = Int64.Parse(key.Split(new char[] { '-' })[1]);

				found = new DataQueryProfile() {
					Key = key,
					Text = "genre",
					Decription = "genre",
					CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((genre_id = @genre_id) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
				};

				found.CommandText = found.CommandText.Replace("@genre_id", genre_id.ToString());
			}
			else
			{
				foreach (DataQueryProfile profile in Database.DataQueryProfiles)
				{
					if (profile.Key == key)
					{
						found = profile;
						break;
					}
				}
			}

			if (found == null)
				throw new ApplicationException($"Data Profile not found {key}");

			return found;
		}

		public DataTable QueryMachine(string key, int offset, int limit, string search)
		{
			DataQueryProfile profile = GetDataQueryProfile(key);

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

			using (var connection = new SQLiteConnection(_MachineConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
				{
					if (search != null)
					{
						command.Parameters.AddWithValue("@name", search);
						command.Parameters.AddWithValue("@description", search);
					}

					table = ExecuteFill(command);
				}
			}

			Globals.Favorites.AddColumnMachines(table, "name", "favorite");

			return table;
		}

		public HashSet<string> GetAllSHA1s()
		{
			HashSet<string> result = new HashSet<string>();

			foreach (string connectionString in new string[] { _MachineConnectionString, _SoftwareConnectionString })
			{
				using (SQLiteConnection connection = new SQLiteConnection(connectionString))
				{
					foreach (string tableName in new string[] { "rom", "disk" })
					{
						DataTable table = ExecuteFill(connection, $"SELECT [sha1] FROM [{tableName}] WHERE [sha1] IS NOT NULL");
						foreach (DataRow row in table.Rows)
							result.Add((string)row["sha1"]);
					}
				}
			}
			
			return result;
		}

		public static string DatabaseFromXML(string xmlFilename, string sqliteFilename, string assemblyVersion)
		{
			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			if (File.Exists(sqliteFilename) == true)
			{
				using (SQLiteConnection connection = new SQLiteConnection(connectionString))
				{
					if (TableExists(connection, "ao_info") == true)
					{
						DataTable aoInfoTable = ExecuteFill(connection, "SELECT * FROM ao_info");
						if (aoInfoTable.Rows.Count == 1)
						{
							string dbAssemblyVersion = (string)aoInfoTable.Rows[0]["assembly_version"];
							if (dbAssemblyVersion == assemblyVersion)
								return connectionString;
						}
					}
					Console.WriteLine("Existing SQLite database is old version re-creating: " + sqliteFilename);
				}
			}

			File.Delete(sqliteFilename);
			File.WriteAllBytes(sqliteFilename, new byte[0]);

			Console.Write($"Loading XML {xmlFilename} ...");
			XElement document = XElement.Load(xmlFilename);
			Console.WriteLine("...done.");

			Console.Write($"Importing XML {document.Name.LocalName} ...");
			DataSet dataSet = ReadXML.ImportXML(document);
			Console.WriteLine("...done.");

			Console.Write($"Adding extra data columns {document.Name.LocalName} ...");
			AddDataExtras(dataSet, document.Name.LocalName, assemblyVersion);
			Console.WriteLine("...done.");

			DatabaseFromXML(document.Name.LocalName, connectionString, dataSet);

			return connectionString;
		}
		public static void DatabaseFromXML(string name, string connectionString, DataSet dataSet)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				Console.Write($"Creating SQLite {name} ...");
				connection.Open();
				try
				{
					foreach (DataTable table in dataSet.Tables)
					{
						List<string> columnDefinitions = new List<string>();

						foreach (DataColumn column in table.Columns)
						{
							string dataType = "TEXT";
							if (column.ColumnName.EndsWith("_id") == true)
							{
								dataType = columnDefinitions.Count == 0 ? "INTEGER PRIMARY KEY" : "INTEGER";
							}
							else
							{
								if (column.DataType == typeof(int) || column.DataType == typeof(long))
									dataType = "INTEGER";
							}

							if (table.TableName == "machine" && column.ColumnName == "description")
								dataType += " COLLATE NOCASE";

							columnDefinitions.Add($"\"{column.ColumnName}\" {dataType}");
						}

						string tableDefinition = $"CREATE TABLE {table.TableName}({String.Join(",", columnDefinitions.ToArray())});";

						using (SQLiteCommand command = new SQLiteCommand(tableDefinition, connection))
						{
							command.ExecuteNonQuery();
						}
					}

					foreach (DataTable table in dataSet.Tables)
					{
						Console.Write($"{table.TableName}...");

						List<string> columnNames = new List<string>();
						List<string> parameterNames = new List<string>();
						foreach (DataColumn column in table.Columns)
						{
							columnNames.Add($"\"{column.ColumnName}\"");
							parameterNames.Add("@" + column.ColumnName);
						}

						string commandText = $"INSERT INTO {table.TableName}({String.Join(",", columnNames.ToArray())}) VALUES({String.Join(",", parameterNames.ToArray())});";

						SQLiteTransaction transaction = connection.BeginTransaction();
						try
						{
							foreach (DataRow row in table.Rows)
							{
								using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
								{
									foreach (DataColumn column in table.Columns)
										command.Parameters.AddWithValue("@" + column.ColumnName, row[column]);

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
					}

					if (name == "mame")
					{
						foreach (string commandText in new string[] {
						"CREATE INDEX machine_name_index ON machine(name);"
						})
							using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
								command.ExecuteNonQuery();
					}

					if (name == "softwarelists")
					{

					}
				}
				finally
				{
					connection.Close();
				}
				Console.WriteLine("...done.");
			}
		}

		public static bool TableExists(SQLiteConnection connection, string tableName)
		{
			object obj = ExecuteScalar(connection, $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'");

			if (obj == null || obj is DBNull)
				return false;

			return true;
		}
		public static string[] TableList(SQLiteConnection connection)
		{
			List<string> tableNames = new List<string>();

			DataTable table = Database.ExecuteFill(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
			foreach (DataRow row in table.Rows)
			{
				string tableName = (string)row[0];

				if (tableName.StartsWith("sqlite_") == false)
					tableNames.Add(tableName);
			}
			return tableNames.ToArray();
		}
		public static object ExecuteScalar(SQLiteConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
					return command.ExecuteScalar();
			}
			finally
			{
				connection.Close();
			}
		}

		public static int ExecuteNonQuery(string connectionString, string commandText)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				connection.Open();
				try
				{
					using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
						return command.ExecuteNonQuery();
				}
				finally
				{
					connection.Close();
				}
			}
		}
		public static int ExecuteNonQuery(SQLiteConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
					return command.ExecuteNonQuery();
			}
			finally
			{
				connection.Close();
			}
		}
		public static DataTable ExecuteFill(string connectionString, string commandText)
		{
			var table = new DataTable();
			using (var connection = new SQLiteConnection(connectionString))
				using (var adapter = new SQLiteDataAdapter(commandText, connection))
					adapter.Fill(table);
			return table;
		}
		public static DataTable ExecuteFill(SQLiteConnection connection, string commandText)
		{
			var table = new DataTable();
			using (var adapter = new SQLiteDataAdapter(commandText, connection))
				adapter.Fill(table);
			return table;
		}
		public static DataTable ExecuteFill(SQLiteCommand command)
		{
			var table = new DataTable();
			using (var adapter = new SQLiteDataAdapter(command))
				adapter.Fill(table);
			return table;
		}

		//
		// MS SQL
		//

		public static bool DatabaseExists(SqlConnection connection, string databaseName)
		{
			object obj = ExecuteScalar(connection, $"SELECT name FROM sys.databases WHERE name = '{databaseName}'");

			if (obj == null || obj is DBNull)
				return false;

			return true;
		}

		public static object ExecuteScalar(SqlConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SqlCommand command = new SqlCommand(commandText, connection))
					return command.ExecuteScalar();
			}
			finally
			{
				connection.Close();
			}

		}

		public static int ExecuteNonQuery(SqlConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SqlCommand command = new SqlCommand(commandText, connection))
				{
					command.CommandTimeout = 15 * 60;
					return command.ExecuteNonQuery();
				}
			}
			finally
			{
				connection.Close();
			}
		}
		public static DataTable ExecuteFill(SqlConnection connection, string commandText)
		{
			DataTable table = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connection))
				adapter.Fill(table);
			return table;
		}

		public static void BulkInsert(SqlConnection connection, DataTable table)
		{
			using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(connection))
			{
				sqlBulkCopy.DestinationTableName = table.TableName;

				sqlBulkCopy.BulkCopyTimeout = 15 * 60;

				connection.Open();
				try
				{
					sqlBulkCopy.WriteToServer(table);
				}
				finally
				{
					connection.Close();
				}
			}
		}

		public static string[] TableList(SqlConnection connection)
		{
			List<string> result = new List<string>();

			DataTable table = ExecuteFill(connection,
				"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");

			foreach (DataRow row in table.Rows)
				result.Add((string)row["TABLE_NAME"]);

			return result.ToArray();
		}

		public static void ConsoleQuery(string database, string commandText)
		{
			using (SQLiteConnection connection = new SQLiteConnection(database == "m" ? Globals.Database._MachineConnectionString : Globals.Database._SoftwareConnectionString))
			{
				try
				{
					DataTable table = ExecuteFill(connection, commandText);

					StringBuilder text = new StringBuilder();

					foreach (DataColumn column in table.Columns)
					{
						if (column.Ordinal > 0)
							text.Append('\t');
						text.Append(column.ColumnName);
					}
					Console.WriteLine(text.ToString());

					text.Length = 0;
					foreach (DataColumn column in table.Columns)
					{
						if (column.Ordinal > 0)
							text.Append('\t');
						text.Append(new String('=', column.ColumnName.Length));
					}
					Console.WriteLine(text.ToString());

					foreach (DataRow row in table.Rows)
					{
						text.Length = 0;
						foreach (DataColumn column in table.Columns)
						{
							if (column.Ordinal > 0)
								text.Append('\t');
							if (row.IsNull(column) == false)
								text.Append(Convert.ToString(row[column]));
						}
						Console.WriteLine(text.ToString());
					}
				}
				catch (SQLiteException e)
				{
					Console.WriteLine(e.Message);
				}

				Console.WriteLine();
			}
		}

	}
}
