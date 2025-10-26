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
	public class OperationsPayload
	{
		private static readonly XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings()
		{
			DtdProcessing = DtdProcessing.Parse,
			IgnoreComments = false,
			IgnoreWhitespace = true,
		};

		//
		// Common
		//
		public static void CreateMetaDataTable(string serverConnectionString, string databaseName, string coreName, string version, string info)
		{
			string agent = $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string tableName = "_metadata";
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
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

				DataTable table = Database.ExecuteFill(connection, $"SELECT * FROM [{tableName}] WHERE (0 = 1)");
				table.TableName = tableName;

				table.Rows.Add(1L, coreName, "", version, info, DateTime.Now, agent);

				Database.BulkInsert(connection, table);
			}
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

		public static void MakeMSSQLPayloadsInsert(string serverConnectionString, string databaseName, DataTable table)
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

				columnDefs.Add($"[{column.ColumnName}] VARCHAR({column.MaxLength})");

				pkNames.Add(column.ColumnName);
			}
			foreach (DataColumn column in table.Columns)
			{
				if (column.MaxLength == -1)
					columnDefs.Add($"[{column.ColumnName}] NVARCHAR(MAX)");
			}

			columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ([{String.Join("], [", pkNames)}])");

			string commandText = $"CREATE TABLE [{table.TableName}] ({String.Join(", ", columnDefs)});";

			Console.WriteLine(commandText);

			using (SqlConnection targetConnection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{	
				Database.ExecuteNonQuery(targetConnection, commandText);
				Database.BulkInsert(targetConnection, table);
			}
		}

		//
		// MAME
		//

		//
		// HBMAME
		//


		//
		// FBNeo
		//
		public static int FBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreFbNeo.FBNeoGetLatestDownloadedVersion(directory);
			directory = Path.Combine(directory, version);

			//
			// Metadata
			//
			string info;
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				info = $"FBNeo: {version} - datafiles: {datafileCount} - games: {gameCount} - roms: {softRomCount}";
			}
			CreateMetaDataTable(serverConnectionString, databaseName, "fbneo", version, info);

			//
			// JSON/XML
			//
			Dictionary<string, string[]> payloadsXmlJson_datafile = FBNeoMSSQLPayloadsXmlJson_Datafile(directory);
			Dictionary<string, string[]> payloadsXmlJson_game = FBNeoMSSQLPayloadsXmlJson_Game(directory);

			//
			// Source data
			//
			DataSet dataSet = new DataSet();
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				foreach (string tableName in new string[] { "datafile", "driver", "game", "rom", "sample", "video" })
				{
					DataTable table = new DataTable(tableName);
					using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * FROM [{tableName}]", connection))
						adapter.Fill(table);
					dataSet.Tables.Add(table);
				}
			}

			//
			// Payloads
			//
			DataTable datafile_payload_table = MakePayloadDataTable("datafile_payload", new string[] { "key" });
			DataTable game_payload_table = MakePayloadDataTable("game_payload", new string[] { "datafile_key", "game_name" });

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

			MakeMSSQLPayloadsInsert(serverConnectionString, databaseName, datafile_payload_table);
			MakeMSSQLPayloadsInsert(serverConnectionString, databaseName, game_payload_table);

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
		// TOSEC
		//

		public static int TosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreTosec.TosecGetLatestDownloadedVersion(directory);
			directory = Path.Combine(directory, version);

			//
			// Metadata
			//
			string info;
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				info = $"TOSEC: {version} - Datafiles: {datafileCount} - Games: {gameCount} - rom: {softRomCount}";
			}
			CreateMetaDataTable(serverConnectionString, databaseName, "tosec", version, info);

			//
			// Archive.org URLs
			//
			Dictionary<long, string> datafileUrls = GetTosecDatafileUrls(serverConnectionString, databaseName);
			Dictionary<long, string> gameUrls = GetTosecGameUrls(serverConnectionString, databaseName);

			//
			// Source Data
			//
			DataSet dataSet = TosecMSSQLPayloadsLoadDataSet(serverConnectionString, databaseName);

			//
			// Payloads
			//
			DataTable category_payload_table = MakePayloadDataTable("category_payload", new string[] { "category" });
			DataTable datafile_payload_table = MakePayloadDataTable("datafile_payload", new string[] { "category", "name" });
			DataTable game_payload_table = MakePayloadDataTable("game_payload", new string[] { "category", "datafile_name", "game_name" });

			foreach (string category in new string[] { "tosec", "tosec-iso", "tosec-pix" })
			{
				//if (category != "tosec-pix")
				//	continue;

				//
				// JSON/XML
				//
				Dictionary<string, string[]>[] payloadParts = TosecMSSQLPayloadsGetDatafileGameXmlJsonPayloads(directory, category);
				Dictionary<string, string[]> datafilePayloads = payloadParts[0];
				Dictionary<string, string[]> gamePayloads = payloadParts[1];

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

				category_payload_table.Rows.Add(category, category_title, "", "", category_html.ToString());
			}

			MakeMSSQLPayloadsInsert(serverConnectionString, databaseName, category_payload_table);
			MakeMSSQLPayloadsInsert(serverConnectionString, databaseName, datafile_payload_table);
			MakeMSSQLPayloadsInsert(serverConnectionString, databaseName, game_payload_table);

			return 0;
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

		public static string TosecExtentionsLink(Dictionary<string, int> sourceExtentions)
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
	}
}
