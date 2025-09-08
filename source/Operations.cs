using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class Operations
	{

		public static int ProcessOperation(Dictionary<string, string> parameters)
		{
			int exitCode;

			DateTime timeStart = DateTime.Now;

			switch (parameters["operation"])
			{
				//
				//	MAME
				//
				case "mame-get":
					exitCode = GetMame(parameters["directory"], parameters["version"]);
					break;

				case "mame-xml":
					exitCode = MakeXML(parameters["directory"], parameters["version"]);
					break;

				case "mame-json":
					exitCode = MakeJSON(parameters["directory"], parameters["version"]);
					break;

				case "mame-sqlite":
					exitCode = MakeSQLite(parameters["directory"], parameters["version"]);
					break;

				case "mame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "mame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "mame-mssql-payload-html":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeMSSQLPayloadHtml(parameters["server"], parameters["names"], "mame");
					break;

				//
				// HBMAME
				//
				case "hbmame-get":
					exitCode = GetHbMame(parameters["directory"], parameters["version"]);
					break;

				case "hbmame-xml":
					exitCode = MakeHbMameXML(parameters["directory"], parameters["version"]);
					break;

				case "hbmame-json":
					exitCode = 0;
					break;

				case "hbmame-sqlite":
					exitCode = MakeHbMameSQLite(parameters["directory"], parameters["version"]);
					break;

				case "hbmame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeHbMameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "hbmame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeHbMameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "hbmame-mssql-payload-html":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeMSSQLPayloadHtml(parameters["server"], parameters["names"], "hbmame");
					break;

				//
				// TOSEC
				//
				case "tosec-get":
					exitCode = GetTosec(parameters["directory"], parameters["version"]);
					break;

				case "tosec-xml":
					exitCode = 0;
					break;

				case "tosec-json":
					exitCode = 0;
					break;

				case "tosec-sqlite":
					exitCode = MakeTosecSQLite(parameters["directory"], parameters["version"]);
					break;

				case "tosec-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeTosecMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "tosec-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeTosecMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "tosec-mssql-payload-html":
					exitCode = 0;
					break;

				//
				// FBNeo
				//
				case "fbneo-get":
					exitCode = GetFBNeo(parameters["directory"], parameters["version"]);
					break;

				case "fbneo-xml":
					exitCode = MakeFBNeoXML(parameters["directory"], parameters["version"]);
					break;

				case "fbneo-json":
					exitCode = 0;
					break;

				case "fbneo-sqlite":
					exitCode = MakeFBNeoSQLite(parameters["directory"], parameters["version"]);
					break;

				case "fbneo-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeFBNeoMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "fbneo-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MakeFBNeoMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "fbneo-mssql-payload-html":
					exitCode = 0;
					break;

				default:
					throw new ApplicationException($"Unknown Operation {parameters["operation"]}");
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
		public static int GetMame(string directory, string version)
		{
			ICore core = new CoreMame();
			core.Initialize(directory, version);
			return core.Get();
		}

		//
		// XML
		//
		public static int MakeXML(string directory, string version)
		{
			ICore core = new CoreMame();
			core.Initialize(directory, version);
			core.Xml();
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

			JsonSerializerSettings serializerSettings = new JsonSerializerSettings
			{
				Formatting = Newtonsoft.Json.Formatting.Indented
			};

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
			ICore core = new CoreMame();
			core.Initialize(directory, version);
			core.SQLite();
			return 0;
		}

		//
		// MS SQL
		//
		public static int MakeMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(directory, "_machine.xml"),
				Path.Combine(directory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XElement document = XElement.Load(sourceXmlFilename);
				DataSet dataSet = new DataSet();
				ReadXML.ImportXMLWork(document, dataSet, null, null);

				Database.DataSet2MSSQL(dataSet, serverConnectionString, targetDatabaseName);

				Database.MakeForeignKeys(serverConnectionString, targetDatabaseName);
			}

			return 0;
		}




		private static readonly XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings() {
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
			xmlFilename = Path.Combine(versionDirectory, "_machine.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[0], "mame", "machine", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsMachine(xmlFilename, serverConnectionString, databaseName);

			//
			// software
			//

			databaseName = databaseNamesEach[1];
			xmlFilename = Path.Combine(versionDirectory, "_software.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[1], "mame", "software", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsSoftware(xmlFilename, serverConnectionString, databaseName);

			return 0;
		}

		public static DataTable CreateMetaDataTable(string serverConnectionString, string databaseName)
		{
			string tableName = "_metadata";
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
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

				DataTable table = Database.ExecuteFill(connection, $"SELECT * FROM [{tableName}] WHERE ({tableName}_id = -1)");
				table.TableName = tableName;
				return table;
			}
		}

		public static void MakeMSSQLPayloadsInfoTable(string version, string serverConnectionString, string databaseName, string dataSetName, string subSetName, string assemblyVersion, string versionDirectory)
		{
			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string exePath = Path.Combine(versionDirectory, "mame.exe");
			string exeTime = File.GetLastWriteTime(exePath).ToString("s");

			CreateMetaDataTable(serverConnectionString, databaseName);

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				DataTable table = Database.ExecuteFill(connection, "SELECT * FROM [_metadata]");
				table.TableName = "_metadata";

				string info;

				switch (subSetName)
				{
					case "machine":

						int machineCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM machine");
						int romCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");
						int diskCount = Database.TableExists(connection, "disk") == true ? (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM disk") : 0;

						info = $"{dataSetName.ToUpper()}: {version} - Released: {exeTime} - Machines: {machineCount} - rom: {romCount} - disk: {diskCount}";
						break;

					case "software":

						int softwarelistCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM softwarelist");
						int softwareCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM software");
						int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");
						int softDiskCount = Database.TableExists(connection, "disk") == true ? (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM disk") : 0;

						info = $"{dataSetName.ToUpper()}: {version} - Released: {exeTime} - Lists: {softwarelistCount} - Software: {softwareCount} - rom: {softRomCount} - disk: {softDiskCount}";
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
			DataTable table = new DataTable("machine_payload");
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
						if (XElement.ReadFrom(reader) is XElement element)
						{
							string key = element.Attribute("name").Value;

							string title = "";
							string xml = element.ToString();
							string json = Tools.XML2JSON(element);
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
			DataTable listsTable = new DataTable("softwarelists_payload");
			listsTable.Columns.Add("key_1", typeof(string));
			listsTable.Columns.Add("title", typeof(string));
			listsTable.Columns.Add("xml", typeof(string));
			listsTable.Columns.Add("json", typeof(string));
			listsTable.Columns.Add("html", typeof(string));

			listsTable.Rows.Add("1", "", "", "", "");

			DataTable listTable = new DataTable("softwarelist_payload");
			listTable.Columns.Add("softwarelist_name", typeof(string));
			listTable.Columns.Add("title", typeof(string));
			listTable.Columns.Add("xml", typeof(string));
			listTable.Columns.Add("json", typeof(string));
			listTable.Columns.Add("html", typeof(string));

			DataTable softwareTable = new DataTable("software_payload");
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
						if (XElement.ReadFrom(reader) is XElement listElement)
						{
							string softwarelist_name = listElement.Attribute("name").Value;

							string title = "";
							string xml = listElement.ToString();
							string json = Tools.XML2JSON(listElement);
							string html = "";

							listTable.Rows.Add(softwarelist_name, title, xml, json, html);

							foreach (XElement element in listElement.Elements("software"))
							{
								string software_name = element.Attribute("name").Value;

								title = "";
								xml = element.ToString();
								json = Tools.XML2JSON(element);
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
				{
					int maxLength = table.Columns[primaryKeyName].MaxLength;
					if (maxLength == -1)
						maxLength = 32;

					columnDefs.Add($"[{primaryKeyName}] VARCHAR({maxLength})");
				}

				columnDefs.Add("[title] NVARCHAR(MAX)");
				columnDefs.Add("[xml] NVARCHAR(MAX)");
				columnDefs.Add("[json] NVARCHAR(MAX)");
				columnDefs.Add("[html] NVARCHAR(MAX)");

				columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ([{String.Join("], [", primaryKeyNames)}])");

				string commandText = $"CREATE TABLE [{table.TableName}] ({String.Join(", ", columnDefs)});";

				Console.WriteLine(commandText);
				Database.ExecuteNonQuery(targetConnection, commandText);

				Database.BulkInsert(targetConnection, table);
			}
		}

		//
		// HTML
		//

		public static int MakeMSSQLPayloadHtml(string serverConnectionString, string databaseNames, string dataSetName)
		{
			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });
			for (int index = 0; index < databaseNamesEach.Length; ++index)
				databaseNamesEach[index] = databaseNamesEach[index].Trim();

			using (SqlConnection machineConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[0]}';"))
			{
				MakeMSSQLPayloadHtmlMachine(machineConnection);

				using (SqlConnection softwareConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[1]}';"))
					MakeMSSQLPayloadHtmlSoftware(softwareConnection, machineConnection, dataSetName);
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

			string datasetName = (string)metaRow["dataset"];
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

						//if (machine_name != "bbcb")
						//	continue;

						StringBuilder html = new StringBuilder();

						//
						// Simple joins
						//
						foreach (string tableName in simpleTableNames)
						{
							if (dataSet.Tables.Contains(tableName) == false)
								continue;

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

											string baseUrl;
											switch (datasetName)
											{
												case "mame":
													baseUrl = $"https://github.com/mamedev/mame/blob/mame{version}/src";

													if (value.Split(new char[] { '/' }).Length == 2 && value.StartsWith("emu/") == false)
														value = $"<a href=\"{baseUrl}/{datasetName}/{value}\" target=\"_blank\">{value}</a>";
													else
														value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
													break;
												case "hbmame":
													baseUrl = $"https://github.com/Robbbert/hbmame/blob/tag{version.Substring(2).Replace(".", "")}/src/hbmame/drivers";

													value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
													break;

												default:
													throw new ApplicationException($"Unknown dataset: {datasetName}");
											}

											targetRow["sourcefile"] = value;
										}
										if (targetRow.IsNull("romof") == false)
										{
											string value = (string)targetRow["romof"];
											targetRow["romof"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										if (targetRow.IsNull("cloneof") == false)
										{
											string value = (string)targetRow["cloneof"];
											targetRow["cloneof"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										break;

									case "device_ref":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										break;


									case "softwarelist":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/{datasetName}/software/{value}\">{value}</a>";
										}
										break;
								}
							}

							if (tableName == "machine")
							{
								html.AppendLine("<br />");
								html.AppendLine($"<div><h2 style=\"display:inline;\">machine</h2> &bull; <a href=\"{machine_name}.xml\">XML</a> &bull; <a href=\"{machine_name}.json\">JSON</a> &bull; <a href=\"#\" onclick=\"mameAO('{machine_name}@{datasetName}'); return false\">AO</a></div>");
								html.AppendLine("<br />");
							}
							else
							{
								html.AppendLine("<hr />");
								html.AppendLine($"<h2>{tableName}</h2>");
							}

							html.AppendLine(Reports.MakeHtmlTable(table, null));
						}

						DataRow[] deviceRows = dataSet.Tables["device"].Select("machine_id = " + machine_id);
						if (deviceRows.Length > 0)
						{
							//	device, instance
							DataTable table = new DataTable();
							foreach (DataTable columnTable in new DataTable[] { dataSet.Tables["device"], dataSet.Tables["instance"] })
								foreach (DataColumn column in columnTable.Columns)
									if (column.ColumnName.EndsWith("_id") == false)
										table.Columns.Add(column.ColumnName, typeof(string));

							foreach (DataRow deviceRow in deviceRows)
							{
								long device_id = (long)deviceRow["device_id"];

								DataRow[] instanceRows = dataSet.Tables["instance"].Select("device_id = " + device_id);
								foreach (DataRow instanceRow in instanceRows)
								{
									DataRow row = table.NewRow();
									foreach (DataColumn column in deviceRow.Table.Columns)
										if (column.ColumnName.EndsWith("_id") == false)
											row[column.ColumnName] = deviceRow[column.ColumnName];

									foreach (DataColumn column in instanceRow.Table.Columns)
										if (column.ColumnName.EndsWith("_id") == false)
											row[column.ColumnName] = instanceRow[column.ColumnName];
									table.Rows.Add(row);
								}
							}

							if (table.Rows.Count > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>device, instance</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}

							//	device, extension
							table = new DataTable();
							foreach (DataColumn column in dataSet.Tables["device"].Columns)
								if (column.ColumnName.EndsWith("_id") == false)
									table.Columns.Add(column.ColumnName, typeof(string));
							table.Columns.Add("extension_names", typeof(string));

							foreach (DataRow deviceRow in deviceRows)
							{
								long device_id = (long)deviceRow["device_id"];

								DataRow[] extensionRows = dataSet.Tables["extension"].Select("device_id = " + device_id);

								DataRow row = table.NewRow();
								foreach (DataColumn column in deviceRow.Table.Columns)
									if (column.ColumnName.EndsWith("_id") == false)
										row[column.ColumnName] = deviceRow[column.ColumnName];

								row["extension_names"] = String.Join(", ", extensionRows.Select(r => (string)r["name"]));

								table.Rows.Add(row);
							}

							if (table.Rows.Count > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>device, extension</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}
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

							html.AppendLine("<hr />");
							html.AppendLine("<h2>input</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["input"], inputRows, null));

							DataRow[] controlRows = dataSet.Tables["control"].Select("input_id = " + input_id);
							if (controlRows.Length > 0)
							{
								html.AppendLine("<h3>control</h3>");
								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["control"], controlRows, null));
							}
						}

						//
						// port, analog
						//
						DataRow[] portRows = dataSet.Tables["port"].Select("machine_id = " + machine_id);
						if (portRows.Length > 0)
						{
							html.AppendLine("<hr />");
							html.AppendLine("<h2>port, analog</h2>");

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
							html.AppendLine("<hr />");
							html.AppendLine("<h2>slot, slotoption</h2>");

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
										row["slotoption_devname"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
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
							html.AppendLine("<hr />");
							html.AppendLine("<h2>configuration</h2>");

							foreach (DataRow configurationRow in configurationRows)
							{
								long configuration_id = (long)configurationRow["configuration_id"];

								html.AppendLine("<hr class='px2' />");

								html.AppendLine($"<h3>{(string)configurationRow["name"]}</h3>");

								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["configuration"], new[] { configurationRow }, null));

								if (dataSet.Tables.Contains("conflocation") == true)
								{
									DataRow[] conflocationRows = dataSet.Tables["conflocation"].Select("configuration_id = " + configuration_id);
									if (conflocationRows.Length > 0)
									{
										html.AppendLine("<h4>location</h4>");

										html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["conflocation"], conflocationRows, null));
									}
								}

								DataRow[] confsettingRows = dataSet.Tables["confsetting"].Select("configuration_id = " + configuration_id);
								if (confsettingRows.Length > 0)
								{
									html.AppendLine("<h4>setting</h4>");

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
							html.AppendLine("<hr />");
							html.AppendLine("<h2>dipswitch</h2>");

							foreach (DataRow dipswitchRow in dipswitchRows)
							{
								long dipswitch_id = (long)dipswitchRow["dipswitch_id"];

								html.AppendLine("<hr class='px2' />");

								html.AppendLine($"<h3>{(string)dipswitchRow["name"]}</h3>");

								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipswitch"], new[] { dipswitchRow }, null));

								DataRow[] diplocationRows = dataSet.Tables["diplocation"].Select("dipswitch_id = " + dipswitch_id);
								if (diplocationRows.Length > 0)
								{
									html.AppendLine("<h4>location</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["diplocation"], diplocationRows, null));
								}

								DataRow[] dipvalueRows = dataSet.Tables["dipvalue"].Select("dipswitch_id = " + dipswitch_id);
								if (dipvalueRows.Length > 0)
								{
									html.AppendLine("<h4>value</h4>");

									html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipvalue"], dipvalueRows, null));
								}
							}
						}

						command.Parameters["@title"].Value = $"{(string)machineRow["description"]} - {datasetName} ({version}) machine";
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

		public static void MakeMSSQLPayloadHtmlSoftware(SqlConnection connection, SqlConnection machineConnection, string dataSetName)
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
				if (dataSet.Tables.Contains("diskarea") == true)
				{
					foreach (DataColumn column in dataSet.Tables["diskarea"].Columns)
						if (column.ColumnName.EndsWith("_id") == false)
							diskTable.Columns.Add("data_" + column.ColumnName);
					foreach (DataColumn column in dataSet.Tables["disk"].Columns)
						if (column.ColumnName.EndsWith("_id") == false)
							diskTable.Columns.Add(column.ColumnName);
				}

				DataTable listTable = Tools.MakeDataTable(
					"name	description",
					"String	String"
				);

				foreach (DataRow softwarelistRow in dataSet.Tables["softwarelist"].Select(null, "description"))
				{
					long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
					string softwarelist_name = (string)softwarelistRow["name"];
					string softwarelist_description = (string)softwarelistRow["description"];

					//if (softwarelist_name != "x68k_flop" && softwarelist_name != "amiga_cd")
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
					
					html.AppendLine("<hr />");
					html.AppendLine("<h2>software</h2>");
					DataTable softwareTable = dataSet.Tables["software"].Clone();
					foreach (DataRow softwareRow in softwareRows)
					{
						softwareTable.ImportRow(softwareRow);
						DataRow row = softwareTable.Rows[softwareTable.Rows.Count - 1];
						string value = (string)row["name"];
						row["name"] = $"<a href=\"/{dataSetName}/software/{softwarelist_name}/{value}\">{value}</a>";
						if (softwareTable.Columns.Contains("cloneof") == true && row.IsNull("cloneof") == false)
						{
							value = (string)row["cloneof"];
							row["cloneof"] = $"<a href=\"/{dataSetName}/software/{softwarelist_name}/{value}\">{value}</a>";
						}
					}
					html.AppendLine(Reports.MakeHtmlTable(softwareTable, null));

					listTable.Rows.Add($"<a href=\"/{dataSetName}/software/{softwarelist_name}\">{softwarelist_name}</a>", softwarelist_description);

					softwarelistCommand.Parameters["@title"].Value = $"{softwarelist_description} - {dataSetName} ({version}) software list";
					softwarelistCommand.Parameters["@html"].Value = html.ToString();
					softwarelistCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;

					softwarelistCommand.ExecuteNonQuery();

					//
					// Software
					//

					softwarelistRow["name"] = $"<a href=\"/{dataSetName}/software/{softwarelist_name}\">{softwarelist_name}</a>";

					foreach (DataRow softwareRow in softwareRows)
					{
						long software_id = (long)softwareRow["software_id"];
						string software_name = (string)softwareRow["name"];

						if (softwareTable.Columns.Contains("cloneof") == true && softwareRow.IsNull("cloneof") == false)
						{
							string value = (string)softwareRow["cloneof"];
							softwareRow["cloneof"] = $"<a href=\"/{dataSetName}/software/{softwarelist_name}/{value}\">{value}</a>";
						}

						html = new StringBuilder();

						html.AppendLine("<br />");
						html.AppendLine($"<div><h2 style=\"display:inline;\">software</h2> &bull; <a href=\"{software_name}.xml\">XML</a> &bull; <a href=\"{software_name}.json\">JSON</a> </div>");
						html.AppendLine("<br />");
						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["software"], new[] { softwareRow }, null));

						html.AppendLine("<hr />");

						html.AppendLine("<h2>softwarelist</h2>");
						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new[] { softwarelistRow }, null));

						DataRow[] rows;

						if (dataSet.Tables.Contains("info") == true)
						{
							rows = dataSet.Tables["info"].Select($"software_id = {software_id}");
							if (rows.Length > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>info</h2>");
								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["info"], rows, null));
							}
						}

						if (dataSet.Tables.Contains("sharedfeat") == true)
						{
							rows = dataSet.Tables["sharedfeat"].Select($"software_id = {software_id}");
							if (rows.Length > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>sharedfeat</h2>");
								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["sharedfeat"], rows, null));
							}
						}

						DataRow[] partRows = dataSet.Tables["part"].Select($"software_id = {software_id}");
						if (partRows.Length > 0)
						{
							DataTable table;

							// part, feature
							if (dataSet.Tables.Contains("feature") == true)
							{
								table = Tools.MakeDataTable(
									"part_name	part_interface	feature_name	feature_value",
									"String		String			String			String"
								);

								foreach (DataRow partRow in partRows)
								{
									long part_id = (long)partRow["part_id"];
									string part_name = (string)partRow["name"];
									string part_interface = (string)partRow["interface"];

									foreach (DataRow featureRow in dataSet.Tables["feature"].Select($"part_id = {part_id}"))
										table.Rows.Add(part_name, part_interface, featureRow["name"], featureRow["value"]);
								}
								if (table.Rows.Count > 0)
								{
									html.AppendLine("<hr />");
									html.AppendLine("<h2>part, feature</h2>");
									html.AppendLine(Reports.MakeHtmlTable(table, null));
								}
							}

							// part, dataarea, rom
							table = Tools.MakeDataTable(
								"part_name	part_interface	dataarea_name	dataarea_size	dataarea_databits	dataarea_endian",
								"String		String			String			String			String				String"
							);
							foreach (DataColumn column in dataSet.Tables["rom"].Columns)
								if (column.ColumnName.EndsWith("_id") == false)
									table.Columns.Add(column.ColumnName, typeof(string));

							foreach (DataRow partRow in partRows)
							{
								long part_id = (long)partRow["part_id"];
								string part_name = (string)partRow["name"];
								string part_interface = (string)partRow["interface"];

								foreach (DataRow dataareaRow in dataSet.Tables["dataarea"].Select($"part_id = {part_id}"))
								{
									long dataarea_id = (long)dataareaRow["dataarea_id"];

									foreach (DataRow romRow in dataSet.Tables["rom"].Select($"dataarea_id = {dataarea_id}"))
									{
										DataRow row = table.Rows.Add(part_name, part_interface,
											(string)dataareaRow["name"], (string)dataareaRow["size"], (string)dataareaRow["databits"], (string)dataareaRow["endian"]);

										foreach (DataColumn column in dataSet.Tables["rom"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												row[column.ColumnName] = romRow[column.ColumnName];
									}
								}
							}
							if (table.Rows.Count > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>part, dataarea, rom</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}

							// part, diskarea, disk
							if (dataSet.Tables.Contains("disk") == true)
							{
								table = Tools.MakeDataTable(
									"part_name	part_interface	diskarea_name",
									"String		String			String"
								);
								foreach (DataColumn column in dataSet.Tables["disk"].Columns)
									if (column.ColumnName.EndsWith("_id") == false)
										table.Columns.Add(column.ColumnName, typeof(string));

								foreach (DataRow partRow in partRows)
								{
									long part_id = (long)partRow["part_id"];
									string part_name = (string)partRow["name"];
									string part_interface = (string)partRow["interface"];

									foreach (DataRow diskareaRow in dataSet.Tables["diskarea"].Select($"part_id = {part_id}"))
									{
										long diskarea_id = (long)diskareaRow["diskarea_id"];

										foreach (DataRow diskRow in dataSet.Tables["disk"].Select($"diskarea_id = {diskarea_id}"))
										{
											DataRow row = table.Rows.Add(part_name, part_interface, (string)diskareaRow["name"]);

											foreach (DataColumn column in dataSet.Tables["disk"].Columns)
												if (column.ColumnName.EndsWith("_id") == false)
													row[column.ColumnName] = diskRow[column.ColumnName];
										}
									}
								}
								if (table.Rows.Count > 0)
								{
									html.AppendLine("<hr />");
									html.AppendLine("<h2>part, diskarea, disk</h2>");
									html.AppendLine(Reports.MakeHtmlTable(table, null));
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

									machinesTable.Rows.Add($"<a href=\"/{dataSetName}/machine/{name}\">{name}</a>", $"<a href=\"#\" onclick=\"mameAO('{name}@{dataSetName} {software_name}@{softwarelist_name}'); return false\">{description}</a>");
								}

								html.AppendLine("<hr />");
								html.AppendLine($"<h2>machines ({status})</h2>");
								html.AppendLine(Reports.MakeHtmlTable(machinesTable, null));
							}
						}

						softwareCommand.Parameters["@title"].Value = $"{(string)softwareRow["description"]} - {(string)softwarelistRow["description"]} - {dataSetName} ({version}) software";
						softwareCommand.Parameters["@html"].Value = html.ToString();
						softwareCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;
						softwareCommand.Parameters["@software_name"].Value = software_name;

						softwareCommand.ExecuteNonQuery();
					}
				}

				SqlCommand softwarelistsCommand = new SqlCommand("UPDATE softwarelists_payload SET [title] = @title, [html] = @html WHERE [key_1] = '1'", connection);
				softwarelistsCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwarelistsCommand.Parameters.Add("@html", SqlDbType.NVarChar);

				softwarelistsCommand.Parameters["@title"].Value = $"Software Lists - {dataSetName} ({version}) software";
				softwarelistsCommand.Parameters["@html"].Value = Reports.MakeHtmlTable(listTable, null);

				softwarelistsCommand.ExecuteNonQuery();
			}
			finally
			{
				connection.Close();
			}

		}

		public static int GetTosec(string directory, string version)
		{
			string url = "https://www.tosecdev.org/downloads/category/22-datfiles";
			string html = Tools.Query(url);
			
			string find;
			int index;

			SortedDictionary<string, string> downloadPageUrls = new SortedDictionary<string, string>();

			using (StringReader reader  = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (line.Length == 0)
						continue;

					find = "<div class=\"pd-subcategory\"><a href=\"";

					index = line.IndexOf(find);
					if (index != -1)
					{
						line = line.Substring(index + find.Length);

						index = line.IndexOf("\"");
						if (index != -1)
						{
							line = line.Substring(0, index);

							index = line.LastIndexOf("/");
							if (index != -1)
							{
								string key = line.Substring(index + 1);
								index = key.IndexOf("-");
								key = key.Substring(index + 1);

								downloadPageUrls.Add(key, new Uri(new Uri(url), line).AbsoluteUri);
							}
						}
					}
				}
			}

			if (downloadPageUrls.Count == 0)
				throw new ApplicationException("Did not find any TOSEC links.");

			if (version == "0")
				version = downloadPageUrls.Keys.Last();

			if (downloadPageUrls.ContainsKey(version) == false)
				throw new ApplicationException($"Did not find TOSEC version: {version}");

			directory = Path.Combine(directory, version);
			Directory.CreateDirectory(directory);

			if (Directory.Exists(Path.Combine(directory, "TOSEC")) == true)
				return 0;

			url = downloadPageUrls[version];
			html = Tools.Query(url);

			find = "<a class=\"btn btn-success\" href=\"";
			index = html.IndexOf(find);
			html = html.Substring(index + find.Length);

			index = html.IndexOf("\"");
			html = html.Substring(0, index);

			url = new Uri(new Uri(url), html).AbsoluteUri;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string zipFilename = Path.Combine(tempDir.Path, "tosec.zip");

				Console.Write($"Downloading {url} {zipFilename} ...");
				Tools.Download(url, zipFilename, 1);
				Console.WriteLine("...done");

				Console.Write($"Extract ZIP {zipFilename} {directory} ...");
				ZipFile.ExtractToDirectory(zipFilename, directory);
				Console.WriteLine("...done");
			}

			return 1;
		}

		public static int MakeTosecSQLite(string directory, string version)
		{
			if (version == "0")
				version = TosecGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string sqlLiteFilename = Path.Combine(directory, "_tosec.sqlite");

			if (File.Exists(sqlLiteFilename) == true)
				return 0;

			DataSet dataSet = TosecDataSet(directory);

			string connectionString = $"Data Source='{sqlLiteFilename}';datetimeformat=CurrentCulture;";

			Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
			Database.DatabaseFromXML("tosec", connectionString, dataSet);
			Console.WriteLine("... done");

			return 1;
		}

		public static int MakeTosecMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = TosecGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = TosecDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}

		public static DataSet TosecDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			string[] categories = new string[] { "TOSEC", "TOSEC-ISO", "TOSEC-PIX" };

			foreach (string category in categories)
			{
				string groupDirectory = Path.Combine(directory, category);

				foreach (string filename in Directory.GetFiles(groupDirectory, "*.dat"))
				{
					string name = Path.GetFileNameWithoutExtension(filename);

					int index;

					index = name.LastIndexOf("(");
					if (index == -1)
						throw new ApplicationException("No last index of open bracket");

					string fileVersion = name.Substring(index).Trim(new char[] { '(', ')' });
					name = name.Substring(0, index).Trim();

					Console.WriteLine($"{category}\t{name}\t{fileVersion}");

					XElement document = XElement.Load(filename);
					DataSet fileDataSet = new DataSet();
					ReadXML.ImportXMLWork(document, fileDataSet, null, null);

					DataFileMoveHeader(fileDataSet);

					foreach (DataTable table in dataSet.Tables)
						foreach (DataColumn column in table.Columns)
							column.AutoIncrement = false;

					DataFileMergeDataSet(fileDataSet, dataSet);
				}
			}

			return dataSet;
		}

		public static string TosecGetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
				versions.Add(Path.GetFileName(versionDirectory));

			if (versions.Count == 0)
				throw new ApplicationException($"No TOSEC versions found in '{directory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}

		public static void DataFileMoveHeader(DataSet dataSet)
		{
			DataTable headerTable = dataSet.Tables["header"];
			DataTable datafileTable = dataSet.Tables["datafile"];

			if (headerTable == null || headerTable.Rows.Count != 1)
				throw new ApplicationException("Did not find one headerTable row");

			if (datafileTable == null || datafileTable.Rows.Count != 1)
				throw new ApplicationException("Did not find one datafileTable row");

			foreach (DataColumn column in headerTable.Columns)
			{
				if (column.ColumnName.EndsWith("_id") == true)
					continue;

				if (datafileTable.Columns.Contains(column.ColumnName) == false)
					datafileTable.Columns.Add(column.ColumnName, typeof(string));

				datafileTable.Rows[0][column.ColumnName] = headerTable.Rows[0][column.ColumnName];
			}

			dataSet.Tables.Remove("header");
		}

		public static void DataFileMergeDataSet(DataSet sourceDataSet, DataSet targetDataSet)
		{
			foreach (DataTable sourceTable in sourceDataSet.Tables)
			{
				sourceTable.PrimaryKey = new DataColumn[0];

				DataTable targetTable = null;
				if (targetDataSet.Tables.Contains(sourceTable.TableName) == false)
				{
					targetTable = new DataTable(sourceTable.TableName);
					targetDataSet.Tables.Add(targetTable);
				}
				else
				{
					targetTable = targetDataSet.Tables[sourceTable.TableName];
				}

				foreach (DataColumn column in sourceTable.Columns)
				{
					column.Unique = false;

					if (targetTable.Columns.Contains(column.ColumnName) == false)
					{
						DataColumn targetColumn = targetTable.Columns.Add(column.ColumnName, column.DataType);
						targetColumn.Unique = false;
					}
				}
			}

			Dictionary<string, long> addIds = new Dictionary<string, long>();
			foreach (DataTable sourceTable in sourceDataSet.Tables)
				addIds.Add(sourceTable.TableName + "_id", targetDataSet.Tables[sourceTable.TableName].Rows.Count);

			foreach (DataTable sourceTable in sourceDataSet.Tables)
			{
				foreach (DataColumn column in sourceTable.Columns)
				{
					if (column.ColumnName.EndsWith("_id") == false)
						continue;

					foreach (DataRow row in sourceTable.Rows)
						row[column] = (long)row[column] + addIds[column.ColumnName];
				}

				DataTable targetTable = targetDataSet.Tables[sourceTable.TableName];

				foreach (DataRow row in sourceTable.Rows)
					targetTable.ImportRow(row);
			}
		}

		public static int MakeTosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName, string assemblyVersion)
		{
			if (version == "0")
				version = TosecGetLatestDownloadedVersion(directory);

			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			Dictionary<long, string> datafileUrls = GetTosecDatafileUrls(serverConnectionString, databaseName);
			Dictionary<long, string> gameUrls = GetTosecGameUrls(serverConnectionString, databaseName);

			//
			//	Metadata table
			//
			DataTable table = CreateMetaDataTable(serverConnectionString, databaseName);

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				string info = $"TOSEC: {version} - Datafiles: {datafileCount} - Games: {gameCount} - rom: {softRomCount}";

				table.Rows.Add(1L, "tosec", "", version, info, DateTime.Now, agent);
				Database.BulkInsert(connection, table);
			}

			//
			//	Payloads
			//
			DataTable category_payload_table = Tools.MakeDataTable("category_payload",
				"category	title	xml		json	html",
				"String		String	String	String	String");
			category_payload_table.Columns["category"].MaxLength = 9;

			DataTable datafile_payload_table = Tools.MakeDataTable("datafile_payload",
				"category	name	title	xml		json	html",
				"String		String	String	String	String	String");
			datafile_payload_table.Columns["category"].MaxLength = 9;
			datafile_payload_table.Columns["name"].MaxLength = 128;

			DataTable game_payload_table = Tools.MakeDataTable("game_payload",
				"category	datafile_name	game_name	title	xml		json	html",
				"String		String			String		String	String	String	String");
			game_payload_table.Columns["category"].MaxLength = 9;
			game_payload_table.Columns["datafile_name"].MaxLength = 128;
			game_payload_table.Columns["game_name"].MaxLength = 256;

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				//
				//	Datafix for duplicates in source data
				//
				DataTable dupTable = Database.ExecuteFill(connection,
					"SELECT datafile.category, datafile.name AS datafile_name, game.name AS game_name FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id " +
					"GROUP BY datafile.category, datafile.name, game.name HAVING (COUNT(*) > 1)");

				List<long> dup_game_ids = new List<long>();
				foreach (DataRow dupRow in dupTable.Rows)
				{
					using (SqlCommand command = new SqlCommand("SELECT game.game_id FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id "
						+ "WHERE ((datafile.category = @category) AND (datafile.[name] = @datafile_name) AND (game.[name] = @game_name))", connection))
					{
						command.Parameters.AddWithValue("@category", (string)dupRow["category"]);
						command.Parameters.AddWithValue("@datafile_name", (string)dupRow["datafile_name"]);
						command.Parameters.AddWithValue("@game_name", (string)dupRow["game_name"]);

						DataTable dupIdTable = new DataTable();
						using (SqlDataAdapter adapter = new SqlDataAdapter(command))
							adapter.Fill(dupIdTable);

						for (int index = 1; index < dupIdTable.Rows.Count; ++index)
							dup_game_ids.Add((long)dupIdTable.Rows[index]["game_id"]);
					}
				}

				string game_commandText = "SELECT * from [game] @WHERE ORDER BY [name]";
				if (dup_game_ids.Count > 0)
				{
					Console.WriteLine($"!!! Warning Duplicate TOSEC games: {String.Join(", ", dup_game_ids)}");
					game_commandText = game_commandText.Replace("@WHERE", $"WHERE ([game_id] NOT IN ({String.Join(", ", dup_game_ids)}))");
				}
				else
				{
					game_commandText = game_commandText.Replace("@WHERE", "");
				}

				//
				//	Get all data
				//
				DataTable datafileTable = Database.ExecuteFill(connection, "SELECT * FROM [datafile] ORDER BY [name]");
				DataTable gameTable = Database.ExecuteFill(connection, game_commandText);
				DataTable romTable = Database.ExecuteFill(connection, "SELECT * FROM [rom] ORDER BY [name]");

				//
				//	Build payloads
				//
				foreach (string category in new string[] { "TOSEC", "TOSEC-ISO", "TOSEC-PIX" })
				{
					StringBuilder category_html = new StringBuilder();
					
					string category_title = $"TOSEC Data Files ({category} {version})";

					category_html.AppendLine($"<h2>{category}</h2>");
					category_html.AppendLine("<table>");
					category_html.AppendLine("<tr><th>Name</th><th>Version</th><th>Game Count</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

					foreach (DataRow datafileRow in datafileTable.Select($"[category] = '{category}'"))
					{
						long datafile_id = (long)datafileRow["datafile_id"];
						string datafile_name = (string)datafileRow["name"];
						string datafile_version = (string)datafileRow["version"];
						string datafile_name_enc = Uri.EscapeDataString(datafile_name);

						long datafile_game_count = 0;
						long datafile_rom_count = 0;
						long datafile_rom_size = 0;

						long game_url_count = 0;

						Dictionary<string, int> datafileExtentions = new Dictionary<string, int>();

						StringBuilder datafile_html = new StringBuilder();

						string datafile_title = $"{datafile_name} ({category} {datafile_version})";

						datafile_html.AppendLine($"<h2>{datafile_title}</h2>");
						datafile_html.AppendLine("<table>");
						datafile_html.AppendLine("<tr><th>Name</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

						foreach (DataRow gameRow in gameTable.Select($"[datafile_id] = {datafile_id}"))
						{
							long game_id = (long)gameRow["game_id"];
							string game_name = (string)gameRow["name"];
							string game_name_enc = Uri.EscapeDataString(game_name);

							long game_rom_count = 0;
							long game_rom_size = 0;

							long rom_url_count = 0;

							Dictionary<string, int> gameExtentions = new Dictionary<string, int>();

							++datafile_game_count;

							StringBuilder game_html = new StringBuilder();

							string game_title = $"{datafile_name} - {game_name} ({category} {datafile_version})";
							game_html.AppendLine($"<h2>{game_title}</h2>");

							game_html.AppendLine("<table>");
							game_html.AppendLine("<tr><th>Name</th><th>Size</th><th>Size Bytes</th><th>CRC32</th><th>MD5</th><th>SHA1</th><th>IA File</th></tr>");

							foreach (DataRow romRow in romTable.Select($"[game_id] = {game_id}"))
							{
								string rom_name = (string)romRow["name"];
								string rom_extention = Path.GetExtension(rom_name).ToLower();
								long rom_size = Int64.Parse((string)romRow["size"]);
								string crc = rom_size == 0 ? "" : (string)romRow["crc"];
								string md5 = rom_size == 0 ? "" : (string)romRow["md5"];
								string sha1 = rom_size == 0 ? "" : (string)romRow["sha1"];

								datafile_rom_count += 1;
								datafile_rom_size += rom_size;

								foreach (var extentions in new Dictionary<string, int>[] { datafileExtentions, gameExtentions })
								{
									if (extentions.ContainsKey(rom_extention) == false)
										extentions[rom_extention] = 0;
									extentions[rom_extention] += 1;
								}

								game_rom_count += 1;
								game_rom_size += rom_size;

								string rom_url = "";
								if (datafileUrls.ContainsKey(datafile_id) == true)
								{
									rom_url = datafileUrls[datafile_id];
									rom_url = $"{rom_url}/{Uri.EscapeDataString(game_name)}%2F{Uri.EscapeDataString(rom_name)}";
									rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
								}
								if (gameUrls.ContainsKey(game_id) == true)
								{
									rom_url = gameUrls[game_id];
									rom_url = $"{rom_url}/{Uri.EscapeDataString(rom_name)}";
									rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
								}
								if (rom_url != "")
									rom_url_count += 1;

								game_html.AppendLine($"<tr><td>{rom_name}</td><td>{Tools.DataSize(rom_size)}</td><td>{rom_size}</td><td>{crc}</td><td>{md5}</td><td>{sha1}</td><td>{rom_url}</td></tr>");
							}

							game_html.AppendLine("</table>");
							game_payload_table.Rows.Add(category.ToLower(), datafile_name, game_name, game_title, "", "", game_html.ToString());

							string game_extentions = TosecExtentionsLink(gameExtentions);

							string game_url = "";
							if (gameUrls.ContainsKey(game_id) == true)
							{
								game_url = gameUrls[game_id];
								game_url = $"<a href=\"{game_url}\">{Path.GetExtension(game_url)}</a>";
								game_url_count += 1;
							}
							else
							{
								if (rom_url_count > 0)
									game_url = $"{rom_url_count}";
							}

							datafile_html.AppendLine($"<tr><td><a href=\"{datafile_name_enc}/{game_name_enc}\">{game_name}</a></td><td>{game_rom_count}</td><td>{Tools.DataSize(game_rom_size)}</td><td>{game_rom_size}</td><td>{game_extentions}</td><td>{game_url}</td></tr>");
						}

						datafile_html.AppendLine("</table>");
						datafile_payload_table.Rows.Add(category.ToLower(), datafile_name, datafile_title, "", "", datafile_html.ToString());

						string datafile_extentions = TosecExtentionsLink(datafileExtentions);

						string datafile_url = "";
						if (datafileUrls.ContainsKey(datafile_id) == true)
						{
							datafile_url = datafileUrls[datafile_id];
							datafile_url = $"<a href=\"{datafile_url}\">{Path.GetExtension(datafile_url)}</a>";
						}
						else
						{
							if (game_url_count > 0)
								datafile_url = $"{game_url_count}";
						}

						category_html.AppendLine($"<tr><td><a href=\"{category.ToLower()}/{datafile_name_enc}\">{datafile_name}</a></td><td>{datafile_version}</td><td>{datafile_game_count}</td><td>{datafile_rom_count}</td><td>{Tools.DataSize(datafile_rom_size)}</td><td>{datafile_rom_size}</td><td>{datafile_extentions}</td><td>{datafile_url}</td></tr>");
					}

					category_html.AppendLine("</table>");
					category_payload_table.Rows.Add(category.ToLower(), category_title, "", "", category_html.ToString());
				}

				datafileTable = null;
				gameTable = null;
				romTable = null;

				GC.Collect();

				MakeMSSQLPayloadsInsert(category_payload_table, serverConnectionString, databaseName, new string[] { "category" });
				MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "category", "name" });
				MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "category", "datafile_name", "game_name" });
			}
			
			return 0;
		}

		private static string TosecExtentionsLink(Dictionary<string, int> sourceExtentions)
		{
			var extentions = sourceExtentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();
			if (extentions.Count() > 10)
			{
				int remainingCount = 0;
				foreach (int count in extentions.Skip(10).Select(pair => pair.Value))
					remainingCount += count;

				sourceExtentions = new Dictionary<string, int>();
				foreach (var pair in extentions.Take(10))
					sourceExtentions.Add(pair.Key, pair.Value);

				sourceExtentions.Add("....", remainingCount);

				extentions = sourceExtentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();
			}

			return String.Join(", ", extentions.Select(pair => $"{pair.Key}({pair.Value})"));
		}

		public static Dictionary<long, string> GetTosecDatafileUrls(string serverConnectionString, string databaseName)
		{
			Dictionary<string, string[]> categorySources = new Dictionary<string, string[]>() {
				{ "TOSEC", new string[] { "tosec-main" } },
				{ "TOSEC-PIX", new string[] { "tosec-pix-part2", "tosec-pix" } }
			};

			Dictionary<long, string> datafileUrls = new Dictionary<long, string>();

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				foreach (string category in categorySources.Keys)
				{
					DataTable table = Database.ExecuteFill(connection, $"SELECT [datafile_id], [name], [description] FROM [datafile] WHERE ([category] = '{category}') ORDER BY [name]");
					
					table.Columns.Add("status", typeof(string));
					table.Columns.Add("url", typeof(string));

					foreach (string itemKey in categorySources[category])
					{
						ArchiveOrgItem item = new ArchiveOrgItem(itemKey, null, null)
						{
							DontIgnore = true
						};
						item.GetFile(null);

						foreach (DataRow row in table.Rows)
						{
							long datafile_id = (long)row["datafile_id"];
							//string datafile_name = (string)row["name"];
							string datafile_description = (string)row["description"];

							string match = $"/{datafile_description}";

							var keys = item.Files.Keys.Where(key => key.EndsWith(match));

							if (keys.Any() == false)
								continue;

							if (keys.Take(2).Count() > 1)
								throw new ApplicationException($"Matched many items: {match}");

							ArchiveOrgFile file = item.GetFile(keys.First());

							string url = item.UrlDownload + String.Join("/", item.DownloadLink(file).Substring(item.UrlDownload.Length).Split('/').Select(part => Uri.EscapeDataString(part)));

							row["status"] = item.Key;
							row["url"] = url;

							if (datafileUrls.ContainsKey(datafile_id) == false)
								datafileUrls.Add(datafile_id, url);
							else
								Console.WriteLine($"Matched before: {category} {datafile_description}");

						}
					}
					//Tools.PopText(table);
				}
			}

			return datafileUrls;
		}

		public static Dictionary<long, string> GetTosecGameUrls(string serverConnectionString, string databaseName)
		{
			Dictionary<string, string[]> categorySources = new Dictionary<string, string[]>(){
				{ "TOSEC-ISO", new string[] {
					"noaen-tosec-iso-3do",
					"noaen-tosec-iso-acorn",
					"noaen-tosec-iso-american-laser-games",
					"noaen-tosec-iso-apple",
					"noaen-tosec-iso-atari",
					"noaen-tosec-iso-bandai",
					"noaen-tosec-iso-capcom",
					"noaen-tosec-iso-commodore-amiga",
					"noaen-tosec-iso-commodore-amiga-cd32",
					"noaen-tosec-iso-commodore-amiga-cdtv",
					"noaen-tosec-iso-commodore-c64",
					"noaen-tosec-iso-fujitsu",
					"noaen-tosec-iso-ibm",
					"noaen-tosec-iso-incredible-technologies",
					"noaen-tosec-iso-konami",
					"noaen-tosec-iso-mattel",
					"noaen-tosec-iso-memorex",
					"noaen-tosec-iso-nec",
					"noaen-tosec-iso-nintendo",
					"noaen-tosec-iso-philips",
					"noaen-tosec-iso-snk",
					"noaen-tosec-iso-sega-32x",
					"noaen-tosec-iso-sega-chihiro",
					"noaen-tosec-iso-sega-dreamcast-applications",
					"noaen-tosec-iso-sega-dreamcast-firmware",
					"noaen-tosec-iso-sega-dreamcast-games-dev-builds",
					"noaen-tosec-iso-sega-dreamcast-games-jp",
					"noaen-tosec-iso-sega-dreamcast-games-pal",
					"noaen-tosec-iso-sega-dreamcast-games-us",
					"noaen-tosec-iso-sega-dreamcast-homebrew",
					"noaen-tosec-iso-sega-dreamcast-multimedia",
					"noaen-tosec-iso-sega-dreamcast-samplers",
					"noaen-tosec-iso-sega-dreamcast-various-unverified-dumps",
					"noaen-tosec-iso-sega-mega-cd-sega-cd",
					"noaen-tosec-iso-sega-naomi",
					"noaen-tosec-iso-sega-naomi-2",
					"noaen-tosec-iso-sega-saturn",
					"noaen-tosec-iso-sega-wondermega",
					"noaen-tosec-iso-sinclair",
					"noaen-tosec-iso-sony",
					"noaen-tosec-iso-tomy",
					"noaen-tosec-iso-vm-labs",
					"noaen-tosec-iso-vtech",
					"noaen-tosec-iso-zapit-games",
					}
				}
			};

			Dictionary<long, string> gameUrls = new Dictionary<long, string>();

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				foreach (string category in categorySources.Keys)
				{
					DataTable table = Database.ExecuteFill(connection,
						"SELECT datafile.datafile_id, datafile.name AS datafile_name, datafile.description AS datafile_description, game.game_id, game.name AS game_name, game.description AS game_description " +
						$"FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id WHERE (datafile.category = '{category}') ORDER BY datafile.name, game.name");

					table.Columns.Add("status", typeof(string));
					table.Columns.Add("url", typeof(string));

					foreach (string itemKey in categorySources[category])
					{
						ArchiveOrgItem item = new ArchiveOrgItem(itemKey, null, null)
						{
							DontIgnore = true
						};
						item.GetFile(null);

						// Data Fix - parent directory mismatch
						if (itemKey == "noaen-tosec-iso-sony")
						{
							foreach (string oldKey in item.Files.Keys.Where(key => key.Contains("/[BIN]/")).ToArray())
							{
								string newKey = oldKey.Replace("/[BIN]/", "/[BIN-CUE]/");
								item.Files.Add(newKey, item.Files[oldKey]);
								item.Files.Remove(oldKey);
							}
						}

						foreach (DataRow row in table.Rows)
						{
							long game_id = (long)row["game_id"];
							string game_name = (string)row["game_name"];

							string match = $"/{game_name}";

							var keys = item.Files.Keys.Where(key => key.EndsWith(match));

							if (keys.Any() == false)
								continue;

							if (keys.Take(2).Count() > 1)
							{
								string datafile_name = (string)row["datafile_name"];

								keys = keys.Where(key => {
									string[] parts = key.Split('/');
									return datafile_name.Contains(parts[parts.Length - 2]);
								});

								if (keys.Any() == false)
									throw new ApplicationException("had it and lost it");

								if (keys.Count() > 1)
									throw new ApplicationException($"Matched many files: {match}");
							}

							ArchiveOrgFile file = item.GetFile(keys.First());

							string url = item.UrlDownload + String.Join("/", item.DownloadLink(file).Substring(item.UrlDownload.Length).Split('/').Select(part => Uri.EscapeDataString(part)));

							row["status"] = item.Key;
							row["url"] = url;

							if (gameUrls.ContainsKey(game_id) == false)
								gameUrls.Add(game_id, url);
							else
								Console.WriteLine($"Matched before: {category} {game_name}");
						}
					}
					//Tools.PopText(table);
				}
			}

			return gameUrls;
		}

		public static int GetHbMame(string directory, string version)
		{
			ICore core = new CoreHbMame();
			core.Initialize(directory, version);
			return core.Get();
		}

		public static int MakeHbMameXML(string directory, string version)
		{
			ICore core = new CoreHbMame();
			core.Initialize(directory, version);
			core.Xml();
			return 0;
		}

		public static int MakeHbMameSQLite(string directory, string version)
		{
			ICore core = new CoreHbMame();
			core.Initialize(directory, version);
			core.SQLite();
			return 0;
		}

		public static int MakeHbMameMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = HbMameGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(directory, "_machine.xml"),
				Path.Combine(directory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XElement document = XElement.Load(sourceXmlFilename);
				DataSet dataSet = new DataSet();
				ReadXML.ImportXMLWork(document, dataSet, null, null);

				Database.DataSet2MSSQL(dataSet, serverConnectionString, targetDatabaseName);

				Database.MakeForeignKeys(serverConnectionString, targetDatabaseName);
			}

			return 0;
		}

		public static int MakeHbMameMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseNames, string assemblyVersion)
		{
			if (version == "0")
				version = HbMameGetLatestDownloadedVersion(directory);

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
			xmlFilename = Path.Combine(versionDirectory, "_machine.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[0], "hbmame", "machine", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsMachine(xmlFilename, serverConnectionString, databaseName);

			//
			// software
			//

			databaseName = databaseNamesEach[1];
			xmlFilename = Path.Combine(versionDirectory, "_software.xml");

			MakeMSSQLPayloadsInfoTable(version, serverConnectionString, databaseNamesEach[1], "hbmame", "software", assemblyVersion, versionDirectory);

			MakeMSSQLPayloadsSoftware(xmlFilename, serverConnectionString, databaseName);

			return 0;
		}


		public static string HbMameGetLatestDownloadedVersion(string directory)
		{
			SortedDictionary<int, string> versions = new SortedDictionary<int, string>();
			string version;

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				version = Path.GetFileName(versionDirectory);

				if (version.StartsWith("0.") == false)
					continue;

				if (File.Exists(Path.Combine(versionDirectory, "hbmame.exe")) == false)
					continue;

				string[] parts = version.Split('.');

				if (parts.Length != 3)
					continue;

				versions.Add(Int32.Parse(parts[2]), version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No MAME versions found in '{directory}'.");

			version = versions[versions.Keys.Last()];

			Console.WriteLine($"HBMAME Version: {version}");

			return version;
		}

		public static int GetFBNeo(string directory, string version)
		{
			dynamic releases = JsonConvert.DeserializeObject<dynamic>(Tools.Query("https://api.github.com/repos/finalburnneo/FBNeo/releases"));

			string downloadUrl = null;
			version = null;

			foreach (dynamic release in releases)
			{
				if ((string)release.name == "nightly builds")
				{
					version = ((DateTime)release.published_at).ToString("s").Replace(":", "-");

					foreach (dynamic asset in release.assets)
					{
						if ((string)asset.name == "windows-x86_64.zip")
							downloadUrl = (string)asset.browser_download_url;
					}
				}
			}

			if (downloadUrl == null)
				throw new ApplicationException("Did not find download asset");

			directory = Path.Combine(directory, version);
			Directory.CreateDirectory(directory);

			if (File.Exists(Path.Combine(directory, "fbneo64.exe")) == true)
				return 0;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "fbneo.zip");

				Console.Write($"Downloading {downloadUrl} {archiveFilename} ...");
				Tools.Download(downloadUrl, archiveFilename, 1);
				Console.WriteLine("...done");

				Console.Write($"Extract 7-Zip {archiveFilename} {directory} ...");
				ZipFile.ExtractToDirectory(archiveFilename, directory);
				Console.WriteLine("...done");
			}

			return 1;
		}

		public static int MakeFBNeoXML(string directory, string version)
		{
			if (version == "0")
				version = FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string iniFileData = $"nIniVersion 0x7FFFFF{Environment.NewLine}bSkipStartupCheck 1{Environment.NewLine}";
			string configDirectory = Path.Combine(directory, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(Path.Combine(configDirectory, "fbneo64.ini"), iniFileData);

			//	https://github.com/finalburnneo/FBNeo/blob/master/src/burner/win32/main.cpp

			string[] systems = new string[] { "arcade", "channelf", "coleco", "fds", "gg", "md", "msx", "neogeo", "nes", "ngp", "pce", "sg1000", "sgx", "sms", "snes", "spectrum", "tg16" };

			foreach (string system in  systems)
			{
				string arguments = system == "arcade" ? "-listinfo" : $"-listinfo{system}only";

				StringBuilder output = new StringBuilder();

				ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(directory, "fbneo64.exe"))
				{
					Arguments = arguments,
					WorkingDirectory = directory,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					StandardOutputEncoding = Encoding.UTF8,
				};

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => output.AppendLine(e.Data));
					process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => Console.WriteLine(e.Data));

					process.Start();
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					process.WaitForExit();

					if (process.ExitCode != 0)
						throw new ApplicationException($"FBNeo Extract bad exit code: {process.ExitCode}");
				}

				string filename = Path.Combine(directory, $"_{system}.xml");
				File.WriteAllText(filename, output.ToString(), Encoding.UTF8);
			}

			return 1;
		}

		public static int MakeFBNeoSQLite(string directory, string version)
		{
			if (version == "0")
				version = FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string sqlLiteFilename = Path.Combine(directory, "_fbneo.sqlite");

			if (File.Exists(sqlLiteFilename) == true)
				return 0;

			DataSet dataSet = FBNeoDataSet(directory);

			string connectionString = $"Data Source='{sqlLiteFilename}';datetimeformat=CurrentCulture;";

			Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
			Database.DatabaseFromXML("fbneo", connectionString, dataSet);
			Console.WriteLine("... done");

			return 1;
		}

		public static int MakeFBNeoMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = FBNeoDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}

		public static DataSet FBNeoDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				string name = Path.GetFileNameWithoutExtension(filename);
				if (name.StartsWith("_") == false)
					continue;
				name = name.Substring(1);

				Console.WriteLine(name);

				XElement document = XElement.Load(filename);

				XElement clrmamepro = document.Element("header").Element("clrmamepro");
				if (clrmamepro != null)
					clrmamepro.Remove();

				DataSet fileDataSet = new DataSet();
				ReadXML.ImportXMLWork(document, fileDataSet, null, null);

				DataFileMoveHeader(fileDataSet);

				string key = name;
				if (key == "gg")
					key = "gamegear";
				if (key == "md")
					key = "megadrive";

				DataTable datafileTable = fileDataSet.Tables["datafile"];
				datafileTable.Columns.Add("key_argument", typeof(string));
				datafileTable.Rows[0]["key_argument"] = name;
				datafileTable.Columns.Add("key", typeof(string));
				datafileTable.Rows[0]["key"] = key;

				foreach (DataTable table in dataSet.Tables)
					foreach (DataColumn column in table.Columns)
						column.AutoIncrement = false;

				DataFileMergeDataSet(fileDataSet, dataSet);
			}

			return dataSet;
		}

		public static string FBNeoGetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string version = Path.GetFileName(versionDirectory);

				if (File.Exists(Path.Combine(versionDirectory, "fbneo64.exe")) == false)
					continue;

				versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No FBNeo versions found in '{directory}'.");

			versions.Sort();

			return versions.Last();
		}

		public static int MakeFBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName, string assemblyVersion)
		{
			if (version == "0")
				version = FBNeoGetLatestDownloadedVersion(directory);

			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			//
			//	Metadata table
			//
			DataTable table = CreateMetaDataTable(serverConnectionString, databaseName);

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				string info = $"FBNeo: {version} - datafiles: {datafileCount} - games: {gameCount} - roms: {softRomCount}";

				table.Rows.Add(1L, "fbneo", "", version, info, DateTime.Now, agent);
				Database.BulkInsert(connection, table);
			}

			DataTable datafile_payload_table = Tools.MakeDataTable("datafile_payload",
				"key	title	xml		json	html",
				"String	String	String	String	String");

			DataTable game_payload_table = Tools.MakeDataTable("game_payload",
				"datafile_key	game_name	title	xml		json	html",
				"String			String		String	String	String	String");

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				DataTable datafileTable = Database.ExecuteFill(connection, $"SELECT * FROM [datafile] ORDER BY [name]");
				DataTable gameTable = Database.ExecuteFill(connection, $"SELECT * FROM [game] ORDER BY [name]");
				DataTable romTable = Database.ExecuteFill(connection, $"SELECT * FROM [rom] ORDER BY [name]");
				DataTable sampleTable = Database.ExecuteFill(connection, $"SELECT * FROM [sample] ORDER BY [name]");
				DataTable videoTable = Database.ExecuteFill(connection, $"SELECT * FROM [video]");
				DataTable driverTable = Database.ExecuteFill(connection, $"SELECT * FROM [driver]");

				foreach (DataRow dataFileRow in datafileTable.Rows)
				{
					long datafile_id = (long)dataFileRow["datafile_id"];
					string datafile_key = (string)dataFileRow["key"];
					string datafile_name = (string)dataFileRow["name"];

					StringBuilder datafile_html = new StringBuilder();
					string datafile_title = $"{datafile_name}";
					datafile_html.AppendLine($"<h2>{datafile_title}</h2>");
					datafile_html.AppendLine("<table>");
					datafile_html.AppendLine("<tr><th>Name</th><th>Description</th><th>Year</th><th>Manufacturer</th><th>cloneof</th><th>romof</th></tr>");

					foreach (DataRow gameRow in gameTable.Select($"datafile_id = {datafile_id}"))
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];
						string game_year = (string)gameRow["year"];
						string game_manufacturer = (string)gameRow["manufacturer"];
						string game_cloneof = Tools.DataRowValue(gameRow, "cloneof");
						string game_romof = Tools.DataRowValue(gameRow, "romof");

						DataRow[] driverRows = driverTable.Select($"game_id = {game_id}");
						DataRow[] romRows = romTable.Select($"game_id = {game_id}");
						DataRow[] videoRows = videoTable.Select($"game_id = {game_id}");
						DataRow[] sampleRows = sampleTable.Select($"game_id = {game_id}");

						if (game_cloneof != null)
							game_cloneof = $"<a href=\"{datafile_key}/{game_cloneof}\">{game_cloneof}</a>";

						if (game_romof != null)
							game_romof = $"<a href=\"{datafile_key}/{game_romof}\">{game_romof}</a>";

						datafile_html.AppendLine($"<tr><td><a href=\"{datafile_key}/{game_name}\">{game_name}</a></td><td>{game_description}</td><td>{game_year}</td><td>{game_manufacturer}</td><td>{game_cloneof}</td><td>{game_romof}</td></tr>");

						StringBuilder game_html = new StringBuilder();
						string game_title = $"{game_description} ({datafile_name})";

						game_html.AppendLine("<h2>game</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(gameTable, new[] { gameRow }, null));
						game_html.AppendLine("<hr />");

						if (driverRows.Length > 0)
						{
							game_html.AppendLine("<h2>driver</h2>");
							game_html.AppendLine(Reports.MakeHtmlTable(driverTable, driverRows, null));
							game_html.AppendLine("<hr />");
						}
						if (romRows.Length > 0)
						{
							game_html.AppendLine("<h2>rom</h2>");
							game_html.AppendLine(Reports.MakeHtmlTable(romTable, romRows, null));
							game_html.AppendLine("<hr />");
						}
						if (videoRows.Length > 0)
						{
							game_html.AppendLine("<h2>video</h2>");
							game_html.AppendLine(Reports.MakeHtmlTable(videoTable, videoRows, null));
							game_html.AppendLine("<hr />");
						}
						if (sampleRows.Length > 0)
						{
							game_html.AppendLine("<h2>sample</h2>");
							game_html.AppendLine(Reports.MakeHtmlTable(sampleTable, sampleRows, null));
							game_html.AppendLine("<hr />");
						}

						game_payload_table.Rows.Add(datafile_key, game_name, game_title, "", "", game_html.ToString());
					}

					datafile_html.AppendLine("</table>");
					datafile_payload_table.Rows.Add(datafile_key, datafile_title, "", "", datafile_html.ToString());

				}

				MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "key" });
				MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "datafile_key", "game_name" });

			}

			return 0;
		}

	}
}
