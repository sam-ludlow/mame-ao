using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

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

		private readonly static HashSet<string> yearFixMatch = new HashSet<string>(new string[] {
			"?", "??", "???", "0", "00", "000", "0000"
		});
		public static int ParseFixYear(string year)
		{
			string yearFix = year;

			if (yearFix.Length > 4)
				yearFix = yearFix.Substring(0, 4);

			yearFix = yearFix.ToUpper().Replace("X", "?");

			if (yearFixMatch.Contains(yearFix))
				yearFix = "????";

			if (yearFix == "20??")
				yearFix = "2005";

			if (yearFix[3] == '?')
				yearFix = yearFix.Substring(0, 3) + "5";

			if (yearFix.Contains("?") == true)
				yearFix = "1985";

			int year_fixed;
			if (Int32.TryParse(yearFix, out year_fixed) == false)
				throw new ApplicationException($"Bad year:\t{year}\t{yearFix}");

			return year_fixed;
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
}
