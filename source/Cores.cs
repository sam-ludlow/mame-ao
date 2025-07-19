using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public interface ICore
	{
		string Name { get; }
		string Version { get; }
		string Directory { get; }

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

		HashSet<string> GetReferencedMachines(string machine_name);
		DataRow[] GetMachineDeviceRefs(string machine_name);
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

		public static void MakeSQLite(string coreDirectory, HashSet<string> requiredMachineTables, HashSet<string> requiredSoftwareTables, bool overwrite, string assemblyVersion)
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
				XML2SQLite(machineXmlFilename, machineSqlLiteFilename, requiredMachineTables, assemblyVersion);
				GC.Collect();
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				XML2SQLite(softwareXmlFilename, softwareSqlLiteFilename, requiredSoftwareTables, assemblyVersion);
				GC.Collect();
			}

		}

		private static void XML2SQLite(string xmlFilename, string sqliteFilename, HashSet<string> tableNames, string assemblyVersion)
		{
			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, tableNames);

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			Database.DatabaseFromXML(document.Name.LocalName, connectionString, dataSet);

			if (assemblyVersion != null)
			{
				using (SQLiteConnection connection = new SQLiteConnection(connectionString))
				{
					if (Database.TableExists(connection, "ao_info") == false)
					{
						Database.ExecuteNonQuery(connection, "CREATE TABLE [ao_info] ([ao_info_id] INTEGER PRIMARY KEY, [assembly_version] TEXT NOT NULL)");
						Database.ExecuteNonQuery(connection, $"INSERT INTO [ao_info] ([ao_info_id], [assembly_version]) VALUES (1, '{Globals.AssemblyVersion}')");
					}
				}
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

		public static void AddExtraAoData(string connectionStringMachine)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionStringMachine))
			{
				connection.Open();
				SQLiteTransaction transaction = null;
				try
				{
					transaction = connection.BeginTransaction();

					string[] columnNames = new string[] { "ao_rom_count", "ao_disk_count", "ao_softwarelist_count", "ao_input_coins" };
					foreach (string columnName in columnNames)
					{
						Database.ExecuteNonQuery(connection, $"ALTER TABLE [machine] ADD COLUMN [{columnName}] INTEGER NULL");
						Database.ExecuteNonQuery(connection, $"UPDATE [machine] SET [{columnName}] = 0");
					}

					Database.ExecuteNonQuery(connection, "ALTER TABLE [machine] ADD COLUMN [ao_driver_status] TEXT NULL");




					transaction.Commit();
				}
				catch
				{
					transaction?.Rollback();
					throw;
				}
				finally
				{
					connection.Close();
				}
			}
		}


		// TODO Finish !!!!!!!!1
		public static void AddDataExtras(DataSet dataSet, string name, string assemblyVersion)	// xml ?
		{


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

	}
}
