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
					exitCode = FBNeoMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
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
					exitCode = TosecMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"], Globals.AssemblyVersion);
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

		//
		//	FBNeo
		//

		public static int FBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName, string assemblyVersion)
		{
			if (version == "0")
				version = CoreFbNeo.FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			DataTable metadataTable = CreateMetaDataTable(serverConnectionString, databaseName);
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				string info = $"FBNeo: {version} - datafiles: {datafileCount} - games: {gameCount} - roms: {softRomCount}";

				metadataTable.Rows.Add(1L, "fbneo", "", version, info, DateTime.Now, agent);
				Database.BulkInsert(connection, metadataTable);
			}

			Dictionary<string, string[]> payloadsXmlJson_datafile = FBNeoMSSQLPayloadsXmlJson_Datafile(directory);
			Dictionary<string, string[]> payloadsXmlJson_game = FBNeoMSSQLPayloadsXmlJson_Game(directory);

			DataSet dataSet = new DataSet();
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				foreach (string tableName in new string[] { "datafile", "driver", "game", "rom", "sample", "video" })
				{
					DataTable table = new DataTable(tableName);
					using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * FROM [{tableName}]", connection))
						adapter.Fill(table);
					dataSet.Tables.Add(table);
				}
			}

			DataTable datafile_payload_table = Tools.MakeDataTable("datafile_payload",
				"key	title	xml		json	html",
				"String	String	String	String	String");

			DataTable game_payload_table = Tools.MakeDataTable("game_payload",
				"datafile_key	game_name	title	xml		json	html",
				"String			String		String	String	String	String");

			foreach (DataRow dataFileRow in dataSet.Tables["datafile"].Rows)
			{
				long datafile_id = (long)dataFileRow["datafile_id"];
				string datafile_key = (string)dataFileRow["key"];
				string datafile_name = (string)dataFileRow["name"];

				StringBuilder datafile_html = new StringBuilder();
				string datafile_title = $"{datafile_name}";

				datafile_html.AppendLine("<br />");
				datafile_html.AppendLine($"<div><h2 style=\"display:inline;\">datafile</h2> &bull; <a href=\"{datafile_key}.xml\">XML</a> &bull; <a href=\"{datafile_key}.json\">JSON</a></div>");
				datafile_html.AppendLine("<br />");

				datafile_html.AppendLine(Reports.MakeHtmlTable(dataFileRow.Table, new[] { dataFileRow }, null));
				datafile_html.AppendLine("<hr />");

				datafile_html.AppendLine("<h2>game</h2>");
				datafile_html.AppendLine("<table>");
				datafile_html.AppendLine("<tr><th>Name</th><th>Description</th><th>Year</th><th>Manufacturer</th><th>cloneof</th><th>romof</th></tr>");

				foreach (DataRow gameRow in dataSet.Tables["game"].Select($"datafile_id = {datafile_id}"))
				{
					long game_id = (long)gameRow["game_id"];
					string game_name = (string)gameRow["name"];
					string game_description = (string)gameRow["description"];
					string game_year = (string)gameRow["year"];
					string game_manufacturer = (string)gameRow["manufacturer"];
					string game_cloneof = Tools.DataRowValue(gameRow, "cloneof");
					string game_romof = Tools.DataRowValue(gameRow, "romof");

					DataRow[] driverRows = dataSet.Tables["driver"].Select($"game_id = {game_id}");
					DataRow[] romRows = dataSet.Tables["rom"].Select($"game_id = {game_id}");
					DataRow[] videoRows = dataSet.Tables["video"].Select($"game_id = {game_id}");
					DataRow[] sampleRows = dataSet.Tables["sample"].Select($"game_id = {game_id}");

					string game_cloneof_datafile_link = game_cloneof == null ? "" : $"<a href=\"{datafile_key}/{game_cloneof}\">{game_cloneof}</a>";
					string game_romof_datafile_link = game_romof == null ? "" : $"<a href=\"{datafile_key}/{game_romof}\">{game_romof}</a>";
	
					datafile_html.AppendLine($"<tr><td><a href=\"{datafile_key}/{game_name}\">{game_name}</a></td><td>{game_description}</td><td>{game_year}</td><td>{game_manufacturer}</td><td>{game_cloneof_datafile_link}</td><td>{game_romof_datafile_link}</td></tr>");

					StringBuilder game_html = new StringBuilder();
					string game_title = $"{game_description} ({datafile_name})";

					game_html.AppendLine("<br />");
					game_html.AppendLine($"<div><h2 style=\"display:inline;\">game</h2> &bull; <a href=\"{game_name}.xml\">XML</a> &bull; <a href=\"{game_name}.json\">JSON</a></div>");
					game_html.AppendLine("<br />");

					DataTable table = gameRow.Table.Clone();
					table.ImportRow(gameRow);
					DataRow row = table.Rows[0];
					if (game_cloneof != null)
						row["cloneof"] = $"<a href=\"{game_cloneof}\">{game_cloneof}</a>";
					if (game_romof != null)
						row["romof"] = $"<a href=\"{game_cloneof}\">{game_cloneof}</a>";

					game_html.AppendLine(Reports.MakeHtmlTable(table, null));
					game_html.AppendLine("<hr />");

					game_html.AppendLine("<h2>datafile</h2>");
					game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["datafile"], new[] { dataFileRow }, null));
					game_html.AppendLine("<hr />");

					if (driverRows.Length > 0)
					{
						game_html.AppendLine("<h2>driver</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["driver"], driverRows, null));
						game_html.AppendLine("<hr />");
					}
					if (romRows.Length > 0)
					{
						game_html.AppendLine("<h2>rom</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["rom"], romRows, null));
						game_html.AppendLine("<hr />");
					}
					if (videoRows.Length > 0)
					{
						game_html.AppendLine("<h2>video</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["video"], videoRows, null));
						game_html.AppendLine("<hr />");
					}
					if (sampleRows.Length > 0)
					{
						game_html.AppendLine("<h2>sample</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["sample"], sampleRows, null));
						game_html.AppendLine("<hr />");
					}

					string gameKey = $"{datafile_key}\t{game_name}";
					if (payloadsXmlJson_game.ContainsKey(gameKey) == false)
						throw new ApplicationException($"payloadsXmlJson_game not found: {gameKey}");

					string[] xmlJsonGame = payloadsXmlJson_game[gameKey];

					game_payload_table.Rows.Add(datafile_key, game_name, game_title, xmlJsonGame[0], xmlJsonGame[1], game_html.ToString());
				}

				datafile_html.AppendLine("</table>");

				if (payloadsXmlJson_datafile.ContainsKey(datafile_key) == false)
					throw new ApplicationException($"payloadsXmlJson_datafile not found: {datafile_key}");

				string[] xmlJsonDatafile = payloadsXmlJson_datafile[datafile_key];

				datafile_payload_table.Rows.Add(datafile_key, datafile_title, xmlJsonDatafile[0], xmlJsonDatafile[1], datafile_html.ToString());
			}

			MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "key" });
			MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "datafile_key", "game_name" });

			return 0;
		}

		public static Dictionary<string, string[]> FBNeoMSSQLPayloadsXmlJson_Datafile(string directory)
		{
			Dictionary<string, string[]> payloads = new Dictionary<string, string[]>();

			foreach (string xmlFilename in Directory.GetFiles(directory, "_*.xml"))
			{
				string datafile_key = Path.GetFileNameWithoutExtension(xmlFilename).Substring(1);

				using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
				{
					reader.MoveToContent();

					XElement datafileElement = (XElement)XElement.ReadFrom(reader);

					string xml = datafileElement.ToString();
					string json = Tools.XML2JSON(datafileElement);

					payloads.Add(datafile_key, new string[] { xml, json });
				}
			}

			return payloads;
		}

		public static Dictionary<string, string[]> FBNeoMSSQLPayloadsXmlJson_Game(string directory)
		{
			Dictionary<string, string[]> payloads = new Dictionary<string, string[]>();

			foreach (string xmlFilename in Directory.GetFiles(directory, "_*.xml"))
			{
				string datafile_key = Path.GetFileNameWithoutExtension(xmlFilename).Substring(1);

				using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
				{
					reader.MoveToContent();

					while (reader.Read())
					{
						while (reader.NodeType == XmlNodeType.Element && reader.Name == "game")
						{
							if (XElement.ReadFrom(reader) is XElement gameElement)
							{
								string game_name = gameElement.Attribute("name").Value;

								string xml = gameElement.ToString();
								string json = Tools.XML2JSON(gameElement);

								payloads.Add($"{datafile_key}\t{game_name}", new string[] { xml, json });
							}
						}
					}
				}
			}

			return payloads;
		}

		//
		//	TOSEC
		//

		public static int TosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName, string assemblyVersion)
		{
			if (version == "0")
				version = CoreTosec.TosecGetLatestDownloadedVersion(directory);
			directory = Path.Combine(directory, version);

			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			DataTable metedataTable = CreateMetaDataTable(serverConnectionString, databaseName);

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				string info = $"TOSEC: {version} - Datafiles: {datafileCount} - Games: {gameCount} - rom: {softRomCount}";

				metedataTable.Rows.Add(1L, "tosec", "", version, info, DateTime.Now, agent);
				Database.BulkInsert(connection, metedataTable);
			}

			Tools.ConsolePrintMemory();

			DataSet dataSet = TosecMSSQLPayloadsLoadDataSet(serverConnectionString, databaseName);

			foreach (DataTable table in dataSet.Tables)
				Console.WriteLine($"{table.TableName} row count: {table.Rows.Count}");

			Tools.ConsolePrintMemory();

			Console.WriteLine("Getting archive.org URLs");

			Dictionary<long, string> datafileUrls = OperationsHtml.GetTosecDatafileUrls(serverConnectionString, databaseName);
			Dictionary<long, string> gameUrls = OperationsHtml.GetTosecGameUrls(serverConnectionString, databaseName);

			Console.WriteLine($"datafile archive.org URL count: {datafileUrls.Count}");
			Console.WriteLine($"game archive.org URL count: {gameUrls.Count}");

			Tools.ConsolePrintMemory();

			foreach (string category in new string[] { "tosec", "tosec-iso", "tosec-pix" })
			{
				//if (category != "tosec-pix")
				//	continue;

				Dictionary<string, string[]>[] payloadParts = TosecMSSQLPayloadsGetDatafileGameXmlJsonPayloads(directory, category);
				Dictionary<string, string[]> datafilePayloads = payloadParts[0];
				Dictionary<string, string[]> gamePayloads = payloadParts[1];

				Console.WriteLine($"{category} datafile payload count: {datafilePayloads.Count}");
				Console.WriteLine($"{category} game payload count: {gamePayloads.Count}");

				Tools.ConsolePrintMemory();

				Console.Write($"{category} make payload table category...");
				TosecMSSQLPayloadsCategory(category, dataSet, version, serverConnectionString, databaseName, datafileUrls, gameUrls);
				Console.WriteLine("...done.");

				Tools.ConsolePrintMemory();
				GC.Collect();
				Tools.ConsolePrintMemory();

				Console.Write($"{category} make payload table datafile...");
				TosecMSSQLPayloadsDatafile(category, dataSet, version, serverConnectionString, databaseName, datafileUrls, gameUrls, datafilePayloads);
				Console.WriteLine("...done.");

				Tools.ConsolePrintMemory();
				GC.Collect();
				Tools.ConsolePrintMemory();

				Console.Write($"{category} make payload table game...");
				TosecMSSQLPayloadsGame(category, dataSet, version, serverConnectionString, databaseName, datafileUrls, gameUrls, gamePayloads);
				Console.WriteLine("...done.");

				Tools.ConsolePrintMemory();
				GC.Collect();
				Tools.ConsolePrintMemory();
			}



			//MakeMSSQLPayloadsInsert(category_payload_table, serverConnectionString, databaseName, new string[] { "category" });
			//MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "category", "name" });
			//MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "category", "datafile_name", "game_name" });

			GC.Collect();



			Tools.ConsolePrintMemory();

			return 0;
		}

		public static void TosecMSSQLPayloadsGame(string category, DataSet dataSet, string version, string serverConnectionString, string databaseName, Dictionary<long, string> datafileUrls, Dictionary<long, string> gameUrls, Dictionary<string, string[]> gamePayloads)
		{
			DataTable game_payload_table = Tools.MakeDataTable("game_payload",
				"category	datafile_name	game_name	title	xml		json	html",
				"String		String			String		String	String	String	String");
			game_payload_table.Columns["category"].MaxLength = 9;
			game_payload_table.Columns["datafile_name"].MaxLength = 128;
			game_payload_table.Columns["game_name"].MaxLength = 256;

			foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[category] = '{category}'"))
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

				//StringBuilder datafile_html = new StringBuilder();

				//string datafile_title = $"{datafile_name} ({category} {datafile_version})";

				//datafile_html.AppendLine($"<h2>{datafile_title}</h2>");
				//datafile_html.AppendLine("<table>");
				//datafile_html.AppendLine("<tr><th>Name</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

				foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
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

					game_html.AppendLine("<br />");
					game_html.AppendLine($"<div><h2 style=\"display:inline;\">game</h2> &bull; <a href=\"{game_name_enc}.xml\">XML</a> &bull; <a href=\"{game_name_enc}.json\">JSON</a></div>");
					game_html.AppendLine("<br />");

					game_html.AppendLine(Reports.MakeHtmlTable(gameRow.Table, new[] { gameRow }, null));
					game_html.AppendLine("<hr />");

					game_html.AppendLine($"<h2>rom</h2>");
					game_html.AppendLine("<table>");
					game_html.AppendLine("<tr><th>Name</th><th>Size</th><th>Size Bytes</th><th>CRC32</th><th>MD5</th><th>SHA1</th><th>IA File</th></tr>");

					foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
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

					string gamePayloadKey = $"{datafile_name}\t{game_name}";
					string[] gamePayload;
					if (gamePayloads.ContainsKey(gamePayloadKey) == true)
					{
						gamePayload = gamePayloads[gamePayloadKey];
					}
					else
					{
						gamePayload = new string[] { "", "" };
						Console.WriteLine($"!!! Did not find game payload:{gamePayloadKey}");
					}
					game_payload_table.Rows.Add(category, datafile_name, game_name, game_title, gamePayload[0], gamePayload[1], game_html.ToString());

					string game_extentions = OperationsHtml.TosecExtentionsLink(gameExtentions);

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

					//datafile_html.AppendLine($"<tr><td><a href=\"{datafile_name_enc}/{game_name_enc}\">{game_name}</a></td><td>{game_rom_count}</td><td>{Tools.DataSize(game_rom_size)}</td><td>{game_rom_size}</td><td>{game_extentions}</td><td>{game_url}</td></tr>");
				}

				//datafile_html.AppendLine("</table>");

				//string[] datafilePayload;
				//if (datafilePayloads.ContainsKey(datafile_name) == true)
				//{
				//	datafilePayload = datafilePayloads[datafile_name];
				//}
				//else
				//{
				//	datafilePayload = new string[] { "", "" };
				//	Console.WriteLine($"!!! Did not find datafile payload:{datafile_name}");
				//}
				//datafile_payload_table.Rows.Add(category, datafile_name, datafile_title, datafilePayload[0], datafilePayload[1], datafile_html.ToString());

				string datafile_extentions = OperationsHtml.TosecExtentionsLink(datafileExtentions);

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

				//category_html.AppendLine($"<tr><td><a href=\"{category.ToLower()}/{datafile_name_enc}\">{datafile_name}</a></td><td>{datafile_version}</td><td>{datafile_game_count}</td><td>{datafile_rom_count}</td><td>{Tools.DataSize(datafile_rom_size)}</td><td>{datafile_rom_size}</td><td>{datafile_extentions}</td><td>{datafile_url}</td></tr>");
			}

			MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "category", "datafile_name", "game_name" });
		}

		public static void TosecMSSQLPayloadsDatafile(string category, DataSet dataSet, string version, string serverConnectionString, string databaseName, Dictionary<long, string> datafileUrls, Dictionary<long, string> gameUrls, Dictionary<string, string[]> datafilePayloads)
		{
			DataTable datafile_payload_table = Tools.MakeDataTable("datafile_payload",
				"category	name	title	xml		json	html",
				"String		String	String	String	String	String");
			datafile_payload_table.Columns["category"].MaxLength = 9;
			datafile_payload_table.Columns["name"].MaxLength = 128;


			foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[category] = '{category}'"))
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

				datafile_html.AppendLine("<br />");
				datafile_html.AppendLine($"<div><h2 style=\"display:inline;\">datafile</h2> &bull; <a href=\"{datafile_name_enc}.xml\">XML</a> &bull; <a href=\"{datafile_name_enc}.json\">JSON</a></div>");
				datafile_html.AppendLine("<br />");

				datafile_html.AppendLine(Reports.MakeHtmlTable(datafileRow.Table, new[] { datafileRow }, null));
				datafile_html.AppendLine("<hr />");

				datafile_html.AppendLine($"<h2>game</h2>");
				datafile_html.AppendLine("<table>");
				datafile_html.AppendLine("<tr><th>Name</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

				foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
				{
					long game_id = (long)gameRow["game_id"];
					string game_name = (string)gameRow["name"];
					string game_name_enc = Uri.EscapeDataString(game_name);

					long game_rom_count = 0;
					long game_rom_size = 0;

					long rom_url_count = 0;

					Dictionary<string, int> gameExtentions = new Dictionary<string, int>();

					++datafile_game_count;

					//StringBuilder game_html = new StringBuilder();

					//string game_title = $"{datafile_name} - {game_name} ({category} {datafile_version})";
					//game_html.AppendLine($"<h2>{game_title}</h2>");

					//game_html.AppendLine("<table>");
					//game_html.AppendLine("<tr><th>Name</th><th>Size</th><th>Size Bytes</th><th>CRC32</th><th>MD5</th><th>SHA1</th><th>IA File</th></tr>");

					foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
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

						//game_html.AppendLine($"<tr><td>{rom_name}</td><td>{Tools.DataSize(rom_size)}</td><td>{rom_size}</td><td>{crc}</td><td>{md5}</td><td>{sha1}</td><td>{rom_url}</td></tr>");
					}

					//game_html.AppendLine("</table>");

					//string gamePayloadKey = $"{datafile_name}\t{game_name}";
					//string[] gamePayload;
					//if (gamePayloads.ContainsKey(gamePayloadKey) == true)
					//{
					//	gamePayload = gamePayloads[gamePayloadKey];
					//}
					//else
					//{
					//	gamePayload = new string[] { "", "" };
					//	Console.WriteLine($"!!! Did not find game payload:{gamePayloadKey}");
					//}
					//game_payload_table.Rows.Add(category, datafile_name, game_name, datafile_title, gamePayload[0], gamePayload[1], datafile_html.ToString());

					string game_extentions = OperationsHtml.TosecExtentionsLink(gameExtentions);

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

				string[] datafilePayload;
				if (datafilePayloads.ContainsKey(datafile_name) == true)
				{
					datafilePayload = datafilePayloads[datafile_name];
				}
				else
				{
					datafilePayload = new string[] { "", "" };
					Console.WriteLine($"!!! Did not find datafile payload:{datafile_name}");
				}
				datafile_payload_table.Rows.Add(category, datafile_name, datafile_title, datafilePayload[0], datafilePayload[1], datafile_html.ToString());

				string datafile_extentions = OperationsHtml.TosecExtentionsLink(datafileExtentions);

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

				//category_html.AppendLine($"<tr><td><a href=\"{category.ToLower()}/{datafile_name_enc}\">{datafile_name}</a></td><td>{datafile_version}</td><td>{datafile_game_count}</td><td>{datafile_rom_count}</td><td>{Tools.DataSize(datafile_rom_size)}</td><td>{datafile_rom_size}</td><td>{datafile_extentions}</td><td>{datafile_url}</td></tr>");
			}



			MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "category", "name" });
		}



		public static void TosecMSSQLPayloadsCategory(string category, DataSet dataSet, string version, string serverConnectionString, string databaseName, Dictionary<long, string> datafileUrls, Dictionary<long, string> gameUrls)
		{
			DataTable category_payload_table = Tools.MakeDataTable("category_payload",
				"category	title	xml		json	html",
				"String		String	String	String	String");
			category_payload_table.Columns["category"].MaxLength = 9;

			StringBuilder category_html = new StringBuilder();

			string category_title = $"TOSEC Data Files ({category} {version})";

			category_html.AppendLine($"<h2>{category}</h2>");
			category_html.AppendLine("<table>");
			category_html.AppendLine("<tr><th>Name</th><th>Version</th><th>Game Count</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

			foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[category] = '{category}'"))
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

				//StringBuilder datafile_html = new StringBuilder();

				//string datafile_title = $"{datafile_name} ({category} {datafile_version})";

				//datafile_html.AppendLine($"<h2>{datafile_title}</h2>");
				//datafile_html.AppendLine("<table>");
				//datafile_html.AppendLine("<tr><th>Name</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

				foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
				{
					long game_id = (long)gameRow["game_id"];
					string game_name = (string)gameRow["name"];
					string game_name_enc = Uri.EscapeDataString(game_name);

					long game_rom_count = 0;
					long game_rom_size = 0;

					long rom_url_count = 0;

					Dictionary<string, int> gameExtentions = new Dictionary<string, int>();

					++datafile_game_count;

					//StringBuilder game_html = new StringBuilder();

					//string game_title = $"{datafile_name} - {game_name} ({category} {datafile_version})";
					//game_html.AppendLine($"<h2>{game_title}</h2>");

					//game_html.AppendLine("<table>");
					//game_html.AppendLine("<tr><th>Name</th><th>Size</th><th>Size Bytes</th><th>CRC32</th><th>MD5</th><th>SHA1</th><th>IA File</th></tr>");

					foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
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

						//string rom_url = "";
						//if (datafileUrls.ContainsKey(datafile_id) == true)
						//{
						//	rom_url = datafileUrls[datafile_id];
						//	rom_url = $"{rom_url}/{Uri.EscapeDataString(game_name)}%2F{Uri.EscapeDataString(rom_name)}";
						//	rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
						//}
						//if (gameUrls.ContainsKey(game_id) == true)
						//{
						//	rom_url = gameUrls[game_id];
						//	rom_url = $"{rom_url}/{Uri.EscapeDataString(rom_name)}";
						//	rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
						//}
						//if (rom_url != "")
						//	rom_url_count += 1;

						//game_html.AppendLine($"<tr><td>{rom_name}</td><td>{Tools.DataSize(rom_size)}</td><td>{rom_size}</td><td>{crc}</td><td>{md5}</td><td>{sha1}</td><td>{rom_url}</td></tr>");
					}

					//game_html.AppendLine("</table>");

					//string gamePayloadKey = $"{datafile_name}\t{game_name}";
					//string[] gamePayload;
					//if (gamePayloads.ContainsKey(gamePayloadKey) == true)
					//{
					//	gamePayload = gamePayloads[gamePayloadKey];
					//}
					//else
					//{
					//	gamePayload = new string[] { "", "" };
					//	Console.WriteLine($"!!! Did not find game payload:{gamePayloadKey}");
					//}
					//game_payload_table.Rows.Add(category, datafile_name, game_name, datafile_title, gamePayload[0], gamePayload[1], datafile_html.ToString());

					string game_extentions = OperationsHtml.TosecExtentionsLink(gameExtentions);

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

					//datafile_html.AppendLine($"<tr><td><a href=\"{datafile_name_enc}/{game_name_enc}\">{game_name}</a></td><td>{game_rom_count}</td><td>{Tools.DataSize(game_rom_size)}</td><td>{game_rom_size}</td><td>{game_extentions}</td><td>{game_url}</td></tr>");
				}

				//datafile_html.AppendLine("</table>");

				//string[] datafilePayload;
				//if (datafilePayloads.ContainsKey(datafile_name) == true)
				//{
				//	datafilePayload = datafilePayloads[datafile_name];
				//}
				//else
				//{
				//	datafilePayload = new string[] { "", "" };
				//	Console.WriteLine($"!!! Did not find datafile payload:{datafile_name}");
				//}
				//datafile_payload_table.Rows.Add(category, datafile_name, datafile_title, datafilePayload[0], datafilePayload[1], datafile_html.ToString());

				string datafile_extentions = OperationsHtml.TosecExtentionsLink(datafileExtentions);

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

			category_payload_table.Rows.Add(category, category_title, "", "", category_html.ToString());

			MakeMSSQLPayloadsInsert(category_payload_table, serverConnectionString, databaseName, new string[] { "category" });
		}





		public static Dictionary<string, string[]>[] TosecMSSQLPayloadsGetDatafileGameXmlJsonPayloads(string directory, string category)
		{
			string xmlFilename = Path.Combine(directory, $"_{category}.xml");
			Console.WriteLine(xmlFilename);

			Dictionary<string, string[]> datafilePayloads = new Dictionary<string, string[]>();
			Dictionary<string, string[]> gamePayloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "datafile")
					{
						if (XElement.ReadFrom(reader) is XElement datafileElement)
						{
							string datafile_name = datafileElement.Element("header").Element("name").Value;

							string xml = datafileElement.ToString();
							string json = Tools.XML2JSON(datafileElement);

							datafilePayloads.Add(datafile_name, new string[] { xml, json });

							HashSet<string> gameNames = new HashSet<string>();  // Duplicates in source data fix

							foreach (XElement element in datafileElement.Elements("game"))
							{
								string game_name = element.Attribute("name").Value;

								xml = element.ToString();
								json = Tools.XML2JSON(element);

								if (gameNames.Add(game_name) == true)
									gamePayloads.Add($"{datafile_name}\t{game_name}", new string[] { xml, json });
								else
									Console.WriteLine($"!!! Warning XML Duplicate TOSEC game: {category}, {datafile_name}, {game_name}");
							}
						}
					}
				}
			}

			return new Dictionary<string, string[]>[] { datafilePayloads, gamePayloads };
		}

		public static DataSet TosecMSSQLPayloadsLoadDataSet(string serverConnectionString, string databaseName)
		{
			DataSet dataSet = new DataSet();
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				//
				//	Datafix for duplicates in source data
				//
				DataTable dupTable = Database.ExecuteFill(connection,
					"SELECT datafile.category, datafile.name AS datafile_name, game.name AS game_name FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id " +
					"GROUP BY datafile.category, datafile.name, game.name HAVING (COUNT(*) > 1)");

				List<DataRow> dupGameRows = new List<DataRow>();
				foreach (DataRow dupRow in dupTable.Rows)
				{
					using (SqlCommand command = new SqlCommand("SELECT game.game_id, datafile.category, datafile.name AS datafile_name, game.name AS game_name FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id "
						+ "WHERE ((datafile.category = @category) AND (datafile.[name] = @datafile_name) AND (game.[name] = @game_name))", connection))
					{
						command.Parameters.AddWithValue("@category", (string)dupRow["category"]);
						command.Parameters.AddWithValue("@datafile_name", (string)dupRow["datafile_name"]);
						command.Parameters.AddWithValue("@game_name", (string)dupRow["game_name"]);

						DataTable dupGameTable = new DataTable();
						using (SqlDataAdapter adapter = new SqlDataAdapter(command))
							adapter.Fill(dupGameTable);

						for (int index = 1; index < dupGameTable.Rows.Count; ++index)
							dupGameRows.Add(dupGameTable.Rows[index]);
					}
				}

				string game_commandText = "SELECT * from [game] @WHERE ORDER BY [name]";
				if (dupGameRows.Count > 0)
				{
					Console.WriteLine($"!!! Warning DATA Duplicate TOSEC games:{Environment.NewLine}{String.Join(Environment.NewLine, dupGameRows.Select(row => $"{(string)row["category"]} / {(string)row["datafile_name"]} / {(string)row["game_name"]}"))}))");
					game_commandText = game_commandText.Replace("@WHERE", $"WHERE ([game_id] NOT IN ({String.Join(", ", dupGameRows.Select(row => (long)row["game_id"]))}))");
				}
				else
				{
					game_commandText = game_commandText.Replace("@WHERE", "");
				}

				Console.Write("Loading all data...");

				DataTable table;
				table = Database.ExecuteFill(connection, "SELECT * FROM [datafile] ORDER BY [name]");
				table.TableName = "datafile";
				dataSet.Tables.Add(table);
				Console.Write("datafile.");
				table = Database.ExecuteFill(connection, game_commandText);
				table.TableName = "game";
				dataSet.Tables.Add(table);
				Console.Write("game.");
				table = Database.ExecuteFill(connection, "SELECT * FROM [rom] ORDER BY [name]");
				table.TableName = "rom";
				dataSet.Tables.Add(table);
				Console.Write("rom.");

				Console.WriteLine("...done");
			}

			return dataSet;
		}


		public static int TosecMSSQLPayloadsOLD(string directory, string version, string serverConnectionString, string databaseName, string assemblyVersion)
		{
			if (version == "0")
				version = CoreTosec.TosecGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			string agent = $"mame-ao/{assemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			DataTable datafileTable;
			DataTable gameTable;
			DataTable romTable;

			DataTable table = CreateMetaDataTable(serverConnectionString, databaseName);

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				string info = $"TOSEC: {version} - Datafiles: {datafileCount} - Games: {gameCount} - rom: {softRomCount}";

				table.Rows.Add(1L, "tosec", "", version, info, DateTime.Now, agent);
				Database.BulkInsert(connection, table);


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

				datafileTable = Database.ExecuteFill(connection, "SELECT * FROM [datafile] ORDER BY [name]");
				gameTable = Database.ExecuteFill(connection, game_commandText);
				romTable = Database.ExecuteFill(connection, "SELECT * FROM [rom] ORDER BY [name]");
			}

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

			foreach (string category in new string[] { "tosec", "tosec-iso", "tosec-pix" })
			{
				string xmlFilename = Path.Combine(directory, $"_{category}.xml");
				Console.WriteLine(xmlFilename);

				Dictionary<string, string[]> datafilePayloads = new Dictionary<string, string[]>();
				Dictionary<string, string[]> gamePayloads = new Dictionary<string, string[]>();

				using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
				{
					reader.MoveToContent();

					while (reader.Read())
					{
						while (reader.NodeType == XmlNodeType.Element && reader.Name == "datafile")
						{
							if (XElement.ReadFrom(reader) is XElement datafileElement)
							{
								string datafile_name = datafileElement.Element("header").Element("name").Value;

								string xml = datafileElement.ToString();
								string json = Tools.XML2JSON(datafileElement);

								datafilePayloads.Add(datafile_name, new string[] { xml, json });

								HashSet<string> gameNames = new HashSet<string>();  // Duplicates in source data fix

								foreach (XElement element in datafileElement.Elements("game"))
								{
									string game_name = element.Attribute("name").Value;

									xml = element.ToString();
									json = Tools.XML2JSON(element);

									if (gameNames.Add(game_name) == true)
										gamePayloads.Add($"{datafile_name}\t{game_name}", new string[] { xml, json });
									else
										Console.WriteLine($"Duplicate game: {category}, {datafile_name}, {game_name}");
								}
							}
						}
					}
				}

				Console.WriteLine(datafilePayloads.Count.ToString());
				Console.WriteLine(gamePayloads.Count.ToString());

				Dictionary<long, string> datafileUrls = OperationsHtml.GetTosecDatafileUrls(serverConnectionString, databaseName);
				Dictionary<long, string> gameUrls = OperationsHtml.GetTosecGameUrls(serverConnectionString, databaseName);

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

						string gamePayloadKey = $"{datafile_name}\t{game_name}";
						string[] gamePayload;
						if (gamePayloads.ContainsKey(gamePayloadKey) == true)
						{
							gamePayload = gamePayloads[gamePayloadKey];
						}
						else
						{
							gamePayload = new string[] { "", "" };
							Console.WriteLine($"!!! Did not find game payload:{gamePayloadKey}");
						}
						game_payload_table.Rows.Add(category, datafile_name, game_name, datafile_title, gamePayload[0], gamePayload[1], datafile_html.ToString());

						string game_extentions = OperationsHtml.TosecExtentionsLink(gameExtentions);

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

					string[] datafilePayload;
					if (datafilePayloads.ContainsKey(datafile_name) == true)
					{
						datafilePayload = datafilePayloads[datafile_name];
					}
					else
					{
						datafilePayload = new string[] { "", "" };
						Console.WriteLine($"!!! Did not find datafile payload:{datafile_name}");
					}
					datafile_payload_table.Rows.Add(category, datafile_name, datafile_title, datafilePayload[0], datafilePayload[1], datafile_html.ToString());

					string datafile_extentions = OperationsHtml.TosecExtentionsLink(datafileExtentions);

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

				category_payload_table.Rows.Add(category, category_title, "", "", category_html.ToString());
			}

			MakeMSSQLPayloadsInsert(category_payload_table, serverConnectionString, databaseName, new string[] { "category" });
			MakeMSSQLPayloadsInsert(datafile_payload_table, serverConnectionString, databaseName, new string[] { "category", "name" });
			MakeMSSQLPayloadsInsert(game_payload_table, serverConnectionString, databaseName, new string[] { "category", "datafile_name", "game_name" });

			return 0;
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

