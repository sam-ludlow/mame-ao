using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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

		void Initialize(string directory, string version);
		int Get();
		void Xml();
		void SQLiteAo();
		void AllSHA1(HashSet<string> hashSet);

		void SQLite();
		void MSSql();
		void MSSqlHtml();
		void MSSqlPayload();

		DataRow GetMachine(string machine_name);
		DataRow[] GetMachineRoms(DataRow machine);
		DataRow[] GetMachineSoftwareLists(DataRow machine);
		DataRow GetSoftwareList(string softwarelist_name);
		HashSet<string> GetReferencedMachines(string machine_name);
		DataRow[] GetMachineDeviceRefs(string machine_name);
		DataRow GetSoftware(DataRow softwarelist, string software_name);
		DataRow[] GetSoftwareSharedFeats(DataRow software);
		DataRow[] GetSoftwareListsSoftware(DataRow softwarelist);
		DataRow[] GetSoftwareRoms(DataRow software);
	}
	public class Cores
	{
		public static void EnableCore(string name, string version)
		{
			if (Globals.Core != null && Globals.Core.Name == name)
				return;

			switch (name)
			{
				case "mame":
					Globals.Core = new CoreMame();
					break;

				case "hbmame":
					Globals.Core = new CoreHbMame();
					break;

				default:
					throw new ApplicationException($"Unknow core: {name}");
			}

			ICore core = Globals.Core;

			core.Initialize(Path.Combine(Globals.RootDirectory, name), version);

			core.Get();
			core.Xml();
			core.SQLiteAo();

			core.AllSHA1(Globals.AllSHA1);
		}

		public static void ExtractXML(string exeFilename)
		{
			string coreDirectory = Path.GetDirectoryName(exeFilename);

			string machineXmlFilename = Path.Combine(coreDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(coreDirectory, "_software.xml");

			if (File.Exists(machineXmlFilename) == false)
				Mame.ExtractXML(exeFilename, machineXmlFilename, "-listxml");

			if (File.Exists(softwareXmlFilename) == false)
			{
				Mame.ExtractXML(exeFilename, softwareXmlFilename, "-listsoftware");
				ReadXML.CombineHashSoftwareLists(softwareXmlFilename);
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
				XML2SQLite(machineXmlFilename, machineSqlLiteFilename, requiredMachineTables, assemblyVersion, aoExtra);
				GC.Collect();
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				XML2SQLite(softwareXmlFilename, softwareSqlLiteFilename, requiredSoftwareTables, assemblyVersion, aoExtra);
				GC.Collect();
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

			Database.DatabaseFromXML(document.Name.LocalName, connectionString, dataSet);
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

			DataRow machineRow = core.GetMachine(machine_name) ?? throw new ApplicationException($"Machine not found (GetReferencedMachines): ${machine_name}");
			if (machineRow.IsNull("cloneof") == false)
				requiredMachines.Add((string)machineRow["cloneof"]);

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

			string romof = machineRow.IsNull("romof") ? null : (string)machineRow["romof"];

			if (romof != null)
				GetReferencedMachines(core, romof, requiredMachines);

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

	}
}
