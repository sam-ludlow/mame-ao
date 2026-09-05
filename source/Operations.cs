using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Spludlow.MameAO
{
	public class Operations
	{
		public static int ProcessOperation(Dictionary<string, string> parameters)
		{
			int exitCode = 0;

			DateTime timeStart = DateTime.Now;

			string operation = parameters["operation"];

			int index = operation.IndexOf("_");
			if (index == -1)
			{
				switch (operation)
				{
					case "snap-machine":
						ValidateRequiredParameters(parameters, new string[] { "source", "target" });
						Snap.ImportSnapMachine(parameters["source"], parameters["target"]);
						break;

					case "snap-software":
						ValidateRequiredParameters(parameters, new string[] { "source", "target" });
						Snap.ImportSnapSoftware(parameters["source"], parameters["target"]);
						break;

					case "snap-index":
						Snap.IndexSnapDirectory(Path.Combine(parameters["directory"]));
						break;

					case "process-phone-home":
						ValidateRequiredParameters(parameters, new string[] { "database", "server", "names" });
						PhoneHome.ProcessPhoneHome(parameters["directory"], parameters["database"], parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					case "approve-phone-home":
						ValidateRequiredParameters(parameters, new string[] { "database" });
						PhoneHome.ApprovePhoneHome(parameters["directory"], parameters["database"]);
						break;

					case "update-pugsys-cheats":
						ValidateRequiredParameters(parameters, new string[] { "server", "names" });
						exitCode = Cheats.UpdateFromPugsy(parameters["directory"], parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					default:
						throw new ApplicationException($"Bad operation: {operation}");
				}
			}
			else
			{
				string coreName = operation.Substring(0, index);
				operation = operation.Substring(index + 1);

				ICore core;
				switch (coreName)
				{
					case "mame":
						core = new CoreMame();
						break;

					case "hbmame":
						core = new CoreHbMame();
						break;

					case "fbneo":
						core = new CoreFbNeo();
						break;

					case "tosec":
						core = new CoreTosec();
						break;

					case "redump":
						core = new CoreRedump();
						break;

					case "no-intro":
						core = new CoreNoIntro();
						break;

					case "pinball-visual":
						core = new CorePinballVisual();
						break;

					default:
						throw new ApplicationException($"Bad core: {coreName}");
				}

				core.Initialize(parameters["directory"], parameters["version"]);

				switch (operation)
				{
					case "get":
						exitCode = core.Get();
						break;

					case "xml":
						core.Xml();
						break;

					case "json":
						core.Json();
						break;

					case "sqlite":
						core.SQLite();
						break;

					case "msaccess":
						core.MsAccess();
						break;

					case "zips":
						core.Zips();
						break;

					case "mssql":
						ValidateRequiredParameters(parameters, new string[] { "server", "names" });
						core.MSSql(parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					case "mssql-payload":
						ValidateRequiredParameters(parameters, new string[] { "server", "names" });
						core.MSSqlPayload(parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					default:
						throw new ApplicationException($"Bad operation: {operation}");
				}
			}

			TimeSpan timeTook = DateTime.Now - timeStart;

			Console.WriteLine($"Operation '{parameters["operation"]}' took: {Math.Round(timeTook.TotalSeconds, 0)} seconds");

			return exitCode;
		}

		private static void ValidateRequiredParameters(Dictionary<string, string> parameters, string[] required)
		{
			List<string> missing = new List<string>();

			foreach (string name in required)
				if (parameters.ContainsKey(name) == false)
					missing.Add(name);

			if (missing.Count > 0)
				throw new ApplicationException($"This operation requires these parameters '{String.Join(", ", missing)}'.");

		}

		public static DataSet SourceData(SqlConnection connection, Dictionary<string, string> tableNameColumnNameOrders)
		{
			DataSet dataSet = new DataSet();

			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName.StartsWith("_") == true || tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				string commandText = $"SELECT * FROM [{tableName}]";
				if (tableNameColumnNameOrders.ContainsKey(tableName) == true)
					commandText += $" ORDER BY [{tableNameColumnNameOrders[tableName]}]";

				var table = new DataTable(tableName);
				using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connection))
					adapter.Fill(table);

				dataSet.Tables.Add(table);
			}

			return dataSet;
		}

		public static Dictionary<string, Dictionary<long, List<DataRow>>> PerformanceDictionaries(DataSet dataSet)
		{
			var result = new Dictionary<string, Dictionary<long, List<DataRow>>>();

			Console.Write("Make Performance Dictionaries...");
			foreach (DataTable table in dataSet.Tables)
			{
				if (table.Columns.Count == 1)
					continue;

				var column = table.Columns[1];
				if (column.ColumnName.EndsWith("_id") == false || column.DataType != typeof(long))
					continue;

				var lookup = new Dictionary<long, List<DataRow>>();

				foreach (DataRow row in table.Rows)
				{
					long id = (long)row[column.ColumnName];
					if (lookup.ContainsKey(id) == false)
						lookup.Add(id, new List<DataRow>());
					lookup[id].Add(row);
				}

				string parentTableName = column.ColumnName.Substring(0, column.ColumnName.Length - 3);

				foreach (DataRow row in dataSet.Tables[parentTableName].Rows)
				{
					long id = (long)row[0];
					if (lookup.ContainsKey(id) == false)
						lookup.Add(id, new List<DataRow>());
				}

				result.Add(table.TableName, lookup);
			}
			Console.WriteLine("...done");

			return result;
		}

		public static void CreateMetaDataTable(SqlConnection connection, string coreName, string version, string info)
		{
			string agent = $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string tableName = "_metadata";

			Database.ExecuteNonQuery(connection, $"DROP TABLE IF EXISTS [{tableName}];");

			string[] columnDefs = new string[] {
				$"[{tableName}_id] BIGINT NOT NULL PRIMARY KEY",
				"[dataset] NVARCHAR(1024) NOT NULL",
				"[subset] NVARCHAR(1024) NOT NULL",
				"[version] NVARCHAR(1024) NOT NULL",
				"[info] NVARCHAR(1024) NOT NULL",
				"[processed] DATETIME NOT NULL",
				"[agent] NVARCHAR(1024) NOT NULL",
			};
			string commandText = $"CREATE TABLE [{tableName}] ({String.Join(", ", columnDefs)});";

			Console.WriteLine(commandText);
			Database.ExecuteNonQuery(connection, commandText);

			DataTable table = Database.ExecuteFill(connection, $"SELECT * FROM [{tableName}] WHERE (0 = 1)");
			table.TableName = tableName;

			table.Rows.Add(1L, coreName, "", version, info, DateTime.Now, agent);

			Database.BulkInsert(connection, table);
		}

		public static DataTable MakePayloadDataTable(string tableName, string[] keyNames)
		{
			string[] columnNames = new string[] { "title", "xml", "json", "html" };

			DataTable table = new DataTable(tableName);

			List<DataColumn> pks = new List<DataColumn>();
			foreach (string keyName in keyNames)
				pks.Add(table.Columns.Add(keyName, typeof(string)));

			table.PrimaryKey = pks.ToArray();

			foreach (string columnName in columnNames)
				table.Columns.Add(columnName, typeof(string));

			return table;
		}

		public static void MakeMSSQLPayloadsInsert(SqlConnection connection, DataTable table)
		{
			List<string> columnDefs = new List<string>();
			List<string> pkNames = new List<string>();

			foreach (DataColumn column in table.PrimaryKey)
			{
				int max = 1;
				foreach (DataRow row in table.Rows)
				{
					if (row.IsNull(column) == false)
					{
						int len = ((string)row[column]).Length;
						if (len > max)
							max = len;
					}
				}
				column.MaxLength = max;

				string pkDataType = "VARCHAR";
				if (table.TableName == "game_payload" && column.ColumnName == "game_name")
					pkDataType = "NVARCHAR";

				columnDefs.Add($"[{column.ColumnName}] {pkDataType}({column.MaxLength})");

				pkNames.Add(column.ColumnName);
			}
			foreach (DataColumn column in table.Columns)
			{
				if (pkNames.Contains(column.ColumnName) == true)
					continue;

				switch (Type.GetTypeCode(column.DataType))
				{
					case TypeCode.Int32:
						columnDefs.Add($"[{column.ColumnName}] [int]");
						break;

					case TypeCode.Boolean:
						columnDefs.Add($"[{column.ColumnName}] [bit]");
						break;

					default:
						columnDefs.Add($"[{column.ColumnName}] nvarchar({(column.MaxLength == -1 ? "max" : column.MaxLength.ToString())})");
						break;
				}
			}

			columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ([{String.Join("], [", pkNames)}])");

			Database.ExecuteNonQuery(connection, $"DROP TABLE IF EXISTS [{table.TableName}];");

			string commandText = $"CREATE TABLE [{table.TableName}] ({String.Join(", ", columnDefs)});";
			Console.WriteLine(commandText);
			Database.ExecuteNonQuery(connection, commandText);

			Database.BulkInsert(connection, table);
		}
	}

	public enum PayloadLevel { Root, Subset, Datafile, Game, Machine, Softwarelist, Software };

	public class Counts
	{
		public long Datafiles = 0;
		public long Games = 0;
		public long Roms = 0;
		public long Size = 0;
		public long Disks = 0;
		public long DiskSize = 0;

		public Dictionary<string, int> Extentions = new Dictionary<string, int>();

		public void Add(Counts counts)
		{
			Datafiles += counts.Datafiles;
			Games += counts.Games;
			Roms += counts.Roms;
			Size += counts.Size;
			Disks += counts.Disks;
			DiskSize += counts.DiskSize;

			foreach (var extention in counts.Extentions)
			{
				if (Extentions.ContainsKey(extention.Key) == false)
					Extentions.Add(extention.Key, 0);
				Extentions[extention.Key] += extention.Value;
			}
		}

		public void AddExtention(string extention)
		{
			if (extention.Length == 0)
				extention = "_";
			else
				extention = extention.Substring(1);

			if (Extentions.ContainsKey(extention) == false)
				Extentions.Add(extention, 0);

			Extentions[extention] += 1;
		}

		public string ExtentionsToString()
		{
			int max = 10;

			var extentions = Extentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();

			if (extentions.Count() > max)
			{
				int remainingCount = 0;
				foreach (int count in extentions.Skip(10).Select(pair => pair.Value))
					remainingCount += count;

				extentions = extentions.Take(max);
				extentions = extentions.Append(new KeyValuePair<string, int>("...", remainingCount));
				extentions = extentions.OrderByDescending(pair => pair.Value);
			}

			return String.Join(", ", extentions.Select(pair => $"{pair.Key}({pair.Value})"));
		}
	}

	public class PayloadLevelInfo
	{
		public DataTable DataTable;

		public Counts Counts = new Counts();

		private string HtmlTitle;
		private readonly StringBuilder HtmlPage = new StringBuilder();

		private int TableWidth = 0;

		private readonly Dictionary<string, string[]> XmlJsonPayloads;

		public PayloadLevelInfo(
			PayloadLevel level,
			Dictionary<string, string[]> xmlJsonPayloads)
		{
			XmlJsonPayloads = xmlJsonPayloads;

			switch (level)
			{
				case PayloadLevel.Root:
					DataTable = Operations.MakePayloadDataTable("root_payload", new string[] { "key_1" });
					break;

				case PayloadLevel.Subset:
					DataTable = Operations.MakePayloadDataTable("subset_payload", new string[] { "subset_name" });
					break;

				case PayloadLevel.Datafile:
					DataTable = Operations.MakePayloadDataTable("datafile_payload", new string[] { "subset_name", "datafile_name" });
					break;

				case PayloadLevel.Game:
					DataTable = Operations.MakePayloadDataTable("game_payload", new string[] { "subset_name", "datafile_name", "game_name" });
					break;

				case PayloadLevel.Machine:
					DataTable = Operations.MakePayloadDataTable("machine_payload", new string[] { "machine_name" });
					break;

				case PayloadLevel.Softwarelist:
					DataTable = Operations.MakePayloadDataTable("softwarelist_payload", new string[] { "softwarelist_name" });
					break;

				case PayloadLevel.Software:
					DataTable = Operations.MakePayloadDataTable("software_payload", new string[] { "softwarelist_name", "software_name" });
					break;

				default:
					throw new ApplicationException("On another level.");
			}
		}

		public void Start(string title)
		{
			if (HtmlPage.Length != 0)
				throw new ApplicationException("Unfinished Business");

			Counts = new Counts();

			HtmlTitle = title;
		}
		public void Finish(params string[] keys)
		{
			if (keys.Length != DataTable.PrimaryKey.Length)
				throw new ApplicationException("Bad keys width");

			if (DataTable.Rows.Find(keys) != null)
			{
				Console.WriteLine($"!!! Warning Duplicate Item {DataTable.TableName}:\t{String.Join("\t", keys)}");
			}
			else
			{
				HtmlPage.AppendLine("<br />");

				string[] xmlJson = new string[] { "", "" };
				if (XmlJsonPayloads != null)
				{
					string key = String.Join("\t", keys);

					if (XmlJsonPayloads.ContainsKey(key) == false)
						throw new ApplicationException($"Did not find xml json lookup {key}");
					xmlJson = XmlJsonPayloads[key];
				}

				var rowData = new List<object>();
				rowData.AddRange(keys);
				rowData.AddRange(new string[] { HtmlTitle, xmlJson[0], xmlJson[1], HtmlPage.ToString() });

				DataTable.Rows.Add(rowData.ToArray());
			}

			HtmlPage.Length = 0;
		}

		public void Append(string html)
		{
			HtmlPage.AppendLine(html);
		}
		public void Append(DataRow row)
		{
			Append(new DataRow[] { row });
		}
		public void Append(IEnumerable<DataRow> rows)
		{
			if (rows.Any() == false)
				return;

			string[] columnNames = rows.First().Table.Columns.Cast<DataColumn>().Select(col => col.ColumnName).Where(name => name.EndsWith("_id") == false).ToArray();

			TableStart(columnNames);
			foreach (var row in rows)
				TableRow(columnNames.Select(col => row.IsNull(col) ? "" : Convert.ToString(row[col])).ToArray());
			TableEnd();
		}
		public void TableStart(params string[] columnNames)
		{
			TableWidth = columnNames.Length;
			HtmlPage.AppendLine("<table>");
			HtmlPage.AppendLine(EncodeTableRow(columnNames, "th"));
		}
		public void TableRow(params string[] values)
		{
			if (values.Length != TableWidth)
				throw new ApplicationException("Bad values width");

			HtmlPage.AppendLine(EncodeTableRow(values, "td"));
		}

		public void TableEnd()
		{
			HtmlPage.AppendLine("</table>");
		}

		private string EncodeTableRow(IEnumerable<string> values, string type)
		{
			values = values.Select(value => {
				if (value != null && value.StartsWith("<a href") == false)
					value = WebUtility.HtmlEncode(value);
				return value;
			});

			return $"<tr>{String.Join("", values.Select(value => $"<{type}>{value}</{type}>"))}</tr>";
		}

		public void Save(SqlConnection connection)
		{
			Operations.MakeMSSQLPayloadsInsert(connection, DataTable);
		}
	}
}
