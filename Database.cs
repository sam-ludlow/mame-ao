using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;
using System.IO;

using System.Data.SQLite;
using System.Net;
using System.Runtime.CompilerServices;

namespace Spludlow.MameAO
{
	public class Database
	{
		public static string[][] DataQueryProfiles = new string[][] {
			new string[] { "Machines - not a clone, status good, without software",

				"SELECT machine.name, machine.description, machine.year, machine.manufacturer, machine.ao_softwarelist_count, machine.ao_rom_count, machine.ao_disk_count, driver.status, driver.emulation " +
				"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
				"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isbios = 'no') AND (machine.isdevice = 'no') AND (machine.ismechanical = 'no') AND (ao_softwarelist_count = 0)) " +
				"ORDER BY machine.description " +
				"LIMIT @LIMIT OFFSET @OFFSET",
			},

			new string[] { "Machines - not a clone, status good, with software",

				"SELECT machine.name, machine.description, machine.year, machine.manufacturer, machine.ao_softwarelist_count, machine.ao_rom_count, machine.ao_disk_count, driver.status, driver.emulation " +
				"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
				"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isbios = 'no') AND (machine.isdevice = 'no') AND (machine.ismechanical = 'no') AND (ao_softwarelist_count > 0)) " +
				"ORDER BY machine.description " +
				"LIMIT @LIMIT OFFSET @OFFSET",
			},
		};

		
		public static SQLiteConnection DatabaseFromXML(XElement document, string sqliteFilename, HashSet<string> keepTables)
		{
			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection connection = new SQLiteConnection(connectionString);


			// TODO check version in DB


			if (File.Exists(sqliteFilename) == true)
				return connection;

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			Console.Write($"Importing XML {document.Name.LocalName} ...");
			DataSet dataSet = ImportXML(document, keepTables);
			Console.WriteLine("...done.");

			Console.Write($"Adding extra data columns {document.Name.LocalName} ...");
			AddDataExtras(dataSet, document.Name.LocalName);
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
							dataType = columnDefinitions.Count == 0 ? "INTEGER PRIMARY KEY" : "INTEGER";

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
			}
			finally
			{
				connection.Close();
			}
			Console.WriteLine("...done.");

			return connection;
		}

		public static void AddDataExtras(DataSet dataSet, string name)
		{
			if (name == "mame")
			{
				DataTable machineTable = dataSet.Tables["machine"];
				DataTable romTable = dataSet.Tables["rom"];
				DataTable diskTable = dataSet.Tables["disk"];
				DataTable softwarelistTable = dataSet.Tables["softwarelist"];

				machineTable.Columns.Add("ao_rom_count", typeof(int));
				machineTable.Columns.Add("ao_disk_count", typeof(int));
				machineTable.Columns.Add("ao_softwarelist_count", typeof(int));

				foreach (DataRow machineRow in machineTable.Rows)
				{
					long machine_id = (long)machineRow["machine_id"];

					DataRow[] romRows = romTable.Select($"machine_id={machine_id}");
					DataRow[] diskRows = diskTable.Select($"machine_id={machine_id}");
					DataRow[] softwarelistRows = softwarelistTable.Select($"machine_id={machine_id}");

					machineRow["ao_rom_count"] = romRows.Length;
					machineRow["ao_disk_count"] = diskRows.Length;
					machineRow["ao_softwarelist_count"] = softwarelistRows.Length;

				}
			}
		}

		public static DataSet ImportXML(XElement document, HashSet<string> keepTables)
		{
			DataSet dataSet = new DataSet();

			ImportXMLWork(document, dataSet, null, keepTables);

			return dataSet;
		}
		public static void ImportXMLWork(XElement element, DataSet dataSet, DataRow parentRow, HashSet<string> keepTables)
		{
			string tableName = element.Name.LocalName;

			if (tableName == "condition")
				tableName = $"{parentRow.Table.TableName}_{tableName}";
			
			if (keepTables != null && keepTables.Contains(tableName) == false)
				return;

			string forignKeyName = null;
			if (parentRow != null)
				forignKeyName = parentRow.Table.TableName + "_id";

			DataTable table;

			if (dataSet.Tables.Contains(tableName) == false)
			{
				table = new DataTable(tableName);
				DataColumn pkColumn = table.Columns.Add(tableName + "_id", typeof(long));
				pkColumn.AutoIncrement = true;
				pkColumn.AutoIncrementSeed = 1;

				table.PrimaryKey = new DataColumn[] { pkColumn };

				if (parentRow != null)
					table.Columns.Add(forignKeyName, parentRow.Table.Columns[forignKeyName].DataType);

				dataSet.Tables.Add(table);
			}
			else
			{
				table = dataSet.Tables[tableName];
			}

			Dictionary<string, string> rowValues = new Dictionary<string, string>();

			foreach (XAttribute attribute in element.Attributes())
				rowValues.Add(attribute.Name.LocalName, attribute.Value);

			foreach (XElement childElement in element.Elements())
			{
				if (childElement.HasAttributes == false && childElement.HasElements == false)
					rowValues.Add(childElement.Name.LocalName, childElement.Value);
			}

			foreach (string columnName in rowValues.Keys)
			{
				if (table.Columns.Contains(columnName) == false)
					table.Columns.Add(columnName, typeof(string));
			}

			DataRow row = table.NewRow();

			if (parentRow != null)
				row[forignKeyName] = parentRow[forignKeyName];

			foreach (string columnName in rowValues.Keys)
				row[columnName] = rowValues[columnName];

			table.Rows.Add(row);

			foreach (XElement childElement in element.Elements())
			{
				if (childElement.HasAttributes == true || childElement.HasElements == true)
					ImportXMLWork(childElement, dataSet, row, keepTables);
			}
		}
	}
}
