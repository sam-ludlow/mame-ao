using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;
using System.IO;
using System.Linq;

using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public class Database
	{
		public static string[][] DataQueryProfiles = new string[][] {
			new string[] { "Machines - not a clone, status good, without software",

				"SELECT machine.name, machine.description, machine.year, machine.manufacturer, machine.ao_softwarelist_count, machine.ao_rom_count, machine.ao_disk_count, driver.status, driver.emulation " +
				"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
				"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isbios = 'no') AND (machine.isdevice = 'no') AND (machine.ismechanical = 'no') AND (ao_softwarelist_count = 0)) " +
				"ORDER BY machine.description COLLATE NOCASE ASC " +
				"LIMIT @LIMIT OFFSET @OFFSET",
			},

			new string[] { "Machines - not a clone, status good, with software",

				"SELECT machine.name, machine.description, machine.year, machine.manufacturer, machine.ao_softwarelist_count, machine.ao_rom_count, machine.ao_disk_count, driver.status, driver.emulation " +
				"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
				"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isbios = 'no') AND (machine.isdevice = 'no') AND (machine.ismechanical = 'no') AND (ao_softwarelist_count > 0)) " +
				"ORDER BY machine.description COLLATE NOCASE ASC " +
				"LIMIT @LIMIT OFFSET @OFFSET",
			},
		};

		public SQLiteConnection _MachineConnection;
		public SQLiteConnection _SoftwareConnection;

		private Dictionary<string, DataRow[]> _DevicesRefs;

		public Database(SQLiteConnection machineConnection, SQLiteConnection softwareConnection)
		{
			_MachineConnection = machineConnection;
			_SoftwareConnection = softwareConnection;


			// Cache device_ref to speed up machine dependancy resolution
			DataTable device_ref_Table = Database.ExecuteFill(_MachineConnection, "SELECT * FROM device_ref");

			_DevicesRefs = new Dictionary<string, DataRow[]>();

			DataTable machineTable = Database.ExecuteFill(_MachineConnection, "SELECT machine_id, name FROM machine");
			foreach (DataRow row in machineTable.Rows)
			{
				string machineName = (string)row["name"];

				DataRow[] rows = device_ref_Table.Select("machine_id = " + (long)row["machine_id"]);

				_DevicesRefs.Add(machineName, rows);
			}
		}

		public DataRow GetMachine(string name)
		{
			DataTable table = ExecuteFill(_MachineConnection, $"SELECT * FROM machine WHERE name = '{name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public DataRow[] GetMachineSoftwareLists(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = ExecuteFill(_MachineConnection, $"SELECT * FROM softwarelist WHERE machine_id = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetMachineFeatures(DataRow machine)
		{
			long machine_id = (long)machine["machine_id"];
			DataTable table = ExecuteFill(_MachineConnection, $"SELECT * FROM feature WHERE machine_id = {machine_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}
		public DataRow[] GetMachineDeviceRefs(string machineName)
		{
			return _DevicesRefs[machineName];
		}


		public DataRow GetSoftwareList(string name)
		{
			DataTable table = ExecuteFill(_SoftwareConnection, $"SELECT * FROM softwarelist WHERE name = '{name}'");
			if (table.Rows.Count == 0)
				return null;
			return table.Rows[0];
		}

		public DataRow[] GetSoftwareListsSoftware(DataRow softwarelist)
		{
			long softwarelist_id = (long)softwarelist["softwarelist_id"];
			DataTable table = ExecuteFill(_SoftwareConnection, $"SELECT * FROM software WHERE softwarelist_id = {softwarelist_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetSoftwareRoms(DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = ExecuteFill(_SoftwareConnection,
				"SELECT rom.* FROM (part INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				$"WHERE part.software_id = {software_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public DataRow[] GetSoftwareDisks(DataRow software)
		{
			long software_id = (long)software["software_id"];
			DataTable table = ExecuteFill(_SoftwareConnection,
				"SELECT disk.* FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				$"WHERE part.software_id = {software_id}");
			return table.Rows.Cast<DataRow>().ToArray();
		}

		public static SQLiteConnection DatabaseFromXML(string xmlFilename, string sqliteFilename, string assemblyVersion)
		{
			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection connection = new SQLiteConnection(connectionString);

			if (File.Exists(sqliteFilename) == true)
			{
				if (TableExists(connection, "ao_info") == true)
				{
					DataTable aoInfoTable = ExecuteFill(connection, "SELECT * FROM ao_info");
					if (aoInfoTable.Rows.Count == 1)
					{
						string dbAssemblyVersion = (string)aoInfoTable.Rows[0]["assembly_version"];
						if (dbAssemblyVersion == assemblyVersion)
							return connection;
					}
				}
				Console.WriteLine("Existing SQLite database is old version re-creating: " + sqliteFilename);
			}

			if (File.Exists(sqliteFilename) == true)
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

			Console.Write($"Creating SQLite {document.Name.LocalName} ...");
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

				if (document.Name.LocalName == "mame")
				{
					foreach (string commandText in new string[] {
						"CREATE INDEX machine_name_index ON machine(name);"
						})
						using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
							command.ExecuteNonQuery();
				}

				if (document.Name.LocalName == "softwarelists")
				{

				}

			}
			finally
			{
				connection.Close();
			}
			Console.WriteLine("...done.");

			return connection;
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

				machineTable.Columns.Add("ao_rom_count", typeof(int));
				machineTable.Columns.Add("ao_disk_count", typeof(int));
				machineTable.Columns.Add("ao_softwarelist_count", typeof(int));
				machineTable.Columns.Add("ao_driver_status", typeof(string));

				foreach (DataRow machineRow in machineTable.Rows)
				{
					long machine_id = (long)machineRow["machine_id"];

					DataRow[] romRows = romTable.Select($"machine_id={machine_id}");
					DataRow[] diskRows = diskTable.Select($"machine_id={machine_id}");
					DataRow[] softwarelistRows = softwarelistTable.Select($"machine_id={machine_id}");
					DataRow[] driverRows = driverTable.Select($"machine_id={machine_id}");

					machineRow["ao_rom_count"] = romRows.Length;
					machineRow["ao_disk_count"] = diskRows.Length;
					machineRow["ao_softwarelist_count"] = softwarelistRows.Length;
					if (driverRows.Length == 1)
						machineRow["ao_driver_status"] = (string)driverRows[0]["status"];

				}
			}

			if (name == "softwarelists")
			{

			}
		}



		public static bool TableExists(SQLiteConnection connection, string tableName)
		{
			object obj = ExecuteScalar(connection, $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'");

			if (obj == null || obj is DBNull)
				return false;

			return true;
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

		public static DataTable ExecuteFill(SQLiteConnection connection, string commandText)
		{
			DataSet dataSet = new DataSet();
			using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(commandText, connection))
				adapter.Fill(dataSet);
			return dataSet.Tables[0];
		}
	}
}
