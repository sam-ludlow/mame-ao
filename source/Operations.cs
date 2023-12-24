using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Spludlow.MameAO
{
	/// <summary>
	/// Operations - Used for MAME data processing pipelines
	/// </summary>
	public class Operations
	{

		public static int ProcessOperation(Dictionary<string, string> parameters, MameAOProcessor proc)
		{
			int exitCode;

			DateTime timeStart = DateTime.Now;

			switch (parameters["OPERATION"])
			{
				case "GET_MAME":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					exitCode = GetMame(parameters["DIRECTORY"], parameters["VERSION"], proc._HttpClient);
					break;

				case "MAKE_XML":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					exitCode = MakeXML(parameters["DIRECTORY"], parameters["VERSION"]);
					break;

				case "MAKE_JSON":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					exitCode = MakeJSON(parameters["DIRECTORY"], parameters["VERSION"]);
					break;

				case "MAKE_SQLITE":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					exitCode = MakeSQLite(parameters["DIRECTORY"], parameters["VERSION"]);
					break;

				case "MAKE_MSSQL":
					ValidateRequiredParameters(parameters, new string[] { "VERSION", "MSSQL_SERVER", "MSSQL_TARGET_NAMES" });

					exitCode = MakeMSSQL(parameters["DIRECTORY"], parameters["VERSION"], parameters["MSSQL_SERVER"], parameters["MSSQL_TARGET_NAMES"]);
					break;

				case "MAME_MSSQL_PAYLOADS":
					ValidateRequiredParameters(parameters, new string[] { "VERSION", "MSSQL_SERVER", "MSSQL_TARGET_NAMES" });

					exitCode = MakeMSSQLPayloads(parameters["DIRECTORY"], parameters["VERSION"], parameters["MSSQL_SERVER"], parameters["MSSQL_TARGET_NAMES"], proc._AssemblyVersion);
					break;

				case "MAME_MSSQL_PAYLOADS_HTML":
					ValidateRequiredParameters(parameters, new string[] { "MSSQL_SERVER", "MSSQL_TARGET_NAMES" });

					exitCode = MakeMSSQLPayloadHtml(parameters["MSSQL_SERVER"], parameters["MSSQL_TARGET_NAMES"]);
					break;

				default:
					throw new ApplicationException($"Unknown Operation {parameters["OPERATION"]}");
			}

			TimeSpan timeTook = DateTime.Now - timeStart;

			Console.WriteLine($"Operation '{parameters["OPERATION"]}' took: {Math.Round(timeTook.TotalSeconds, 0)} seconds");

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

		public static string GetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string version = Path.GetFileName(versionDirectory);

				string exeFilename = Path.Combine(versionDirectory, "mame.exe");

				if (File.Exists(exeFilename) == true)
					versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No MAME versions found in '{directory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}

		//
		// MAME
		//
		public static int GetMame(string directory, string version, HttpClient httpClient)
		{
			int newVersion = 0;

			string mameLatestJson = Tools.Query(httpClient, "https://api.github.com/repos/mamedev/mame/releases/latest");
			mameLatestJson = Tools.PrettyJSON(mameLatestJson);

			dynamic mameLatest = JsonConvert.DeserializeObject<dynamic>(mameLatestJson);

			if (version == "0")
				version = ((string)mameLatest.tag_name).Substring(4);

			string versionDirectory = Path.Combine(directory, version);

			if (Directory.Exists(versionDirectory) == false)
				Directory.CreateDirectory(versionDirectory);

			string exeFilename = Path.Combine(versionDirectory, "mame.exe");

			if (File.Exists(exeFilename) == false)
			{
				newVersion = 1;

				string binariesUrl = "https://github.com/mamedev/mame/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";
				binariesUrl = binariesUrl.Replace("@VERSION@", version);

				string binariesFilename = Path.Combine(versionDirectory, Path.GetFileName(binariesUrl));

				Tools.Download(binariesUrl, binariesFilename, 0, 15);

				Mame.RunSelfExtract(binariesFilename);
			}

			return newVersion;
		}

		//
		// XML
		//
		public static int MakeXML(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string exeFilename = Path.Combine(versionDirectory, "mame.exe");

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			if (File.Exists(machineXmlFilename) == false)
				Mame.ExtractXML(exeFilename, machineXmlFilename, "-listxml");

			if (File.Exists(softwareXmlFilename) == false)
				Mame.ExtractXML(exeFilename, softwareXmlFilename, "-listsoftware");

			return 0;
		}

		//
		// JSON
		//
		public static int MakeJSON(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			string machineJsonFilename = Path.Combine(versionDirectory, "_machine.json");
			string softwareJsonFilename = Path.Combine(versionDirectory, "_software.json");

			if (File.Exists(machineJsonFilename) == false)
				XML2JSON(machineXmlFilename, machineJsonFilename);

			if (File.Exists(softwareJsonFilename) == false)
				XML2JSON(softwareXmlFilename, softwareJsonFilename);

			return 0;
		}
		public static void XML2JSON(string inputXmlFilename, string outputJsonFilename)
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(inputXmlFilename);

			JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
			serializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;

			using (StreamWriter streamWriter = new StreamWriter(outputJsonFilename, false, new UTF8Encoding(false)))
			{
				CustomJsonWriter customJsonWriter = new CustomJsonWriter(streamWriter);

				JsonSerializer jsonSerializer = JsonSerializer.Create(serializerSettings);
				jsonSerializer.Serialize(customJsonWriter, xmlDocument);
			}
		}

		//
		// SQLite
		//
		public static int MakeSQLite(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string machineSqlLiteFilename = Path.Combine(versionDirectory, "_machine.sqlite");
			string softwareSqlLiteFilename = Path.Combine(versionDirectory, "_software.sqlite");

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			if (File.Exists(machineSqlLiteFilename) == false)
			{
				XML2SQLite(machineXmlFilename, machineSqlLiteFilename);
				GC.Collect();
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				XML2SQLite(softwareXmlFilename, softwareSqlLiteFilename);
				GC.Collect();
			}

			return 0;
		}
		public static void XML2SQLite(string xmlFilename, string sqliteFilename)
		{
			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, null);

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection connection = new SQLiteConnection(connectionString);

			Database.DatabaseFromXML(document, connection, dataSet);
		}

		//
		// MS SQL
		//
		public static int MakeMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(versionDirectory, "_machine.xml"),
				Path.Combine(versionDirectory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XML2MSSQL(sourceXmlFilename, serverConnectionString, targetDatabaseName);

				GC.Collect();
			}

			MakeForeignKeys(serverConnectionString, databaseNames);

			return 0;
		}
		public static void XML2MSSQL(string xmlFilename, string serverConnectionString, string databaseName)
		{
			SqlConnection targetConnection = new SqlConnection(serverConnectionString);

			if (Database.DatabaseExists(targetConnection, databaseName) == true)
				return;

			Database.ExecuteNonQuery(targetConnection, $"CREATE DATABASE[{databaseName}]");

			targetConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';");

			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, null);

			foreach (DataTable table in dataSet.Tables)
			{
				List<string> columnDefs = new List<string>();

				foreach (DataColumn column in table.Columns)
				{
					int max = 1;
					if (column.DataType.Name == "String")
					{
						foreach (DataRow row in table.Rows)
						{
							if (row.IsNull(column) == false)
								max = Math.Max(max, ((string)row[column]).Length);
						}
					}

					switch (column.DataType.Name)
					{
						case "String":
							columnDefs.Add($"[{column.ColumnName}] NVARCHAR({max})");
							break;

						case "Int64":
							columnDefs.Add($"[{column.ColumnName}] BIGINT" + (columnDefs.Count == 0 ? " NOT NULL" : ""));
							break;

						default:
							throw new ApplicationException($"SQL Bulk Copy, Unknown datatype {column.DataType.Name}");
					}
				}

				columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ([{table.Columns[0].ColumnName}])");

				string createText = $"CREATE TABLE [{table.TableName}]({String.Join(", ", columnDefs.ToArray())});";

				Console.WriteLine(createText);
				Database.ExecuteNonQuery(targetConnection, createText);

				Database.BulkInsert(targetConnection, table);

			}
		}

		public static int MakeForeignKeys(string serverConnectionString, string databaseNames)
		{
			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });
			for (int index = 0; index < databaseNamesEach.Length; ++index)
				databaseNamesEach[index] = databaseNamesEach[index].Trim();

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			foreach (string databaseName in databaseNamesEach)
			{
				using (var connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
				{
					DataTable table = Database.ExecuteFill(connection, "SELECT * FROM INFORMATION_SCHEMA.COLUMNS");

					foreach (DataRow row in table.Rows)
					{
						string TABLE_NAME = (string)row["TABLE_NAME"];
						string COLUMN_NAME = (string)row["COLUMN_NAME"];
						int ORDINAL_POSITION = (int)row["ORDINAL_POSITION"];
						string DATA_TYPE = (string)row["DATA_TYPE"];

						if (ORDINAL_POSITION > 1 && COLUMN_NAME.EndsWith("_id") && DATA_TYPE == "bigint")
						{
							string parentTableName = COLUMN_NAME.Substring(0, COLUMN_NAME.Length - 3);

							string commandText =
								$"ALTER TABLE [{TABLE_NAME}] ADD CONSTRAINT [FK_{parentTableName}_{TABLE_NAME}] FOREIGN KEY ([{COLUMN_NAME}]) REFERENCES [{parentTableName}] ([{COLUMN_NAME}])";

							Console.WriteLine(commandText);
							Database.ExecuteNonQuery(connection, commandText);

							commandText = $"CREATE INDEX [IX_{TABLE_NAME}_{COLUMN_NAME}] ON [{TABLE_NAME}] ([{COLUMN_NAME}])";

							Console.WriteLine(commandText);
							Database.ExecuteNonQuery(connection, commandText);
						}
					}
				}
			}

			return 0;
		}

		private static XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings() {
			DtdProcessing = DtdProcessing.Parse,
			IgnoreComments = false,
			IgnoreWhitespace = true,
		};

		public static int MakeMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseNames, string assemblyVersion)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < databaseNamesEach.Length; ++index)
				databaseNamesEach[index] = databaseNamesEach[index].Trim();

			string databaseName;
			string xmlFilename;

			//
			// machine
			//

			databaseName = databaseNamesEach[0];
			xmlFilename = Path.Combine(versionDirectory, $"_machine.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[0], "mame", "machine", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsMachine(xmlFilename, serverConnectionString, databaseName);

			//
			// software
			//

			databaseName = databaseNamesEach[1];
			xmlFilename = Path.Combine(versionDirectory, $"_software.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[1], "mame", "software", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsSoftware(xmlFilename, serverConnectionString, databaseName);

			return 0;
		}

		public static void MakeMSSQLPayloadsInfoTable(string version, string serverConnectionString, string databaseName, string dataSetName, string subSetName, string assemblyVersion, string versionDirectory)
		{
			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string exePath = Path.Combine(versionDirectory, "mame.exe");
			string exeTime = File.GetLastWriteTime(exePath).ToString("s");

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				string[] columnDefs = new string[] {
					"[_metadata_id] BIGINT NOT NULL PRIMARY KEY",
					"[dataset] NVARCHAR(1024) NOT NULL",
					"[subset] NVARCHAR(1024) NOT NULL",
					"[version] NVARCHAR(1024) NOT NULL",
					"[info] NVARCHAR(1024) NOT NULL",
					"[processed] DATETIME NOT NULL",
					"[agent] NVARCHAR(1024) NOT NULL",
				};
				string commandText = $"CREATE TABLE [_metadata] ({String.Join(", ", columnDefs)});";

				Console.WriteLine(commandText);
				Database.ExecuteNonQuery(connection, commandText);

				DataTable table = Database.ExecuteFill(connection, "SELECT * FROM [_metadata]");
				table.TableName = "_metadata";

				string info;

				switch (subSetName)
				{
					case "machine":

						int machineCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM machine");
						int romCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");
						int diskCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM disk");

						info = $"MAME: {version} - Released: {exeTime} - Machines: {machineCount} - rom: {romCount} - disk: {diskCount}";
						break;

					case "software":

						int softwarelistCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM softwarelist");
						int softwareCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM software");
						int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");
						int softDiskCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM disk");

						info = $"MAME: {version} - Released: {exeTime} - Lists: {softwarelistCount} - Software: {softwareCount} - rom: {softRomCount} - disk: {softDiskCount}";
						break;

					default:
						throw new ApplicationException($"bad subset: {subSetName}");
				}

				table.Rows.Add(1L, dataSetName, subSetName, version, info, DateTime.Now, agent);

				Database.BulkInsert(connection, table);
			}
		}

		public static void MakeMSSQLPayloadsMachine(string xmlFilename, string serverConnectionString, string databaseName)
		{
			DataTable table = new DataTable($"machine_payload");
			table.Columns.Add("machine_name", typeof(string));
			table.Columns.Add("title", typeof(string));
			table.Columns.Add("xml", typeof(string));
			table.Columns.Add("json", typeof(string));
			table.Columns.Add("html", typeof(string));

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "machine")
					{
						XElement element = XElement.ReadFrom(reader) as XElement;
						if (element != null)
						{
							string key = element.Attribute("name").Value;

							string title = "";
							string xml = element.ToString();
							string json = XML2JSON(element);
							string html = "";

							table.Rows.Add(key, title, xml, json, html);
						}
					}
				}
			}

			MakeMSSQLPayloadsInsert(table, serverConnectionString, databaseName, new string[] { "machine_name" });
		}

		public static void MakeMSSQLPayloadsSoftware(string xmlFilename, string serverConnectionString, string databaseName)
		{
			DataTable listsTable = new DataTable($"softwarelists_payload");
			listsTable.Columns.Add("key_1", typeof(string));
			listsTable.Columns.Add("title", typeof(string));
			listsTable.Columns.Add("xml", typeof(string));
			listsTable.Columns.Add("json", typeof(string));
			listsTable.Columns.Add("html", typeof(string));

			listsTable.Rows.Add("1", "", "", "", "");

			DataTable listTable = new DataTable($"softwarelist_payload");
			listTable.Columns.Add("softwarelist_name", typeof(string));
			listTable.Columns.Add("title", typeof(string));
			listTable.Columns.Add("xml", typeof(string));
			listTable.Columns.Add("json", typeof(string));
			listTable.Columns.Add("html", typeof(string));

			DataTable softwareTable = new DataTable($"software_payload");
			softwareTable.Columns.Add("softwarelist_name", typeof(string));
			softwareTable.Columns.Add("software_name", typeof(string));
			softwareTable.Columns.Add("title", typeof(string));
			softwareTable.Columns.Add("xml", typeof(string));
			softwareTable.Columns.Add("json", typeof(string));
			softwareTable.Columns.Add("html", typeof(string));

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "softwarelist")
					{
						XElement listElement = XElement.ReadFrom(reader) as XElement;
						if (listElement != null)
						{
							string softwarelist_name = listElement.Attribute("name").Value;

							string title = "";
							string xml = listElement.ToString();
							string json = XML2JSON(listElement);
							string html = "";

							listTable.Rows.Add(softwarelist_name, title, xml, json, html);

							foreach (XElement element in listElement.Elements("software"))
							{
								string software_name = element.Attribute("name").Value;

								title = "";
								xml = element.ToString();
								json = XML2JSON(element);
								html = "";

								softwareTable.Rows.Add(softwarelist_name, software_name, title, xml, json, html);
							}
						}
					}
				}
			}

			MakeMSSQLPayloadsInsert(listTable, serverConnectionString, databaseName, new string[] { "softwarelist_name" });

			MakeMSSQLPayloadsInsert(softwareTable, serverConnectionString, databaseName, new string[] { "softwarelist_name", "software_name" });

			MakeMSSQLPayloadsInsert(listsTable, serverConnectionString, databaseName, new string[] { "key_1" });
		}

		public static void MakeMSSQLPayloadsInsert(DataTable table, string serverConnectionString, string databaseName, string[] primaryKeyNames)
		{
			using (SqlConnection targetConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				List<string> columnDefs = new List<string>();

				foreach (string primaryKeyName in primaryKeyNames)
					columnDefs.Add($"{primaryKeyName} VARCHAR(32)");

				columnDefs.Add("[title] NVARCHAR(MAX)");
				columnDefs.Add("[xml] NVARCHAR(MAX)");
				columnDefs.Add("[json] NVARCHAR(MAX)");
				columnDefs.Add("[html] NVARCHAR(MAX)");

				columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ({String.Join(", ", primaryKeyNames)})");

				string commandText = $"CREATE TABLE [{table.TableName}] ({String.Join(", ", columnDefs)});";

				Console.WriteLine(commandText);
				Database.ExecuteNonQuery(targetConnection, commandText);

				Database.BulkInsert(targetConnection, table);
			}
		}

		//
		// HTML
		//

		public static int MakeMSSQLPayloadHtml(string serverConnectionString, string databaseNames)
		{
			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });
			for (int index = 0; index < databaseNamesEach.Length; ++index)
				databaseNamesEach[index] = databaseNamesEach[index].Trim();

			using (SqlConnection machineConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[0]}';"))
			{
				MakeMSSQLPayloadHtmlMachine(machineConnection);

				using (SqlConnection softwareConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[1]}';"))
					MakeMSSQLPayloadHtmlSoftware(softwareConnection, machineConnection);
			}

			return 0;
		}

		public static void MakeMSSQLPayloadHtmlMachine(SqlConnection connection)
		{
			//
			// Load all database
			//
			DataSet dataSet = new DataSet();

			List<string> conditionTableNames = new List<string>();

			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connection))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;

				if (tableName.EndsWith("_condition") == true)
					conditionTableNames.Add(tableName);
			}

			//ReportRelations(dataSet);

			//
			// Merge condition tables
			//
			foreach (string conditionTableName in conditionTableNames)
			{
				string parentTableName = conditionTableName.Substring(0, conditionTableName.Length - 10);

				DataTable parentTable = dataSet.Tables[parentTableName];
				DataTable conditionTable = dataSet.Tables[conditionTableName];

				conditionTable.PrimaryKey = new DataColumn[] { conditionTable.Columns[1] };

				foreach (DataColumn column in conditionTable.Columns)
				{
					string newColumnName = $"condition_{column.ColumnName}";
					parentTable.Columns.Add(newColumnName, column.DataType);
				}

				string keyColumnName = parentTable.Columns[0].ColumnName;

				foreach (DataRow parentRow in parentTable.Rows)
				{
					long key = (long)parentRow[0];

					DataRow conditionRow = conditionTable.Rows.Find(key);

					if (conditionRow == null)
						continue;

					foreach (DataColumn column in conditionTable.Columns)
					{
						string newColumnName = $"condition_{column.ColumnName}";
						parentRow[newColumnName] = conditionRow[column];
					}
				}
			}

			DataRow metaRow = dataSet.Tables["_metadata"].Rows[0];

			string version = (string)metaRow["version"];

			string[] simpleTableNames = new string[] {
				"machine",
				"display",
				"driver",
				"rom",
				"disk",
				"chip",
				"softwarelist",
				"device_ref",
				"sample",
				"adjuster",
				"biosset",
				"sound",
				"feature",
				"ramoption",
			};

			using (SqlCommand command = new SqlCommand("UPDATE machine_payload SET [title] = @title, [html] = @html WHERE [machine_name] = @machine_name", connection))
			{
				command.Parameters.Add("@title", SqlDbType.NVarChar);
				command.Parameters.Add("@html", SqlDbType.NVarChar);
				command.Parameters.Add("@machine_name", SqlDbType.VarChar);

				connection.Open();

				try
				{
					foreach (DataRow machineRow in dataSet.Tables["machine"].Rows)
					{
						long machine_id = (long)machineRow["machine_id"];
						string machine_name = (string)machineRow["name"];

						//if (machine_name != "software_list")
						//	continue;

						StringBuilder html = new StringBuilder();

						//
						// Simple joins
						//
						foreach (string tableName in simpleTableNames)
						{
							DataTable sourceTable = dataSet.Tables[tableName];

							DataRow[] rows = sourceTable.Select("machine_id = " + machine_id);

							if (rows.Length == 0)
								continue;

							DataTable table = sourceTable.Clone();

							foreach (DataRow row in rows)
							{
								table.ImportRow(row);

								DataRow targetRow = table.Rows[table.Rows.Count - 1];

								switch (tableName)
								{
									case "machine":
										if (targetRow.IsNull("sourcefile") == false)
										{
											string value = (string)targetRow["sourcefile"];

											string baseUrl = $"https://github.com/mamedev/mame/blob/mame{version}/src";

											if (value.Split(new char[] { '/' }).Length == 2 && value.StartsWith("emu/") == false)
												value = $"<a href=\"{baseUrl}/mame/{value}\" target=\"_blank\">{value}</a>";
											else
												value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";

											targetRow["sourcefile"] = value;
										}
										if (targetRow.IsNull("romof") == false)
										{
											string value = (string)targetRow["romof"];
											targetRow["romof"] = $"<a href=\"/mame/machine/{value}\">{value}</a>";
										}
										if (targetRow.IsNull("cloneof") == false)
										{
											string value = (string)targetRow["cloneof"];
											targetRow["cloneof"] = $"<a href=\"/mame/machine/{value}\">{value}</a>";
										}
										break;

									case "device_ref":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/mame/machine/{value}\">{value}</a>";
										}
										break;


									case "softwarelist":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/mame/software/{value}\">{value}</a>";
										}
										break;
								}
							}

							if (tableName == "machine")
							{
								html.AppendLine("<br />");
								html.AppendLine($"<div><h2 style=\"display:inline;\">machine</h2> &bull; <a href=\"{machine_name}.xml\">XML</a> &bull; <a href=\"{machine_name}.json\">JSON</a> &bull; <a href=\"#\" onclick=\"mameAO('{machine_name}'); return false\">AO</a></div>");
								html.AppendLine("<br />");
							}
							else
							{
								html.AppendLine($"<hr />");
								html.AppendLine($"<h2>{tableName}</h2>");
							}

							html.AppendLine(Reports.MakeHtmlTable(table, null));
						}

						//
						// input, control
						//
						DataRow[] inputRows = dataSet.Tables["input"].Select("machine_id = " + machine_id);
						if (inputRows.Length > 0)
						{
							if (inputRows.Length != 1)
								throw new ApplicationException("Not one [input] row.");

							long input_id = (long)inputRows[0]["input_id"];

							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>input</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["input"], inputRows, null));

							DataRow[] controlRows = dataSet.Tables["control"].Select("input_id = " + input_id);
							if (controlRows.Length > 0)
							{
								html.AppendLine($"<h3>control</h3>");
								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["control"], controlRows, null));
							}
						}

						//
						// port, analog
						//
						DataRow[] portRows = dataSet.Tables["port"].Select("machine_id = " + machine_id);
						if (portRows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>port, analog</h2>");

							DataTable table = Tools.MakeDataTable(
								"port_tag	analog_masks",
								"String		String"
							);
							foreach (DataRow portRow in portRows)
							{
								long port_id = (long)portRow["port_id"];

								DataRow[] analogRows = dataSet.Tables["analog"].Select("port_id = " + port_id);

								string masks = String.Join(", ", analogRows.Select(row => (string)row["mask"]));

								table.Rows.Add((string)portRow["tag"], masks);
							}

							html.AppendLine(Reports.MakeHtmlTable(table, null));
						}

						//
						// slot, slotoption
						//
						DataRow[] slotRows = dataSet.Tables["slot"].Select("machine_id = " + machine_id);
						if (slotRows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>slot, slotoption</h2>");

							DataTable table = Tools.MakeDataTable(
								"slot_name	slotoption_name	slotoption_devname	slotoption_default",
								"String		String			String				String"
							);

							foreach (DataRow slotRow in slotRows)
							{
								long slot_id = (long)slotRow["slot_id"];
								DataRow[] slotoptionRows = dataSet.Tables["slotoption"].Select("slot_id = " + slot_id);

								if (slotoptionRows.Length == 0)
									table.Rows.Add(slotRow["name"], null, null, null);

								foreach (DataRow slotoptionRow in slotoptionRows)
								{
									DataRow row = table.Rows.Add(slotRow["name"], slotoptionRow["name"], slotoptionRow["devname"], slotoptionRow["default"]);

									if (row.IsNull("slotoption_devname") == false)
									{
										string value = (string)row["slotoption_devname"];
										row["slotoption_devname"] = $"<a href=\"/mame/machine/{value}\">{value}</a>";
									}
								}
							}

							html.AppendLine(Reports.MakeHtmlTable(table, null));
						}

						//
						// configuration
						//
						DataRow[] configurationRows = dataSet.Tables["configuration"].Select("machine_id = " + machine_id);
						if (configurationRows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>configuration</h2>");

							foreach (DataRow configurationRow in configurationRows)
							{
								long configuration_id = (long)configurationRow["configuration_id"];

								html.AppendLine($"<hr class='px2' />");

								html.AppendLine($"<h3>{(string)configurationRow["name"]}</h3>");

								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["configuration"], new[] { configurationRow }, null));

								DataRow[] conflocationRows = dataSet.Tables["conflocation"].Select("configuration_id = " + configuration_id);
								if (conflocationRows.Length > 0)
								{
									html.AppendLine($"<h4>location</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["conflocation"], conflocationRows, null));
								}

								DataRow[] confsettingRows = dataSet.Tables["confsetting"].Select("configuration_id = " + configuration_id);
								if (confsettingRows.Length > 0)
								{
									html.AppendLine($"<h4>setting</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["confsetting"], confsettingRows, null));
								}

							}
						}

						//
						// dipswitch
						//
						DataRow[] dipswitchRows = dataSet.Tables["dipswitch"].Select("machine_id = " + machine_id);
						if (dipswitchRows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>dipswitch</h2>");

							foreach (DataRow dipswitchRow in dipswitchRows)
							{
								long dipswitch_id = (long)dipswitchRow["dipswitch_id"];

								html.AppendLine($"<hr class='px2' />");

								html.AppendLine($"<h3>{(string)dipswitchRow["name"]}</h3>");

								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipswitch"], new[] { dipswitchRow }, null));

								DataRow[] diplocationRows = dataSet.Tables["diplocation"].Select("dipswitch_id = " + dipswitch_id);
								if (diplocationRows.Length > 0)
								{
									html.AppendLine($"<h4>location</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["diplocation"], diplocationRows, null));
								}

								DataRow[] dipvalueRows = dataSet.Tables["dipvalue"].Select("dipswitch_id = " + dipswitch_id);
								if (dipvalueRows.Length > 0)
								{
									html.AppendLine($"<h4>value</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipvalue"], dipvalueRows, null));
								}
							}
						}

						command.Parameters["@title"].Value = $"{(string)machineRow["description"]} - mame machine";
						command.Parameters["@html"].Value = html.ToString();
						command.Parameters["@machine_name"].Value = machine_name;

						command.ExecuteNonQuery();

					}
				}
				finally
				{
					connection.Close();
				}
			}
		}

		public static void MakeMSSQLPayloadHtmlSoftware(SqlConnection connection, SqlConnection machineConnection)
		{
			DataSet dataSet = new DataSet();

			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connection))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;
			}

			DataTable machineListTable = Database.ExecuteFill(machineConnection, "SELECT machine.name AS machine_name, driver.status, softwarelist.name AS softwarelist_name " +
				"FROM (machine LEFT JOIN driver ON machine.machine_id = driver.machine_id) INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id");

			foreach (DataRow row in machineListTable.Rows)
			{
				if (row.IsNull("status") == true)
					row["status"] = "no driver";
			}

			DataTable machineDetailTable = Database.ExecuteFill(machineConnection, "SELECT machine.name, machine.description FROM machine");
			machineDetailTable.PrimaryKey = new DataColumn[] { machineDetailTable.Columns["name"] };

			//ReportRelations(dataSet);

			DataRow metaRow = dataSet.Tables["_metadata"].Rows[0];

			string version = (string)metaRow["version"];

			connection.Open();

			try
			{
				SqlCommand softwarelistCommand = new SqlCommand("UPDATE softwarelist_payload SET [title] = @title, [html] = @html WHERE [softwarelist_name] = @softwarelist_name", connection);
				softwarelistCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwarelistCommand.Parameters.Add("@html", SqlDbType.NVarChar);
				softwarelistCommand.Parameters.Add("@softwarelist_name", SqlDbType.VarChar);

				SqlCommand softwareCommand = new SqlCommand("UPDATE software_payload SET [title] = @title, [html] = @html WHERE ([softwarelist_name] = @softwarelist_name AND [software_name] = @software_name)", connection);
				softwareCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwareCommand.Parameters.Add("@html", SqlDbType.NVarChar);
				softwareCommand.Parameters.Add("@softwarelist_name", SqlDbType.VarChar);
				softwareCommand.Parameters.Add("@software_name", SqlDbType.VarChar);

				DataTable romTable = new DataTable();
				foreach (DataColumn column in dataSet.Tables["dataarea"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						romTable.Columns.Add("data_" + column.ColumnName);
				foreach (DataColumn column in dataSet.Tables["rom"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						romTable.Columns.Add(column.ColumnName);

				DataTable diskTable = new DataTable();
				foreach (DataColumn column in dataSet.Tables["diskarea"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						diskTable.Columns.Add("data_" + column.ColumnName);
				foreach (DataColumn column in dataSet.Tables["disk"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						diskTable.Columns.Add(column.ColumnName);

				DataTable listTable = Tools.MakeDataTable(
					"name	description",
					"String	String"
				);

				foreach (DataRow softwarelistRow in dataSet.Tables["softwarelist"].Rows)
				{
					long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
					string softwarelist_name = (string)softwarelistRow["name"];
					string softwarelist_description = (string)softwarelistRow["description"];

					//if (softwarelist_name != "a800")
					//	continue;

					//if (softwarelist_name != "spectrum_flop_opus")
					//	continue;

					DataRow[] softwareRows = dataSet.Tables["software"].Select($"softwarelist_id = {softwarelist_id}");

					//
					// SoftwareLists
					//

					StringBuilder html = new StringBuilder();

					html.AppendLine("<br />");
					html.AppendLine($"<div><h2 style=\"display:inline;\">softwarelist</h2> &bull; <a href=\"{softwarelist_name}.xml\">XML</a> &bull; <a href=\"{softwarelist_name}.json\">JSON</a> </div>");
					html.AppendLine("<br />");
					html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new DataRow[] { softwarelistRow }, null));
					
					html.AppendLine($"<hr />");
					html.AppendLine($"<h2>software</h2>");
					DataTable softwareTable = dataSet.Tables["software"].Clone();
					foreach (DataRow softwareRow in softwareRows)
					{
						softwareTable.ImportRow(softwareRow);
						DataRow row = softwareTable.Rows[softwareTable.Rows.Count - 1];
						string value = (string)row["name"];
						row["name"] = $"<a href=\"/mame/software/{softwarelist_name}/{value}\">{value}</a>";
						if (row.IsNull("cloneof") == false)
						{
							value = (string)row["cloneof"];
							row["cloneof"] = $"<a href=\"/mame/software/{softwarelist_name}/{value}\">{value}</a>";
						}
					}
					html.AppendLine(Reports.MakeHtmlTable(softwareTable, null));

					listTable.Rows.Add($"<a href=\"/mame/software/{softwarelist_name}\">{softwarelist_name}</a>", softwarelist_description);

					softwarelistCommand.Parameters["@title"].Value = $"{softwarelist_description} - mame ({version}) software list";
					softwarelistCommand.Parameters["@html"].Value = html.ToString();
					softwarelistCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;

					softwarelistCommand.ExecuteNonQuery();

					//
					// Software
					//

					softwarelistRow["name"] = $"<a href=\"/mame/software/{softwarelist_name}\">{softwarelist_name}</a>";

					foreach (DataRow softwareRow in softwareRows)
					{
						long software_id = (long)softwareRow["software_id"];
						string software_name = (string)softwareRow["name"];

						if (softwareRow.IsNull("cloneof") == false)
						{
							string value = (string)softwareRow["cloneof"];
							softwareRow["cloneof"] = $"<a href=\"/mame/software/{softwarelist_name}/{value}\">{value}</a>";
						}

						html = new StringBuilder();

						html.AppendLine("<br />");
						html.AppendLine($"<div><h2 style=\"display:inline;\">software</h2> &bull; <a href=\"{software_name}.xml\">XML</a> &bull; <a href=\"{software_name}.json\">JSON</a> </div>");
						html.AppendLine("<br />");
						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["software"], new[] { softwareRow }, null));

						html.AppendLine($"<hr />");

						html.AppendLine($"<h2>softwarelist</h2>");
						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new[] { softwarelistRow }, null));

						DataRow[] rows;

						rows = dataSet.Tables["info"].Select($"software_id = {software_id}");
						if (rows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>info</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["info"], rows, null));
						}

						rows = dataSet.Tables["sharedfeat"].Select($"software_id = {software_id}");
						if (rows.Length > 0)
						{
							html.AppendLine($"<hr />");
							html.AppendLine($"<h2>sharedfeat</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["sharedfeat"], rows, null));
						}

						foreach (DataRow partRow in dataSet.Tables["part"].Select($"software_id = {software_id}"))
						{
							long part_id = (long)partRow["part_id"];
							string part_name = (string)partRow["name"];
							string part_interface = (string)partRow["interface"];

							rows = dataSet.Tables["feature"].Select($"part_id = {part_id}");
							if (rows.Length > 0)
							{
								DataTable table = Tools.MakeDataTable(
									"part_name	part_interface	feature_name	feature_value",
									"String		String			String			String"
								);
								foreach (DataRow row in rows)
									table.Rows.Add(part_name, part_interface, row["name"], row["value"]);

								html.AppendLine($"<hr />");	//	!!!!!!!!!!!! dont always get feature hr is bad
								html.AppendLine($"<h2>part, feature</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}

							rows = dataSet.Tables["dataarea"].Select($"part_id = {part_id}");
							if (rows.Length > 0)
							{
								foreach (DataRow row in rows)
								{
									long dataarea_id = (long)row["dataarea_id"];

									romTable.Clear();

									DataRow[] romRows = dataSet.Tables["rom"].Select($"dataarea_id = {dataarea_id}");

									if (romRows.Length == 0)
									{
										DataRow targetRow = romTable.NewRow();

										foreach (DataColumn column in dataSet.Tables["dataarea"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow["data_" + column.ColumnName] = row[column.ColumnName];

										romTable.Rows.Add(targetRow);
									}
									foreach (DataRow romRow in romRows)
									{
										DataRow targetRow = romTable.NewRow();

										foreach (DataColumn column in dataSet.Tables["dataarea"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow["data_" + column.ColumnName] = row[column.ColumnName];

										foreach (DataColumn column in dataSet.Tables["rom"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow[column.ColumnName] = romRow[column.ColumnName];

										romTable.Rows.Add(targetRow);
									}

									html.AppendLine($"<hr class='px2' />");
									html.AppendLine($"<h2>dataarea, rom</h2>");
									html.AppendLine(Reports.MakeHtmlTable(romTable, null));
								}
							}

							rows = dataSet.Tables["diskarea"].Select($"part_id = {part_id}");
							if (rows.Length > 0)
							{
								foreach (DataRow row in rows)
								{
									long diskarea_id = (long)row["diskarea_id"];

									diskTable.Clear();

									DataRow[] diskRows = dataSet.Tables["disk"].Select($"diskarea_id = {diskarea_id}");

									if (diskRows.Length == 0)
									{
										DataRow targetRow = diskTable.NewRow();

										foreach (DataColumn column in dataSet.Tables["diskarea"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow["data_" + column.ColumnName] = row[column.ColumnName];

										diskTable.Rows.Add(targetRow);
									}
									foreach (DataRow diskRow in diskRows)
									{
										DataRow targetRow = diskTable.NewRow();

										foreach (DataColumn column in dataSet.Tables["diskarea"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow["data_" + column.ColumnName] = row[column.ColumnName];

										foreach (DataColumn column in dataSet.Tables["disk"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												targetRow[column.ColumnName] = diskRow[column.ColumnName];

										diskTable.Rows.Add(targetRow);
									}

									html.AppendLine($"<hr class='px2' />");
									html.AppendLine($"<h2>diskarea, disk</h2>");
									html.AppendLine(Reports.MakeHtmlTable(diskTable, null));
								}
							}
						}

						DataRow[] machineListRows = machineListTable.Select($"softwarelist_name = '{softwarelist_name}'");

						foreach (string status in new string[] { "good", "imperfect", "preliminary", "no driver" })
						{
							DataRow[] statusRows = machineListRows.Where(row => (string)row["status"] == status).ToArray();

							if (statusRows.Length > 0)
							{
								DataTable machinesTable = new DataTable();
								machinesTable.Columns.Add("name", typeof(string));
								machinesTable.Columns.Add("description (AO)", typeof(string));

								foreach (DataRow statusRow in statusRows)
								{
									string name = (string)statusRow["machine_name"];
									DataRow detailRow = machineDetailTable.Rows.Find(name);
									string description = detailRow != null ? (string)detailRow["description"] : "not found";

									machinesTable.Rows.Add($"<a href=\"/mame/machine/{name}\">{name}</a>", $"<a href=\"#\" onclick=\"mameAO('{name} {software_name}'); return false\">{description}</a>");
								}

								html.AppendLine($"<hr />");
								html.AppendLine($"<h2>machines ({status})</h2>");
								html.AppendLine(Reports.MakeHtmlTable(machinesTable, null));
							}
						}

						softwareCommand.Parameters["@title"].Value = $"{(string)softwareRow["description"]} - {(string)softwarelistRow["description"]} - mame ({version}) software";
						softwareCommand.Parameters["@html"].Value = html.ToString();
						softwareCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;
						softwareCommand.Parameters["@software_name"].Value = software_name;

						softwareCommand.ExecuteNonQuery();
					}
				}

				SqlCommand softwarelistsCommand = new SqlCommand("UPDATE softwarelists_payload SET [title] = @title, [html] = @html WHERE [key_1] = '1'", connection);
				softwarelistsCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwarelistsCommand.Parameters.Add("@html", SqlDbType.NVarChar);

				softwarelistsCommand.Parameters["@title"].Value = $"Software Lists - mame ({version}) software";
				softwarelistsCommand.Parameters["@html"].Value = Reports.MakeHtmlTable(listTable, null);

				softwarelistsCommand.ExecuteNonQuery();
			}
			finally
			{
				connection.Close();
			}

		}

		public static void ReportRelations(DataSet dataSet)
		{
			StringBuilder report = new StringBuilder();
			foreach (DataTable parentTable in dataSet.Tables)
			{
				string pkName = parentTable.Columns[0].ColumnName;

				DataTable[] childTables = FindChildTables(pkName, dataSet);

				if (childTables.Length > 0)
				{
					foreach (DataTable childTable in childTables)
					{
						int min = Int32.MaxValue;
						int max = Int32.MinValue;

						foreach (DataRow row in parentTable.Rows)
						{
							long id = (long)row[pkName];

							DataRow[] childRows = childTable.Select($"{pkName} = {id}");

							min = Math.Min(min, childRows.Length);
							max = Math.Max(max, childRows.Length);
						}

						report.AppendLine($"{parentTable.TableName}\t{childTable.TableName}\t{min}\t{max}");
					}
				}
			}
			Tools.PopText(report.ToString());
		}

		public static DataTable[] FindChildTables(string parentKeyName, DataSet dataSet)
		{
			List<DataTable> childTables = new List<DataTable>();

			foreach (DataTable table in dataSet.Tables)
			{
				DataColumn column = table.Columns.Cast<DataColumn>().Where(col => col.Ordinal > 0 && col.ColumnName == parentKeyName).SingleOrDefault();

				if (column != null)
					childTables.Add(table);
			}

			return childTables.ToArray();
		}

		public static string XML2JSON(XElement element)
		{
			JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
			serializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;

			using (StringWriter writer  = new StringWriter())
			{
				CustomJsonWriter customJsonWriter = new CustomJsonWriter(writer);

				JsonSerializer jsonSerializer = JsonSerializer.Create(serializerSettings);
				jsonSerializer.Serialize(customJsonWriter, element);

				return writer.ToString();
			}
		}

	}

	public class CustomJsonWriter : JsonTextWriter
	{
		public CustomJsonWriter(TextWriter writer) : base(writer) { }
		public override void WritePropertyName(string name)
		{
			if (name.StartsWith("@") == true)
				base.WritePropertyName(name.Substring(1));
			else
				base.WritePropertyName(name);
		}
	}
}
