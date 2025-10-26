using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class Operations
	{
		private static readonly XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings()
		{
			DtdProcessing = DtdProcessing.Parse,
			IgnoreComments = false,
			IgnoreWhitespace = true,
		};

		public static int ProcessOperation(Dictionary<string, string> parameters)
		{
			int exitCode;
			ICore core;

			DateTime timeStart = DateTime.Now;

			switch (parameters["operation"])
			{
				//
				//	MAME
				//
				case "mame-get":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "mame-xml":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "mame-json":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "mame-sqlite":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "mame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "mame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "mame-mssql-payload-html":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsHtml.MSSQLPayloadHtml(parameters["server"], parameters["names"], "mame");
					break;

				//
				// HBMAME
				//
				case "hbmame-get":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "hbmame-xml":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "hbmame-json":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "hbmame-sqlite":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "hbmame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = HbMameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "hbmame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = HbMameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
					break;

				case "hbmame-mssql-payload-html":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsHtml.MSSQLPayloadHtml(parameters["server"], parameters["names"], "hbmame");
					break;

				//
				// FBNeo
				//
				case "fbneo-get":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "fbneo-xml":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "fbneo-json":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "fbneo-sqlite":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "fbneo-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = FBNeoMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "fbneo-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.FBNeoMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "fbneo-mssql-payload-html":
					exitCode = 0;
					break;

				//
				// TOSEC
				//
				case "tosec-get":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "tosec-xml":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "tosec-json":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "tosec-sqlite":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "tosec-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = TosecMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "tosec-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.TosecMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "tosec-mssql-payload-html":
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

		//
		// MS SQL
		//
		public static int MameMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = CoreMame.LatestLocalVersion(directory);

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

		public static int HbMameMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = CoreHbMame.LatestLocalVersion(directory);

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

		public static int FBNeoMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreFbNeo.FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = CoreFbNeo.FBNeoDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}

		public static int TosecMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreTosec.TosecGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = CoreTosec.TosecDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}

		//
		// MS SQL Payloads
		//
		public static int MameMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseNames, string assemblyVersion)
		{
			if (version == "0")
				version = CoreMame.LatestLocalVersion(directory);

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

		public static int HbMameMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseNames, string assemblyVersion)
		{
			if (version == "0")
				version = CoreHbMame.LatestLocalVersion(directory);

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









		public static void MakeMSSQLPayloadsInfoTable(string version, string serverConnectionString, string databaseName, string dataSetName, string subSetName, string assemblyVersion, string versionDirectory)
		{
			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string exePath = Path.Combine(versionDirectory, "mame.exe");
			string exeTime = File.GetLastWriteTime(exePath).ToString("s");

			//CreateMetaDataTable(serverConnectionString, databaseName);

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



		public static void MakeMSSQLPayloadsInsert(DataTable table, string serverConnectionString, string databaseName, string[] primaryKeyNames)
		{
			using (SqlConnection targetConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				if (Database.TableExists(targetConnection, table.TableName) == false)
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
				}

				Database.BulkInsert(targetConnection, table);
			}
		}

	}
}

